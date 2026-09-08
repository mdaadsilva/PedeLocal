import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/notifications/push_notification_service.dart';
import 'package:pedelocal_mobile/core/storage/preferences_service.dart';
import 'package:pedelocal_mobile/core/storage/secure_storage_service.dart';
import 'package:pedelocal_mobile/features/me/data/me_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'fake_push_messaging_gateway.dart';

class MockMeRepository extends Mock implements MeRepository {}

class MockSecureStorageService extends Mock implements SecureStorageService {}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late FakePushMessagingGateway gateway;
  late MockMeRepository meRepository;
  late MockSecureStorageService storage;
  late PreferencesService preferences;
  late PushNotificationService service;

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    preferences = PreferencesService(prefs);
    gateway = FakePushMessagingGateway();
    meRepository = MockMeRepository();
    storage = MockSecureStorageService();

    when(
      () => meRepository.registerDeviceToken(
        token: any(named: 'token'),
        platform: any(named: 'platform'),
      ),
    ).thenAnswer((_) async {});
    when(
      () => meRepository.removeDeviceToken(any()),
    ).thenAnswer((_) async {});
    when(() => storage.saveFcmToken(any())).thenAnswer((_) async {});
    when(() => storage.readFcmToken()).thenAnswer((_) async => null);
    when(() => storage.clearFcmToken()).thenAnswer((_) async {});

    service = PushNotificationService(
      gateway: gateway,
      meRepository: meRepository,
      storage: storage,
      preferences: preferences,
      platformResolver: () => 'Android',
    );
  });

  tearDown(() async {
    await gateway.dispose();
  });

  test('após login registra o token FCM no endpoint real', () async {
    await service.startAfterAuthentication();

    verify(
      () => meRepository.registerDeviceToken(
        token: 'fcm-token',
        platform: 'Android',
      ),
    ).called(1);
    verify(() => storage.saveFcmToken('fcm-token')).called(1);
    expect(gateway.permissionRequests, 1);
    expect(preferences.notificationPermissionAsked, isTrue);
  });

  test('não registra de novo o mesmo token na mesma sessão', () async {
    await service.startAfterAuthentication();
    await service.startAfterAuthentication();

    verify(
      () => meRepository.registerDeviceToken(
        token: any(named: 'token'),
        platform: any(named: 'platform'),
      ),
    ).called(1);
    expect(gateway.permissionRequests, 1);
  });

  test('refresh de token registra o novo valor', () async {
    await service.startAfterAuthentication();

    gateway.tokenRefreshController.add('fcm-rotated');
    await Future<void>.delayed(Duration.zero);

    verify(
      () => meRepository.registerDeviceToken(
        token: 'fcm-rotated',
        platform: 'Android',
      ),
    ).called(1);
  });

  test('logout remove o token no backend e limpa o storage local', () async {
    await service.startAfterAuthentication();
    await service.unregisterCurrentToken();

    verify(() => meRepository.removeDeviceToken('fcm-token')).called(1);
    verify(() => storage.clearFcmToken()).called(1);
  });

  test('logout sem token local não chama o backend', () async {
    await service.unregisterCurrentToken();

    verifyNever(() => meRepository.removeDeviceToken(any()));
    verify(() => storage.clearFcmToken()).called(1);
  });

  test('falha ao registrar token não lança', () async {
    when(
      () => meRepository.registerDeviceToken(
        token: any(named: 'token'),
        platform: any(named: 'platform'),
      ),
    ).thenThrow(Exception('network'));

    await expectLater(service.startAfterAuthentication(), completes);
  });

  test('sem Firebase não chama a API e não quebra o fluxo', () async {
    gateway.available = false;

    await service.startAfterAuthentication();

    verifyNever(
      () => meRepository.registerDeviceToken(
        token: any(named: 'token'),
        platform: any(named: 'platform'),
      ),
    );
    expect(gateway.permissionRequests, 0);
  });
}
