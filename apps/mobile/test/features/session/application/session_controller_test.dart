import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/core/storage/secure_storage_service.dart';
import 'package:pedelocal_mobile/features/auth/data/auth_repository.dart';
import 'package:pedelocal_mobile/features/me/data/me_repository.dart';
import 'package:pedelocal_mobile/features/me/domain/my_profile.dart';
import 'package:pedelocal_mobile/features/session/application/session_controller.dart';
import 'package:pedelocal_mobile/features/session/domain/session_state.dart';
import 'package:pedelocal_mobile/features/session/domain/user_role.dart';

class MockSecureStorageService extends Mock implements SecureStorageService {}

class MockMeRepository extends Mock implements MeRepository {}

class MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late MockSecureStorageService secureStorage;
  late MockMeRepository meRepository;
  late MockAuthRepository authRepository;

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      overrides: [
        secureStorageServiceProvider.overrideWithValue(secureStorage),
        meRepositoryProvider.overrideWithValue(meRepository),
        authRepositoryProvider.overrideWithValue(authRepository),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  setUp(() {
    secureStorage = MockSecureStorageService();
    meRepository = MockMeRepository();
    authRepository = MockAuthRepository();
  });

  test(
    'sem sessão salva resolve como não autenticado, sem chamar o backend',
    () async {
      when(() => secureStorage.hasSession()).thenAnswer((_) async => false);
      final container = buildContainer();

      final state = await container.read(sessionControllerProvider.future);

      expect(state, const SessionState.unauthenticated());
      verifyNever(() => meRepository.getProfile());
    },
  );

  test(
    'com sessão salva e perfil válido, resolve autenticado com a role correta',
    () async {
      when(() => secureStorage.hasSession()).thenAnswer((_) async => true);
      when(() => meRepository.getProfile()).thenAnswer(
        (_) async => MyProfile(
          id: 'user-1',
          name: 'Maria Silva',
          email: 'maria@example.com',
          phoneNumber: '14999990000',
          roles: const ['CUSTOMER'],
          createdAtUtc: DateTime.utc(2026, 1, 1),
        ),
      );
      final container = buildContainer();

      final state = await container.read(sessionControllerProvider.future);

      expect(state.isAuthenticated, isTrue);
      expect(state.role, UserRole.customer);
      expect(state.userName, 'Maria Silva');
    },
  );

  test('quando o perfil falha mesmo com sessão salva, limpa tokens e vira não autenticado', () async {
    when(() => secureStorage.hasSession()).thenAnswer((_) async => true);
    when(() => meRepository.getProfile()).thenThrow(
      const AppException(
        code: 'UNAUTHENTICATED',
        message: 'Sessão expirada.',
        statusCode: 401,
      ),
    );
    when(() => secureStorage.clearTokens()).thenAnswer((_) async {});
    final container = buildContainer();

    final state = await container.read(sessionControllerProvider.future);

    expect(state, const SessionState.unauthenticated());
    verify(() => secureStorage.clearTokens()).called(1);
  });

  test('logout chama o AuthRepository e limpa o estado local', () async {
    when(() => secureStorage.hasSession()).thenAnswer((_) async => false);
    when(() => authRepository.logout()).thenAnswer((_) async {});
    final container = buildContainer();

    await container.read(sessionControllerProvider.future);
    await container.read(sessionControllerProvider.notifier).logout();

    final state = container.read(sessionControllerProvider).value;
    expect(state, const SessionState.unauthenticated());
    verify(() => authRepository.logout()).called(1);
  });
}
