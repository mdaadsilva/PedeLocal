import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'notification_payload.dart';

/// Evita dois snackbars iguais quando SignalR e FCM representam o mesmo
/// evento em janela curta (app em foreground com hub conectado).
class RecentNotificationFeedback {
  RecentNotificationFeedback({
    this.window = const Duration(seconds: 4),
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  final Duration window;
  final DateTime Function() _clock;
  final Map<String, DateTime> _seen = {};

  /// `true` se este feedback ainda não foi mostrado nesta janela.
  bool claim(String key) {
    if (key.isEmpty) return true;
    final now = _clock();
    _seen.removeWhere((_, seenAt) => now.difference(seenAt) > window);
    if (_seen.containsKey(key)) return false;
    _seen[key] = now;
    return true;
  }

  static String? keyFor(NotificationPayload payload) {
    if (payload.eventType == NotificationEventTypes.deliveryAvailable) {
      return 'delivery-available';
    }
    if (payload.eventType == NotificationEventTypes.paymentReported) {
      return 'payment:${payload.batchId ?? 'reported'}';
    }
    if (payload.orderId != null) {
      return 'order:${payload.orderId}';
    }
    if (payload.deliveryId != null) {
      return 'delivery:${payload.deliveryId}';
    }
    if (payload.eventType != null) {
      return 'event:${payload.eventType}';
    }
    return null;
  }
}

final recentNotificationFeedbackProvider =
    Provider<RecentNotificationFeedback>((ref) {
      return RecentNotificationFeedback();
    });
