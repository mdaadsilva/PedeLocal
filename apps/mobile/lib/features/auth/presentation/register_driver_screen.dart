import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/validators.dart';
import '../../../core/widgets/pedelocal_app_bar.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../../cities/application/cities_controller.dart';
import '../../cities/domain/city.dart';
import '../application/register_driver_controller.dart';
import '../domain/auth_models.dart';

class RegisterDriverScreen extends ConsumerStatefulWidget {
  const RegisterDriverScreen({super.key});

  @override
  ConsumerState<RegisterDriverScreen> createState() =>
      _RegisterDriverScreenState();
}

class _RegisterDriverScreenState extends ConsumerState<RegisterDriverScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _cpfController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _pixKeyController = TextEditingController();

  City? _selectedCity;
  VehicleType _vehicleType = VehicleType.motorcycle;
  PixKeyType _pixKeyType = PixKeyType.cpf;

  @override
  void dispose() {
    _nameController.dispose();
    _cpfController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _pixKeyController.dispose();
    super.dispose();
  }

  void _submit() {
    final formValid = _formKey.currentState?.validate() ?? false;
    final city = _selectedCity;
    if (!formValid) return;
    if (city == null) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(const SnackBar(content: Text('Selecione sua cidade.')));
      return;
    }

    ref
        .read(registerDriverControllerProvider.notifier)
        .register(
          name: _nameController.text.trim(),
          cpf: _cpfController.text.trim(),
          phone: _phoneController.text.trim(),
          email: _emailController.text.trim(),
          password: _passwordController.text,
          cityId: city.id,
          vehicleType: _vehicleType,
          pixKeyType: _pixKeyType,
          pixKey: _pixKeyController.text.trim(),
        );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(registerDriverControllerProvider, (previous, next) {
      next.when(
        data: (result) {
          if (result == null) return;
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              const SnackBar(
                content: Text(
                  'Cadastro enviado! Faça login para acompanhar a aprovação da sua conta.',
                ),
                duration: Duration(seconds: 4),
              ),
            );
          context.go(AppRoutes.login);
        },
        loading: () {},
        error: (error, stackTrace) {
          final message = error is AppException
              ? error.message
              : 'Não foi possível concluir o cadastro.';
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text(message)));
        },
      );
    });

    final isLoading = ref.watch(registerDriverControllerProvider).isLoading;
    final citiesAsync = ref.watch(citiesControllerProvider);

    return Scaffold(
      appBar: const PedeLocalAppBar(title: 'Criar conta de entregador'),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                PedeLocalTextField(
                  controller: _nameController,
                  label: 'Nome completo',
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.person_outline_rounded,
                  validator: Validators.name,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _cpfController,
                  label: 'CPF',
                  keyboardType: TextInputType.number,
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.badge_outlined,
                  validator: Validators.cpf,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _phoneController,
                  label: 'Telefone (com DDD)',
                  keyboardType: TextInputType.phone,
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.phone_outlined,
                  validator: Validators.phone,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _emailController,
                  label: 'E-mail',
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.mail_outline_rounded,
                  validator: Validators.email,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _passwordController,
                  label: 'Senha',
                  obscureText: true,
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.lock_outline_rounded,
                  validator: Validators.password,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.lg),
                Text('Cidade de atuação', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                citiesAsync.when(
                  data: (cities) => DropdownButtonFormField<City>(
                    initialValue: _selectedCity,
                    items: cities
                        .map(
                          (city) => DropdownMenuItem(
                            value: city,
                            child: Text(city.name),
                          ),
                        )
                        .toList(),
                    onChanged: isLoading
                        ? null
                        : (value) => setState(() => _selectedCity = value),
                    decoration: const InputDecoration(
                      hintText: 'Selecione sua cidade',
                    ),
                  ),
                  loading: () => const LinearProgressIndicator(),
                  error: (error, stackTrace) => Text(
                    'Não foi possível carregar as cidades.',
                    style: AppTypography.bodyMuted.copyWith(color: Colors.red),
                  ),
                ),
                const SizedBox(height: AppSpacing.md),
                Text('Veículo', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                DropdownButtonFormField<VehicleType>(
                  initialValue: _vehicleType,
                  items: VehicleType.values
                      .map(
                        (v) => DropdownMenuItem(value: v, child: Text(v.label)),
                      )
                      .toList(),
                  onChanged: isLoading
                      ? null
                      : (value) => setState(
                          () => _vehicleType = value ?? _vehicleType,
                        ),
                ),
                const SizedBox(height: AppSpacing.md),
                Text('Tipo de chave PIX', style: AppTypography.label),
                const SizedBox(height: AppSpacing.xs),
                DropdownButtonFormField<PixKeyType>(
                  initialValue: _pixKeyType,
                  items: PixKeyType.values
                      .map(
                        (v) => DropdownMenuItem(value: v, child: Text(v.label)),
                      )
                      .toList(),
                  onChanged: isLoading
                      ? null
                      : (value) =>
                            setState(() => _pixKeyType = value ?? _pixKeyType),
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _pixKeyController,
                  label: 'Chave PIX',
                  textInputAction: TextInputAction.done,
                  prefixIcon: Icons.pix_outlined,
                  validator: (value) =>
                      Validators.required(value, field: 'A chave PIX'),
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.lg),
                PedeLocalButton(
                  label: 'Criar conta',
                  isLoading: isLoading,
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
