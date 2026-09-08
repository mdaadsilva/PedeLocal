import 'dart:io' show Platform;

import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Valores aceitos por `PUT /api/me/device-tokens`
/// (`DevicePlatform`: `Unknown` | `Ios` | `Android` | `Web`).
/// O backend faz `Enum.TryParse` case-insensitive; enviamos PascalCase
/// igual ao contrato em `docs/PUSH_NOTIFICATIONS.md`.
String resolveDevicePlatform() {
  if (Platform.isIOS) return 'Ios';
  if (Platform.isAndroid) return 'Android';
  return 'Unknown';
}

final devicePlatformResolverProvider = Provider<String Function()>((ref) {
  return resolveDevicePlatform;
});
