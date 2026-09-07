import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

enum PedeLocalButtonVariant { primary, secondary, text }

/// Botão padrão da marca. Sempre usar este componente em vez de
/// `ElevatedButton`/`OutlinedButton` crus, para garantir:
/// - altura de toque adequada (mín. 52dp);
/// - estado de loading embutido (evita duplo clique — enquanto [isLoading]
///   é `true`, o botão fica desabilitado e mostra um spinner no lugar do
///   texto, então basta ligar [isLoading] ao estado da chamada em
///   andamento do controller para prevenir duplo submit);
/// - aparência consistente entre todas as telas.
class PedeLocalButton extends StatelessWidget {
  const PedeLocalButton({
    super.key,
    required this.label,
    this.onPressed,
    this.isLoading = false,
    this.variant = PedeLocalButtonVariant.primary,
    this.icon,
    this.expand = true,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool isLoading;
  final PedeLocalButtonVariant variant;
  final IconData? icon;
  final bool expand;

  bool get _isDisabled => isLoading || onPressed == null;

  @override
  Widget build(BuildContext context) {
    final spinnerColor = variant == PedeLocalButtonVariant.primary
        ? AppColors.onPrimary
        : AppColors.primary;

    final child = isLoading
        ? SizedBox(
            height: 22,
            width: 22,
            child: CircularProgressIndicator(
              strokeWidth: 2.4,
              color: spinnerColor,
            ),
          )
        : Row(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icon != null) ...[
                Icon(icon, size: 20),
                const SizedBox(width: AppSpacing.xs),
              ],
              Text(label),
            ],
          );

    final Widget button = switch (variant) {
      PedeLocalButtonVariant.primary => ElevatedButton(
        onPressed: _isDisabled ? null : onPressed,
        child: child,
      ),
      PedeLocalButtonVariant.secondary => OutlinedButton(
        onPressed: _isDisabled ? null : onPressed,
        child: child,
      ),
      PedeLocalButtonVariant.text => TextButton(
        onPressed: _isDisabled ? null : onPressed,
        child: child,
      ),
    };

    if (!expand) return button;
    return SizedBox(width: double.infinity, child: button);
  }
}
