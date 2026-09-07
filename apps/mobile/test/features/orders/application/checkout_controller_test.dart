import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/features/cart/application/cart_controller.dart';
import 'package:pedelocal_mobile/features/orders/application/checkout_controller.dart';
import 'package:pedelocal_mobile/features/orders/data/orders_repository.dart';
import 'package:pedelocal_mobile/features/orders/domain/order.dart';
import 'package:shared_preferences/shared_preferences.dart';

class MockOrdersRepository extends Mock implements OrdersRepository {}

/// Checkout (`docs/ORDER_FLOW.md#criar-pedido-cliente`) — cria o pedido
/// usando apenas `POST /api/orders`, só limpa o carrinho depois de sucesso
/// confirmado pelo backend, e propaga erro sem tocar no carrinho em caso de
/// falha.
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

  Order buildOrder({String status = 'Pending'}) {
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
    when(
      () => ordersRepository.create(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
      ),
    ).thenAnswer((_) async => order);

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(addressId: 'addr-1', paymentMethod: PaymentMethodOption.pix);

    final state = container.read(checkoutControllerProvider);
    expect(state.value, order);
    expect(state.value!.total, 25);
  });

  test(
    'submit só limpa o carrinho depois de sucesso confirmado pelo backend',
    () async {
      final container = await containerWithCartItem();
      when(
        () => ordersRepository.create(
          establishmentId: any(named: 'establishmentId'),
          addressId: any(named: 'addressId'),
          paymentMethod: any(named: 'paymentMethod'),
          changeForAmount: any(named: 'changeForAmount'),
          items: any(named: 'items'),
          idempotencyKey: any(named: 'idempotencyKey'),
        ),
      ).thenAnswer((_) async => buildOrder());

      expect(container.read(cartControllerProvider).isEmpty, isFalse);

      await container
          .read(checkoutControllerProvider.notifier)
          .submit(addressId: 'addr-1', paymentMethod: PaymentMethodOption.pix);

      expect(container.read(cartControllerProvider).isEmpty, isTrue);
    },
  );

  test('submit propaga AppException e mantém o carrinho intacto quando a '
      'criação do pedido falha', () async {
    final container = await containerWithCartItem();
    when(
      () => ordersRepository.create(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
      ),
    ).thenThrow(
      const AppException(
        code: 'ESTABLISHMENT_CLOSED',
        message: 'O estabelecimento está fechado no momento.',
      ),
    );

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(addressId: 'addr-1', paymentMethod: PaymentMethodOption.pix);

    final state = container.read(checkoutControllerProvider);
    expect(state.hasError, isTrue);
    expect((state.error as AppException).code, 'ESTABLISHMENT_CLOSED');
    // Carrinho não pode ser limpo em caso de erro na criação.
    expect(container.read(cartControllerProvider).isEmpty, isFalse);
  });

  test('submit propaga DELIVERY_NOT_AVAILABLE e mantém o carrinho', () async {
    final container = await containerWithCartItem();
    when(
      () => ordersRepository.create(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
      ),
    ).thenThrow(
      const AppException(
        code: 'DELIVERY_NOT_AVAILABLE',
        message: 'Este endereço está fora da área de entrega.',
        statusCode: 400,
      ),
    );

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(addressId: 'addr-1', paymentMethod: PaymentMethodOption.pix);

    expect(
      (container.read(checkoutControllerProvider).error as AppException).code,
      'DELIVERY_NOT_AVAILABLE',
    );
    expect(container.read(cartControllerProvider).isEmpty, isFalse);
  });

  test('submit com carrinho vazio nunca chama o backend', () async {
    final container = await buildContainer();
    // Garante que o build() inicial (assíncrono) já resolveu antes de
    // acionar submit(), evitando que ele sobrescreva o estado de erro.
    await container.read(checkoutControllerProvider.future);

    await container
        .read(checkoutControllerProvider.notifier)
        .submit(addressId: 'addr-1', paymentMethod: PaymentMethodOption.pix);

    verifyNever(
      () => ordersRepository.create(
        establishmentId: any(named: 'establishmentId'),
        addressId: any(named: 'addressId'),
        paymentMethod: any(named: 'paymentMethod'),
        changeForAmount: any(named: 'changeForAmount'),
        items: any(named: 'items'),
        idempotencyKey: any(named: 'idempotencyKey'),
      ),
    );
    expect(container.read(checkoutControllerProvider).hasError, isTrue);
  });
}
