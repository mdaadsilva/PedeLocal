import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/notifications/recent_notification_feedback.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../cities/application/cities_controller.dart';
import '../../session/presentation/widgets/account_sheet.dart';
import '../application/driver_home_controller.dart';
import '../application/driver_home_state.dart';
import '../domain/driver_profile.dart';
import 'widgets/current_delivery_card.dart';
import 'widgets/delivery_tile.dart';
import 'widgets/driver_status_card.dart';

/// Home do Entregador — mostra claramente nome, cidade e situação de
/// aprovação (`docs/DELIVERY_FLOW.md#pré-requisitos-do-entregador`).
class HomeDriverScreen extends ConsumerWidget {
  const HomeDriverScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final homeAsync = ref.watch(driverHomeControllerProvider);

    ref.listen(driverHomeControllerProvider, (previous, next) {
      final previousCount = previous?.value?.availableDeliveries.length;
      final nextCount = next.value?.availableDeliveries.length;
      if (previousCount == null || nextCount == null) return;
      if (nextCount > previousCount) {
        final feedback = ref.read(recentNotificationFeedbackProvider);
        if (!feedback.claim('delivery-available')) {
          return;
        }
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Nova entrega disponível')),
        );
      }
    });

    return Scaffold(
      appBar: PedeLocalAppBar(
        title: 'PedeLocal Entregador',
        automaticallyImplyLeading: false,
        actions: [
          IconButton(
            onPressed: () => showAccountSheet(context),
            icon: const Icon(Icons.account_circle_outlined),
            tooltip: 'Perfil',
          ),
        ],
      ),
      body: SafeArea(
        child: homeAsync.when(
          data: (state) => _DriverHomeBody(state: state),
          loading: () =>
              const LoadingIndicator(message: 'Carregando seu perfil...'),
          error: (error, stackTrace) => ErrorStateView(
            message: error is AppException
                ? error.message
                : 'Não foi possível carregar seus dados.',
            onRetry: () =>
                ref.read(driverHomeControllerProvider.notifier).refresh(),
          ),
        ),
      ),
    );
  }
}

class _DriverHomeBody extends ConsumerWidget {
  const _DriverHomeBody({required this.state});

  final DriverHomeState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = state.profile;
    final citiesAsync = ref.watch(citiesControllerProvider);
    final cityName = citiesAsync.maybeWhen(
      data: (cities) {
        for (final city in cities) {
          if (city.id == profile.cityId) return city.name;
        }
        return null;
      },
      orElse: () => null,
    );

    return RefreshIndicator(
      onRefresh: () =>
          ref.read(driverHomeControllerProvider.notifier).refresh(),
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          DriverStatusCard(
            profile: profile,
            cityName: cityName ?? 'Cidade não identificada',
            isToggling: state.isTogglingStatus,
            onGoOnline: () => _runAction(
              context,
              ref.read(driverHomeControllerProvider.notifier).goOnline,
            ),
            onGoOffline: () => _runAction(
              context,
              ref.read(driverHomeControllerProvider.notifier).goOffline,
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          ..._buildSectionContent(context, ref, profile),
        ],
      ),
    );
  }

  Future<void> _runAction(
    BuildContext context,
    Future<void> Function() action,
  ) async {
    try {
      await action();
    } on AppException catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  List<Widget> _buildSectionContent(
    BuildContext context,
    WidgetRef ref,
    DriverProfile profile,
  ) {
    if (!profile.isApproved) {
      return [
        EmptyStateView(
          title: profile.approvalStatus == 'Rejected'
              ? 'Cadastro não aprovado'
              : 'Aguardando aprovação',
          message: profile.approvalStatus == 'Rejected'
              ? 'Seu cadastro de entregador não foi aprovado. Entre em contato com o suporte para mais informações.'
              : 'Seu cadastro está em análise pelo administrador. Você poderá ficar online assim que for aprovado.',
          icon: profile.approvalStatus == 'Rejected'
              ? Icons.block_rounded
              : Icons.hourglass_top_rounded,
        ),
      ];
    }

    if (profile.isBlocked) {
      return [
        const EmptyStateView(
          title: 'Conta bloqueada',
          message: 'Seu acesso como entregador foi bloqueado. Entre em contato com o suporte.',
          icon: Icons.block_rounded,
        ),
      ];
    }

    if (profile.isBusy) {
      final current = state.currentDelivery;
      final notifier = ref.read(driverHomeControllerProvider.notifier);
      return [
        Text('Entrega atual', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        if (current != null)
          CurrentDeliveryCard(
            delivery: current,
            isProcessing: state.isProcessingAction,
            onPickup: () =>
                _runAction(context, () => notifier.markPickedUp(current.id)),
            onOutForDelivery: () => _runAction(
              context,
              () => notifier.markOutForDelivery(current.id),
            ),
            onConfirm: (code) => _runAction(
              context,
              () => notifier.confirmDelivery(current.id, code),
            ),
          )
        else
          const EmptyStateView(
            title: 'Buscando sua entrega atual...',
            icon: Icons.local_shipping_outlined,
          ),
      ];
    }

    if (profile.status == 'Available') {
      final deliveries = state.availableDeliveries;
      final notifier = ref.read(driverHomeControllerProvider.notifier);
      return [
        Text('Entregas disponíveis', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        if (deliveries.isEmpty)
          const EmptyStateView(
            title: 'Nenhuma entrega disponível',
            message: 'Assim que houver um pedido pronto na sua cidade, ele aparecerá aqui.',
            icon: Icons.local_shipping_outlined,
          )
        else
          ...deliveries.map(
            (delivery) => Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: DeliveryTile(
                delivery: delivery,
                isProcessing: state.isProcessingAction,
                onAccept: () => _acceptDelivery(context, notifier, delivery.id),
              ),
            ),
          ),
      ];
    }

    return [
      const EmptyStateView(
        title: 'Você está offline',
        message: 'Fique online para ver as entregas disponíveis na sua cidade.',
        icon: Icons.wifi_off_rounded,
      ),
    ];
  }

  /// `409 DELIVERY_ALREADY_TAKEN` é fluxo normal em horário de pico — o
  /// backend já removeu a entrega da lista local; mostramos apenas um aviso
  /// não alarmante, nunca uma mensagem de erro técnica
  /// (`docs/DELIVERY_FLOW.md#a-corrida-de-aceite-crítico`).
  Future<void> _acceptDelivery(
    BuildContext context,
    DriverHomeController notifier,
    String deliveryId,
  ) async {
    try {
      await notifier.acceptDelivery(deliveryId);
    } on AppException catch (e) {
      if (!context.mounted) return;
      final message = e.code == 'DELIVERY_ALREADY_TAKEN'
          ? 'Essa entrega já foi aceita por outro entregador. Veja as demais disponíveis.'
          : e.message;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(message)));
    }
  }
}
