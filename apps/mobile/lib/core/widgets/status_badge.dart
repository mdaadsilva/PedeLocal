import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_radius.dart';
import '../theme/app_spacing.dart';
import '../theme/app_typography.dart';

/// Badge de status genérico (pílula colorida com texto), usado para exibir
/// os enums de status da API (`docs/FRONTEND_GUIDE.md#enums-de-status`) de
/// forma visual e consistente. Sempre construir via um dos factories
/// nomeados (`StatusBadge.driverStatus`, `.driverApprovalStatus`) para
/// garantir que o mapeamento de cor/label fique centralizado num único
/// lugar, em vez de espalhado pelas telas.
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, required this.color});

  final String label;
  final Color color;

  /// Mapeia `Driver.status` (`Offline | Available | Busy | Blocked`).
  factory StatusBadge.driverStatus(String status) {
    switch (status) {
      case 'Available':
        return StatusBadge(
          label: 'Disponível',
          color: AppColors.statusAvailable,
        );
      case 'Busy':
        return StatusBadge(label: 'Em entrega', color: AppColors.statusBusy);
      case 'Blocked':
        return StatusBadge(label: 'Bloqueado', color: AppColors.statusBlocked);
      case 'Offline':
      default:
        return StatusBadge(label: 'Offline', color: AppColors.statusOffline);
    }
  }

  /// Mapeia `Driver.approvalStatus` (`PendingApproval | Approved | Rejected`).
  factory StatusBadge.driverApprovalStatus(String status) {
    switch (status) {
      case 'Approved':
        return StatusBadge(label: 'Aprovado', color: AppColors.statusAvailable);
      case 'Rejected':
        return StatusBadge(
          label: 'Cadastro rejeitado',
          color: AppColors.statusBlocked,
        );
      case 'PendingApproval':
      default:
        return StatusBadge(
          label: 'Aguardando aprovação',
          color: AppColors.statusPending,
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: AppRadius.radiusPill,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: AppSpacing.xxs),
          Text(
            label,
            style: AppTypography.label.copyWith(
              color: color,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
