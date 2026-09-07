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
import '../../cities/application/selected_city_controller.dart';
import '../../session/application/session_controller.dart';
import '../../establishments/domain/establishment.dart';
import '../../session/presentation/widgets/account_sheet.dart';
import '../application/home_customer_controller.dart';
import '../application/home_customer_state.dart';
import 'widgets/category_chip.dart';
import 'widgets/establishment_card.dart';

class HomeCustomerScreen extends ConsumerStatefulWidget {
  const HomeCustomerScreen({super.key});

  @override
  ConsumerState<HomeCustomerScreen> createState() => _HomeCustomerScreenState();
}

class _HomeCustomerScreenState extends ConsumerState<HomeCustomerScreen> {
  final _searchController = TextEditingController();
  String _query = '';
  String? _selectedCategoryId;

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  List<Establishment> _filter(HomeCustomerState state) {
    return state.establishments.where((establishment) {
      final matchesQuery =
          _query.isEmpty ||
          establishment.name.toLowerCase().contains(_query.toLowerCase());
      final matchesCategory =
          _selectedCategoryId == null ||
          establishment.categoryId == _selectedCategoryId;
      return matchesQuery && matchesCategory;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final homeState = ref.watch(homeCustomerControllerProvider);
    final selectedCity = ref.watch(selectedCityControllerProvider);
    final session = ref.watch(sessionControllerProvider).value;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: () =>
              ref.read(homeCustomerControllerProvider.notifier).refresh(),
          child: CustomScrollView(
            slivers: [
              SliverToBoxAdapter(
                child: _TopBar(
                  cityName: selectedCity?.name ?? '—',
                  userName: session?.userName,
                ),
              ),
              SliverPadding(
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                sliver: SliverToBoxAdapter(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const SizedBox(height: AppSpacing.md),
                      _SearchField(
                        controller: _searchController,
                        onChanged: (value) {
                          setState(() => _query = value);
                        },
                      ),
                      const SizedBox(height: AppSpacing.lg),
                    ],
                  ),
                ),
              ),
              homeState.when(
                data: (state) => _buildContent(state),
                loading: () => _buildLoadingSkeleton(),
                error: (error, stackTrace) => SliverFillRemaining(
                  child: ErrorStateView(
                    message: error is AppException
                        ? error.message
                        : 'Não foi possível carregar os estabelecimentos.',
                    onRetry: () => ref
                        .read(homeCustomerControllerProvider.notifier)
                        .refresh(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildContent(HomeCustomerState state) {
    final filtered = _filter(state);

    return SliverPadding(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
      sliver: SliverList.list(
        children: [
          if (state.categories.isNotEmpty) ...[
            SizedBox(
              height: 40,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: state.categories.length + 1,
                separatorBuilder: (_, _) =>
                    const SizedBox(width: AppSpacing.xs),
                itemBuilder: (context, index) {
                  if (index == 0) {
                    return CategoryChip(
                      label: 'Todas',
                      selected: _selectedCategoryId == null,
                      onTap: () => setState(() => _selectedCategoryId = null),
                    );
                  }
                  final category = state.categories[index - 1];
                  return CategoryChip(
                    label: category.name,
                    selected: _selectedCategoryId == category.id,
                    onTap: () =>
                        setState(() => _selectedCategoryId = category.id),
                  );
                },
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
          ],
          Text('Estabelecimentos', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          if (filtered.isEmpty)
            const Padding(
              padding: EdgeInsets.only(top: AppSpacing.xl),
              child: EmptyStateView(
                title: 'Nenhum estabelecimento encontrado',
                message: 'Ainda não há estabelecimentos disponíveis para esta cidade ou filtro.',
                icon: Icons.storefront_outlined,
              ),
            )
          else
            ...filtered.map(
              (establishment) => Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: EstablishmentCard(
                  establishment: establishment,
                  onTap: () => context.push(
                    AppRoutes.establishmentDetail(establishment.id),
                  ),
                ),
              ),
            ),
          const SizedBox(height: AppSpacing.xl),
        ],
      ),
    );
  }

  Widget _buildLoadingSkeleton() {
    return SliverPadding(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
      sliver: SliverList.list(
        children: List.generate(
          4,
          (index) => const Padding(
            padding: EdgeInsets.only(bottom: AppSpacing.sm),
            child: SkeletonEstablishmentCard(),
          ),
        ),
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.cityName, required this.userName});

  final String cityName;
  final String? userName;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.lg,
        AppSpacing.md,
        AppSpacing.lg,
        0,
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(
                      Icons.location_on_rounded,
                      color: AppColors.primary,
                      size: 18,
                    ),
                    const SizedBox(width: AppSpacing.xxs),
                    Text(
                      cityName,
                      style: AppTypography.label.copyWith(
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(
                  userName != null ? 'Olá, $userName!' : 'Olá!',
                  style: AppTypography.headline,
                ),
              ],
            ),
          ),
          CartBadge(onTap: () => context.push(AppRoutes.cart)),
          IconButton(
            onPressed: () => showAccountSheet(context),
            icon: const Icon(
              Icons.account_circle_outlined,
              size: 32,
              color: AppColors.secondary,
            ),
            tooltip: 'Perfil',
          ),
        ],
      ),
    );
  }
}

class _SearchField extends StatelessWidget {
  const _SearchField({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final void Function(String) onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      onChanged: onChanged,
      decoration: InputDecoration(
        hintText: 'Buscar estabelecimento...',
        prefixIcon: const Icon(Icons.search_rounded),
        filled: true,
        fillColor: AppColors.surfaceAlt,
        border: OutlineInputBorder(
          borderRadius: AppRadius.radiusMd,
          borderSide: BorderSide.none,
        ),
        contentPadding: const EdgeInsets.symmetric(
          vertical: 0,
          horizontal: AppSpacing.md,
        ),
      ),
    );
  }
}
