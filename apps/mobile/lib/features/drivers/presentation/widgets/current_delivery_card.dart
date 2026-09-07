import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../../core/widgets/pedelocal_text_field.dart';
import '../../domain/delivery.dart';
import 'delivery_tile.dart';

/// Card da entrega ativa do entregador (`status == BUSY`).
///
/// Mostra a ação correta para o status atual da entrega
/// (`docs/DELIVERY_FLOW.md#retirar-sair-para-entrega-confirmar`):
/// - `Assigned` → marcar retirada no estabelecimento;
/// - `PickedUp` → marcar saída para entrega;
/// - `OutForDelivery` → informar o código de 6 dígitos (recebido
///   verbalmente do cliente) para concluir.
///
/// IMPORTANTE: `DeliveryResponse` não traz endereço do estabelecimento,
/// endereço do cliente nem os itens do pedido — por isso não há botão
/// "Abrir rota" nem lista de itens aqui. Isso é uma pendência documentada
/// do backend, não uma omissão do frontend.
class CurrentDeliveryCard extends StatefulWidget {
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
  State<CurrentDeliveryCard> createState() => _CurrentDeliveryCardState();
}

class _CurrentDeliveryCardState extends State<CurrentDeliveryCard> {
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  String get _shortId => widget.delivery.id.split('-').first;

  @override
  Widget build(BuildContext context) {
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
