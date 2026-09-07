import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';

import '../../../core/maps/maps_launcher.dart';
import '../../../core/network/app_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_radius.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/widgets/map_pin_preview.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../../cities/application/selected_city_controller.dart';
import '../application/addresses_controller.dart';
import '../data/addresses_repository.dart';
import '../domain/address.dart';

/// Criação/edição de endereço (`docs/FRONTEND_GUIDE.md#modelo-de-endereço`).
///
/// Sem uma chave de mapa configurada nativamente ainda (pendência de infra —
/// ver relatório final), o pin em mapa interativo não é embutido aqui: a
/// coordenada é obtida via geolocalização do dispositivo ou digitada
/// manualmente, e pode ser conferida externamente com "Ver no mapa"
/// (`url_launcher`, sem necessidade de chave de API).
class AddressFormScreen extends ConsumerStatefulWidget {
  const AddressFormScreen({super.key, this.existing});

  final Address? existing;

  @override
  ConsumerState<AddressFormScreen> createState() => _AddressFormScreenState();
}

class _AddressFormScreenState extends ConsumerState<AddressFormScreen> {
  final _formKey = GlobalKey<FormState>();

  late String _type;
  late final TextEditingController _label;
  late final TextEditingController _street;
  late final TextEditingController _number;
  late final TextEditingController _district;
  late final TextEditingController _complement;
  late final TextEditingController _zipCode;
  late final TextEditingController _propertyName;
  late final TextEditingController _road;
  late final TextEditingController _kilometer;
  late final TextEditingController _reference;
  late final TextEditingController _latitude;
  late final TextEditingController _longitude;

  bool _submitting = false;
  bool _locating = false;
  String? _error;

  bool get _isEditing => widget.existing != null;

  @override
  void initState() {
    super.initState();
    final e = widget.existing;
    _type = e?.type ?? 'Urban';
    _label = TextEditingController(text: e?.label ?? '');
    _street = TextEditingController(text: e?.street ?? '');
    _number = TextEditingController(text: e?.number ?? '');
    _district = TextEditingController(text: e?.district ?? '');
    _complement = TextEditingController(text: e?.complement ?? '');
    _zipCode = TextEditingController(text: e?.zipCode ?? '');
    _propertyName = TextEditingController(text: e?.propertyName ?? '');
    _road = TextEditingController(text: e?.road ?? '');
    _kilometer = TextEditingController(text: e?.kilometer ?? '');
    _reference = TextEditingController(text: e?.reference ?? '');
    _latitude = TextEditingController(text: e?.latitude.toString() ?? '');
    _longitude = TextEditingController(text: e?.longitude.toString() ?? '');
  }

