import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/realtime/app_lifecycle_controller.dart';
import '../../../core/realtime/delivery_events.dart';
import '../data/driver_repository.dart';
import '../domain/delivery.dart';
import '../domain/driver_profile.dart';
import 'driver_home_state.dart';

/// Estado da Home do Entregador: perfil (aprovação/status), entregas
/// disponíveis (quando `Available`) e entrega atual (quando `Busy`).
class DriverHomeController extends AsyncNotifier<DriverHomeState> {
  @override
  Future<DriverHomeState> build() async {
    // Reage a `DeliveryAvailable` (nova entrega na cidade) via SignalR,
    // atualizando a lista sem polling — só quando o entregador está
    // realmente olhando a lista de disponíveis (`Available`, sem ação em
    // andamento). `docs/DELIVERY_FLOW.md#tempo-real-signalr`.
    ref.listen(deliveryAvailableStreamProvider, (previous, next) {
      next.whenData((_) {
        final current = state.value;
        if (current != null &&
            current.profile.status == 'Available' &&
            !current.isProcessingAction &&
            !current.isTogglingStatus) {
          refresh();
        }
      });
    });

    ref.listen(appLifecycleControllerProvider, (previous, next) {
      if (previous != null && next > previous) {
        refresh();
      }
    });

    return _load();
  }

  Future<DriverHomeState> _load() async {
    final repository = ref.read(driverRepositoryProvider);
    final profile = await repository.getMyProfile();
    return _loadDependentData(repository, profile);
  }

  Future<DriverHomeState> _loadDependentData(
    DriverRepository repository,
    DriverProfile profile,
  ) async {
    if (!profile.isApproved) {
      return DriverHomeState(profile: profile);
    }

    if (profile.isBusy) {
      final current = await repository.getCurrentDelivery();
      return DriverHomeState(profile: profile, currentDelivery: current);
    }

    if (profile.status == 'Available') {
      final available = await repository.getAvailableDeliveries();
      return DriverHomeState(profile: profile, availableDeliveries: available);
    }

    // Offline ou Blocked: sem listas para carregar.
    return DriverHomeState(profile: profile);
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_load);
  }

  Future<void> goOnline() async {
    await _toggleStatus((repository) => repository.goOnline());
  }

  Future<void> goOffline() async {
    await _toggleStatus((repository) => repository.goOffline());
  }

  Future<void> _toggleStatus(
    Future<DriverProfile> Function(DriverRepository repository) action,
  ) async {
    final current = state.value;
    if (current == null || current.isTogglingStatus) return;

    state = AsyncValue.data(current.copyWith(isTogglingStatus: true));
    final repository = ref.read(driverRepositoryProvider);

    try {
      final updatedProfile = await action(repository);
      final newState = await _loadDependentData(repository, updatedProfile);
      state = AsyncValue.data(newState);
    } on AppException {
      state = AsyncValue.data(current.copyWith(isTogglingStatus: false));
      rethrow;
    }
  }

  /// `docs/DELIVERY_FLOW.md#a-corrida-de-aceite-crítico` — se outro
  /// entregador já aceitou (`DELIVERY_ALREADY_TAKEN`, 409), removemos a
  /// entrega da lista local e recarregamos as disponíveis, sem tratar como
  /// erro alarmante. O chamador decide a mensagem exibida via `e.code`.
  Future<void> acceptDelivery(String deliveryId) async {
    final current = state.value;
    if (current == null || current.isProcessingAction) return;

    state = AsyncValue.data(current.copyWith(isProcessingAction: true));
    final repository = ref.read(driverRepositoryProvider);

    try {
      await repository.acceptDelivery(deliveryId);
      final newState = await _load();
      state = AsyncValue.data(newState);
    } on AppException catch (e) {
      if (e.code == 'DELIVERY_ALREADY_TAKEN') {
        final refreshed = current.availableDeliveries
            .where((d) => d.id != deliveryId)
            .toList(growable: false);
        state = AsyncValue.data(
          current.copyWith(
            availableDeliveries: refreshed,
            isProcessingAction: false,
          ),
        );
      } else {
        state = AsyncValue.data(current.copyWith(isProcessingAction: false));
      }
      rethrow;
    }
  }

  Future<void> markPickedUp(String deliveryId) =>
      _runDeliveryAction((repository) => repository.markPickedUp(deliveryId));

  Future<void> markOutForDelivery(String deliveryId) => _runDeliveryAction(
    (repository) => repository.markOutForDelivery(deliveryId),
  );

  Future<void> confirmDelivery(String deliveryId, String code) =>
      _runDeliveryAction(
        (repository) => repository.confirmDelivery(deliveryId, code),
      );

  Future<void> _runDeliveryAction(
    Future<Delivery> Function(DriverRepository repository) action,
  ) async {
    final current = state.value;
    if (current == null || current.isProcessingAction) return;

    state = AsyncValue.data(current.copyWith(isProcessingAction: true));
    final repository = ref.read(driverRepositoryProvider);

    try {
      await action(repository);
      final newState = await _load();
      state = AsyncValue.data(newState);
    } on AppException {
      state = AsyncValue.data(current.copyWith(isProcessingAction: false));
      rethrow;
    }
  }
}

final driverHomeControllerProvider =
    AsyncNotifierProvider<DriverHomeController, DriverHomeState>(
      DriverHomeController.new,
    );
