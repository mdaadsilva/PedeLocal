import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/utils/formatters.dart';
import '../../domain/order.dart';

/// Timeline visual dos estados reais do pedido
/// (`docs/ORDER_FLOW.md#máquina-de-estados`). Passos concluídos vêm do
/// `statusHistory` retornado pelo backend — nada é inferido/inventado.
class OrderStatusTimeline extends StatelessWidget {
  const OrderStatusTimeline({super.key, required this.order});

  final Order order;

  @override
  Widget build(BuildContext context) {
    if (order.isCancelled) {
      return _CancelledBanner(order: order);
    }

    final historyByStatus = {for (final h in order.statusHistory) h.status: h};
    final currentIndex = Order.deliveryLifecycle.indexOf(order.status);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < Order.deliveryLifecycle.length; i++)
          _TimelineStep(
            label: Order.deliveryLifecycle[i].orderStatusLabel,
            changedAtUtc:
                historyByStatus[Order.deliveryLifecycle[i]]?.changedAtUtc,
            isCompleted: i <= currentIndex,
            isCurrent: i == currentIndex,
            isLast: i == Order.deliveryLifecycle.length - 1,
          ),
      ],
    );
  }
}

class _TimelineStep extends StatelessWidget {
  const _TimelineStep({
    required this.label,
    required this.changedAtUtc,
    required this.isCompleted,
    required this.isCurrent,
    required this.isLast,
  });

  final String label;
  final DateTime? changedAtUtc;
  final bool isCompleted;
  final bool isCurrent;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final color = isCompleted ? AppColors.primary : AppColors.textDisabled;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                width: 20,
                height: 20,
                decoration: BoxDecoration(
                  color: isCompleted ? AppColors.primary : AppColors.surfaceAlt,
                  shape: BoxShape.circle,
                  border: Border.all(color: color, width: 2),
                ),
                child: isCompleted
                    ? const Icon(
                        Icons.check_rounded,
                        size: 14,
                        color: Colors.white,
                      )
                    : null,
              ),
              if (!isLast)
                Expanded(
                  child: Container(
                    width: 2,
                    color: isCompleted ? AppColors.primary : AppColors.divider,
                  ),
                ),
            ],
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style:
                        (isCurrent
                                ? AppTypography.subtitle
                                : AppTypography.body)
                            .copyWith(
                              color: isCompleted
                                  ? AppColors.textPrimary
                                  : AppColors.textDisabled,
                            ),
                  ),
                  if (changedAtUtc != null)
                    Text(
                      Formatters.dateTime(changedAtUtc!),
                      style: AppTypography.caption,
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CancelledBanner extends StatelessWidget {
  const _CancelledBanner({required this.order});

  final Order order;

  @override
  Widget build(BuildContext context) {
    final cancelledEntry = order.statusHistory
        .where((h) => h.status == 'Cancelled')
        .cast<OrderStatusHistoryEntry?>()
        .firstWhere((_) => true, orElse: () => null);

    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.error.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          const Icon(Icons.cancel_outlined, color: AppColors.error),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Pedido cancelado', style: AppTypography.subtitle),
                if (cancelledEntry?.notes != null)
                  Text(cancelledEntry!.notes!, style: AppTypography.bodyMuted),
                if (cancelledEntry != null)
                  Text(
                    Formatters.dateTime(cancelledEntry.changedAtUtc),
                    style: AppTypography.caption,
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
