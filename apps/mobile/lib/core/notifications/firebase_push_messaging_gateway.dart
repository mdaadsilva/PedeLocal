import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

import 'push_messaging_gateway.dart';

/// Implementação real do FCM. Qualquer falha de init (projeto sem
/// `google-services.json` / `GoogleService-Info.plist`, plugin nativo
/// ausente em testes) devolve `false` e o restante do app segue sem push.
class FirebasePushMessagingGateway implements PushMessagingGateway {
  bool _ready = false;

  @override
  Future<bool> ensureInitialized() async {
    if (_ready) return true;
    try {
      if (Firebase.apps.isEmpty) {
        await Firebase.initializeApp();
      }
      _ready = Firebase.apps.isNotEmpty;
      return _ready;
    } catch (_) {
      _ready = false;
      return false;
    }
  }

  @override
  Future<void> requestPermission() async {
    if (!_ready) return;
    await FirebaseMessaging.instance.requestPermission(
      alert: true,
      badge: true,
      sound: true,
    );
  }

  @override
  Future<String?> getToken() async {
    if (!_ready) return null;
    try {
      return await FirebaseMessaging.instance.getToken();
    } catch (_) {
      return null;
    }
  }

  @override
  Stream<String> get onTokenRefresh {
    if (!_ready) return const Stream.empty();
    return FirebaseMessaging.instance.onTokenRefresh;
  }

  @override
  Stream<PushIncomingMessage> get onForegroundMessage {
    if (!_ready) return const Stream.empty();
    return FirebaseMessaging.onMessage.map(_fromRemote);
  }

  @override
  Stream<PushIncomingMessage> get onMessageOpenedApp {
    if (!_ready) return const Stream.empty();
    return FirebaseMessaging.onMessageOpenedApp.map(_fromRemote);
  }

  @override
  Future<PushIncomingMessage?> getInitialMessage() async {
    if (!_ready) return null;
    try {
      final message = await FirebaseMessaging.instance.getInitialMessage();
      if (message == null) return null;
      return _fromRemote(message);
    } catch (_) {
      return null;
    }
  }

  static PushIncomingMessage _fromRemote(RemoteMessage message) {
    return PushIncomingMessage(
      data: Map<String, dynamic>.from(message.data),
      title: message.notification?.title,
      body: message.notification?.body,
    );
  }
}
