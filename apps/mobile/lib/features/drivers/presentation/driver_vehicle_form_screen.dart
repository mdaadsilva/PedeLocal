import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/validators.dart';
import '../../../core/widgets/document_image_picker.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../application/driver_documents_controller.dart';
import '../application/driver_vehicle_form_controller.dart';

const _vehicleTypes = ['Bike', 'Motorcycle', 'Car'];

String _vehicleTypeLabel(String type) => switch (type) {
  'Bike' => 'Bicicleta',
  'Motorcycle' => 'Moto',
  'Car' => 'Carro',
  _ => type,
};

/// `PUT /api/driver/me/documents/vehicle` — envio/atualização do veículo
/// (`docs/DRIVER_ONBOARDING.md#veículo`). Trocar o tipo depois de `Approved`
/// volta o entregador para `UnderReview` (recapacitação de risco).
class DriverVehicleFormScreen extends ConsumerStatefulWidget {
  const DriverVehicleFormScreen({super.key});

  @override
  ConsumerState<DriverVehicleFormScreen> createState() =>
      _DriverVehicleFormScreenState();
}

class _DriverVehicleFormScreenState
    extends ConsumerState<DriverVehicleFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _plateController = TextEditingController();
  final _modelController = TextEditingController();
  final _colorController = TextEditingController();
  String _type = _vehicleTypes[1];
  String? _documentImageUrl;

  @override
  void initState() {
    super.initState();
    final current = ref.read(driverDocumentsControllerProvider).value?.vehicle;
    if (current != null) {
      _type = current.type;
      _plateController.text = current.plate ?? '';
      _modelController.text = current.model;
      _colorController.text = current.color;
      _documentImageUrl = current.documentImageUrl;
    } else {
      final registeredType =
          ref.read(driverDocumentsControllerProvider).value?.driver.vehicleType;
      if (registeredType != null) _type = registeredType;
    }
  }

  @override
  void dispose() {
    _plateController.dispose();
    _modelController.dispose();
    _colorController.dispose();
    super.dispose();
  }

  void _submit() {
    final valid = _formKey.currentState?.validate() ?? false;
    if (!valid) return;

    ref
        .read(driverVehicleFormControllerProvider.notifier)
        .submit(
          type: _type,
          plate: _plateController.text.trim().isEmpty
              ? null
              : _plateController.text.trim(),
          model: _modelController.text.trim(),
          color: _colorController.text.trim(),
          documentImageUrl: _documentImageUrl,
        );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(driverVehicleFormControllerProvider, (previous, next) {
      next.when(
        data: (result) {
          if (result == null) return;
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              const SnackBar(content: Text('Veículo enviado com sucesso.')),
            );
          if (context.canPop()) context.pop();
        },
        loading: () {},
        error: (error, stackTrace) {
          final message = error is AppException
              ? error.message
              : 'Não foi possível enviar os dados do veículo.';
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text(message)));
        },
      );
    });

    final isSubmitting = ref.watch(driverVehicleFormControllerProvider).isLoading;
    final isBike = _type == 'Bike';

    return Scaffold(
      appBar: const PedeLocalAppBar(title: 'Dados do veículo'),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('Tipo de veículo', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                DropdownButtonFormField<String>(
                  initialValue: _type,
                  items: _vehicleTypes
                      .map(
                        (type) => DropdownMenuItem(
                          value: type,
                          child: Text(_vehicleTypeLabel(type)),
                        ),
                      )
                      .toList(),
                  onChanged: isSubmitting
                      ? null
                      : (value) => setState(() => _type = value ?? _type),
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _plateController,
                  label: isBike ? 'Placa (não se aplica a bicicletas)' : 'Placa',
                  prefixIcon: Icons.pin_outlined,
                  enabled: !isSubmitting && !isBike,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _modelController,
                  label: 'Modelo',
                  prefixIcon: Icons.directions_bike_outlined,
                  enabled: !isSubmitting,
                  validator: (value) =>
                      Validators.required(value, field: 'O modelo'),
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _colorController,
                  label: 'Cor',
                  prefixIcon: Icons.palette_outlined,
                  enabled: !isSubmitting,
                  validator: (value) =>
                      Validators.required(value, field: 'A cor'),
                ),
                const SizedBox(height: AppSpacing.md),
                DocumentImagePicker(
                  label: isBike
                      ? 'Foto do veículo (opcional)'
                      : 'Foto do documento do veículo (opcional)',
                  currentImageUrl: _documentImageUrl,
                  onUploaded: (url) =>
                      setState(() => _documentImageUrl = url),
                ),
                const SizedBox(height: AppSpacing.lg),
                PedeLocalButton(
                  label: 'Enviar veículo',
                  isLoading: isSubmitting,
                  onPressed: _submit,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
