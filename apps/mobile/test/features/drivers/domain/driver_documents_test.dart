import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/drivers/domain/driver_documents.dart';

Map<String, dynamic> _driverJson() => {
  'id': 'driver-1',
  'userId': 'user-1',
  'name': 'Carlos Motoboy',
  'cityId': 'city-1',
  'cpf': '12345678900',
  'vehicleType': 'Motorcycle',
  'pixKeyType': 'Cpf',
  'pixKey': '12345678900',
  'onboardingStatus': 'PendingDocuments',
  'status': 'Offline',
  'eligibilityStatus': 'PendingDocuments',
  'reviewedAtUtc': null,
  'rejectionReason': null,
};

void main() {
  test('fromJson tolera license e vehicle nulos (antes do envio)', () {
    final documents = DriverDocuments.fromJson({
      'driver': _driverJson(),
      'license': null,
      'vehicle': null,
    });

    expect(documents.hasSubmittedLicense, isFalse);
    expect(documents.hasSubmittedVehicle, isFalse);
    expect(documents.driver.onboardingStatus, 'PendingDocuments');
  });

  test('fromJson mapeia CNH e veículo já enviados', () {
    final documents = DriverDocuments.fromJson({
      'driver': _driverJson(),
      'license': {
        'registrationNumber': '12345678900',
        'category': 'A',
        'expiresAt': '2028-05-10',
        'hasEar': true,
        'documentImageUrl': '/uploads/cnh.jpg',
        'daysUntilExpiration': 500,
      },
      'vehicle': {
        'type': 'Motorcycle',
        'plate': 'ABC1D23',
        'model': 'Honda CG 160',
        'color': 'Preto',
        'documentImageUrl': null,
      },
    });

    expect(documents.hasSubmittedLicense, isTrue);
    expect(documents.license!.category, 'A');
    expect(documents.license!.isExpired, isFalse);
    expect(documents.hasSubmittedVehicle, isTrue);
    expect(documents.vehicle!.plate, 'ABC1D23');
  });

  test('DriverLicense.isExpired é true quando daysUntilExpiration é negativo', () {
    final documents = DriverDocuments.fromJson({
      'driver': _driverJson(),
      'license': {
        'registrationNumber': '12345678900',
        'category': 'A',
        'expiresAt': '2020-01-01',
        'hasEar': null,
        'documentImageUrl': null,
        'daysUntilExpiration': -10,
      },
      'vehicle': null,
    });

    expect(documents.license!.isExpired, isTrue);
  });
}
