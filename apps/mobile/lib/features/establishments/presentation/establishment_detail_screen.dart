import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_radius.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/skeleton_box.dart';
import '../../cart/presentation/widgets/cart_badge.dart';
import '../../products/presentation/product_detail_sheet.dart';
import '../application/establishment_detail_controller.dart';
import '../application/establishment_detail_state.dart';
import 'widgets/product_tile.dart';

class EstablishmentDetailScreen extends ConsumerWidget {
  const EstablishmentDetailScreen({super.key, required this.establishmentId});

  final String establishmentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = establishmentDetailControllerProvider(establishmentId);
    final detailAsync = ref.watch(provider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(detailAsync.value?.establishment.name ?? 'Estabelecimento'),
        actions: [CartBadge(onTap: () => context.push(AppRoutes.cart))],
      ),
      body: detailAsync.when(
        data: (state) => _EstablishmentDetailBody(
          establishmentId: establishmentId,
          state: state,
          onRefresh: () => ref.read(provider.notifier).refresh(),
        ),
        loading: () => const _LoadingSkeleton(),
        error: (error, stackTrace) => ErrorStateView(
          message: error is AppException
              ? error.message
              : 'Não foi possível carregar este estabelecimento.',
          onRetry: () => ref.read(provider.notifier).refresh(),
        ),
      ),
    );
  }
}

class _EstablishmentDetailBody extends StatelessWidget {
  const _EstablishmentDetailBody({
    required this.establishmentId,
    required this.state,
    required this.onRefresh,
  });

  final String establishmentId;
  final EstablishmentDetailState state;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final establishment = state.establishment;
    final canOrder = state.canReceiveNewOrders;

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(establishment.name, style: AppTypography.headline),
                    const SizedBox(height: AppSpacing.xxs),
                    if (state.categoryName != null)
                      Text(state.categoryName!, style: AppTypography.bodyMuted),
                  ],
                ),
              ),
              _OpenStatusBadge(
                isOpen: establishment.isOpenNow,
                isBlocked: establishment.isCommerciallyBlocked,
              ),
            ],
          ),
          if (!canOrder) ...[
            const SizedBox(height: AppSpacing.md),
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.accentLight,
                borderRadius: AppRadius.radiusMd,
              ),
              child: Row(
                children: [
                  const Icon(Icons.schedule_rounded, color: AppColors.accent),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      establishment.isCommerciallyBlocked
                          ? 'Este estabelecimento não está aceitando novos pedidos no momento.'
                          : 'Este estabelecimento está fechado no momento.',
                      style: AppTypography.body,
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.lg),
          Text('Cardápio', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          if (state.products.isEmpty)
            const Padding(
              padding: EdgeInsets.only(top: AppSpacing.xl),
              child: EmptyStateView(
                title: 'Nenhum produto disponível',
                message: 'Este estabelecimento ainda não cadastrou produtos disponíveis.',
                icon: Icons.no_food_outlined,
              ),
            )
          else
            ..._buildProductGroups(context, state, canOrder),
        ],
      ),
    );
  }

  List<Widget> _buildProductGroups(
    BuildContext context,
    EstablishmentDetailState state,
    bool canOrder,
  ) {
    final groups = state.productsGroupedByMenuCategory;
    final widgets = <Widget>[];
    for (var i = 0; i < groups.length; i++) {
      if (i > 0) {
        widgets.add(const SizedBox(height: AppSpacing.sm));
      }
      for (final product in groups[i]) {
        widgets.add(
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.sm),
            child: ProductTile(
              product: product,
              onTap: (canOrder && product.isPurchasable)
                  ? () => showProductDetailSheet(
                      context,
                      product: product,
                      establishmentId: establishmentId,
                      establishmentName: state.establishment.name,
                    )
                  : null,
            ),
          ),
        );
      }
    }
    return widgets;
  }
}

class _OpenStatusBadge extends StatelessWidget {
  const _OpenStatusBadge({required this.isOpen, required this.isBlocked});

  final bool isOpen;
  final bool isBlocked;

  @override
  Widget build(BuildContext context) {
    final label = isBlocked ? 'Indisponível' : (isOpen ? 'Aberto' : 'Fechado');
    final color = isBlocked
        ? AppColors.error
        : (isOpen ? AppColors.success : AppColors.textSecondary);

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: AppRadius.radiusPill,
      ),
      child: Text(label, style: AppTypography.label.copyWith(color: color)),
    );
  }
}

class _LoadingSkeleton extends StatelessWidget {
  const _LoadingSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: const [
        SkeletonBox(width: 200, height: 24),
        SizedBox(height: AppSpacing.sm),
        SkeletonBox(width: 120, height: 14),
        SizedBox(height: AppSpacing.lg),
        SkeletonBox(width: 100, height: 18),
        SizedBox(height: AppSpacing.sm),
        _SkeletonProductTile(),
        SizedBox(height: AppSpacing.sm),
        _SkeletonProductTile(),
        SizedBox(height: AppSpacing.sm),
        _SkeletonProductTile(),
      ],
    );
  }
}

class _SkeletonProductTile extends StatelessWidget {
  const _SkeletonProductTile();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.radiusLg,
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                SkeletonBox(width: double.infinity, height: 16),
                SizedBox(height: AppSpacing.xs),
                SkeletonBox(width: 160, height: 12),
              ],
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          const SkeletonBox(
            width: 72,
            height: 72,
            borderRadius: AppRadius.radiusMd,
          ),
        ],
      ),
    );
  }
}
