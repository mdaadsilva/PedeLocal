import 'package:flutter/material.dart';

/// Paleta de cores centralizada da marca PedeLocal.
///
/// Direção visual: verde como cor principal (proximidade, comércio local,
/// confiança), verde escuro para contraste/textos importantes, branco/neutros
/// claros como base, e laranja usado pontualmente em destaques comerciais
/// (promoções, avisos de atenção). Nenhuma outra tela deve declarar cores
/// "soltas" fora desta classe — sempre reutilize estes tokens.
abstract class AppColors {
  AppColors._();

  // Verde — cor de marca.
  static const Color primary = Color(0xFF2E9E4F);
  static const Color primaryDark = Color(0xFF1B5E32);
  static const Color primaryLight = Color(0xFFDFF3E4);

  // Verde escuro — contraste, textos importantes, elementos de "entrega".
  static const Color secondary = Color(0xFF0F3D24);

  // Laranja — uso pontual (comércio, promoções, destaque, avisos).
  static const Color accent = Color(0xFFF4802B);
  static const Color accentLight = Color(0xFFFDE7D6);

  // Base neutra.
  static const Color background = Color(0xFFF7F8F6);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color surfaceAlt = Color(0xFFF0F2EF);

  // Texto.
  static const Color textPrimary = Color(0xFF14201A);
  static const Color textSecondary = Color(0xFF5B6B62);
  static const Color textDisabled = Color(0xFFA3ADA7);
  static const Color onPrimary = Color(0xFFFFFFFF);

  // Bordas / divisores.
  static const Color border = Color(0xFFE1E5E1);
  static const Color divider = Color(0xFFEAECE9);

  // Estados semânticos.
  static const Color success = Color(0xFF2E9E4F);
  static const Color warning = Color(0xFFF4A527);
  static const Color error = Color(0xFFD1443A);
  static const Color info = Color(0xFF2E7BD1);

  // Status de entregador (Driver.status) — cores de apoio para StatusBadge.
  static const Color statusOffline = Color(0xFF9AA39D);
  static const Color statusAvailable = Color(0xFF2E9E4F);
  static const Color statusBusy = Color(0xFFF4802B);
  static const Color statusBlocked = Color(0xFFD1443A);
  static const Color statusPending = Color(0xFF2E7BD1);
}
