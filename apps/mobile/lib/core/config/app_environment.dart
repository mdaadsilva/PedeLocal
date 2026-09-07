import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Configuração de ambiente/rede da aplicação.
///
/// [baseUrl] é configurável (nunca hardcoded no meio do código) para permitir
/// apontar para outros ambientes (staging, dispositivo físico, emulador
/// Android que precisa de `10.0.2.2` em vez de `localhost`, etc.) sem
/// recompilar telas — apenas trocando o valor passado ao bootstrap ou via
/// `--dart-define=PEDELOCAL_BASE_URL=...`.
class AppEnvironment {
  const AppEnvironment({
    required this.baseUrl,
    this.connectTimeoutMs = 15000,
    this.receiveTimeoutMs = 20000,
  });

  final String baseUrl;
  final int connectTimeoutMs;
  final int receiveTimeoutMs;

  /// Ambiente padrão de desenvolvimento local, conforme
  /// `docs/FRONTEND_GUIDE.md` (porta `8080` exposta pelo `docker compose`).
  /// Pode ser sobrescrito em tempo de build com
  /// `--dart-define=PEDELOCAL_BASE_URL=http://10.0.2.2:8080`.
  factory AppEnvironment.defaultLocal() {
    const definedBaseUrl = String.fromEnvironment('PEDELOCAL_BASE_URL');
    return AppEnvironment(
      baseUrl: definedBaseUrl.isNotEmpty
          ? definedBaseUrl
          : 'http://localhost:8080',
    );
  }

  /// Endpoint do hub SignalR (`docs/FRONTEND_GUIDE.md#signalr`).
  String get hubUrl => '$baseUrl/hubs/pedelocal';

  /// Prefixo de todas as rotas REST documentadas em `docs/API_CONTRACT.md`.
  String get apiBaseUrl => '$baseUrl/api';
}

final appEnvironmentProvider = Provider<AppEnvironment>((ref) {
  return AppEnvironment.defaultLocal();
});
