import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/error_state_view.dart';
import '../../../core/widgets/loading_indicator.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../application/driver_documents_controller.dart';
import '../domain/driver_documents.dart';
import '../domain/driver_license.dart';
import '../domain/driver_profile.dart';
import '../domain/driver_vehicle_document.dart';

final _dateFormat = DateFormat('dd/MM/yyyy', 'pt_BR');

/// Hub de onboarding do entregador — status geral, dados pessoais (só
/// leitura, vêm do cadastro), CNH e veículo
/// (`docs/DRIVER_ONBOARDING.md`).
class DriverOnboardingScreen extends ConsumerWidget {
  const DriverOnboardingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final documentsAsync = ref.watch(driverDocumentsControllerProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: const PedeLocalAppBar(title: 'Cadastro de entregador'),
      body: SafeArea(
        child: documentsAsync.when(
          data: (documents) => RefreshIndicator(
            onRefresh: () =>
                ref.read(driverDocumentsControllerProvider.notifier).refresh(),
            child: _DocumentsBody(documents: documents),
          ),
          loading: () => const LoadingIndicator(),
          error: (error, stackTrace) => ErrorStateView(
            message: error is AppException
                ? error.message
                : 'Não foi possível carregar seu cadastro.',
            onRetry: () =>
                ref.read(driverDocumentsControllerProvider.notifier).refresh(),
          ),
        ),
      ),
    );
  }
}

class _DocumentsBody extends StatelessWidget {
  const _DocumentsBody({required this.documents});

  final DriverDocuments documents;

  @override
  Widget build(BuildContext context) {
    final driver = documents.driver;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        PedeLocalCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text('Status da análise', style: AppTypography.title),
                  ),
                  StatusBadge.driverOnboardingStatus(driver.onboardingStatus),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(driver.eligibilityMessage, style: AppTypography.bodyMuted),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        Text('Dados pessoais', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        PedeLocalCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _InfoRow(label: 'Nome', value: driver.name),
              _InfoRow(label: 'CPF', value: driver.cpf),
              const SizedBox(height: AppSpacing.xs),
              const Text(
                'Enviados no cadastro. Para corrigir, entre em contato com o suporte.',
                style: AppTypography.caption,
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        Text('CNH', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        _LicenseCard(license: documents.license),
        const SizedBox(height: AppSpacing.lg),
        Text('Veículo', style: AppTypography.title),
        const SizedBox(height: AppSpacing.sm),
        _VehicleCard(vehicle: documents.vehicle),
        const SizedBox(height: AppSpacing.xxl),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
      child: Row(
        children: [
          SizedBox(
            width: 60,
            child: Text(label, style: AppTypography.bodyMuted),
          ),
          Expanded(child: Text(value, style: AppTypography.body)),
        ],
      ),
    );
  }
}

class _LicenseCard extends StatelessWidget {
  const _LicenseCard({required this.license});

  final DriverLicense? license;

  @override
  Widget build(BuildContext context) {
    if (license == null) {
      return PedeLocalCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Você ainda não enviou os dados da sua CNH.',
              style: AppTypography.bodyMuted,
            ),
            const SizedBox(height: AppSpacing.sm),
            PedeLocalButton(
              label: 'Enviar CNH',
              icon: Icons.badge_outlined,
              onPressed: () => context.push(AppRoutes.driverOnboardingLicense),
            ),
          ],
        ),
      );
    }

    final expiredOrSoon = license!.isExpired || license!.isExpiringSoon;
    return PedeLocalCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _InfoRow(label: 'Nº', value: license!.registrationNumber),
          _InfoRow(label: 'Categoria', value: license!.category),
          Row(
            children: [
              const SizedBox(
                width: 60,
                child: Text('Validade', style: AppTypography.bodyMuted),
              ),
              Text(
                _dateFormat.format(license!.expiresAt),
                style: AppTypography.body,
              ),
              if (expiredOrSoon) ...[
                const SizedBox(width: AppSpacing.xs),
                Icon(
                  Icons.warning_amber_rounded,
                  size: 16,
                  color: license!.isExpired
                      ? AppColors.error
                      : AppColors.warning,
                ),
              ],
            ],
          ),
          if (license!.isExpired) ...[
            const SizedBox(height: AppSpacing.xs),
            const Text(
              'Sua CNH está vencida. Envie a validade atualizada para voltar a '
              'ficar online.',
              style: AppTypography.caption,
            ),
          ] else if (license!.isExpiringSoon) ...[
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Vence em ${license!.daysUntilExpiration} dia(s). Atualize com '
              'antecedência para não ficar indisponível.',
              style: AppTypography.caption,
            ),
          ],
          const SizedBox(height: AppSpacing.sm),
          PedeLocalButton(
            label: 'Atualizar CNH',
            variant: PedeLocalButtonVariant.secondary,
            icon: Icons.edit_outlined,
            onPressed: () => context.push(AppRoutes.driverOnboardingLicense),
          ),
        ],
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({required this.vehicle});

  final DriverVehicleDocument? vehicle;

  @override
  Widget build(BuildContext context) {
    if (vehicle == null) {
      return PedeLocalCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Você ainda não enviou os dados do seu veículo.',
              style: AppTypography.bodyMuted,
            ),
            const SizedBox(height: AppSpacing.sm),
            PedeLocalButton(
              label: 'Enviar veículo',
              icon: Icons.two_wheeler_outlined,
              onPressed: () => context.push(AppRoutes.driverOnboardingVehicle),
            ),
          ],
        ),
      );
    }

    return PedeLocalCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _InfoRow(label: 'Tipo', value: vehicle!.type.vehicleTypeLabel),
          _InfoRow(label: 'Modelo', value: vehicle!.model),
          _InfoRow(label: 'Cor', value: vehicle!.color),
          if (vehicle!.plate != null && vehicle!.plate!.isNotEmpty)
            _InfoRow(label: 'Placa', value: vehicle!.plate!),
          const SizedBox(height: AppSpacing.sm),
          PedeLocalButton(
            label: 'Atualizar veículo',
            variant: PedeLocalButtonVariant.secondary,
            icon: Icons.edit_outlined,
            onPressed: () => context.push(AppRoutes.driverOnboardingVehicle),
          ),
        ],
      ),
    );
  }
}

extension on String {
  String get vehicleTypeLabel => switch (this) {
    'Bike' => 'Bicicleta',
    'Motorcycle' => 'Moto',
    'Car' => 'Carro',
    _ => this,
  };
}
