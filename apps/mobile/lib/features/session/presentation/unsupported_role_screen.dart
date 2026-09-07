import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_spacing.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../application/session_controller.dart';

/// Exibida quando um usuário autenticado não tem nenhum papel suportado
/// pelo aplicativo mobile (`CUSTOMER`/`DRIVER`) — por exemplo, uma conta de
/// staff de estabelecimento ou Super Admin, que pertence ao futuro Painel
/// Web (Next.js), fora do escopo desta etapa.
class UnsupportedRoleScreen extends ConsumerWidget {
  const UnsupportedRoleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.info_outline_rounded, size: 56),
                const SizedBox(height: AppSpacing.md),
                const Text(
                  'Este app é exclusivo para clientes e entregadores',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: AppSpacing.xs),
                const Text(
                  'Contas de estabelecimento e administração usam o painel web do PedeLocal.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppSpacing.lg),
                PedeLocalButton(
                  label: 'Sair',
                  onPressed: () =>
                      ref.read(sessionControllerProvider.notifier).logout(),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
