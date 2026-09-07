import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/app_environment.dart';
import '../storage/secure_storage_service.dart';
import 'auth_interceptor.dart';
import 'session_expired_notifier.dart';

/// Cliente HTTP único da aplicação (Dio), com `baseUrl` configurável,
/// interceptor de autenticação (Bearer + refresh automático em 401) e
/// tratamento de log seguro (nunca loga headers/tokens).
///
/// Repositories devem depender apenas de [ApiClient.dio] — nenhuma tela ou
/// controller deve instanciar `Dio` diretamente.
class ApiClient {
  ApiClient({
    required AppEnvironment environment,
    required SecureStorageService secureStorage,
    required Future<void> Function() onSessionExpired,
  }) : _plainDio = Dio(
         BaseOptions(
           // baseUrl é a raiz (`http://host:porta`), sem o prefixo `/api` —
           // cada chamada de repository usa o caminho completo documentado
           // em `docs/API_CONTRACT.md` (ex.: `/api/auth/login`), igual ao
           // que o `AuthInterceptor` espera ao decidir quais rotas são
           // públicas.
           baseUrl: environment.baseUrl,
           connectTimeout: Duration(milliseconds: environment.connectTimeoutMs),
           receiveTimeout: Duration(milliseconds: environment.receiveTimeoutMs),
           contentType: 'application/json',
         ),
       ) {
    dio = Dio(_plainDio.options);
    dio.interceptors.add(
      AuthInterceptor(
        secureStorage: secureStorage,
        plainDio: _plainDio,
        onSessionExpired: onSessionExpired,
      ),
    );
    if (kDebugMode) {
      dio.interceptors.add(
        LogInterceptor(
          requestBody: true,
          responseBody: false,
          requestHeader: false,
          responseHeader: false,
          error: true,
          logPrint: (log) => debugPrint('[HTTP] $log'),
        ),
      );
    }
  }

  /// Instância "crua", sem interceptors — usada internamente pelo
  /// [AuthInterceptor] para chamar `/api/auth/refresh` e para repetir
  /// requisições sem risco de recursão.
  final Dio _plainDio;

  /// Instância usada por todos os repositories da aplicação.
  late final Dio dio;
}

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(
    environment: ref.watch(appEnvironmentProvider),
    secureStorage: ref.watch(secureStorageServiceProvider),
    onSessionExpired: () async {
      ref.read(sessionExpiredControllerProvider).add(null);
    },
  );
  return client;
});

final dioProvider = Provider<Dio>((ref) => ref.watch(apiClientProvider).dio);
