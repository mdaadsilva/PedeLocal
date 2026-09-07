import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Wrapper único sobre [FlutterSecureStorage].
///
/// SEGURANÇA (conforme instrução do produto):
/// - Nunca armazena senha em nenhum lugar do app.
/// - Tokens (access/refresh) só existem aqui, nunca em `SharedPreferences`,
///   memória compartilhada ou logs.
/// - Nenhum método desta classe deve ser chamado dentro de um `log`/`print`.
class SecureStorageService {
  SecureStorageService({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _accessTokenKey = 'pedelocal.access_token';
  static const _refreshTokenKey = 'pedelocal.refresh_token';
  static const _accessTokenExpiresAtKey = 'pedelocal.access_token_expires_at';

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
    DateTime? accessTokenExpiresAtUtc,
  }) async {
    await Future.wait([
      _storage.write(key: _accessTokenKey, value: accessToken),
      _storage.write(key: _refreshTokenKey, value: refreshToken),
      if (accessTokenExpiresAtUtc != null)
        _storage.write(
          key: _accessTokenExpiresAtKey,
          value: accessTokenExpiresAtUtc.toIso8601String(),
        ),
    ]);
  }

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  Future<String?> readRefreshToken() => _storage.read(key: _refreshTokenKey);

  Future<bool> hasSession() async {
    final refreshToken = await readRefreshToken();
    return refreshToken != null && refreshToken.isNotEmpty;
  }

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _accessTokenExpiresAtKey),
    ]);
  }
}

final secureStorageServiceProvider = Provider<SecureStorageService>((ref) {
  return SecureStorageService();
});
