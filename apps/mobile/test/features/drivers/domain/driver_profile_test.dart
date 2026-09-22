import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/drivers/domain/driver_profile.dart';

DriverProfile _buildProfile({
  String onboardingStatus = 'Approved',
  String status = 'Available',
  String eligibilityStatus = 'Eligible',
  String? rejectionReason,
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
    reviewedAtUtc: null,
    rejectionReason: rejectionReason,
  );
}

void main() {
  test('fromJson mapeia onboardingStatus/eligibilityStatus (não approvalStatus)', () {
    final profile = DriverProfile.fromJson({
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
    });

    expect(profile.onboardingStatus, 'UnderReview');
    expect(profile.eligibilityStatus, 'UnderReview');
    expect(profile.isEligible, isFalse);
  });

  test('isEligible só é true quando eligibilityStatus == Eligible', () {
    expect(_buildProfile(eligibilityStatus: 'Eligible').isEligible, isTrue);
    for (final status in [
      'PendingDocuments',
      'UnderReview',
      'Rejected',
      'Blocked',
      'DocumentExpired',
    ]) {
      expect(_buildProfile(eligibilityStatus: status).isEligible, isFalse);
    }
  });

  test('isDocumentExpired reflete eligibilityStatus == DocumentExpired', () {
    expect(
      _buildProfile(eligibilityStatus: 'DocumentExpired').isDocumentExpired,
      isTrue,
    );
    expect(_buildProfile(eligibilityStatus: 'Eligible').isDocumentExpired, isFalse);
  });

  test('eligibilityMessage nunca mostra o enum técnico cru', () {
    for (final status in [
      'PendingDocuments',
      'UnderReview',
      'Rejected',
      'Blocked',
      'DocumentExpired',
    ]) {
      final profile = _buildProfile(eligibilityStatus: status);
      expect(profile.eligibilityMessage, isNot(equals(status)));
      expect(profile.eligibilityMessage, isNotEmpty);
    }
  });

  test('eligibilityMessage inclui o motivo da reprovação quando presente', () {
    final profile = _buildProfile(
      eligibilityStatus: 'Rejected',
      rejectionReason: 'CNH ilegível',
    );
    expect(profile.eligibilityMessage, contains('CNH ilegível'));
  });
}