  @override
  void dispose() {
    for (final c in [
      _label,
      _street,
      _number,
      _district,
      _complement,
      _zipCode,
      _propertyName,
      _road,
      _kilometer,
      _reference,
      _latitude,
      _longitude,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _useCurrentLocation() async {
    setState(() {
      _locating = true;
      _error = null;
    });
    try {
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        setState(() {
          _error = 'Permissão de localização negada. Informe as coordenadas manualmente.';
        });
        return;
      }
      final serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        setState(() {
          _error =
              'Ative o serviço de localização do aparelho e tente novamente.';
        });
        return;
      }
      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
        ),
      );
      setState(() {
        _latitude.text = position.latitude.toStringAsFixed(6);
        _longitude.text = position.longitude.toStringAsFixed(6);
      });
    } catch (_) {
      setState(() => _error = 'Não foi possível obter sua localização atual.');
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  Future<void> _openInMaps() async {
    final lat = double.tryParse(_latitude.text.replaceAll(',', '.'));
    final lng = double.tryParse(_longitude.text.replaceAll(',', '.'));
    if (lat == null || lng == null) return;
    await MapsLauncher.openPin(lat, lng);
  }

  Future<void> _submit() async {
    if (_submitting) return;
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final lat = double.tryParse(_latitude.text.replaceAll(',', '.'));
    final lng = double.tryParse(_longitude.text.replaceAll(',', '.'));
    if (lat == null || lng == null) {
      setState(
        () => _error = 'Informe latitude e longitude válidas (obrigatórias).',
      );
      return;
    }
    if (lat == 0 && lng == 0) {
      setState(
        () => _error =
            'Informe uma localização real. Coordenadas 0,0 não são aceitas.',
      );
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      final repository = ref.read(addressesRepositoryProvider);
      if (_isEditing) {
        await repository.update(
          id: widget.existing!.id,
          type: _type,
          label: _label.text.trim().isEmpty ? null : _label.text.trim(),
          street: _type == 'Urban' ? _street.text.trim() : null,
          number: _type == 'Urban' ? _number.text.trim() : null,
          district: _type == 'Urban' ? _district.text.trim() : null,
          complement: _type == 'Urban' ? _complement.text.trim() : null,
          zipCode: _type == 'Urban' ? _zipCode.text.trim() : null,
          propertyName: _type == 'Rural' ? _propertyName.text.trim() : null,
          road: _type == 'Rural' ? _road.text.trim() : null,
          kilometer: _type == 'Rural' ? _kilometer.text.trim() : null,
          reference: _reference.text.trim().isEmpty
              ? null
              : _reference.text.trim(),
          latitude: lat,
          longitude: lng,
        );
      } else {
        final cityId = ref.read(selectedCityControllerProvider)?.id;
        if (cityId == null) {
          setState(
            () =>
                _error = 'Selecione uma cidade antes de cadastrar um endereço.',
          );
          return;
        }
        await repository.create(
          cityId: cityId,
          type: _type,
          label: _label.text.trim().isEmpty ? null : _label.text.trim(),
          street: _type == 'Urban' ? _street.text.trim() : null,
          number: _type == 'Urban' ? _number.text.trim() : null,
          district: _type == 'Urban' ? _district.text.trim() : null,
          complement: _type == 'Urban' ? _complement.text.trim() : null,
          zipCode: _type == 'Urban' ? _zipCode.text.trim() : null,
          propertyName: _type == 'Rural' ? _propertyName.text.trim() : null,
          road: _type == 'Rural' ? _road.text.trim() : null,
          kilometer: _type == 'Rural' ? _kilometer.text.trim() : null,
          reference: _reference.text.trim().isEmpty
              ? null
              : _reference.text.trim(),
          latitude: lat,
          longitude: lng,
        );
      }

      await ref.read(addressesControllerProvider.notifier).refresh();
      if (!mounted) return;
      context.pop();
    } on AppException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final hasCoordinates =
        double.tryParse(_latitude.text.replaceAll(',', '.')) != null &&
        double.tryParse(_longitude.text.replaceAll(',', '.')) != null;

    return Scaffold(
      appBar: AppBar(
        title: Text(_isEditing ? 'Editar endereço' : 'Novo endereço'),
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'Urban', label: Text('Urbano')),
                ButtonSegment(value: 'Rural', label: Text('Rural')),
              ],
              selected: {_type},
              onSelectionChanged: (selection) =>
                  setState(() => _type = selection.first),
            ),
            const SizedBox(height: AppSpacing.lg),
            PedeLocalTextField(
              controller: _label,
              label: 'Nome do endereço (ex.: Casa)',
            ),
            const SizedBox(height: AppSpacing.md),
            if (_type == 'Urban') ...[
              PedeLocalTextField(
                controller: _street,
                label: 'Rua',
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Obrigatório' : null,
              ),
              const SizedBox(height: AppSpacing.md),
              Row(
                children: [
                  Expanded(
                    child: PedeLocalTextField(
                      controller: _number,
                      label: 'Número',
                      keyboardType: TextInputType.text,
                      validator: (v) => (v == null || v.trim().isEmpty)
                          ? 'Obrigatório'
                          : null,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(
                    flex: 2,
                    child: PedeLocalTextField(
                      controller: _district,
                      label: 'Bairro',
                      validator: (v) => (v == null || v.trim().isEmpty)
                          ? 'Obrigatório'
                          : null,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.md),
              PedeLocalTextField(
                controller: _complement,
                label: 'Complemento (opcional)',
              ),
              const SizedBox(height: AppSpacing.md),
              PedeLocalTextField(
                controller: _zipCode,
                label: 'CEP (opcional)',
                keyboardType: TextInputType.number,
              ),
            ] else ...[
              PedeLocalTextField(
                controller: _propertyName,
                label: 'Nome da propriedade (opcional)',
              ),
              const SizedBox(height: AppSpacing.md),
              PedeLocalTextField(
                controller: _road,
                label: 'Estrada (opcional)',
              ),
              const SizedBox(height: AppSpacing.md),
              PedeLocalTextField(
                controller: _kilometer,
                label: 'Km (opcional)',
              ),
            ],
            const SizedBox(height: AppSpacing.md),
            PedeLocalTextField(
              controller: _reference,
              label: _type == 'Rural'
                  ? 'Referência / instruções'
                  : 'Ponto de referência (opcional)',
              maxLines: 2,
              validator: _type == 'Rural'
                  ? (v) => (v == null || v.trim().isEmpty)
                        ? 'Obrigatório para endereço rural'
                        : null
                  : null,
            ),
            const SizedBox(height: AppSpacing.lg),
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: _type == 'Rural'
                    ? AppColors.accentLight
                    : AppColors.surfaceAlt,
                borderRadius: AppRadius.radiusMd,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(
                        Icons.location_on_rounded,
                        size: 18,
                        color: _type == 'Rural'
                            ? AppColors.accent
                            : AppColors.primary,
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      Text(
                        'Coordenadas (obrigatórias)',
                        style: AppTypography.subtitle,
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Text(
                    'Usadas para calcular a distância/taxa de entrega. Nunca envie 0,0.',
                    style: AppTypography.caption,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Row(
                    children: [
                      Expanded(
                        child: PedeLocalTextField(
                          controller: _latitude,
                          label: 'Latitude',
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          onChanged: (_) => setState(() {}),
                        ),
                      ),
                      const SizedBox(width: AppSpacing.md),
                      Expanded(
                        child: PedeLocalTextField(
                          controller: _longitude,
                          label: 'Longitude',
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          onChanged: (_) => setState(() {}),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Row(
                    children: [
                      Expanded(
                        child: PedeLocalButton(
                          label: 'Usar localização atual',
                          icon: Icons.my_location_rounded,
                          variant: PedeLocalButtonVariant.secondary,
                          isLoading: _locating,
                          onPressed: _useCurrentLocation,
                        ),
                      ),
                      if (hasCoordinates) ...[
                        const SizedBox(width: AppSpacing.sm),
                        IconButton(
                          onPressed: _openInMaps,
                          icon: const Icon(Icons.map_outlined),
                          tooltip: 'Ver no mapa',
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),
            if (hasCoordinates) ...[
              const SizedBox(height: AppSpacing.md),
              MapPinPreview(
                latitude: double.parse(_latitude.text.replaceAll(',', '.')),
                longitude: double.parse(_longitude.text.replaceAll(',', '.')),
                highlighted: _type == 'Rural',
                openRouteLabel: 'Ver no Google Maps',
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text(
                _error!,
                style: AppTypography.body.copyWith(color: AppColors.error),
              ),
            ],
            const SizedBox(height: AppSpacing.lg),
            PedeLocalButton(
              label: _isEditing ? 'Salvar alterações' : 'Salvar endereço',
              isLoading: _submitting,
              onPressed: _submit,
            ),
          ],
        ),
      ),
    );
  }
}
