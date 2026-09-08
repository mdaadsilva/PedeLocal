import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/features/me/data/me_repository.dart';

class MockDio extends Mock implements Dio {}

void main() {
  late MockDio dio;
  late MeRepository repository;

  setUp(() {
    dio = MockDio();
    repository = MeRepository(dio);
  });

  test('registerDeviceToken faz PUT /api/me/device-tokens sem userId', () async {
    when(
      () => dio.put<void>(
        '/api/me/device-tokens',
        data: any(named: 'data'),
      ),
    ).thenAnswer(
      (_) async => Response<void>(
        requestOptions: RequestOptions(path: '/api/me/device-tokens'),
        statusCode: 204,
      ),
    );

    await repository.registerDeviceToken(
      token: 'fcm-token',
      platform: 'Android',
    );

    final captured = verify(
      () => dio.put<void>(
        '/api/me/device-tokens',
        data: captureAny(named: 'data'),
      ),
    ).captured.single as Map<String, dynamic>;

    expect(captured, {'token': 'fcm-token', 'platform': 'Android'});
    expect(captured.containsKey('userId'), isFalse);
  });

  test('removeDeviceToken faz DELETE /api/me/device-tokens com o token', () async {
    when(
      () => dio.delete<void>(
        '/api/me/device-tokens',
        data: any(named: 'data'),
      ),
    ).thenAnswer(
      (_) async => Response<void>(
        requestOptions: RequestOptions(path: '/api/me/device-tokens'),
        statusCode: 204,
      ),
    );

    await repository.removeDeviceToken('fcm-token');

    final captured = verify(
      () => dio.delete<void>(
        '/api/me/device-tokens',
        data: captureAny(named: 'data'),
      ),
    ).captured.single as Map<String, dynamic>;

    expect(captured, {'token': 'fcm-token'});
  });
}
