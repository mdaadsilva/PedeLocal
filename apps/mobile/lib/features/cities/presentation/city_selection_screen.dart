import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../application/cities_controller.dart';
import '../application/selected_city_controller.dart';
import '../domain/city.dart';

/// Exibe somente cidades retornadas pela API (`GET /api/cities`) — nunca
/// hardcoded. Ao escolher, persiste localmente como última cidade
/// selecionada (`SelectedCityController`).
class CitySelectionScreen extends ConsumerWidget {
  const CitySelectionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final citiesAsync = ref.watch(citiesControllerProvider);

    return Scaffold(
      appBar: const PedeLocalAppBar(
        title: 'Escolha sua cidade',
        automaticallyImplyLeading: false,
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Onde você quer comprar hoje?', style: AppTypography.title),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'O PedeLocal mostra os estabelecimentos da sua cidade.',
                style: AppTypography.bodyMuted,
              ),
              const SizedBox(height: AppSpacing.lg),
              Expanded(
                child: citiesAsync.when(
                  data: (cities) {
                    if (cities.isEmpty) {
                      return EmptyStateView(
                        title: 'Nenhuma cidade disponível',
                        message: 'Ainda não há cidades ativas cadastradas.',
                        icon: Icons.location_city_outlined,
                        actionLabel: 'Tentar novamente',
                        onAction: () => ref
                            .read(citiesControllerProvider.notifier)
                            .reload(),
                      );
                    }
                    return ListView.separated(
                      itemCount: cities.length,
                      separatorBuilder: (_, _) =>
                          const SizedBox(height: AppSpacing.sm),
                      itemBuilder: (context, index) {
                        final city = cities[index];
                        return _CityTile(
                          city: city,
                          onTap: () => ref
                              .read(selectedCityControllerProvider.notifier)
                              .select(city),
                        );
                      },
                    );
                  },
                  loading: () =>
                      const LoadingIndicator(message: 'Carregando cidades...'),
                  error: (error, stackTrace) => ErrorStateView(
                    message: error is AppException
                        ? error.message
                        : 'Não foi possível carregar as cidades.',
                    onRetry: () =>
                        ref.read(citiesControllerProvider.notifier).reload(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CityTile extends StatelessWidget {
  const _CityTile({required this.city, required this.onTap});

  final City city;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return PedeLocalCard(
      onTap: onTap,
      child: Row(
        children: [
          const Icon(Icons.location_on_outlined, color: AppColors.primary),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(city.name, style: AppTypography.subtitle),
                Text(city.state, style: AppTypography.bodyMuted),
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
