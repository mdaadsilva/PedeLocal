import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/notifications/notification_payload.dart';
import 'package:pedelocal_mobile/core/notifications/notification_router.dart';
import 'package:pedelocal_mobile/core/routing/app_routes.dart';
import 'package:pedelocal_mobile/features/session/domain/user_role.dart';

void main() {
  const router = NotificationRouter();

  group('CUSTOMER', () {
    test('com orderId vai para acompanhamento do pedido', () {
      final location = router.resolve(
        payload: const NotificationPayload(
          orderId: 'order-1',
          eventType: NotificationEventTypes.orderAccepted,
        ),
        isAuthenticated: true,
        role: UserRole.customer,
      );

      expect(location, AppRoutes.orderTracking('order-1'));
    });

    test('sem orderId não navega', () {
      final location = router.resolve(
        payload: const NotificationPayload(
          deliveryId: 'delivery-1',
          eventType: NotificationEventTypes.deliveryAvailable,
        ),
        isAuthenticated: true,
        role: UserRole.customer,
      );

      expect(location, isNull);
    });
  });

  group('DRIVER', () {
    test('DeliveryAvailable vai para a home do entregador', () {
      final location = router.resolve(
        payload: const NotificationPayload(
          deliveryId: 'delivery-1',
          eventType: NotificationEventTypes.deliveryAvailable,
        ),
        isAuthenticated: true,
        role: UserRole.driver,
      );

      expect(location, AppRoutes.homeDriver);
    });

    test('PaymentReported vai para recebíveis', () {
      final location = router.resolve(
        payload: const NotificationPayload(
          batchId: 'batch-1',
          eventType: NotificationEventTypes.paymentReported,
        ),
        isAuthenticated: true,
        role: UserRole.driver,
      );

      expect(location, AppRoutes.driverReceivables);
    });

    test('não usa role/userId do payload — só a sessão', () {
      final location = router.resolve(
        payload: const NotificationPayload(
          orderId: 'order-1',
          eventType: NotificationEventTypes.orderAccepted,
        ),
        isAuthenticated: true,
        role: UserRole.driver,
      );

      expect(location, AppRoutes.homeDriver);
      expect(location, isNot(AppRoutes.orderTracking('order-1')));
    });
  });

  test('sem autenticação não navega', () {
    final location = router.resolve(
      payload: const NotificationPayload(
        orderId: 'order-1',
        eventType: NotificationEventTypes.orderAccepted,
      ),
      isAuthenticated: false,
      role: UserRole.customer,
    );

    expect(location, isNull);
  });

  test('role unsupported não navega', () {
    final location = router.resolve(
      payload: const NotificationPayload(
        orderId: 'order-1',
        eventType: NotificationEventTypes.orderAccepted,
      ),
      isAuthenticated: true,
      role: UserRole.unsupported,
    );

    expect(location, isNull);
  });

  test('payload vazio não navega', () {
    final location = router.resolve(
      payload: const NotificationPayload.empty(),
      isAuthenticated: true,
      role: UserRole.customer,
    );

    expect(location, isNull);
  });
}
