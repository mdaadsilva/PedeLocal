// ignore_for_file: prefer_initializing_formals — nomes de parâmetro mais
// descritivos na API pública do que os nomes dos campos privados.
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logging/logging.dart' as logging;
import 'package:signalr_netcore/signalr_client.dart';

import '../config/app_environment.dart';
import '../storage/secure_storage_service.dart';

/// Wrapper fino sobre `signalr_netcore` preparando a conexão com o hub
/// SignalR do PedeLocal (`docs/FRONTEND_GUIDE.md#signalr`).
///
/// Escopo desta etapa: apenas a infraestrutura de conexão fica pronta
/// (conectar, escutar eventos por nome, reconectar com backoff, desconectar
/// de forma limpa). Nenhuma tela desta etapa (splash/login/cadastro/seleção
/// de cidade/home cliente/home entregador) assina eventos de pedido ou
/// entrega ainda — isso pertence às próximas etapas (carrinho/checkout/
/// acompanhamento de pedido/fluxo completo do entregador), conforme
/// solicitado.
///
/// Importante (`docs/FRONTEND_GUIDE.md`): o servidor decide sozinho em quais
/// grupos a conexão entra a partir do token — não existe (e não deve ser
/// criado) um método `Subscribe`. Ao reconectar, a tela responsável deve
/// sempre buscar o estado atual via REST (eventos perdidos durante a
/// desconexão não são reenviados) — isso é responsabilidade de cada feature
/// que futuramente escutar eventos, não deste cliente.
class SignalRClient {
  SignalRClient({
    required AppEnvironment environment,
    required SecureStorageService secureStorage,
  }) : _environment = environment,
       _secureStorage = secureStorage;

  final AppEnvironment _environment;
  final SecureStorageService _secureStorage;

  HubConnection? _connection;

  /// Handlers registrados via [on] — guardados aqui (e não só na conexão
  /// nativa) para sobreviver a um ciclo explícito [disconnect] + [connect]
  /// (ex.: logout seguido de login de outro usuário). Reconexões automáticas
  /// (`withAutomaticReconnect`) preservam os handlers da conexão nativa por
  /// conta própria; isto cobre o caso em que `on` é chamado antes do
  /// primeiro [connect] ou depois de um [disconnect] explícito.
  final Map<String, void Function(List<Object?>? arguments)> _handlers = {};

  HubConnectionState get state =>
      _connection?.state ?? HubConnectionState.Disconnected;

  bool get isConnected => state == HubConnectionState.Connected;

  /// Conecta ao hub autenticando via access token (renovado sob demanda pelo
  /// [AccessTokenFactory] a cada tentativa de conexão/reconexão — nunca
  /// logado, nunca embutido "fixo" na URL).
  Future<void> connect() async {
    if (_connection != null) return;

    final logger = logging.Logger('SignalR');
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
          retryDelays: [0, 2000, 5000, 10000, 20000, 30000],
        )
        .configureLogging(logger)
        .build();

    _connection = connection;
    // Reaplica handlers registrados antes deste connect() (ex.: chamados
    // durante a fase "desconectado", ou mantidos de um ciclo anterior).
    for (final entry in _handlers.entries) {
      connection.on(entry.key, entry.value);
    }
    await connection.start();
  }

  /// Registra um handler para um evento do hub (ver [SignalREvents]).
  /// Seguro chamar antes de [connect] — o handler é aplicado assim que a
  /// conexão existir.
  void on(String eventName, void Function(List<Object?>? arguments) handler) {
    _handlers[eventName] = handler;
    _connection?.on(eventName, handler);
  }

  void off(String eventName) {
    _handlers.remove(eventName);
    _connection?.off(eventName);
  }

  void onClose(void Function({Exception? error}) handler) {
    _connection?.onclose(handler);
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
