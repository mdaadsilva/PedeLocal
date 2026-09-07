import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_radius.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/utils/formatters.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../products/domain/product.dart';

/// Card de produto no catálogo do estabelecimento. Mostra apenas dados
/// reais do contrato (`ProductResponse`) — sem inventar avaliação/tempo de
/// preparo.
class ProductTile extends StatelessWidget {
  const ProductTile({super.key, required this.product, this.onTap});

  final Product product;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final disabled = !product.isPurchasable;

    return Opacity(
      opacity: disabled ? 0.6 : 1,
      child: PedeLocalCard(
        onTap: onTap,
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    product.name,
                    style: AppTypography.subtitle,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (product.description != null &&
                      product.description!.isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      product.description!,
                      style: AppTypography.bodyMuted,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                  const SizedBox(height: AppSpacing.xs),
                  Row(
                    children: [
                      if (product.hasPromotion) ...[
                        Text(
                          Formatters.currency(product.price),
                          style: AppTypography.caption.copyWith(
                            decoration: TextDecoration.lineThrough,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.xxs),
                      ],
                      Text(
                        Formatters.currency(product.displayPrice),
                        style: AppTypography.subtitle.copyWith(
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                  if (!product.isAvailable) ...[
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      'Indisponível',
                      style: AppTypography.caption.copyWith(
                        color: AppColors.error,
                      ),
                    ),
                  ] else if (product.isOutOfStock) ...[
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      'Sem estoque',
                      style: AppTypography.caption.copyWith(
                        color: AppColors.error,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            if (product.imageUrl != null)
              ClipRRect(
                borderRadius: AppRadius.radiusMd,
                child: Image.network(
                  product.imageUrl!,
                  width: 72,
                  height: 72,
                  fit: BoxFit.cover,
                  errorBuilder: (context, error, stackTrace) => _placeholder(),
                ),
              )
            else
              _placeholder(),
          ],
        ),
      ),
    );
  }

  Widget _placeholder() {
    return Container(
      width: 72,
      height: 72,
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        borderRadius: AppRadius.radiusMd,
      ),
      child: const Icon(Icons.fastfood_rounded, color: AppColors.primary),
    );
  }
}
