// ignore_for_file: prefer_initializing_formals — os nomes de parâmetro
// (`secureStorage`, `plainDio`, `onSessionExpired`) são intencionalmente
// mais descritivos na API pública do que os nomes dos campos privados.
import 'dart:async';

import 'package:dio/dio.dart';

import '../storage/secure_storage_service.dart';

/// Interceptor Dio responsável por:
/// 1. Anexar `Authorization: Bearer <accessToken>` em toda requisição
///    autenticada (todas, exceto `/api/auth/*` e `/api/cities` /
///    `/api/establishment-categories`, que são públicas).
/// 2. Ao receber `401`, tentar renovar a sessão **uma única vez** via
///    `POST /api/auth/refresh` e repetir a requisição original — exatamente
///    o comportamento descrito em `docs/AUTH_FLOW.md`.
/// 3. Se o refresh também falhar (refresh token inválido/expirado/já usado —
///    `401 INVALID_REFRESH_TOKEN`), limpar a sessão local e notificar
///    [onSessionExpired] para que a camada de sessão redirecione ao login.
///
/// Requisições concorrentes que recebem `401` ao mesmo tempo compartilham a
/// mesma tentativa de refresh (evita múltiplas chamadas a `/refresh`, que
/// invalidariam umas às outras por ser rotativo).
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required SecureStorageService secureStorage,
    required Dio plainDio,
    required Future<void> Function() onSessionExpired,
  }) : _secureStorage = secureStorage,
       _plainDio = plainDio,
       _onSessionExpired = onSessionExpired;

  final SecureStorageService _secureStorage;

  /// Dio "cru", sem interceptors, usado exclusivamente para chamar
  /// `/api/auth/refresh` e para repetir a requisição original — evita
  /// recursão infinita caso o refresh também retorne 401.
  final Dio _plainDio;

  final Future<void> Function() _onSessionExpired;

  static const _publicPathPrefixes = [
    '/api/auth/',
    '/api/cities',
    '/api/establishment-categories',
  ];

  Completer<String?>? _refreshCompleter;

  bool _isPublicPath(String path) => _publicPathPrefixes.any(path.contains);

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (!_isPublicPath(options.path)) {
      final accessToken = await _secureStorage.readAccessToken();
      if (accessToken != null && accessToken.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $accessToken';
      }
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final statusCode = err.response?.statusCode;
    final requestPath = err.requestOptions.path;
    final alreadyRetried =
        err.requestOptions.extra['pedelocal_retried'] == true;

    final shouldTryRefresh =
        statusCode == 401 && !alreadyRetried && !_isPublicPath(requestPath);

    if (!shouldTryRefresh) {
      handler.next(err);
      return;
    }

    final newAccessToken = await _refreshAccessToken();
    if (newAccessToken == null) {
      await _onSessionExpired();
      handler.next(err);
      return;
    }

    try {
      final retryOptions = err.requestOptions;
      retryOptions.headers['Authorization'] = 'Bearer $newAccessToken';
      retryOptions.extra['pedelocal_retried'] = true;
      final response = await _plainDio.fetch<dynamic>(retryOptions);
      handler.resolve(response);
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  /// Garante que apenas uma chamada de refresh esteja em voo por vez —
  /// chamadas concorrentes aguardam o mesmo [Completer].
  Future<String?> _refreshAccessToken() async {
    final ongoing = _refreshCompleter;
    if (ongoing != null && !ongoing.isCompleted) {
      return ongoing.future;
    }

    final completer = Completer<String?>();
    _refreshCompleter = completer;

    try {
      final refreshToken = await _secureStorage.readRefreshToken();
      if (refreshToken == null || refreshToken.isEmpty) {
        completer.complete(null);
        return null;
      }

      final response = await _plainDio.post<dynamic>(
        '/api/auth/refresh',
        data: {'refreshToken': refreshToken},
      );

      final data = response.data as Map<String, dynamic>;
      final newAccessToken = data['accessToken'] as String;
      final newRefreshToken = data['refreshToken'] as String;
      final expiresAtRaw = data['accessTokenExpiresAtUtc'] as String?;

      await _secureStorage.saveTokens(
        accessToken: newAccessToken,
        refreshToken: newRefreshToken,
        accessTokenExpiresAtUtc: expiresAtRaw != null
            ? DateTime.tryParse(expiresAtRaw)
            : null,
      );

      completer.complete(newAccessToken);
      return newAccessToken;
    } catch (_) {
      // Refresh token inválido/expirado/já usado (401 INVALID_REFRESH_TOKEN)
      // ou qualquer outra falha de rede durante o refresh: trata como sessão
      // expirada, igual a `docs/AUTH_FLOW.md`.
      await _secureStorage.clearTokens();
      completer.complete(null);
      return null;
    } finally {
      if (identical(_refreshCompleter, completer)) {
        _refreshCompleter = null;
      }
    }
  }
}
