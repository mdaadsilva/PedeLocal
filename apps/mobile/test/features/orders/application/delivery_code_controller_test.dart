import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/features/orders/application/delivery_code_controller.dart';
import 'package:pedelocal_mobile/features/orders/data/orders_repository.dart';
import 'package:pedelocal_mobile/features/orders/domain/delivery_code.dart';

class MockOrdersRepository extends Mock implements OrdersRepository {}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late MockOrdersRepository ordersRepository;

  setUp(() {
    ordersRepository = MockOrdersRepository();
  });

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      overrides: [
        ordersRepositoryProvider.overrideWithValue(ordersRepository),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  test('carrega GET /api/orders/{id}/delivery-code', () async {
    when(() => ordersRepository.getDeliveryCode('order-1')).thenAnswer(
      (_) async => DeliveryCode(
        code: '123456',
        expiresAtUtc: DateTime.utc(2026, 9, 8, 18),
      ),
    );

    final container = buildContainer();
    final code = await container.read(
      deliveryCodeControllerProvider('order-1').future,
    );

    expect(code.code, '123456');
    verify(() => ordersRepository.getDeliveryCode('order-1')).called(1);
  });

  test('propaga DELIVERY_CODE_EXPIRED sem inventar código', () async {
    when(() => ordersRepository.getDeliveryCode('order-1')).thenThrow(
      const AppException(
        code: 'DELIVERY_CODE_EXPIRED',
        message: 'O código de confirmação expirou.',
        statusCode: 409,
      ),
    );

    final container = buildContainer();
    final subscription = container.listen(
      deliveryCodeControllerProvider('order-1'),
      (previous, next) {},
      fireImmediately: true,
    );

    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);

    expect(subscription.read().hasError, isTrue);
    expect(
      (subscription.read().error as AppException).code,
      'DELIVERY_CODE_EXPIRED',
    );
  });
}
