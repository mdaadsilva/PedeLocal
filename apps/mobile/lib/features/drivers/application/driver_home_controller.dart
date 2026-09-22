import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/realtime/app_lifecycle_controller.dart';
import '../../../core/realtime/delivery_accepted_events.dart';
import '../../../core/realtime/delivery_events.dart';
import '../../../core/realtime/reconnect_tick.dart';
import '../data/driver_repository.dart';
import '../domain/delivery.dart';
import '../domain/driver_profile.dart';
import 'driver_home_state.dart';

/// Home do entregador. `DeliveryAccepted` remove a corrida da lista na hora
/// (`docs/REALTIME_EVENTS.md`). `DeliveryAvailable` reconcilia via REST.
class DriverHomeController extends AsyncNotifier<DriverHomeState> {
  @override
  Future<DriverHomeState> build() async {
    ref.listen(deliveryAvailableStreamProvider, (previous, next) {
      next.whenData((_) => silentRefreshAvailable());
    });

    ref.listen(deliveryAcceptedStreamProvider, (previous, next) {
      next.whenData((event) => removeAcceptedDelivery(event.deliveryId));
    });

    ref.listen(appLifecycleControllerProvider, (previous, next) {
      if (previous != null && next > previous) {
        silentRefresh();
      }
    });

    ref.listen(realtimeReconnectTickProvider, (previous, next) {
      if (previous != null && next > previous) {
        silentRefresh();
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
    // `Busy`/`Available` só ocorrem para um entregador já `Approved` — não
    // é preciso checar elegibilidade aqui: se não for, o status já vem
    // `Offline`/`Blocked` do backend e cai direto no `return` final.
    if (profile.isBusy) {
      final current = await repository.getCurrentDelivery();
      return DriverHomeState(profile: profile, currentDelivery: current);
    }

    if (profile.status == 'Available') {
      final available = await repository.getAvailableDeliveries();
      return DriverHomeState(profile: profile, availableDeliveries: available);
    }

    return DriverHomeState(profile: profile);
  }

  void removeAcceptedDelivery(String deliveryId) {
    final current = state.value;
    if (current == null) return;
    final filtered = current.availableDeliveries
        .where((delivery) => delivery.id != deliveryId)
        .toList(growable: false);
    if (filtered.length == current.availableDeliveries.length) return;
    state = AsyncValue.data(current.copyWith(availableDeliveries: filtered));
  }

  Future<void> silentRefreshAvailable() async {
    final current = state.value;
    if (current == null ||
        current.profile.status != 'Available' ||
        current.isProcessingAction ||
        current.isTogglingStatus) {
      return;
    }
    try {
      final available = await ref
          .read(driverRepositoryProvider)
          .getAvailableDeliveries();
      final latest = state.value;
      if (latest == null) return;
      state = AsyncValue.data(latest.copyWith(availableDeliveries: available));
    } catch (_) {}
  }

  Future<void> silentRefresh() async {
    try {
      final next = await _load();
      state = AsyncValue.data(next);
    } catch (_) {}
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
            .where((delivery) => delivery.id != deliveryId)
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

  /// Registra uma ocorrência operacional na entrega atual
  /// (`docs/DELIVERY_FLOW.md#ocorrências-na-entrega`) — não altera o status
  /// da entrega nem exige recarregar o estado inteiro, só bloqueia novas
  /// ações enquanto a chamada está em andamento.
  Future<void> reportOccurrence(
    String deliveryId,
    String type,
    String? note,
  ) async {
    final current = state.value;
    if (current == null || current.isProcessingAction) return;

    state = AsyncValue.data(current.copyWith(isProcessingAction: true));
    final repository = ref.read(driverRepositoryProvider);

    try {
      await repository.addOccurrence(deliveryId, type, note);
      final latest = state.value;
      if (latest != null) {
        state = AsyncValue.data(latest.copyWith(isProcessingAction: false));
      }
    } on AppException {
      state = AsyncValue.data(current.copyWith(isProcessingAction: false));
      rethrow;
    }
  }

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
