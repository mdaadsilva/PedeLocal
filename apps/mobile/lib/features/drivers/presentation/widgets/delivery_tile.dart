import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../domain/delivery.dart';

/// Item de entrega — usado na lista de disponíveis. Mostra somente os
/// campos que existem em `DeliveryResponse` (sem inventar endereço,
/// estabelecimento ou valor, que não vêm nesse contrato).
///
/// Quando [onAccept] é informado, mostra o botão "Aceitar entrega"
/// (`docs/DELIVERY_FLOW.md#ver-e-aceitar-entregas-disponíveis`).
class DeliveryTile extends StatelessWidget {
  const DeliveryTile({
    super.key,
    required this.delivery,
    this.onAccept,
    this.isProcessing = false,
  });

  final Delivery delivery;
  final VoidCallback? onAccept;
  final bool isProcessing;

  String get _shortId => delivery.id.split('-').first;

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
                      deliveryStatusLabel(delivery.status),
                      style: AppTypography.bodyMuted,
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (onAccept != null) ...[
            const SizedBox(height: AppSpacing.md),
            PedeLocalButton(
              label: 'Aceitar entrega',
              icon: Icons.check_circle_outline,
              isLoading: isProcessing,
              onPressed: onAccept,
            ),
          ],
        ],
      ),
    );
  }
}

/// Rótulo amigável para `Delivery.status`
/// (`Available | Assigned | PickedUp | OutForDelivery | Completed |
/// Cancelled`), reutilizado nas telas de entregas disponíveis e atual.
String deliveryStatusLabel(String status) {
  switch (status) {
    case 'Available':
      return 'Aguardando um entregador';
    case 'Assigned':
      return 'Aceita — a caminho do estabelecimento';
    case 'PickedUp':
      return 'Retirada no estabelecimento';
    case 'OutForDelivery':
      return 'Saiu para entrega';
    case 'Completed':
      return 'Entrega concluída';
    case 'Cancelled':
      return 'Entrega cancelada';
    default:
      return status;
  }
}
