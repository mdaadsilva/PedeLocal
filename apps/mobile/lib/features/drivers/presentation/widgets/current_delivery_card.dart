import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/maps/maps_launcher.dart';
import '../../../../core/network/app_exception.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/utils/formatters.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../../core/widgets/pedelocal_text_field.dart';
import '../../../addresses/domain/address.dart';
import '../../application/delivery_details_controller.dart';
import '../../domain/delivery.dart';
import '../../domain/delivery_detail.dart';
import 'delivery_tile.dart';

/// Card da entrega ativa (`status == BUSY`).
///
/// Ações por status (`docs/DELIVERY_FLOW.md`):
/// - `Assigned` → retirada no estabelecimento;
/// - `PickedUp` → saída para entrega;
/// - `OutForDelivery` → confirmar código de 6 dígitos.
///
/// Detalhes vêm de `GET /api/driver/deliveries/{id}/details`.
class CurrentDeliveryCard extends ConsumerStatefulWidget {
  const CurrentDeliveryCard({
    super.key,
    required this.delivery,
    required this.isProcessing,
    required this.onPickup,
    required this.onOutForDelivery,
    required this.onConfirm,
  });

  final Delivery delivery;
  final bool isProcessing;
  final VoidCallback onPickup;
  final VoidCallback onOutForDelivery;
  final ValueChanged<String> onConfirm;

  @override
  ConsumerState<CurrentDeliveryCard> createState() =>
      _CurrentDeliveryCardState();
}

class _CurrentDeliveryCardState extends ConsumerState<CurrentDeliveryCard> {
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  String get _shortId => widget.delivery.id.split('-').first;

  Future<void> _openRoute(Address address) async {
    final opened = await MapsLauncher.openRoute(
      address.latitude,
      address.longitude,
    );
    if (!opened && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Não foi possível abrir o mapa.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final detailsAsync = ref.watch(
      deliveryDetailsControllerProvider(widget.delivery.id),
    );

    return PedeLocalCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: const BoxDecoration(
                  color: AppColors.accentLight,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.local_shipping_outlined,
                  color: AppColors.accent,
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Entrega #$_shortId', style: AppTypography.subtitle),
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      deliveryStatusLabel(widget.delivery.status),
                      style: AppTypography.bodyMuted,
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
          detailsAsync.when(
            data: (details) => _DeliveryDetailsBody(
              details: details,
              onOpenEstablishmentRoute: () =>
                  _openRoute(details.establishment.address),
              onOpenDeliveryRoute: () => _openRoute(details.deliveryAddress),
            ),
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: Center(
                child: SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            ),
            error: (error, stackTrace) {
              final message = error is AppException
                  ? error.message
                  : 'Não foi possível carregar os detalhes da entrega.';
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(message, style: AppTypography.bodyMuted),
                  TextButton(
                    onPressed: () => ref
                        .read(
                          deliveryDetailsControllerProvider(
                            widget.delivery.id,
                          ).notifier,
                        )
                        .refresh(),
                    child: const Text('Tentar novamente'),
                  ),
                ],
              );
            },
          ),
          ..._buildAction(),
        ],
      ),
    );
  }

  List<Widget> _buildAction() {
    switch (widget.delivery.status) {
      case 'Assigned':
        return [
          const Divider(),
          const SizedBox(height: AppSpacing.xs),
          const Text(
            'Vá até o estabelecimento buscar o pedido.',
            style: AppTypography.bodyMuted,
          ),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalButton(
            label: 'Marcar retirada no estabelecimento',
            icon: Icons.storefront_outlined,
            isLoading: widget.isProcessing,
            onPressed: widget.onPickup,
          ),
        ];
      case 'PickedUp':
        return [
          const Divider(),
          const SizedBox(height: AppSpacing.xs),
          const Text(
            'Pedido retirado. Quando sair para entregar, marque abaixo.',
            style: AppTypography.bodyMuted,
          ),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalButton(
            label: 'Marcar saída para entrega',
            icon: Icons.two_wheeler_outlined,
            isLoading: widget.isProcessing,
            onPressed: widget.onOutForDelivery,
          ),
        ];
      case 'OutForDelivery':
        return [
          const Divider(),
          const SizedBox(height: AppSpacing.xs),
          Container(
            padding: const EdgeInsets.all(AppSpacing.sm),
            decoration: BoxDecoration(
              color: AppColors.accentLight,
              borderRadius: BorderRadius.circular(8),
            ),
            child: const Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.info_outline, color: AppColors.accent, size: 18),
                SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    'Peça o código de confirmação de 6 dígitos ao cliente '
                    'somente após entregar o pedido a ele.',
                    style: AppTypography.caption,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalTextField(
            controller: _codeController,
            label: 'Código de confirmação (6 dígitos)',
            keyboardType: TextInputType.number,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalButton(
            label: 'Concluir entrega',
            icon: Icons.check_circle_outline,
            isLoading: widget.isProcessing,
            onPressed: _codeController.text.trim().length == 6
                ? () => widget.onConfirm(_codeController.text.trim())
                : null,
          ),
        ];
      default:
        return const [];
    }
  }
}

