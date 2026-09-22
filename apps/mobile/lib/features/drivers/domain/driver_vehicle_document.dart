import 'package:equatable/equatable.dart';

/// Espelha `DriverVehicleResponse` (`docs/DRIVER_ONBOARDING.md#veículo`).
/// Renomeado para não colidir com o enum `VehicleType` de
/// `features/auth/domain/auth_models.dart`.
class DriverVehicleDocument extends Equatable {
  const DriverVehicleDocument({
    required this.type,
    required this.plate,
    required this.model,
    required this.color,
    required this.documentImageUrl,
  });

  /// `Bike | Motorcycle | Car`.
  final String type;

  /// Opcional — bicicletas comumente não têm placa no Brasil.
  final String? plate;
  final String model;
  final String color;
  final String? documentImageUrl;

  factory DriverVehicleDocument.fromJson(Map<String, dynamic> json) {
    return DriverVehicleDocument(
      type: json['type'] as String,
      plate: json['plate'] as String?,
      model: json['model'] as String,
      color: json['color'] as String,
      documentImageUrl: json['documentImageUrl'] as String?,
    );
  }

  @override
  List<Object?> get props => [type, plate, model, color, documentImageUrl];
}
