import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../domain/driver_profile.dart';

/// Card principal de status da Home Entregador — sempre visível no topo,
/// mostrando nome, aprovação (`onboardingStatus`) e elegibilidade
/// (`eligibilityStatus`), com o botão de online/offline só quando o backend
/// permite (`docs/DRIVER_ONBOARDING.md#elegibilidade-computada-não-persistida`).
class DriverStatusCard extends StatelessWidget {
  const DriverStatusCard({
    super.key,
    required this.profile,
    required this.cityName,
    required this.isToggling,
    required this.onGoOnline,
    required this.onGoOffline,
    required this.onCompleteOnboarding,
  });

  final DriverProfile profile;
  final String cityName;
  final bool isToggling;
  final VoidCallback onGoOnline;
  final VoidCallback onGoOffline;

  /// Abre o hub de onboarding (CNH/veículo) — sempre disponível, mesmo já
  /// aprovado, para reenviar documentos vencidos ou atualizar dados.
  final VoidCallback onCompleteOnboarding;

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
              StatusBadge.driverOnboardingStatus(profile.onboardingStatus),
              if (profile.isEligible) StatusBadge.driverStatus(profile.status)
              else
                StatusBadge.driverEligibilityStatus(profile.eligibilityStatus),
            ],
          ),
          if (profile.isEligible && !profile.isBusy) ...[
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
          if (!profile.isEligible) ...[
            const SizedBox(height: AppSpacing.md),
            Text(profile.eligibilityMessage, style: AppTypography.bodyMuted),
            const SizedBox(height: AppSpacing.sm),
            PedeLocalButton(
              label:
                  profile.eligibilityStatus == 'DocumentExpired' ||
                      profile.eligibilityStatus == 'Rejected'
                  ? 'Atualizar documentos'
                  : 'Ver status do cadastro',
              variant: PedeLocalButtonVariant.secondary,
              onPressed: onCompleteOnboarding,
            ),
          ],
        ],
      ),
    );
  }
}
