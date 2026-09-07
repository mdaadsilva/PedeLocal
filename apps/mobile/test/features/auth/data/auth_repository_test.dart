import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/core/storage/secure_storage_service.dart';
import 'package:pedelocal_mobile/features/auth/data/auth_repository.dart';

class MockDio extends Mock implements Dio {}

class MockSecureStorageService extends Mock implements SecureStorageService {}

void main() {
  late MockDio dio;
  late MockSecureStorageService secureStorage;
  late AuthRepository repository;

  setUp(() {
    dio = MockDio();
    secureStorage = MockSecureStorageService();
    repository = AuthRepository(dio, secureStorage);
  });

  group('login', () {
    test(
      'faz POST /api/auth/login e salva os tokens no secure storage',
      () async {
        final response = Response<Map<String, dynamic>>(
          requestOptions: RequestOptions(path: '/api/auth/login'),
          statusCode: 200,
          data: {
            'accessToken': 'access-token',
            'refreshToken': 'refresh-token',
            'accessTokenExpiresAtUtc': '2026-09-07T15:30:00Z',
          },
        );

        when(
          () => dio.post<Map<String, dynamic>>(
            '/api/auth/login',
            data: any(named: 'data'),
          ),
        ).thenAnswer((_) async => response);
        when(
          () => secureStorage.saveTokens(
            accessToken: any(named: 'accessToken'),
            refreshToken: any(named: 'refreshToken'),
            accessTokenExpiresAtUtc: any(named: 'accessTokenExpiresAtUtc'),
          ),
        ).thenAnswer((_) async {});

        final tokens = await repository.login(
          email: 'maria@example.com',
          password: 'Senha123',
        );

        expect(tokens.accessToken, 'access-token');
        expect(tokens.refreshToken, 'refresh-token');
        verify(
          () => secureStorage.saveTokens(
            accessToken: 'access-token',
            refreshToken: 'refresh-token',
            accessTokenExpiresAtUtc: any(named: 'accessTokenExpiresAtUtc'),
          ),
        ).called(1);
      },
    );

    test(
      'propaga AppException amigável em credenciais inválidas (401)',
      () async {
        final requestOptions = RequestOptions(path: '/api/auth/login');
        final response = Response<Map<String, dynamic>>(
          requestOptions: requestOptions,
          statusCode: 401,
          data: {
            'code': 'INVALID_CREDENTIALS',
            'message': 'E-mail ou senha incorretos.',
            'traceId': '0HN123',
          },
        );

        when(
          () => dio.post<Map<String, dynamic>>(
            '/api/auth/login',
            data: any(named: 'data'),
          ),
        ).thenThrow(
          DioException(
            requestOptions: requestOptions,
            response: response,
            type: DioExceptionType.badResponse,
          ),
        );

        await expectLater(
          () =>
              repository.login(email: 'maria@example.com', password: 'errada'),
          throwsA(
            isA<AppException>().having(
              (e) => e.code,
              'code',
              'INVALID_CREDENTIALS',
            ),
          ),
        );
      },
    );
  });

  group('logout', () {
    test(
      'sempre limpa os tokens locais, mesmo se a chamada de rede falhar',
      () async {
        when(() => secureStorage.readRefreshToken())
            .thenAnswer((_) async => 'refresh-token');
        when(() => dio.post<void>('/api/auth/logout', data: any(named: 'data')))
            .thenThrow(
              DioException(
                requestOptions: RequestOptions(path: '/api/auth/logout'),
                type: DioExceptionType.connectionError,
              ),
            );
        when(() => secureStorage.clearTokens()).thenAnswer((_) async {});

        await repository.logout();

        verify(() => secureStorage.clearTokens()).called(1);
      },
    );
  });
}
