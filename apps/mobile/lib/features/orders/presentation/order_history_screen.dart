import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../application/order_history_controller.dart';
import 'widgets/order_list_tile.dart';

/// `GET /api/orders?page=` — pedidos anteriores do cliente
/// (`docs/API_CONTRACT.md#pedidos`).
class OrderHistoryScreen extends ConsumerWidget {
  const OrderHistoryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ordersAsync = ref.watch(orderHistoryControllerProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Meus pedidos')),
      body: ordersAsync.when(
        data: (orders) {
          if (orders.isEmpty) {
            return const EmptyStateView(
              title: 'Nenhum pedido ainda',
              message:
                  'Seus pedidos aparecerão aqui depois da primeira compra.',
              icon: Icons.receipt_long_outlined,
            );
          }
          final hasMore = ref
              .read(orderHistoryControllerProvider.notifier)
              .hasMore;
          return RefreshIndicator(
            onRefresh: () =>
                ref.read(orderHistoryControllerProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.lg),
              itemCount: orders.length + (hasMore ? 1 : 0),
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                if (index >= orders.length) {
                  ref.read(orderHistoryControllerProvider.notifier).loadMore();
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
                    child: LoadingIndicator(),
                  );
                }
                final order = orders[index];
                return OrderListTile(
                  order: order,
                  onTap: () => context.push(AppRoutes.orderTracking(order.id)),
                );
              },
            ),
          );
        },
        loading: () => const LoadingIndicator(),
        error: (error, stackTrace) => ErrorStateView(
          message: error is AppException
              ? error.message
              : 'Não foi possível carregar seus pedidos.',
          onRetry: () =>
              ref.read(orderHistoryControllerProvider.notifier).refresh(),
        ),
      ),
    );
  }
}
