import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
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
import '../application/order_tracking_controller.dart';
import '../domain/order.dart';
import 'widgets/order_status_timeline.dart';

class OrderTrackingScreen extends ConsumerWidget {
  const OrderTrackingScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = orderTrackingControllerProvider(orderId);
    final orderAsync = ref.watch(provider);

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
                'Feito em ${Formatters.dateTime(order.createdAtUtc)}',
                style: AppTypography.bodyMuted,
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        PedeLocalCard(child: OrderStatusTimeline(order: order)),
        if (order.shouldShowDeliveryCodeNotice) ...[
          const SizedBox(height: AppSpacing.lg),
          const _DeliveryCodeNotice(),
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
                order.paymentMethod.paymentMethodLabel,
                style: AppTypography.body,
              ),
              const SizedBox(height: AppSpacing.xxs),
              Text(
                order.paymentStatus.paymentStatusLabel,
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
        const SizedBox(height: AppSpacing.lg),
        Text('Endereço de entrega', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalCard(
          child: Row(
            children: [
              const Icon(Icons.location_on_outlined, color: AppColors.primary),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  address?.summary ?? 'Endereço não disponível',
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
        _TotalRow(label: 'Taxa de entrega', value: order.deliveryFee),
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

class _DeliveryCodeNotice extends StatelessWidget {
  const _DeliveryCodeNotice();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.accentLight,
        borderRadius: AppRadius.radiusLg,
        border: Border.all(color: AppColors.accent.withValues(alpha: 0.4)),
      ),
      child: const Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
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
          SizedBox(height: AppSpacing.sm),
          Text(
            'Informe este código ao entregador somente após receber seu pedido.',
            style: AppTypography.body,
          ),
          SizedBox(height: AppSpacing.xs),
          Text(
            'O código de 6 dígitos é gerado quando o pedido fica pronto. '
            'O aplicativo ainda não recebe o número do servidor — o entregador '
            'vai pedi-lo a você no momento da entrega.',
            style: AppTypography.caption,
          ),
        ],
      ),
    );
  }
}

extension on String {
  String get paymentMethodLabel => switch (this) {
    'Pix' => 'Pix',
    'Cash' => 'Dinheiro',
    'DebitOnDelivery' => 'Débito na entrega',
    'CreditOnDelivery' => 'Crédito na entrega',
    _ => this,
  };

  String get paymentStatusLabel => switch (this) {
    'Pending' => 'Pagamento pendente',
    'Paid' => 'Pagamento confirmado',
    'NotApplicable' => 'Pago na entrega',
    _ => this,
  };
}
