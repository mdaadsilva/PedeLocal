import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/app_routes.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../../core/theme/app_spacing.dart';
import '../../../../core/theme/app_typography.dart';
import '../../../../core/widgets/pedelocal_button.dart';
import '../../application/session_controller.dart';
import '../../domain/user_role.dart';

/// Bottom sheet de acesso ao perfil — usado no topo da Home Cliente e da
/// Home Entregador ("acesso ao perfil"). Esta etapa não implementa a tela
/// completa de edição de perfil (`GET/PUT /api/me/profile`); apenas mostra
/// os dados básicos já disponíveis na sessão e a ação de logout.
void showAccountSheet(BuildContext context) {
  showModalBottomSheet<void>(
    context: context,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (context) => const _AccountSheetContent(),
  );
}

class _AccountSheetContent extends ConsumerWidget {
  const _AccountSheetContent();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider).value;
    final roleLabel = switch (session?.role) {
      UserRole.customer => 'Cliente',
      UserRole.driver => 'Entregador',
      _ => null,
    };

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
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
                      Text(
                        session?.userName ?? 'Usuário',
                        style: AppTypography.title,
                      ),
                      if (roleLabel != null)
                        Text(roleLabel, style: AppTypography.bodyMuted),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            if (roleLabel == 'Cliente') ...[
              PedeLocalButton(
                label: 'Ver perfil completo',
                variant: PedeLocalButtonVariant.secondary,
                onPressed: () {
                  Navigator.of(context).pop();
                  context.push(AppRoutes.profile);
                },
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            if (roleLabel == 'Entregador') ...[
              PedeLocalButton(
                label: 'Ver perfil',
                variant: PedeLocalButtonVariant.secondary,
                onPressed: () {
                  Navigator.of(context).pop();
                  context.push(AppRoutes.profile);
                },
              ),
              const SizedBox(height: AppSpacing.sm),
              PedeLocalButton(
                label: 'Histórico de entregas',
                variant: PedeLocalButtonVariant.secondary,
                onPressed: () {
                  Navigator.of(context).pop();
                  context.push(AppRoutes.driverReceivables);
                },
              ),
              const SizedBox(height: AppSpacing.sm),
            ],
            PedeLocalButton(
              label: 'Sair da conta',
              variant: PedeLocalButtonVariant.secondary,
              onPressed: () async {
                Navigator.of(context).pop();
                await ref.read(sessionControllerProvider.notifier).logout();
              },
            ),
          ],
        ),
      ),
    );
  }
}
