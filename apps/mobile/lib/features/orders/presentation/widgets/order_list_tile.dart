import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_radius.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/utils/formatters.dart';
import '../../../../core/widgets/pedelocal_card.dart';
import '../../../establishments/application/establishment_lookup_provider.dart';
import '../../domain/order.dart';

class OrderListTile extends ConsumerWidget {
  const OrderListTile({super.key, required this.order, this.onTap});

  final Order order;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final establishmentAsync = ref.watch(
      establishmentLookupProvider(order.establishmentId),
    );
    final color = order.isCancelled
        ? AppColors.error
        : order.isCompleted
        ? AppColors.success
        : AppColors.accent;

    return PedeLocalCard(
      onTap: onTap,
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  establishmentAsync.value?.name ?? 'Estabelecimento',
                  style: AppTypography.subtitle,
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  '${order.isPickup ? 'Retirada' : 'Entrega'} · ${Formatters.dateTime(order.createdAtUtc)}',
                  style: AppTypography.caption,
                ),
                const SizedBox(height: AppSpacing.xs),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: AppSpacing.sm,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: AppRadius.radiusPill,
                  ),
                  child: Text(
                    order.status.orderStatusLabel,
                    style: AppTypography.label.copyWith(color: color),
                  ),
                ),
              ],
            ),
          ),
          Text(Formatters.currency(order.total), style: AppTypography.subtitle),
        ],
      ),
    );
  }
}
