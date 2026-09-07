import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/core/network/app_exception.dart';
import 'package:pedelocal_mobile/features/cities/data/cities_repository.dart';

class MockDio extends Mock implements Dio {}

void main() {
  late MockDio dio;
  late CitiesRepository repository;

  setUp(() {
    dio = MockDio();
    repository = CitiesRepository(dio);
  });

  test('getActiveCities faz GET /api/cities e mapeia a lista', () async {
    final response = Response<List<dynamic>>(
      requestOptions: RequestOptions(path: '/api/cities'),
      statusCode: 200,
      data: [
        {'id': 'city-1', 'name': 'Guaimbê', 'state': 'SP', 'isActive': true},
        {
          'id': 'city-2',
          'name': 'Júlio Mesquita',
          'state': 'SP',
          'isActive': true,
        },
        {'id': 'city-3', 'name': 'Getulina', 'state': 'SP', 'isActive': true},
      ],
    );

    when(() => dio.get<List<dynamic>>('/api/cities'))
        .thenAnswer((_) async => response);

    final cities = await repository.getActiveCities();

    expect(cities, hasLength(3));
    expect(
      cities.map((c) => c.name),
      containsAll(['Guaimbê', 'Júlio Mesquita', 'Getulina']),
    );
    expect(cities.every((c) => c.isActive), isTrue);
  });

  test('propaga AppException amigável em falha de rede', () async {
    when(() => dio.get<List<dynamic>>('/api/cities')).thenThrow(
      DioException(
        requestOptions: RequestOptions(path: '/api/cities'),
        type: DioExceptionType.connectionError,
      ),
    );

    await expectLater(
      () => repository.getActiveCities(),
      throwsA(
        isA<AppException>().having(
          (e) => e.code,
          'code',
          AppException.codeNoConnection,
        ),
      ),
    );
  });
}
