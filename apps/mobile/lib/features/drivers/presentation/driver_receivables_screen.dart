import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/empty_state_view.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../application/driver_receivables_controller.dart';
import '../domain/driver_receivable.dart';

/// Histórico de entregas do entregador.
///
/// IMPORTANTE: o backend não expõe um endpoint dedicado de "entregas
/// concluídas" — usamos `GET /api/driver/receivables` (um recebível é
/// criado automaticamente por entrega confirmada), que é a única fonte real
/// de histórico disponível hoje (`docs/DELIVERY_FLOW.md#financeiro-do-
/// entregador`). Endereço/itens do pedido não estão disponíveis aqui.
class DriverReceivablesScreen extends ConsumerWidget {
  const DriverReceivablesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final receivablesAsync = ref.watch(driverReceivablesControllerProvider);

    return Scaffold(
      appBar: const PedeLocalAppBar(title: 'Histórico de entregas'),
      body: SafeArea(
        child: receivablesAsync.when(
          data: (receivables) => _ReceivablesList(receivables: receivables),
          loading: () =>
              const LoadingIndicator(message: 'Carregando seu histórico...'),
          error: (error, stackTrace) => ErrorStateView(
            message: error is AppException
                ? error.message
                : 'Não foi possível carregar seu histórico.',
            onRetry: () => ref
                .read(driverReceivablesControllerProvider.notifier)
                .refresh(),
          ),
        ),
      ),
    );
  }
}

class _ReceivablesList extends ConsumerWidget {
  const _ReceivablesList({required this.receivables});

  final List<DriverReceivable> receivables;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (receivables.isEmpty) {
      return const EmptyStateView(
        title: 'Nenhuma entrega concluída ainda',
        message:
            'Suas entregas concluídas e valores a receber aparecerão aqui.',
        icon: Icons.receipt_long_outlined,
      );
    }

    final total = receivables.fold<double>(0, (sum, r) => sum + r.amount);

    return RefreshIndicator(
      onRefresh: () =>
          ref.read(driverReceivablesControllerProvider.notifier).refresh(),
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          PedeLocalCard(
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Total recebível', style: AppTypography.subtitle),
                Text(
                  Formatters.currency(total),
                  style: AppTypography.title.copyWith(color: AppColors.primary),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Entregas', style: AppTypography.title),
          const SizedBox(height: AppSpacing.sm),
          ...receivables.map(
            (r) => Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: PedeLocalCard(
                child: Row(
                  children: [
                    Container(
                      width: 40,
                      height: 40,
                      decoration: const BoxDecoration(
                        color: AppColors.accentLight,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.local_shipping_outlined,
                        color: AppColors.accent,
                        size: 20,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Entrega #${r.deliveryId.split('-').first}',
                            style: AppTypography.subtitle,
                          ),
                          const SizedBox(height: AppSpacing.xxs),
                          Text(
                            Formatters.dateTime(r.createdAtUtc),
                            style: AppTypography.bodyMuted,
                          ),
                        ],
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text(
                          Formatters.currency(r.amount),
                          style: AppTypography.subtitle,
                        ),
                        const SizedBox(height: AppSpacing.xxs),
                        Text(
                          _statusLabel(r.status),
                          style: AppTypography.caption,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(String status) {
    switch (status) {
      case 'Pending':
        return 'Pendente';
      case 'PaymentReported':
        return 'Pagamento informado';
      case 'Received':
        return 'Recebido';
      default:
        return status;
    }
  }
}
