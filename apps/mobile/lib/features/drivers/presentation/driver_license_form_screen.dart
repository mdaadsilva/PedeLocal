import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/validators.dart';
import '../../../core/widgets/document_image_picker.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../application/driver_documents_controller.dart';
import '../application/driver_license_form_controller.dart';
import '../domain/driver_license.dart';

final _dateFormat = DateFormat('dd/MM/yyyy', 'pt_BR');

/// `PUT /api/driver/me/documents/license` — envio/atualização da CNH
/// (`docs/DRIVER_ONBOARDING.md#cnh`). Não há validação automática oficial
/// (Senatran) — só a decisão humana do SUPER_ADMIN depois.
class DriverLicenseFormScreen extends ConsumerStatefulWidget {
  const DriverLicenseFormScreen({super.key});

  @override
  ConsumerState<DriverLicenseFormScreen> createState() =>
      _DriverLicenseFormScreenState();
}

class _DriverLicenseFormScreenState
    extends ConsumerState<DriverLicenseFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _registrationController = TextEditingController();
  String _category = driverLicenseCategories.first;
  DateTime? _expiresAt;
  bool? _hasEar;
  String? _documentImageUrl;

  @override
  void initState() {
    super.initState();
    final current = ref.read(driverDocumentsControllerProvider).value?.license;
    if (current != null) {
      _registrationController.text = current.registrationNumber;
      _category = current.category;
      _expiresAt = current.expiresAt;
      _hasEar = current.hasEar;
      _documentImageUrl = current.documentImageUrl;
    }
  }

  @override
  void dispose() {
    _registrationController.dispose();
    super.dispose();
  }

  Future<void> _pickExpiresAt() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _expiresAt ?? now.add(const Duration(days: 365)),
      firstDate: DateTime(now.year - 1),
      lastDate: DateTime(now.year + 15),
    );
    if (picked != null) setState(() => _expiresAt = picked);
  }

  void _submit() {
    final valid = _formKey.currentState?.validate() ?? false;
    if (!valid) return;
    if (_expiresAt == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Selecione a validade da CNH.')),
      );
      return;
    }

    ref
        .read(driverLicenseFormControllerProvider.notifier)
        .submit(
          registrationNumber: _registrationController.text.trim(),
          category: _category,
          expiresAt: _expiresAt!,
          hasEar: _hasEar,
          documentImageUrl: _documentImageUrl,
        );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(driverLicenseFormControllerProvider, (previous, next) {
      next.when(
        data: (result) {
          if (result == null) return;
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(const SnackBar(content: Text('CNH enviada com sucesso.')));
          if (context.canPop()) context.pop();
        },
        loading: () {},
        error: (error, stackTrace) {
          final message = error is AppException
              ? error.message
              : 'Não foi possível enviar sua CNH.';
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text(message)));
        },
      );
    });

    final isSubmitting = ref.watch(driverLicenseFormControllerProvider).isLoading;

    return Scaffold(
      appBar: const PedeLocalAppBar(title: 'Dados da CNH'),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PedeLocalTextField(
                  controller: _registrationController,
                  label: 'Número de registro da CNH',
                  keyboardType: TextInputType.number,
                  prefixIcon: Icons.badge_outlined,
                  enabled: !isSubmitting,
                  validator: (value) =>
                      Validators.required(value, field: 'O número de registro'),
                ),
                const SizedBox(height: AppSpacing.md),
                Text('Categoria', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                DropdownButtonFormField<String>(
                  initialValue: _category,
                  items: driverLicenseCategories
                      .map(
                        (c) => DropdownMenuItem(value: c, child: Text(c)),
                      )
                      .toList(),
                  onChanged: isSubmitting
                      ? null
                      : (value) => setState(() => _category = value ?? _category),
                ),
                const SizedBox(height: AppSpacing.md),
                Text('Validade', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                OutlinedButton.icon(
                  onPressed: isSubmitting ? null : _pickExpiresAt,
                  icon: const Icon(Icons.calendar_today_outlined),
                  label: Text(
                    _expiresAt != null
                        ? _dateFormat.format(_expiresAt!)
                        : 'Selecionar data de validade',
                  ),
                ),
                const SizedBox(height: AppSpacing.md),
                Text(
                  'EAR (Exercício de Atividade Remunerada) — relevante para motos',
                  style: AppTypography.label,
                ),
                const SizedBox(height: AppSpacing.xs),
                DropdownButtonFormField<bool?>(
                  initialValue: _hasEar,
                  items: const [
                    DropdownMenuItem(value: null, child: Text('Não se aplica')),
                    DropdownMenuItem(value: true, child: Text('Sim')),
                    DropdownMenuItem(value: false, child: Text('Não')),
                  ],
                  onChanged: isSubmitting
                      ? null
                      : (value) => setState(() => _hasEar = value),
                ),
                const SizedBox(height: AppSpacing.md),
                DocumentImagePicker(
                  label: 'Foto da CNH',
                  currentImageUrl: _documentImageUrl,
                  onUploaded: (url) =>
                      setState(() => _documentImageUrl = url),
                ),
                const SizedBox(height: AppSpacing.lg),
                PedeLocalButton(
                  label: 'Enviar CNH',
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
