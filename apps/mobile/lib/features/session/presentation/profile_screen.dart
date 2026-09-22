import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../../me/application/delete_account_controller.dart';
import '../../me/data/me_repository.dart';
import '../application/session_controller.dart';
import '../domain/user_role.dart';

/// Perfil/Conta — dados básicos (`GET /api/me/profile`), acesso a
/// endereços, pedidos, logout e exclusão de conta
/// (`DELETE /api/me/account`, quando aplicável).
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(myProfileProvider);
    final session = ref.watch(sessionControllerProvider).value;

    ref.listen(deleteAccountControllerProvider, (previous, next) {
      if (next is AsyncError) {
        final error = next.error;
        final message = error is AppException
            ? error.message
            : 'Não foi possível excluir sua conta.';
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(message)));
      }
    });

    final isDeletingAccount = ref
        .watch(deleteAccountControllerProvider)
        .isLoading;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Perfil')),
      body: profileAsync.when(
        data: (profile) => ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            PedeLocalCard(
              child: Row(
                children: [
                  const CircleAvatar(
                    radius: 28,
                    backgroundColor: AppColors.primaryLight,
                    child: Icon(
                      Icons.person_rounded,
                      color: AppColors.primary,
                      size: 32,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(profile.name, style: AppTypography.title),
                        const SizedBox(height: AppSpacing.xxs),
                        Text(profile.email, style: AppTypography.bodyMuted),
                        Text(
                          profile.phoneNumber,
                          style: AppTypography.bodyMuted,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            if (session?.role == UserRole.customer) ...[
              _ProfileAction(
                icon: Icons.location_on_outlined,
                label: 'Meus endereços',
                onTap: () => context.push(AppRoutes.addresses),
              ),
              const SizedBox(height: AppSpacing.sm),
              _ProfileAction(
                icon: Icons.receipt_long_outlined,
                label: 'Meus pedidos',
                onTap: () => context.push(AppRoutes.orderHistory),
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            if (session?.role == UserRole.driver) ...[
              _ProfileAction(
                icon: Icons.badge_outlined,
                label: 'Meu cadastro (CNH e veículo)',
                onTap: () => context.push(AppRoutes.driverOnboarding),
              ),
              const SizedBox(height: AppSpacing.sm),
              _ProfileAction(
                icon: Icons.receipt_long_outlined,
                label: 'Meus recebíveis',
                onTap: () => context.push(AppRoutes.driverReceivables),
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            _ProfileAction(
              icon: Icons.logout_rounded,
              label: 'Sair da conta',
              onTap: () =>
                  ref.read(sessionControllerProvider.notifier).logout(),
            ),
            const SizedBox(height: AppSpacing.xl),
            _ProfileAction(
              icon: Icons.delete_forever_outlined,
              label: 'Excluir minha conta',
              isDestructive: true,
              isLoading: isDeletingAccount,
              onTap: () => _confirmDeleteAccount(context, ref),
            ),
          ],
        ),
        loading: () => const LoadingIndicator(),
        error: (error, stackTrace) => ErrorStateView(
          message: error is AppException
              ? error.message
              : 'Não foi possível carregar seu perfil.',
          onRetry: () => ref.refresh(myProfileProvider),
        ),
      ),
    );
  }

  Future<void> _confirmDeleteAccount(
    BuildContext context,
    WidgetRef ref,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Excluir sua conta?'),
        content: const Text(
          'Isso anonimiza e desativa sua conta permanentemente. Esta ação não pode ser desfeita.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancelar'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Excluir permanentemente'),
          ),
        ],
      ),
    );
    if (confirmed == true) {
      await ref.read(deleteAccountControllerProvider.notifier).deleteAccount();
    }
  }
}

class _ProfileAction extends StatelessWidget {
  const _ProfileAction({
    required this.icon,
    required this.label,
    required this.onTap,
    this.isDestructive = false,
    this.isLoading = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool isDestructive;
  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    final color = isDestructive ? AppColors.error : AppColors.textPrimary;

    return PedeLocalCard(
      onTap: isLoading ? null : onTap,
      child: Row(
        children: [
          Icon(
            icon,
            color: isDestructive ? AppColors.error : AppColors.primary,
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Text(
              label,
              style: AppTypography.body.copyWith(color: color),
            ),
          ),
          if (isLoading)
            const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          else
            const Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary,
            ),
        ],
      ),
    );
  }
}
