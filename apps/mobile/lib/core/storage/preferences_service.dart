import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Preferências locais não sensíveis (não usar para tokens/senhas — isso é
/// responsabilidade exclusiva de [SecureStorageService]).
///
/// Usado hoje para lembrar a última cidade escolhida pelo cliente
/// (`docs` — "Salvar localmente a última cidade escolhida quando apropriado").
class PreferencesService {
  PreferencesService(this._prefs);

  final SharedPreferences _prefs;

  static const _lastCityIdKey = 'pedelocal.last_city_id';
  static const _lastCityNameKey = 'pedelocal.last_city_name';
  static const _notificationPermissionAskedKey =
      'pedelocal.notification_permission_asked';

  String? get lastCityId => _prefs.getString(_lastCityIdKey);

  String? get lastCityName => _prefs.getString(_lastCityNameKey);

  Future<void> saveLastCity({required String id, required String name}) async {
    await _prefs.setString(_lastCityIdKey, id);
    await _prefs.setString(_lastCityNameKey, name);
  }

  Future<void> clearLastCity() async {
    await _prefs.remove(_lastCityIdKey);
    await _prefs.remove(_lastCityNameKey);
  }

  /// Permissão de notificação já foi pedida uma vez — não repetir de
  /// forma agressiva (Android 13+ / iOS).
  bool get notificationPermissionAsked =>
      _prefs.getBool(_notificationPermissionAskedKey) ?? false;

  Future<void> markNotificationPermissionAsked() =>
      _prefs.setBool(_notificationPermissionAskedKey, true);

  // ---------------------------------------------------------------------
  // Carrinho — dado não sensível (nenhum token/senha/dado de pagamento é
  // persistido aqui), guardado só para sobreviver a um fechamento acidental
  // do app antes do checkout.
  // ---------------------------------------------------------------------
  static const _cartKey = 'pedelocal.cart';

  Map<String, dynamic>? readCart() {
    final raw = _prefs.getString(_cartKey);
    if (raw == null || raw.isEmpty) return null;
    try {
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }

  Future<void> saveCart(Map<String, dynamic> json) async {
    await _prefs.setString(_cartKey, jsonEncode(json));
  }

  Future<void> clearCart() async {
    await _prefs.remove(_cartKey);
  }
}

/// Sobrescrito em `main.dart` com a instância real de [SharedPreferences]
/// obtida antes de `runApp` — nunca lido antes disso.
final sharedPreferencesProvider = Provider<SharedPreferences>((ref) {
  throw UnimplementedError(
    'sharedPreferencesProvider deve ser sobrescrito em main.dart com a instância real.',
  );
});

final preferencesServiceProvider = Provider<PreferencesService>((ref) {
  return PreferencesService(ref.watch(sharedPreferencesProvider));
});
