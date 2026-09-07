import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/widgets/brand_logo.dart';
import '../../../core/widgets/brand_wordmark.dart';
import '../../../core/widgets/error_state_view.dart';
import '../application/session_controller.dart';

/// Splash oficial do PedeLocal.
///
/// Responsabilidade única desta tela: mostrar a marca enquanto
/// [sessionControllerProvider] resolve (sessão salva? token válido? qual
/// role?). A navegação para a rota correta acontece no `redirect` do
/// `GoRouter` (`core/routing/app_router.dart`), que reage automaticamente à
/// mudança de estado deste provider — esta tela não navega sozinha.
class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessionState = ref.watch(sessionControllerProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: sessionState.when(
              data: (_) => const _BrandSplash(showSpinner: false),
              loading: () => const _BrandSplash(showSpinner: true),
              error: (error, stackTrace) => ErrorStateView(
                message:
                    'Não foi possível verificar sua sessão. Tente novamente.',
                onRetry: () => ref.invalidate(sessionControllerProvider),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _BrandSplash extends StatelessWidget {
  const _BrandSplash({required this.showSpinner});

  final bool showSpinner;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const BrandLogo(size: 120),
        const SizedBox(height: AppSpacing.lg),
        const BrandWordmark(fontSize: 34),
        const SizedBox(height: AppSpacing.xs),
        const BrandSlogan(),
        const SizedBox(height: AppSpacing.xxl),
        if (showSpinner)
          const CircularProgressIndicator(color: AppColors.primary),
      ],
    );
  }
}
