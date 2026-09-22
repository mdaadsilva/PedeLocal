import 'package:equatable/equatable.dart';

import 'driver_license.dart';
import 'driver_profile.dart';
import 'driver_vehicle_document.dart';

/// Espelha `DriverDetailResponse` (`GET /api/driver/me/documents`) — perfil
/// mais o que já foi enviado de CNH/veículo (qualquer um pode ser `null`
/// antes do envio, `docs/DRIVER_ONBOARDING.md`).
class DriverDocuments extends Equatable {
  const DriverDocuments({
    required this.driver,
    required this.license,
    required this.vehicle,
  });

  final DriverProfile driver;
  final DriverLicense? license;
  final DriverVehicleDocument? vehicle;

  bool get hasSubmittedLicense => license != null;
  bool get hasSubmittedVehicle => vehicle != null;

  factory DriverDocuments.fromJson(Map<String, dynamic> json) {
    return DriverDocuments(
      driver: DriverProfile.fromJson(json['driver'] as Map<String, dynamic>),
      license: json['license'] != null
          ? DriverLicense.fromJson(json['license'] as Map<String, dynamic>)
          : null,
      vehicle: json['vehicle'] != null
          ? DriverVehicleDocument.fromJson(
              json['vehicle'] as Map<String, dynamic>,
            )
          : null,
    );
  }

  @override
  List<Object?> get props => [driver, license, vehicle];
}
