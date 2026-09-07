import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_radius.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../establishments/domain/establishment.dart';

/// Card de estabelecimento da Home Cliente.
///
/// Mostra apenas dados que existem de fato no contrato
/// (`EstablishmentResponse`): nome e status operacional. Avaliação, tempo de
/// entrega, desconto e foto **não existem** neste endpoint — não são
/// inventados aqui (instrução do produto).
class EstablishmentCard extends StatelessWidget {
  const EstablishmentCard({super.key, required this.establishment, this.onTap});

  final Establishment establishment;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final isOpen = establishment.isOpenNow;

    return PedeLocalCard(
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: AppRadius.radiusMd,
            ),
            child: const Icon(
              Icons.storefront_rounded,
              color: AppColors.primary,
              size: 28,
            ),
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  establishment.name,
                  style: AppTypography.subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: AppSpacing.xxs),
                Row(
                  children: [
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: isOpen
                            ? AppColors.success
                            : AppColors.textDisabled,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.xxs),
                    Text(
                      isOpen ? 'Aberto agora' : 'Fechado no momento',
                      style: AppTypography.caption,
                    ),
                  ],
                ),
              ],
            ),
          ),
          const Icon(
            Icons.chevron_right_rounded,
            color: AppColors.textSecondary,
          ),
        ],
      ),
    );
  }
}
