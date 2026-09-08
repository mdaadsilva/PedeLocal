import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/notifications/recent_notification_feedback.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_radius.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../../addresses/application/addresses_controller.dart';
import '../../addresses/domain/address.dart';
import '../../establishments/application/establishment_lookup_provider.dart';
import '../application/delivery_code_controller.dart';
import '../application/order_tracking_controller.dart';
import '../domain/delivery_code.dart';
import '../domain/order.dart';
import 'widgets/order_status_timeline.dart';

class OrderTrackingScreen extends ConsumerWidget {
  const OrderTrackingScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = orderTrackingControllerProvider(orderId);
    final orderAsync = ref.watch(provider);

    ref.listen(provider, (previous, next) {
      final previousStatus = previous?.value?.status;
      final nextStatus = next.value?.status;
      if (previousStatus == null ||
          nextStatus == null ||
          previousStatus == nextStatus) {
        return;
      }
      final feedback = ref.read(recentNotificationFeedbackProvider);
      if (!feedback.claim('order:$orderId')) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(nextStatus.orderStatusLabel)),
      );
    });

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Acompanhar pedido')),
      body: orderAsync.when(
        data: (order) => RefreshIndicator(
          onRefresh: () => ref.read(provider.notifier).refresh(),
          child: _OrderTrackingBody(order: order),
        ),
        loading: () => const LoadingIndicator(),
        error: (error, stackTrace) => ErrorStateView(
          message: error is AppException
              ? error.message
              : 'Não foi possível carregar o pedido.',
          onRetry: () => ref.read(provider.notifier).refresh(),
        ),
      ),
    );
  }
}

class _OrderTrackingBody extends ConsumerWidget {
  const _OrderTrackingBody({required this.order});

  final Order order;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final establishmentAsync = ref.watch(
      establishmentLookupProvider(order.establishmentId),
    );
    final addressesAsync = ref.watch(addressesControllerProvider);

    Address? address;
    for (final candidate in addressesAsync.value ?? const <Address>[]) {
      if (candidate.id == order.addressId) {
        address = candidate;
        break;
      }
    }

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        PedeLocalCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      establishmentAsync.value?.name ?? 'Estabelecimento',
                      style: AppTypography.title,
                    ),
                  ),
                  Text(
                    '#${order.id.substring(0, 8)}',
                    style: AppTypography.caption,
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.xxs),
              Text(
                '${order.isPickup ? 'Retirada' : 'Entrega'} · ${Formatters.dateTime(order.createdAtUtc)}',
                style: AppTypography.bodyMuted,
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        PedeLocalCard(child: OrderStatusTimeline(order: order)),
        if (order.shouldShowDeliveryCodeNotice) ...[
          const SizedBox(height: AppSpacing.lg),
          _DeliveryCodeCard(orderId: order.id),
        ],
        if (order.shouldShowPickupReadyNotice) ...[
          const SizedBox(height: AppSpacing.lg),
          const _PickupReadyNotice(),
        ],
        const SizedBox(height: AppSpacing.lg),
        Text('Itens', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalCard(
          child: Column(
            children: [
              for (final item in order.items) _OrderItemRow(item: item),
              const Divider(height: AppSpacing.lg),
              _TotalsSection(order: order),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        Text('Pagamento', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                order.paymentMethod.paymentMethodLabel(isPickup: order.isPickup),
                style: AppTypography.body,
              ),
              const SizedBox(height: AppSpacing.xxs),
              Text(
                order.paymentStatus.paymentStatusLabel(isPickup: order.isPickup),
                style: AppTypography.bodyMuted,
              ),
              if (order.changeForAmount != null) ...[
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  'Troco para ${Formatters.currency(order.changeForAmount!)}',
                  style: AppTypography.bodyMuted,
                ),
              ],
            ],
          ),
        ),
        if (order.notes != null && order.notes!.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.lg),
          Text('Observações', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalCard(
            child: Text(order.notes!, style: AppTypography.body),
          ),
        ],
        const SizedBox(height: AppSpacing.lg),
        Text(
          order.isPickup ? 'Retirada' : 'Endereço de entrega',
          style: AppTypography.title,
        ),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalCard(
          child: Row(
            children: [
              Icon(
                order.isPickup
                    ? Icons.storefront_outlined
                    : Icons.location_on_outlined,
                color: AppColors.primary,
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  order.isPickup
                      ? 'Retire em ${establishmentAsync.value?.name ?? 'o estabelecimento'}'
                      : (address?.summary ?? 'Endereço não disponível'),
                  style: AppTypography.body,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.xl),
      ],
    );
  }
}

class _OrderItemRow extends StatelessWidget {
  const _OrderItemRow({required this.item});

