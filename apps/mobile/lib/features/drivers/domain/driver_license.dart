import 'package:equatable/equatable.dart';

/// Espelha `DriverLicenseResponse` — CNH do entregador
/// (`docs/DRIVER_ONBOARDING.md#cnh`). Nunca exibido a CUSTOMER/MERCHANT,
/// só ao próprio entregador (self-service) e a SUPER_ADMIN.
class DriverLicense extends Equatable {
  const DriverLicense({
    required this.registrationNumber,
    required this.category,
    required this.expiresAt,
    required this.hasEar,
    required this.documentImageUrl,
    required this.daysUntilExpiration,
  });

  final String registrationNumber;

  /// Categoria da CNH — `A`, `B`, `AB`, `C`, `D` ou `E`.
  final String category;
  final DateTime expiresAt;

  /// EAR (Exercício de Atividade Remunerada) — relevante para motos.
  /// `null` quando não se aplica.
  final bool? hasEar;
  final String? documentImageUrl;

  /// Negativo quando já vencida.
  final int daysUntilExpiration;

  bool get isExpired => daysUntilExpiration < 0;
  bool get isExpiringSoon => !isExpired && daysUntilExpiration <= 30;

  factory DriverLicense.fromJson(Map<String, dynamic> json) {
    return DriverLicense(
      registrationNumber: json['registrationNumber'] as String,
      category: json['category'] as String,
      expiresAt: DateTime.parse(json['expiresAt'] as String),
      hasEar: json['hasEar'] as bool?,
      documentImageUrl: json['documentImageUrl'] as String?,
      daysUntilExpiration: json['daysUntilExpiration'] as int,
    );
  }

  @override
  List<Object?> get props => [
    registrationNumber,
    category,
    expiresAt,
    hasEar,
    documentImageUrl,
    daysUntilExpiration,
  ];
}

/// Categorias de CNH válidas no cadastro (`docs/DRIVER_ONBOARDING.md`).
const driverLicenseCategories = ['A', 'B', 'AB', 'C', 'D', 'E'];
