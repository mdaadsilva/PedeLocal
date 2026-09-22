import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pedelocal_mobile/features/drivers/data/driver_repository.dart';

class MockDio extends Mock implements Dio {}

Map<String, dynamic> _driverJson() => {
  'id': 'driver-1',
  'userId': 'user-1',
  'name': 'Carlos Motoboy',
  'cityId': 'city-1',
  'cpf': '12345678900',
  'vehicleType': 'Motorcycle',
  'pixKeyType': 'Cpf',
  'pixKey': '12345678900',
  'onboardingStatus': 'UnderReview',
  'status': 'Offline',
  'eligibilityStatus': 'UnderReview',
  'reviewedAtUtc': null,
  'rejectionReason': null,
};

void main() {
  late MockDio dio;
  late DriverRepository repository;

  setUp(() {
    dio = MockDio();
    repository = DriverRepository(dio);
  });

  test('getMyDocuments faz GET /api/driver/me/documents e tolera license/vehicle nulos', () async {
    final response = Response<Map<String, dynamic>>(
      requestOptions: RequestOptions(path: '/api/driver/me/documents'),
      statusCode: 200,
      data: {'driver': _driverJson(), 'license': null, 'vehicle': null},
    );
    when(() => dio.get<Map<String, dynamic>>('/api/driver/me/documents'))
        .thenAnswer((_) async => response);

    final documents = await repository.getMyDocuments();

    expect(documents.hasSubmittedLicense, isFalse);
    expect(documents.driver.onboardingStatus, 'UnderReview');
  });

  test('submitLicense faz PUT com data no formato yyyy-MM-dd', () async {
    when(
      () => dio.put<Map<String, dynamic>>(
        '/api/driver/me/documents/license',
        data: any(named: 'data'),
      ),
    ).thenAnswer(
      (_) async => Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(
          path: '/api/driver/me/documents/license',
        ),
        statusCode: 200,
        data: {'driver': _driverJson(), 'license': null, 'vehicle': null},
      ),
    );

    await repository.submitLicense(
      registrationNumber: '12345678900',
      category: 'A',
      expiresAt: DateTime(2028, 5, 10),
      hasEar: true,
      documentImageUrl: '/uploads/cnh.jpg',
    );

    final captured = verify(
      () => dio.put<Map<String, dynamic>>(
        '/api/driver/me/documents/license',
        data: captureAny(named: 'data'),
      ),
    ).captured.single as Map<String, dynamic>;

    expect(captured['expiresAt'], '2028-05-10');
    expect(captured['registrationNumber'], '12345678900');
    expect(captured['category'], 'A');
    expect(captured['hasEar'], isTrue);
  });

  test('submitVehicle faz PUT com os campos do veículo', () async {
    when(
      () => dio.put<Map<String, dynamic>>(
        '/api/driver/me/documents/vehicle',
        data: any(named: 'data'),
      ),
    ).thenAnswer(
      (_) async => Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(
          path: '/api/driver/me/documents/vehicle',
        ),
        statusCode: 200,
        data: {'driver': _driverJson(), 'license': null, 'vehicle': null},
      ),
    );

    await repository.submitVehicle(
      type: 'Bike',
      plate: null,
      model: 'Caloi 10',
      color: 'Azul',
      documentImageUrl: null,
    );

    final captured = verify(
      () => dio.put<Map<String, dynamic>>(
        '/api/driver/me/documents/vehicle',
        data: captureAny(named: 'data'),
      ),
    ).captured.single as Map<String, dynamic>;

    expect(captured['type'], 'Bike');
    expect(captured['plate'], isNull);
    expect(captured['model'], 'Caloi 10');
  });

  test('addOccurrence faz POST /api/driver/deliveries/{id}/occurrences', () async {
    when(
      () => dio.post<Map<String, dynamic>>(
        '/api/driver/deliveries/delivery-1/occurrences',
        data: any(named: 'data'),
      ),
    ).thenAnswer(
      (_) async => Response<Map<String, dynamic>>(
        requestOptions: RequestOptions(
          path: '/api/driver/deliveries/delivery-1/occurrences',
        ),
        statusCode: 201,
        data: {
          'id': 'occurrence-1',
          'deliveryId': 'delivery-1',
          'driverId': 'driver-1',
          'type': 'CustomerNotFound',
          'note': 'Ninguém atendeu',
          'createdAtUtc': '2026-01-01T12:00:00Z',
        },
      ),
    );

    final occurrence = await repository.addOccurrence(
      'delivery-1',
      'CustomerNotFound',
      'Ninguém atendeu',
    );

    expect(occurrence.type, 'CustomerNotFound');
    expect(occurrence.note, 'Ninguém atendeu');
  });
}
