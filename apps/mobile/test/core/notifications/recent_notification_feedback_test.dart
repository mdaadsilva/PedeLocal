import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/notifications/notification_payload.dart';
import 'package:pedelocal_mobile/core/notifications/recent_notification_feedback.dart';

void main() {
  test('o mesmo evento não reivindica feedback duas vezes na janela', () {
    var now = DateTime.utc(2026, 9, 7, 12);
    final feedback = RecentNotificationFeedback(
      window: const Duration(seconds: 4),
      clock: () => now,
    );

    expect(feedback.claim('order:1'), isTrue);
    expect(feedback.claim('order:1'), isFalse);

    now = now.add(const Duration(seconds: 5));
    expect(feedback.claim('order:1'), isTrue);
  });

  test('DeliveryAvailable usa chave compartilhada com o SignalR', () {
    expect(
      RecentNotificationFeedback.keyFor(
        const NotificationPayload(
          deliveryId: 'd1',
          eventType: NotificationEventTypes.deliveryAvailable,
        ),
      ),
      'delivery-available',
    );
  });
}
