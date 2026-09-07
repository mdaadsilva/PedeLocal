import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/app_exception.dart';
import '../../../core/routing/app_routes.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_spacing.dart';
import '../../../core/theme/app_typography.dart';
import '../../../core/utils/validators.dart';
import '../../../core/widgets/brand_logo.dart';
import '../../../core/widgets/brand_wordmark.dart';
import '../../../core/widgets/pedelocal_button.dart';
import '../../../core/widgets/pedelocal_text_field.dart';
import '../application/login_controller.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    ref
        .read(loginControllerProvider.notifier)
        .login(
          email: _emailController.text.trim(),
          password: _passwordController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(loginControllerProvider, (previous, next) {
      if (next is AsyncError) {
        final error = next.error;
        final message = error is AppException
            ? error.message
            : 'Não foi possível entrar. Tente novamente.';
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(message)));
      }
    });

    final isLoading = ref.watch(loginControllerProvider).isLoading;

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SizedBox(height: AppSpacing.xl),
                const Center(child: BrandLogo(size: 88)),
                const SizedBox(height: AppSpacing.md),
                const Center(child: BrandWordmark()),
                const SizedBox(height: AppSpacing.xs),
                const Center(child: BrandSlogan()),
                const SizedBox(height: AppSpacing.xxl),
                Text('Entrar', style: AppTypography.headline),
                const SizedBox(height: AppSpacing.lg),
                PedeLocalTextField(
                  controller: _emailController,
                  label: 'E-mail',
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  prefixIcon: Icons.mail_outline_rounded,
                  autofillHints: const [AutofillHints.email],
                  validator: Validators.email,
                  enabled: !isLoading,
                ),
                const SizedBox(height: AppSpacing.md),
                PedeLocalTextField(
                  controller: _passwordController,
                  label: 'Senha',
                  obscureText: true,
                  textInputAction: TextInputAction.done,
                  prefixIcon: Icons.lock_outline_rounded,
                  autofillHints: const [AutofillHints.password],
                  validator: (value) =>
                      Validators.required(value, field: 'A senha'),
                  enabled: !isLoading,
                  onChanged: (_) {},
                ),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton(
                    onPressed: isLoading
                        ? null
                        : () => context.push(AppRoutes.forgotPassword),
                    child: const Text('Esqueci minha senha'),
                  ),
                ),
                const SizedBox(height: AppSpacing.sm),
                PedeLocalButton(
                  label: 'Entrar',
                  isLoading: isLoading,
                  onPressed: _submit,
                ),
                const SizedBox(height: AppSpacing.lg),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Text(
                      'Ainda não tem conta?',
                      style: AppTypography.bodyMuted,
                    ),
                    TextButton(
                      onPressed: isLoading
                          ? null
                          : () => _showCreateAccountOptions(context),
                      child: const Text('Criar conta'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _showCreateAccountOptions(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Como você quer usar o PedeLocal?',
                  style: AppTypography.title,
                ),
                const SizedBox(height: AppSpacing.md),
                ListTile(
                  leading: const Icon(
                    Icons.shopping_bag_outlined,
                    color: AppColors.primary,
                  ),
                  title: const Text('Quero comprar (Cliente)'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    context.push(AppRoutes.registerCustomer);
                  },
                ),
                ListTile(
                  leading: const Icon(
                    Icons.delivery_dining_outlined,
                    color: AppColors.primary,
                  ),
                  title: const Text('Quero entregar (Entregador)'),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    context.push(AppRoutes.registerDriver);
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
