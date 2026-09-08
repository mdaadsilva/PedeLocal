import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/notifications/notification_payload.dart';

void main() {
  group('NotificationPayload.parse', () {
    test('lê orderId e eventType do contrato de pedido', () {
      final payload = NotificationPayload.parse({
        'orderId': '11111111-1111-1111-1111-111111111111',
        'eventType': 'OrderAccepted',
      });

      expect(payload.orderId, '11111111-1111-1111-1111-111111111111');
      expect(payload.eventType, NotificationEventTypes.orderAccepted);
      expect(payload.deliveryId, isNull);
      expect(payload.isEmpty, isFalse);
    });

    test('lê orderId, deliveryId e eventType de evento de entrega', () {
      final payload = NotificationPayload.parse({
        'orderId': 'order-1',
        'deliveryId': 'delivery-1',
        'eventType': 'DeliveryAccepted',
      });

      expect(payload.orderId, 'order-1');
      expect(payload.deliveryId, 'delivery-1');
      expect(payload.eventType, NotificationEventTypes.deliveryAccepted);
    });

    test('lê só deliveryId em DeliveryAvailable', () {
      final payload = NotificationPayload.parse({
        'deliveryId': 'delivery-9',
        'eventType': 'DeliveryAvailable',
      });

      expect(payload.deliveryId, 'delivery-9');
      expect(payload.orderId, isNull);
      expect(payload.eventType, NotificationEventTypes.deliveryAvailable);
    });

    test('lê batchId em PaymentReported', () {
      final payload = NotificationPayload.parse({
        'batchId': 'batch-1',
        'eventType': 'PaymentReported',
      });

      expect(payload.batchId, 'batch-1');
      expect(payload.eventType, NotificationEventTypes.paymentReported);
    });

    test('payload nulo, vazio ou inválido não lança e fica vazio', () {
      expect(NotificationPayload.parse(null).isEmpty, isTrue);
      expect(NotificationPayload.parse({}).isEmpty, isTrue);
      expect(
        NotificationPayload.parse({'orderId': '', 'eventType': '  '}).isEmpty,
        isTrue,
      );
    });

    test('ignora userId e role mesmo que venham no mapa', () {
      final payload = NotificationPayload.parse({
        'orderId': 'order-2',
        'eventType': 'OrderCancelled',
        'userId': 'should-be-ignored',
        'role': 'SUPER_ADMIN',
      });

      expect(payload.orderId, 'order-2');
      expect(payload.eventType, NotificationEventTypes.orderCancelled);
    });

    test('converte valores não-string sem quebrar', () {
      final payload = NotificationPayload.parse({
        'orderId': 42,
        'eventType': true,
      });

      expect(payload.orderId, '42');
      expect(payload.eventType, 'true');
    });
  });
}