class _DeliveryDetailsBody extends StatelessWidget {
  const _DeliveryDetailsBody({
    required this.details,
    required this.onOpenEstablishmentRoute,
    required this.onOpenDeliveryRoute,
  });

  final DeliveryDetail details;
  final VoidCallback onOpenEstablishmentRoute;
  final VoidCallback onOpenDeliveryRoute;

  @override
  Widget build(BuildContext context) {
    final order = details.order;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(details.establishment.name, style: AppTypography.subtitle),
        const SizedBox(height: AppSpacing.xxs),
        Text(details.establishment.phone, style: AppTypography.bodyMuted),
        const SizedBox(height: AppSpacing.xs),
        Text(
          details.establishment.address.summary,
          style: AppTypography.bodyMuted,
        ),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalButton(
          label: 'Rota até o estabelecimento',
          icon: Icons.storefront_outlined,
          variant: PedeLocalButtonVariant.secondary,
          onPressed: onOpenEstablishmentRoute,
        ),
        const SizedBox(height: AppSpacing.md),
        Text('Entregar em', style: AppTypography.subtitle),
        const SizedBox(height: AppSpacing.xxs),
        Text(details.deliveryAddress.summary, style: AppTypography.body),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalButton(
          label: 'Abrir rota até o cliente',
          icon: Icons.map_outlined,
          variant: PedeLocalButtonVariant.secondary,
          onPressed: onOpenDeliveryRoute,
        ),
        const SizedBox(height: AppSpacing.md),
        Text('Itens', style: AppTypography.subtitle),
        const SizedBox(height: AppSpacing.xs),
        for (final item in order.items)
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.xs),
            child: Text(
              '${item.quantity}x ${item.productName}'
              '${item.options.isEmpty ? '' : ' (${item.options.map((o) => o.optionName).join(', ')})'}',
              style: AppTypography.body,
            ),
          ),
        const SizedBox(height: AppSpacing.sm),
        Text(
          'Pagamento: ${order.paymentMethod.driverPaymentLabel}',
          style: AppTypography.body,
        ),
        if (order.changeForAmount != null)
          Text(
            'Troco para ${Formatters.currency(order.changeForAmount!)}',
            style: AppTypography.bodyMuted,
          ),
        Text(
          'Total ${Formatters.currency(order.total)} · taxa ${Formatters.currency(order.deliveryFee)}',
          style: AppTypography.bodyMuted,
        ),
        if (order.notes != null && order.notes!.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.sm),
          Text('Observações', style: AppTypography.subtitle),
          const SizedBox(height: AppSpacing.xxs),
          Text(order.notes!, style: AppTypography.body),
        ],
      ],
    );
  }
}

extension on String {
  String get driverPaymentLabel => switch (this) {
    'Pix' => 'Pix',
    'Cash' => 'Dinheiro',
    'DebitOnDelivery' => 'Débito na entrega',
    'CreditOnDelivery' => 'Crédito na entrega',
    _ => this,
  };
}