  final OrderItem item;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 24,
            height: 24,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: AppRadius.radiusSm,
            ),
            child: Text(
              '${item.quantity}x',
              style: AppTypography.caption.copyWith(color: AppColors.primary),
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.productName, style: AppTypography.body),
                if (item.options.isNotEmpty)
                  Text(
                    item.options.map((o) => o.optionName).join(', '),
                    style: AppTypography.caption,
                  ),
              ],
            ),
          ),
          Text(Formatters.currency(item.lineTotal), style: AppTypography.body),
        ],
      ),
    );
  }
}

class _TotalsSection extends StatelessWidget {
  const _TotalsSection({required this.order});

  final Order order;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _TotalRow(label: 'Subtotal', value: order.subtotal),
        _TotalRow(
          label: 'Taxa de entrega',
          value: order.isPickup ? 0 : order.deliveryFee,
        ),
        const SizedBox(height: AppSpacing.xs),
        _TotalRow(label: 'Total', value: order.total, isEmphasis: true),
      ],
    );
  }
}

class _TotalRow extends StatelessWidget {
  const _TotalRow({
    required this.label,
    required this.value,
    this.isEmphasis = false,
  });

  final String label;
  final double value;
  final bool isEmphasis;

  @override
  Widget build(BuildContext context) {
    final style = isEmphasis ? AppTypography.subtitle : AppTypography.body;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Text(label, style: style),
          const Spacer(),
          Text(Formatters.currency(value), style: style),
        ],
      ),
    );
  }
}

class _PickupReadyNotice extends StatelessWidget {
  const _PickupReadyNotice();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        borderRadius: AppRadius.radiusLg,
      ),
      child: const Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.storefront_outlined, color: AppColors.primary),
              SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  'Pronto para retirada',
                  style: AppTypography.subtitle,
                ),
              ),
            ],
          ),
          SizedBox(height: AppSpacing.sm),
          Text(
            'Seu pedido está pronto. Retire no estabelecimento. '
            'Não há entregador nem código de confirmação neste fluxo.',
            style: AppTypography.body,
          ),
        ],
      ),
    );
  }
}

class _DeliveryCodeCard extends ConsumerWidget {
  const _DeliveryCodeCard({required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final codeAsync = ref.watch(deliveryCodeControllerProvider(orderId));

    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.accentLight,
        borderRadius: AppRadius.radiusLg,
        border: Border.all(color: AppColors.accent.withValues(alpha: 0.4)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.lock_outline_rounded, color: AppColors.accent),
              SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  'Código de confirmação da entrega',
                  style: AppTypography.subtitle,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          const Text(
            'Informe este código ao entregador somente após receber seu pedido.',
            style: AppTypography.body,
          ),
          const SizedBox(height: AppSpacing.sm),
          codeAsync.when(
            data: (DeliveryCode code) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  code.code,
                  style: AppTypography.title.copyWith(
                    letterSpacing: 4,
                    fontSize: 28,
                  ),
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  'Válido até ${Formatters.dateTime(code.expiresAtUtc)}',
                  style: AppTypography.caption,
                ),
              ],
            ),
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: LoadingIndicator(),
            ),
            error: (error, stackTrace) {
              final exception = error is AppException ? error : null;
              final message = switch (exception?.code) {
                'DELIVERY_CODE_NOT_FOUND' =>
                  'O código ainda não está disponível.',
                'DELIVERY_CODE_EXPIRED' =>
                  'Este código expirou. Fale com o estabelecimento.',
                'DELIVERY_CODE_ALREADY_USED' =>
                  'A entrega já foi confirmada com este código.',
                _ =>
                  exception?.message ??
                      'Não foi possível carregar o código de confirmação.',
              };
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(message, style: AppTypography.bodyMuted),
                  TextButton(
                    onPressed: () => ref
                        .read(deliveryCodeControllerProvider(orderId).notifier)
                        .refresh(),
                    child: const Text('Tentar novamente'),
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

extension on String {
  String paymentMethodLabel({required bool isPickup}) => switch (this) {
    'Pix' => 'Pix',
    'Cash' => 'Dinheiro',
    'DebitOnDelivery' =>
      isPickup ? 'Débito na retirada' : 'Débito na entrega',
    'CreditOnDelivery' =>
      isPickup ? 'Crédito na retirada' : 'Crédito na entrega',
    _ => this,
  };

  String paymentStatusLabel({required bool isPickup}) => switch (this) {
    'Pending' => 'Pagamento pendente',
    'Paid' => 'Pagamento confirmado',
    'NotApplicable' => isPickup ? 'Pago na retirada' : 'Pago na entrega',
    _ => this,
  };
}
