import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/features/cart/application/cart_controller.dart';
import 'package:pedelocal_mobile/features/orders/application/checkout_controller.dart';
import 'package:pedelocal_mobile/features/orders/data/orders_repository.dart';
import 'package:pedelocal_mobile/features/orders/domain/order.dart';
import 'package:pedelocal_mobile/features/orders/domain/order_preview.dart';
import 'package:shared_preferences/shared_preferences.dart';

class MockOrdersRepository extends Mock implements OrdersRepository {}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late MockOrdersRepository ordersRepository;

  Future<ProviderContainer> buildContainer() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final container = ProviderContainer(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(prefs),
        ordersRepositoryProvider.overrideWithValue(ordersRepository),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  Order buildOrder({
    String status = 'Pending',
    String type = 'Delivery',
    String? addressId = 'addr-1',
    double deliveryFee = 5,
    double total = 25,
  }) {
    return Order(
      id: 'order-1',
      customerUserId: 'user-1',
      establishmentId: 'est-1',
      cityId: 'city-1',
      addressId: addressId,
      status: status,
      type: type,
      paymentMethod: 'Pix',
      paymentStatus: 'Pending',
      subtotal: 20,
      deliveryFee: deliveryFee,
      total: total,
      createdAtUtc: DateTime.utc(2026, 9, 7),
      items: const [],
      statusHistory: const [],
    );
  }

  void stubCreate({Order? order, Object? error}) {
    Future<Order> createStub() => ordersRepository.create(
      establishmentId: any(named: 'establishmentId'),
      addressId: any(named: 'addressId'),
      paymentMethod: any(named: 'paymentMethod'),
      changeForAmount: any(named: 'changeForAmount'),
      items: any(named: 'items'),
      idempotencyKey: any(named: 'idempotencyKey'),
      orderType: any(named: 'orderType'),
      notes: any(named: 'notes'),
    );
    if (error != null) {
      when(createStub).thenThrow(error);
    } else {
      when(createStub).thenAnswer((_) async => order ?? buildOrder());
    }
  }

  setUp(() {
    ordersRepository = MockOrdersRepository();
  });

  Future<ProviderContainer> containerWithCartItem() async {
    final container = await buildContainer();
    await container
        .read(cartControllerProvider.notifier)
        .addItem(
          establishmentId: 'est-1',
          establishmentName: 'Padaria do João',
          productId: 'prod-1',
          productName: 'X-Burguer',
          unitPrice: 20,
          quantity: 1,
          selectedOptions: const [],
        );
    return container;
  }

  test('submit cria o pedido via POST /api/orders e usa os valores retornados '
      'pelo backend como verdade final', () async {
    final container = await containerWithCartItem();
    final order = buildOrder();
    stubCreate(order: order);

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: OrderFulfillmentType.delivery,
          addressId: 'addr-1',
          paymentMethod: PaymentMethodOption.pix,
        );

    final state = container.read(checkoutControllerProvider);
    expect(state.value, order);
    expect(state.value!.total, 25);
  });

  test(
    'submit só limpa o carrinho depois de sucesso confirmado pelo backend',
    () async {
      final container = await containerWithCartItem();
      stubCreate();

      expect(container.read(cartControllerProvider).isEmpty, isFalse);

      await container
          .read(checkoutControllerProvider.notifier)
          .submit(
            orderType: OrderFulfillmentType.delivery,
            addressId: 'addr-1',
            paymentMethod: PaymentMethodOption.pix,
          );

      expect(container.read(cartControllerProvider).isEmpty, isTrue);
    },
  );

  test('submit propaga AppException e mantém o carrinho intacto quando a '
      'criação do pedido falha', () async {
    final container = await containerWithCartItem();
    stubCreate(
      error: const AppException(
        code: 'ESTABLISHMENT_CLOSED',
        message: 'O estabelecimento está fechado no momento.',
      ),
    );

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: OrderFulfillmentType.delivery,
          addressId: 'addr-1',
          paymentMethod: PaymentMethodOption.pix,
        );

    final state = container.read(checkoutControllerProvider);
    expect(state.hasError, isTrue);
    expect((state.error as AppException).code, 'ESTABLISHMENT_CLOSED');
    expect(container.read(cartControllerProvider).isEmpty, isFalse);
  });

  test('submit propaga OUTSIDE_DELIVERY_AREA e mantém o carrinho', () async {
    final container = await containerWithCartItem();
    stubCreate(
      error: const AppException(
        code: 'OUTSIDE_DELIVERY_AREA',
        message: 'Este estabelecimento não entrega neste endereço.',
        statusCode: 400,
      ),
    );

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: OrderFulfillmentType.delivery,
          addressId: 'addr-1',
          paymentMethod: PaymentMethodOption.pix,
        );

    final error =
        container.read(checkoutControllerProvider).error as AppException;
    expect(error.code, 'OUTSIDE_DELIVERY_AREA');
    expect(error.message, 'Este estabelecimento não entrega neste endereço.');
    expect(container.read(cartControllerProvider).isEmpty, isFalse);
  });

  test('submit com carrinho vazio nunca chama o backend', () async {
    final container = await buildContainer();
    await container.read(checkoutControllerProvider.future);

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: OrderFulfillmentType.delivery,
          addressId: 'addr-1',
          paymentMethod: PaymentMethodOption.pix,
        );

    verifyNever(
      () => ordersRepository.create(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
        orderType: any(named: 'orderType'),
        notes: any(named: 'notes'),
      ),
    );
    expect(container.read(checkoutControllerProvider).hasError, isTrue);
  });

  test('submit Pickup envia addressId nulo e orderType Pickup', () async {
    final container = await containerWithCartItem();
    stubCreate(
      order: buildOrder(
        type: 'Pickup',
        addressId: null,
        deliveryFee: 0,
        total: 20,
      ),
    );

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(
          orderType: OrderFulfillmentType.pickup,
          paymentMethod: PaymentMethodOption.pix,
          notes: 'Sem cebola',
        );

    final captured = verify(
      () => ordersRepository.create(
        establishmentId: 'est-1',
        addressId: null,
        paymentMethod: 'Pix',
        changeForAmount: null,
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
        orderType: 'Pickup',
        notes: 'Sem cebola',
      ),
    )..called(1);
    expect(captured, isNotNull);
    expect(container.read(checkoutControllerProvider).value?.isPickup, isTrue);
    expect(container.read(checkoutControllerProvider).value?.deliveryFee, 0);
  });

  test('preview Delivery chama POST /api/orders/preview', () async {
    final container = await containerWithCartItem();
    when(
      () => ordersRepository.preview(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        orderType: any(named: 'orderType'),
        notes: any(named: 'notes'),
      ),
    ).thenAnswer(
      (_) async => const OrderPreview(
        establishmentId: 'est-1',
        type: 'Delivery',
        addressId: 'addr-1',
        subtotal: 20,
        deliveryFee: 5,
        total: 25,
        distanceKm: 1.2,
        items: [],
      ),
    );

    await container
        .read(checkoutPreviewControllerProvider.notifier)
        .load(
          orderType: OrderFulfillmentType.delivery,
          addressId: 'addr-1',
          paymentMethod: PaymentMethodOption.pix,
        );

    final preview = container.read(checkoutPreviewControllerProvider).value;
    expect(preview?.total, 25);
    expect(preview?.deliveryFee, 5);
  });

  test('preview Pickup sem endereço ainda consulta o backend', () async {
    final container = await containerWithCartItem();
    when(
      () => ordersRepository.preview(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        orderType: any(named: 'orderType'),
        notes: any(named: 'notes'),
      ),
    ).thenAnswer(
      (_) async => const OrderPreview(
        establishmentId: 'est-1',
        type: 'Pickup',
        subtotal: 20,
        deliveryFee: 0,
        total: 20,
        items: [],
      ),
    );

    await container
        .read(checkoutPreviewControllerProvider.notifier)
        .load(
          orderType: OrderFulfillmentType.pickup,
          paymentMethod: PaymentMethodOption.cash,
        );

    verify(
      () => ordersRepository.preview(
        establishmentId: 'est-1',
        addressId: null,
        paymentMethod: 'Cash',
        changeForAmount: null,
        items: any(named: 'items'),
        orderType: 'Pickup',
        notes: null,
      ),
    ).called(1);
    expect(
      container.read(checkoutPreviewControllerProvider).value?.deliveryFee,
      0,
    );
  });
}
