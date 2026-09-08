/// Mensagem FCM já reduzida ao que o app pode usar com segurança
/// (título/corpo da notificação + mapa `data`). Sem token, sem JWT.
class PushIncomingMessage {
  const PushIncomingMessage({required this.data, this.title, this.body});

  final Map<String, dynamic> data;
  final String? title;
  final String? body;
}

/// Abstração sobre Firebase Cloud Messaging. Testes e o caminho "sem
/// Firebase configurado" usam [UnavailablePushMessagingGateway] — nenhum
/// teste unitário deve instanciar o SDK real.
abstract class PushMessagingGateway {
  Future<bool> ensureInitialized();

  Future<void> requestPermission();

  Future<String?> getToken();

  Stream<String> get onTokenRefresh;

  Stream<PushIncomingMessage> get onForegroundMessage;

  Stream<PushIncomingMessage> get onMessageOpenedApp;

  Future<PushIncomingMessage?> getInitialMessage();
}

/// Gateway inerte: Firebase ausente/falhou. REST e SignalR seguem iguais.
class UnavailablePushMessagingGateway implements PushMessagingGateway {
  const UnavailablePushMessagingGateway();

  @override
  Future<bool> ensureInitialized() async => false;

  @override
  Future<void> requestPermission() async {}

  @override
  Future<String?> getToken() async => null;

  @override
  Stream<String> get onTokenRefresh => const Stream.empty();

  @override
  Stream<PushIncomingMessage> get onForegroundMessage => const Stream.empty();

  @override
  Stream<PushIncomingMessage> get onMessageOpenedApp => const Stream.empty();

  @override
  Future<PushIncomingMessage?> getInitialMessage() async => null;
}
