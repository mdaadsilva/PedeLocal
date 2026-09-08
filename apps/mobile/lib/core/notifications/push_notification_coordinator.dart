import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/cities/application/selected_city_controller.dart';
import '../../features/session/application/session_controller.dart';
import '../../features/session/domain/user_role.dart';
import '../realtime/reconnect_tick.dart';
import '../realtime/signalr_client.dart';
import '../routing/app_router.dart';
import 'notification_payload.dart';
import 'notification_router.dart';
import 'push_messaging_gateway.dart';
import 'push_notification_service.dart';
import 'recent_notification_feedback.dart';

/// Chave do [ScaffoldMessenger] raiz — snackbars de push em qualquer rota.
final rootScaffoldMessengerKeyProvider =
    Provider<GlobalKey<ScaffoldMessengerState>>((ref) {
      return GlobalKey<ScaffoldMessengerState>();
    });

final notificationRouterProvider = Provider<NotificationRouter>((ref) {
  return const NotificationRouter();
});

/// Liga FCM à sessão e ao `go_router`: registra token após login, snackbar
/// discreto em foreground (sem duplicar SignalR) e navegação no toque.
///
/// Observar uma única vez na raiz (`app.dart`).
final pushNotificationCoordinatorProvider = Provider<void>((ref) {
  final gateway = ref.read(pushMessagingGatewayProvider);
  final router = ref.read(notificationRouterProvider);
  var consumedInitialMessage = false;
  NotificationPayload? pendingTap;
  final subscriptions = <StreamSubscription<dynamic>>[];

  ref.onDispose(() {
    for (final subscription in subscriptions) {
      unawaited(subscription.cancel());
    }
  });

  void navigateIfAllowed(NotificationPayload payload) {
    final session = ref.read(sessionControllerProvider).value;
    if (session == null || !session.isAuthenticated) {
      pendingTap = payload;
      return;
    }

    if (session.role == UserRole.customer &&
        ref.read(selectedCityControllerProvider) == null) {
      pendingTap = payload;
      return;
    }

    final location = router.resolve(
      payload: payload,
      isAuthenticated: session.isAuthenticated,
      role: session.role,
    );
    if (location == null) return;
    ref.read(goRouterProvider).go(location);
  }

  void flushPendingTap() {
    final pending = pendingTap;
    if (pending == null) return;
    pendingTap = null;
    navigateIfAllowed(pending);
  }

  void showForegroundFeedback(PushIncomingMessage message) {
    final payload = NotificationPayload.parse(message.data);
    final key = RecentNotificationFeedback.keyFor(payload);
    final feedback = ref.read(recentNotificationFeedbackProvider);
    if (key != null && !feedback.claim(key)) {
      return;
    }

    final text = (message.body ?? message.title)?.trim();
    if (text != null && text.isNotEmpty) {
      ref
          .read(rootScaffoldMessengerKeyProvider)
          .currentState
          ?.showSnackBar(SnackBar(content: Text(text)));
    }

    final signalR = ref.read(signalRClientProvider);
    if (!signalR.isConnected) {
      ref.read(realtimeReconnectTickProvider.notifier).bump();
    }
  }

  Future<void> bindMessaging() async {
    final available = await gateway.ensureInitialized();
    if (!available) return;

    subscriptions.add(
      gateway.onForegroundMessage.listen(showForegroundFeedback),
    );
    subscriptions.add(
      gateway.onMessageOpenedApp.listen((message) {
        navigateIfAllowed(NotificationPayload.parse(message.data));
      }),
    );
  }

  unawaited(bindMessaging());

  ref.listen(sessionControllerProvider, (previous, next) {
    final session = next.value;
    if (session == null || next.isLoading) return;

    if (!session.isAuthenticated) return;

    unawaited(
      ref.read(pushTokenLifecycleProvider).startAfterAuthentication(),
    );

    if (!consumedInitialMessage) {
      consumedInitialMessage = true;
      unawaited(() async {
        try {
          final initial = await gateway.getInitialMessage();
          if (initial != null) {
            navigateIfAllowed(NotificationPayload.parse(initial.data));
          }
        } catch (_) {}
      }());
    }

    flushPendingTap();
  }, fireImmediately: true);

  ref.listen(selectedCityControllerProvider, (previous, next) {
    if (next != null) flushPendingTap();
  });
});
