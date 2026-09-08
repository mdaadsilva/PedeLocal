// ignore_for_file: prefer_initializing_formals
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logging/logging.dart' as logging;
import 'package:signalr_netcore/signalr_client.dart';

import '../config/app_environment.dart';
import '../storage/secure_storage_service.dart';

/// Wrapper sobre `signalr_netcore` para o hub `{baseUrl}/hubs/pedelocal`
/// (`docs/REALTIME_EVENTS.md`).
///
/// O servidor atribui grupos a partir do JWT — o cliente nunca informa
/// userId/establishmentId/driverId para entrar em grupo. O token vai pelo
/// [HttpConnectionOptions.accessTokenFactory] (query `access_token` no
/// handshake) e é relido a cada reconexão. Nunca é logado.
class SignalRClient {
  SignalRClient({
    required AppEnvironment environment,
    required SecureStorageService secureStorage,
  }) : _environment = environment,
       _secureStorage = secureStorage;

  final AppEnvironment _environment;
  final SecureStorageService _secureStorage;

  HubConnection? _connection;
  final Map<String, void Function(List<Object?>? arguments)> _handlers = {};
  final List<void Function()> _reconnectedListeners = [];

  HubConnectionState get state =>
      _connection?.state ?? HubConnectionState.Disconnected;

  bool get isConnected => state == HubConnectionState.Connected;

  Future<void> connect() async {
    final existing = _connection;
    if (existing != null) {
      if (existing.state == HubConnectionState.Disconnected) {
        await existing.start();
      }
      return;
    }

    final token = await _secureStorage.readAccessToken();
    if (token == null || token.isEmpty) return;

    final logger = logging.Logger('SignalR');
    logger.level = logging.Level.WARNING;

    final connection = HubConnectionBuilder()
        .withUrl(
          _environment.hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async =>
                await _secureStorage.readAccessToken() ?? '',
            logMessageContent: false,
          ),
        )
        .withAutomaticReconnect(
          retryDelays: const [0, 2000, 5000, 10000, 20000, 30000],
        )
        .configureLogging(logger)
        .build();

    for (final entry in _handlers.entries) {
      connection.on(entry.key, entry.value);
    }
    connection.onreconnected(({String? connectionId}) {
      for (final listener in List<void Function()>.from(_reconnectedListeners)) {
        listener();
      }
    });

    _connection = connection;
    try {
      await connection.start();
    } catch (_) {
      _connection = null;
      rethrow;
    }
  }

  void on(String eventName, void Function(List<Object?>? arguments) handler) {
    _handlers[eventName] = handler;
    _connection?.on(eventName, handler);
  }

  void off(String eventName) {
    _handlers.remove(eventName);
    _connection?.off(eventName);
  }

  void addReconnectedListener(void Function() listener) {
    _reconnectedListeners.add(listener);
  }

  void removeReconnectedListener(void Function() listener) {
    _reconnectedListeners.remove(listener);
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
  }
}

final signalRClientProvider = Provider<SignalRClient>((ref) {
  final client = SignalRClient(
    environment: ref.watch(appEnvironmentProvider),
    secureStorage: ref.watch(secureStorageServiceProvider),
  );
  ref.onDispose(() => client.disconnect());
  return client;
});
