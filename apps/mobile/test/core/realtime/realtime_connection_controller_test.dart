import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/config/app_environment.dart';
import 'package:pedelocal_mobile/core/notifications/push_notification_service.dart';
import 'package:pedelocal_mobile/core/realtime/realtime_connection_controller.dart';
import 'package:pedelocal_mobile/core/realtime/signalr_client.dart';
import 'package:pedelocal_mobile/core/storage/secure_storage_service.dart';

import '../notifications/fake_push_token_lifecycle.dart';
import 'package:pedelocal_mobile/features/auth/data/auth_repository.dart';
import 'package:pedelocal_mobile/features/me/data/me_repository.dart';
import 'package:pedelocal_mobile/features/me/domain/my_profile.dart';
import 'package:pedelocal_mobile/features/session/application/session_controller.dart';

class MockSecureStorageService extends Mock implements SecureStorageService {}

class MockMeRepository extends Mock implements MeRepository {}

class MockAuthRepository extends Mock implements AuthRepository {}

class FakeSignalRClient extends SignalRClient {
  FakeSignalRClient()
    : super(
        environment: const AppEnvironment(baseUrl: 'http://test'),
        secureStorage: _UnusedStorage(),
      );

  int connectCalls = 0;
  int disconnectCalls = 0;

  @override
  Future<void> connect() async {
    connectCalls++;
  }

  @override
  Future<void> disconnect() async {
    disconnectCalls++;
  }
}

class _UnusedStorage extends Fake implements SecureStorageService {}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late MockSecureStorageService secureStorage;
  late MockMeRepository meRepository;
  late MockAuthRepository authRepository;
  late FakeSignalRClient signalR;

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      overrides: [
        secureStorageServiceProvider.overrideWithValue(secureStorage),
        meRepositoryProvider.overrideWithValue(meRepository),
        authRepositoryProvider.overrideWithValue(authRepository),
        signalRClientProvider.overrideWithValue(signalR),
        pushTokenLifecycleProvider.overrideWithValue(FakePushTokenLifecycle()),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  setUp(() {
    secureStorage = MockSecureStorageService();
    meRepository = MockMeRepository();
    authRepository = MockAuthRepository();
    signalR = FakeSignalRClient();
  });

  test('conecta após sessão autenticada e desconecta no logout', () async {
    when(() => secureStorage.hasSession()).thenAnswer((_) async => true);
    when(() => meRepository.getProfile()).thenAnswer(
      (_) async => MyProfile(
        id: 'user-1',
        name: 'Maria',
        email: 'maria@example.com',
        phoneNumber: '14999990000',
        roles: const ['CUSTOMER'],
        createdAtUtc: DateTime.utc(2026, 1, 1),
      ),
    );
    when(() => authRepository.logout()).thenAnswer((_) async {});

    final container = buildContainer();
    container.listen(realtimeConnectionControllerProvider, (_, _) {});
    await container.read(sessionControllerProvider.future);
    await Future<void>.delayed(Duration.zero);

    expect(signalR.connectCalls, greaterThan(0));

    await container.read(sessionControllerProvider.notifier).logout();
    await Future<void>.delayed(Duration.zero);

    expect(signalR.disconnectCalls, greaterThan(0));
  });

  test('sem sessão não tenta conectar', () async {
    when(() => secureStorage.hasSession()).thenAnswer((_) async => false);
    final container = buildContainer();
    container.listen(realtimeConnectionControllerProvider, (_, _) {});
    await container.read(sessionControllerProvider.future);
    await Future<void>.delayed(Duration.zero);

    expect(signalR.connectCalls, 0);
    expect(signalR.disconnectCalls, greaterThan(0));
  });
}
