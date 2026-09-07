import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/maps/maps_launcher.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../application/addresses_controller.dart';
import '../domain/address.dart';
import 'address_form_screen.dart';

/// Gerenciamento de endereços do cliente (`GET/POST/PUT/DELETE
/// /api/me/addresses`). Usada tanto a partir do Perfil quanto (em modo
/// seleção) a partir do Checkout.
class AddressesScreen extends ConsumerWidget {
  const AddressesScreen({
    super.key,
    this.selectionMode = false,
    this.filterCityId,
  });

  /// Quando `true`, tocar em um endereço apenas retorna o [Address]
  /// selecionado via `Navigator.pop` em vez de abrir edição — usado pelo
  /// checkout.
  final bool selectionMode;

  /// Quando informado (checkout), mostra somente endereços desta cidade —
  /// entrega exige endereço da mesma cidade do estabelecimento
  /// (`docs/ORDER_FLOW.md` — `ADDRESS_CITY_MISMATCH`).
  final String? filterCityId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final addressesAsync = ref.watch(addressesControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(selectionMode ? 'Selecionar endereço' : 'Meus endereços'),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const AddressFormScreen()),
        ),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Novo endereço'),
      ),
      body: addressesAsync.when(
        data: (allAddresses) {
          final addresses = filterCityId == null
              ? allAddresses
              : allAddresses.where((a) => a.cityId == filterCityId).toList();

          if (addresses.isEmpty) {
            return EmptyStateView(
              title: 'Nenhum endereço cadastrado',
              message: filterCityId != null && allAddresses.isNotEmpty
                  ? 'Você não tem endereços nesta cidade. Cadastre um novo para continuar.'
                  : 'Adicione um endereço para receber seus pedidos.',
              icon: Icons.location_off_outlined,
            );
          }
          return ListView.separated(
            padding: const EdgeInsets.all(AppSpacing.lg),
            itemCount: addresses.length,
            separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
            itemBuilder: (context, index) => _AddressCard(
              address: addresses[index],
              selectionMode: selectionMode,
            ),
          );
        },
        loading: () => const LoadingIndicator(),
        error: (error, stackTrace) => ErrorStateView(
          message: error is AppException
              ? error.message
              : 'Não foi possível carregar seus endereços.',
          onRetry: () =>
              ref.read(addressesControllerProvider.notifier).refresh(),
        ),
      ),
    );
  }
}

class _AddressCard extends ConsumerWidget {
  const _AddressCard({required this.address, required this.selectionMode});

  final Address address;
  final bool selectionMode;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return PedeLocalCard(
      onTap: selectionMode
          ? () => Navigator.of(context).pop(address)
          : () => Navigator.of(context).push(
              MaterialPageRoute<void>(
                builder: (_) => AddressFormScreen(existing: address),
              ),
            ),
      child: Row(
        children: [
          Icon(
            address.isRural ? Icons.landscape_outlined : Icons.home_outlined,
            color: AppColors.primary,
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  address.label ??
                      (address.isRural ? 'Endereço rural' : 'Endereço'),
                  style: AppTypography.subtitle,
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(address.summary, style: AppTypography.bodyMuted),
                if (address.isRural &&
                    address.reference != null &&
                    address.reference!.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.xxs),
                  Text(
                    address.reference!,
                    style: AppTypography.caption.copyWith(
                      color: AppColors.accent,
                    ),
                  ),
                ],
              ],
            ),
          ),
          IconButton(
            onPressed: () =>
                MapsLauncher.openRoute(address.latitude, address.longitude),
            icon: Icon(
              Icons.directions_rounded,
              color: address.isRural ? AppColors.accent : AppColors.primary,
            ),
            tooltip: 'Abrir rota',
          ),
          if (!selectionMode)
            IconButton(
              onPressed: () => _confirmDelete(context, ref),
              icon: const Icon(
                Icons.delete_outline_rounded,
                color: AppColors.error,
              ),
              tooltip: 'Excluir',
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

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Excluir endereço?'),
        content: const Text('Esta ação não pode ser desfeita.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancelar'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Excluir'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await ref.read(addressesControllerProvider.notifier).delete(address.id);
    } on AppException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }
}
