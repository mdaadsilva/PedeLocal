import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Nome da marca ("PedeLocal") estilizado — "Pede" em verde escuro e
/// "Local" em verde, espelhando a logo oficial.
class BrandWordmark extends StatelessWidget {
  const BrandWordmark({super.key, this.fontSize = 32});

  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return RichText(
      text: TextSpan(
        style: TextStyle(fontSize: fontSize, fontWeight: FontWeight.w800),
        children: const [
          TextSpan(
            text: 'Pede',
            style: TextStyle(color: AppColors.secondary),
          ),
          TextSpan(
            text: 'Local',
            style: TextStyle(color: AppColors.primary),
          ),
        ],
      ),
    );
  }
}

/// Slogan oficial: "Da sua cidade, pra você".
class BrandSlogan extends StatelessWidget {
  const BrandSlogan({super.key, this.fontSize = 14});

  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Text(
      'Da sua cidade, pra você',
      style: TextStyle(
        fontSize: fontSize,
        fontWeight: FontWeight.w500,
        color: AppColors.textSecondary,
        letterSpacing: 0.2,
      ),
    );
  }
}
