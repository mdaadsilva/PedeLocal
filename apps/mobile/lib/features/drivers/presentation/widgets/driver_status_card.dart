import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../domain/driver_profile.dart';

/// Card principal de status da Home Entregador — sempre visível no topo,
/// mostrando nome, aprovação e situação atual, com o botão de
/// online/offline quando permitido pelo backend
/// (`docs/DELIVERY_FLOW.md#pré-requisitos-do-entregador`).
class DriverStatusCard extends StatelessWidget {
  const DriverStatusCard({
    super.key,
    required this.profile,
    required this.cityName,
    required this.isToggling,
    required this.onGoOnline,
    required this.onGoOffline,
  });

  final DriverProfile profile;
  final String cityName;
  final bool isToggling;
  final VoidCallback onGoOnline;
  final VoidCallback onGoOffline;

  @override
  Widget build(BuildContext context) {
    return PedeLocalCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const CircleAvatar(
                radius: 26,
                backgroundColor: AppColors.primaryLight,
                child: Icon(
                  Icons.delivery_dining_rounded,
                  color: AppColors.primary,
                  size: 28,
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(profile.name, style: AppTypography.title),
                    const SizedBox(height: AppSpacing.xxs),
                    Row(
                      children: [
                        const Icon(
                          Icons.location_on_outlined,
                          size: 14,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: 2),
                        Text(cityName, style: AppTypography.bodyMuted),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
          Wrap(
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xs,
            children: [
              StatusBadge.driverApprovalStatus(profile.approvalStatus),
              if (profile.isApproved) StatusBadge.driverStatus(profile.status),
            ],
          ),
          if (profile.isApproved && !profile.isBlocked && !profile.isBusy) ...[
            const SizedBox(height: AppSpacing.lg),
            PedeLocalButton(
              label: profile.status == 'Available'
                  ? 'Ficar offline'
                  : 'Ficar online',
              variant: profile.status == 'Available'
                  ? PedeLocalButtonVariant.secondary
                  : PedeLocalButtonVariant.primary,
              isLoading: isToggling,
              onPressed: profile.status == 'Available'
                  ? onGoOffline
                  : onGoOnline,
            ),
          ],
        ],
      ),
    );
  }
}
