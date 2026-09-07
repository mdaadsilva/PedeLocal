import 'package:flutter/material.dart';

import '../branding/brand_assets.dart';
import '../theme/app_colors.dart';
import '../theme/app_radius.dart';

/// Logo oficial do PedeLocal.
///
/// Usa `assets/branding/logo.png` quando disponível. Se o asset ainda não
/// tiver sido entregue (arquivo ausente do diretório), cai graciosamente
/// para um placeholder desenhado com os tokens da marca (pin + sacola em
/// verde) — a tela nunca quebra por falta do arquivo físico.
class BrandLogo extends StatelessWidget {
  const BrandLogo({super.key, this.size = 96});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Image.asset(
      BrandAssets.logo,
      width: size,
      height: size,
      errorBuilder: (context, error, stackTrace) =>
          _PlaceholderMark(size: size),
    );
  }
}

/// Placeholder visual (mesma linguagem de marca: pin de localização + verde)
/// usado enquanto os arquivos finais de `assets/branding/` não chegam.
class _PlaceholderMark extends StatelessWidget {
  const _PlaceholderMark({required this.size});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: AppColors.primary,
        borderRadius: AppRadius.radiusLg,
      ),
      child: Icon(
        Icons.storefront_rounded,
        color: AppColors.onPrimary,
        size: size * 0.55,
      ),
    );
  }
}
