import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/realtime/delivery_completed_events.dart';
import 'package:pedelocal_mobile/core/realtime/order_events.dart';
import 'package:pedelocal_mobile/features/orders/application/order_tracking_controller.dart';
import 'package:pedelocal_mobile/features/orders/data/orders_repository.dart';
import 'package:pedelocal_mobile/features/orders/domain/order.dart';

class MockOrdersRepository extends Mock implements OrdersRepository {}

/// Acompanhamento de pedido (`docs/ORDER_FLOW.md`) — carrega
/// `GET /api/orders/{id}` e reage a eventos `OrderStatusChanged` do SignalR
/// refazendo o GET (nunca faz polling contínuo).
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late MockOrdersRepository ordersRepository;
  late StreamController<OrderStatusChangedEvent> eventController;
  late StreamController<DeliveryCompletedEvent> completedController;

  Order buildOrder(String status) {
    return Order(
      id: 'order-1',
      customerUserId: 'user-1',
      establishmentId: 'est-1',
      cityId: 'city-1',
      addressId: 'addr-1',
      status: status,
      paymentMethod: 'Pix',
      paymentStatus: 'Pending',
      subtotal: 20,
      deliveryFee: 5,
      total: 25,
      createdAtUtc: DateTime.utc(2026, 9, 7),
      items: const [],
      statusHistory: const [],
    );
  }

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      overrides: [
        ordersRepositoryProvider.overrideWithValue(ordersRepository),
        orderStatusChangedStreamProvider.overrideWith(
          (ref) => eventController.stream,
        ),
        deliveryCompletedStreamProvider.overrideWith(
          (ref) => completedController.stream,
        ),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  setUp(() {
    ordersRepository = MockOrdersRepository();
    eventController = StreamController<OrderStatusChangedEvent>.broadcast();
    completedController = StreamController<DeliveryCompletedEvent>.broadcast();
  });

  tearDown(() {
    eventController.close();
    completedController.close();
  });

  test('build faz GET /api/orders/{id} e retorna o pedido', () async {
    when(() => ordersRepository.getById('order-1'))
        .thenAnswer((_) async => buildOrder('Pending'));
    final container = buildContainer();

    final order = await container.read(
      orderTrackingControllerProvider('order-1').future,
    );

    expect(order.status, 'Pending');
    verify(() => ordersRepository.getById('order-1')).called(1);
  });

  test('ao receber OrderStatusChanged deste pedido via SignalR, refaz o GET e '
      'atualiza a tela sem polling', () async {
    var callCount = 0;
    when(() => ordersRepository.getById('order-1')).thenAnswer((_) async {
      callCount++;
      return buildOrder(callCount == 1 ? 'Pending' : 'Accepted');
    });
    final container = buildContainer();

    // Mantém o provider vivo e observa as mudanças de estado.
    final states = <AsyncValue<Order>>[];
    container.listen(
      orderTrackingControllerProvider('order-1'),
      (previous, next) => states.add(next),
      fireImmediately: true,
    );
    await container.read(orderTrackingControllerProvider('order-1').future);

    eventController.add(
      const OrderStatusChangedEvent(orderId: 'order-1', status: 'Accepted'),
    );
    // Deixa o listener assíncrono processar o evento e o refresh().
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);

    final finalState = container.read(
      orderTrackingControllerProvider('order-1'),
    );
    expect(finalState.value?.status, 'Accepted');
    expect(callCount, 2);
  });

  test('ignora eventos OrderStatusChanged de outros pedidos', () async {
    when(() => ordersRepository.getById('order-1'))
        .thenAnswer((_) async => buildOrder('Pending'));
    final container = buildContainer();
    await container.read(orderTrackingControllerProvider('order-1').future);

    eventController.add(
      const OrderStatusChangedEvent(orderId: 'order-outro', status: 'Accepted'),
    );
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);

    verify(() => ordersRepository.getById('order-1')).called(1);
    expect(
      container.read(orderTrackingControllerProvider('order-1')).value?.status,
      'Pending',
    );
  });

  test('ao receber DeliveryCompleted deste pedido, refaz o GET', () async {
    var callCount = 0;
    when(() => ordersRepository.getById('order-1')).thenAnswer((_) async {
      callCount++;
      return buildOrder(callCount == 1 ? 'OutForDelivery' : 'Delivered');
    });
    final container = buildContainer();
    container.listen(
      orderTrackingControllerProvider('order-1'),
      (previous, next) {},
      fireImmediately: true,
    );
    await container.read(orderTrackingControllerProvider('order-1').future);

    completedController.add(const DeliveryCompletedEvent(orderId: 'order-1'));
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);

    expect(
      container.read(orderTrackingControllerProvider('order-1')).value?.status,
      'Delivered',
    );
    expect(callCount, 2);
  });
}
