import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_radius.dart';
import '../theme/app_spacing.dart';
import '../theme/app_typography.dart';

/// Badge de status genérico (pílula colorida com texto), usado para exibir
/// os enums de status da API (`docs/FRONTEND_GUIDE.md#enums-de-status`) de
/// forma visual e consistente. Sempre construir via um dos factories
/// nomeados (`StatusBadge.driverStatus`, `.driverOnboardingStatus`,
/// `.driverEligibilityStatus`) para
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

  /// Mapeia `Driver.onboardingStatus` (`PendingDocuments | UnderReview |
  /// Approved | Rejected` — `docs/DRIVER_ONBOARDING.md`).
  factory StatusBadge.driverOnboardingStatus(String status) {
    switch (status) {
      case 'Approved':
        return StatusBadge(label: 'Aprovado', color: AppColors.statusAvailable);
      case 'Rejected':
        return StatusBadge(
          label: 'Não aprovado',
          color: AppColors.statusBlocked,
        );
      case 'UnderReview':
        return StatusBadge(label: 'Em análise', color: AppColors.statusPending);
      case 'PendingDocuments':
      default:
        return StatusBadge(
          label: 'Documentação pendente',
          color: AppColors.statusPending,
        );
    }
  }

  /// Mapeia `Driver.eligibilityStatus` (`Eligible | PendingDocuments |
  /// UnderReview | Rejected | Blocked | DocumentExpired` — computado, ver
  /// `docs/DRIVER_ONBOARDING.md#elegibilidade-computada-não-persistida`).
  factory StatusBadge.driverEligibilityStatus(String status) {
    switch (status) {
      case 'Eligible':
        return StatusBadge(
          label: 'Apto a trabalhar',
          color: AppColors.statusAvailable,
        );
      case 'Rejected':
        return StatusBadge(label: 'Não aprovado', color: AppColors.statusBlocked);
      case 'Blocked':
        return StatusBadge(label: 'Suspenso', color: AppColors.statusBlocked);
      case 'DocumentExpired':
        return StatusBadge(label: 'CNH vencida', color: AppColors.statusBlocked);
      case 'UnderReview':
        return StatusBadge(label: 'Em análise', color: AppColors.statusPending);
      case 'PendingDocuments':
      default:
        return StatusBadge(
          label: 'Documentação pendente',
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
