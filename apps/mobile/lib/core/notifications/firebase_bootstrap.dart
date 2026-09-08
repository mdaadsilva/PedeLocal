import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

import 'notification_payload.dart';

/// Handler de isolate de background exigido pelo Firebase Messaging.
///
/// Não faz rede, não lê JWT, não registra token e não navega. Só confirma
/// que o payload é parseável — o toque é tratado em
/// [FirebaseMessaging.onMessageOpenedApp] / [getInitialMessage] no isolate
/// principal.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  try {
    if (Firebase.apps.isEmpty) {
      await Firebase.initializeApp();
    }
    NotificationPayload.parse(message.data);
  } catch (_) {
    // Isolate de background: qualquer falha é ignorada para não derrubar
    // o processo. REST/SignalR no isolate principal não dependem daqui.
  }
}

/// Inicializa o Firebase no isolate principal. Sem
/// `google-services.json` / `GoogleService-Info.plist` (ou sem options),
/// retorna `false` e o app segue sem push.
Future<bool> bootstrapFirebasePush() async {
  try {
    if (Firebase.apps.isEmpty) {
      await Firebase.initializeApp();
    }
    FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
    return true;
  } catch (_) {
    return false;
  }
}
