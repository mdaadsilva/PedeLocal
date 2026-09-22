import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/core/realtime/delivery_accepted_events.dart';
import 'package:pedelocal_mobile/core/realtime/delivery_events.dart';
import 'package:pedelocal_mobile/features/drivers/application/driver_home_controller.dart';
import 'package:pedelocal_mobile/features/drivers/data/driver_repository.dart';
import 'package:pedelocal_mobile/features/drivers/domain/delivery.dart';
import 'package:pedelocal_mobile/features/drivers/domain/driver_profile.dart';

class MockDriverRepository extends Mock implements DriverRepository {}

/// Fluxo do entregador (`docs/DELIVERY_FLOW.md`): aceitar entrega, corrida
/// de aceite (409 `DELIVERY_ALREADY_TAKEN`), retirar/sair para entrega/
/// confirmar com código, e a regra de "uma entrega ativa por vez" (`BUSY`).
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late MockDriverRepository driverRepository;

  DriverProfile buildProfile({
    String onboardingStatus = 'Approved',
    String status = 'Available',
    String eligibilityStatus = 'Eligible',
  }) {
    return DriverProfile(
      id: 'driver-1',
      userId: 'user-1',
      name: 'Carlos Motoboy',
      cityId: 'city-1',
      cpf: '12345678900',
      vehicleType: 'Motorcycle',
      pixKeyType: 'Cpf',
      pixKey: '12345678900',
      onboardingStatus: onboardingStatus,
      status: status,
      eligibilityStatus: eligibilityStatus,
      reviewedAtUtc: DateTime.utc(2026, 1, 1),
      rejectionReason: null,
    );
  }

  Delivery buildDelivery({
    String id = 'delivery-1',
    String status = 'Available',
  }) {
    return Delivery(
      id: id,
      orderId: 'order-1',
      establishmentId: 'est-1',
      cityId: 'city-1',
      driverId: null,
      status: status,
      assignedAtUtc: null,
      pickedUpAtUtc: null,
      outForDeliveryAtUtc: null,
      completedAtUtc: null,
    );
  }

  late StreamController<DeliveryAvailableEvent> deliveryAvailableController;
  late StreamController<DeliveryAcceptedEvent> deliveryAcceptedController;

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      overrides: [
        driverRepositoryProvider.overrideWithValue(driverRepository),
        deliveryAvailableStreamProvider.overrideWith(
          (ref) => deliveryAvailableController.stream,
        ),
        deliveryAcceptedStreamProvider.overrideWith(
          (ref) => deliveryAcceptedController.stream,
        ),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  setUp(() {
    driverRepository = MockDriverRepository();
    deliveryAvailableController =
        StreamController<DeliveryAvailableEvent>.broadcast();
    deliveryAcceptedController =
        StreamController<DeliveryAcceptedEvent>.broadcast();
  });

  tearDown(() {
    deliveryAvailableController.close();
    deliveryAcceptedController.close();
  });

  test('perfil Available carrega a lista de entregas disponíveis (nunca a entrega atual)', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Available'));
    when(() => driverRepository.getAvailableDeliveries())
        .thenAnswer((_) async => [buildDelivery()]);
    final container = buildContainer();

    final state = await container.read(driverHomeControllerProvider.future);

    expect(state.availableDeliveries, hasLength(1));
    expect(state.currentDelivery, isNull);
    verifyNever(() => driverRepository.getCurrentDelivery());
  });

  test('enquanto BUSY, carrega apenas a entrega atual — nunca a lista de disponíveis', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'Assigned'));
    final container = buildContainer();

    final state = await container.read(driverHomeControllerProvider.future);

    expect(state.currentDelivery?.status, 'Assigned');
    expect(state.availableDeliveries, isEmpty);
    verifyNever(() => driverRepository.getAvailableDeliveries());
  });

  test('aceitar entrega com sucesso recarrega o perfil (agora BUSY) e a entrega atual', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Available'));
    when(() => driverRepository.getAvailableDeliveries())
        .thenAnswer((_) async => [buildDelivery()]);
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.acceptDelivery('delivery-1'))
        .thenAnswer((_) async => buildDelivery(status: 'Assigned'));
    // Após aceitar, o backend já reflete o entregador como BUSY.
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'Assigned'));

    await container
        .read(driverHomeControllerProvider.notifier)
        .acceptDelivery('delivery-1');

    final state = container.read(driverHomeControllerProvider).value!;
    expect(state.currentDelivery?.status, 'Assigned');
    expect(state.profile.status, 'Busy');
    expect(state.isProcessingAction, isFalse);
  });

  test('409 DELIVERY_ALREADY_TAKEN remove a entrega da lista local sem erro alarmante', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Available'));
    when(() => driverRepository.getAvailableDeliveries()).thenAnswer(
      (_) async => [
        buildDelivery(id: 'delivery-1'),
        buildDelivery(id: 'delivery-2'),
      ],
    );
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.acceptDelivery('delivery-1')).thenThrow(
      const AppException(
        code: 'DELIVERY_ALREADY_TAKEN',
        message: 'Essa entrega já foi aceita por outro entregador.',
        statusCode: 409,
      ),
    );

    await expectLater(
      () => container
          .read(driverHomeControllerProvider.notifier)
          .acceptDelivery('delivery-1'),
      throwsA(
        isA<AppException>().having(
          (e) => e.code,
          'code',
          'DELIVERY_ALREADY_TAKEN',
        ),
      ),
    );

    final state = container.read(driverHomeControllerProvider).value!;
    expect(state.availableDeliveries.map((d) => d.id), ['delivery-2']);
    expect(state.isProcessingAction, isFalse);
    // Entregador que perdeu a corrida continua Available, não é chamado
    // um novo carregamento de perfil.
    expect(state.profile.status, 'Available');
  });

  test('marcar retirada avança a entrega atual para PickedUp', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'Assigned'));
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.markPickedUp('delivery-1'))
        .thenAnswer((_) async => buildDelivery(status: 'PickedUp'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'PickedUp'));

    await container
        .read(driverHomeControllerProvider.notifier)
        .markPickedUp('delivery-1');

    expect(
      container
          .read(driverHomeControllerProvider)
          .value!
          .currentDelivery
          ?.status,
      'PickedUp',
    );
  });

  test('confirmar entrega com código correto conclui e libera o entregador (Available)', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'OutForDelivery'));
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.confirmDelivery('delivery-1', '123456'))
        .thenAnswer((_) async => buildDelivery(status: 'Completed'));
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Available'));
    when(() => driverRepository.getAvailableDeliveries())
        .thenAnswer((_) async => const []);

    await container
        .read(driverHomeControllerProvider.notifier)
        .confirmDelivery('delivery-1', '123456');

    final state = container.read(driverHomeControllerProvider).value!;
    expect(state.profile.status, 'Available');
    expect(state.currentDelivery, isNull);
  });

  test('confirmar entrega com código incorreto propaga erro amigável e mantém a entrega atual', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'OutForDelivery'));
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.confirmDelivery('delivery-1', '000000'))
        .thenThrow(
          const AppException(
            code: 'INVALID_DELIVERY_CODE',
            message: 'Código de confirmação incorreto.',
            statusCode: 400,
          ),
        );

    await expectLater(
      () => container
          .read(driverHomeControllerProvider.notifier)
          .confirmDelivery('delivery-1', '000000'),
      throwsA(
        isA<AppException>().having(
          (e) => e.code,
          'code',
          'INVALID_DELIVERY_CODE',
        ),
      ),
    );

    final state = container.read(driverHomeControllerProvider).value!;
    expect(state.currentDelivery?.status, 'OutForDelivery');
    expect(state.isProcessingAction, isFalse);
  });

  test('ficar offline enquanto BUSY propaga o erro do backend sem alterar o status local', () async {
    when(() => driverRepository.getMyProfile())
        .thenAnswer((_) async => buildProfile(status: 'Busy'));
    when(() => driverRepository.getCurrentDelivery())
        .thenAnswer((_) async => buildDelivery(status: 'Assigned'));
    final container = buildContainer();
    await container.read(driverHomeControllerProvider.future);

    when(() => driverRepository.goOffline()).thenThrow(
      const AppException(
        code: 'DRIVER_ON_DELIVERY',
        message: 'Você não pode ficar offline com uma entrega em andamento.',
        statusCode: 409,
      ),
    );

    await expectLater(
      () => container.read(driverHomeControllerProvider.notifier).goOffline(),
      throwsA(isA<AppException>()),
    );

    expect(
      container.read(driverHomeControllerProvider).value!.profile.status,
      'Busy',
    );
  });

  test(
    'ao receber DeliveryAvailable via SignalR enquanto Available, atualiza a '
    'lista de entregas disponíveis sem polling',
    () async {
      var callCount = 0;
      when(() => driverRepository.getMyProfile())
          .thenAnswer((_) async => buildProfile(status: 'Available'));
      when(() => driverRepository.getAvailableDeliveries())
          .thenAnswer((_) async {
            callCount++;
            return callCount == 1
                ? [buildDelivery(id: 'delivery-1')]
                : [
                    buildDelivery(id: 'delivery-1'),
                    buildDelivery(id: 'delivery-2'),
                  ];
          });
      final container = buildContainer();
      container.listen(
        driverHomeControllerProvider,
        (previous, next) {},
        fireImmediately: true,
      );
      await container.read(driverHomeControllerProvider.future);
      expect(callCount, 1);

      deliveryAvailableController.add(
        const DeliveryAvailableEvent(deliveryId: 'delivery-2'),
      );
      await Future<void>.delayed(Duration.zero);
      await Future<void>.delayed(Duration.zero);

      expect(callCount, 2);
      expect(
        container.read(driverHomeControllerProvider).value!.availableDeliveries,
        hasLength(2),
      );
    },
  );

  test(
    'DeliveryAccepted remove a entrega da lista imediatamente, sem esperar REST',
    () async {
      when(() => driverRepository.getMyProfile())
          .thenAnswer((_) async => buildProfile(status: 'Available'));
      when(() => driverRepository.getAvailableDeliveries()).thenAnswer(
        (_) async => [
          buildDelivery(id: 'delivery-1'),
          buildDelivery(id: 'delivery-2'),
        ],
      );
      final container = buildContainer();
      container.listen(driverHomeControllerProvider, (previous, next) {});
      await container.read(driverHomeControllerProvider.future);

      deliveryAcceptedController.add(
        const DeliveryAcceptedEvent(deliveryId: 'delivery-1'),
      );
      await Future<void>.delayed(Duration.zero);

      expect(
        container
            .read(driverHomeControllerProvider)
            .value!
            .availableDeliveries
            .map((delivery) => delivery.id),
        ['delivery-2'],
      );
      verify(() => driverRepository.getAvailableDeliveries()).called(1);
    },
  );
}
