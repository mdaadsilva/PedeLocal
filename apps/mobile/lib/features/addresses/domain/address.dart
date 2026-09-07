import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Addresses.AddressResponse`
/// (`GET/POST/PUT /api/me/addresses` — `docs/FRONTEND_GUIDE.md#modelo-de-endereço`).
///
/// `type = Urban`: `street`/`number`/`district` obrigatórios, campos rurais
/// ficam `null`. `type = Rural`: `reference` obrigatório,
/// `propertyName`/`road`/`kilometer` opcionais, campos urbanos ficam `null`.
/// `latitude`/`longitude` são sempre obrigatórios para os dois tipos.
class Address extends Equatable {
  const Address({
    required this.id,
    required this.cityId,
    required this.type,
    this.label,
    this.street,
    this.number,
    this.district,
    this.complement,
    this.reference,
    this.zipCode,
    this.propertyName,
    this.road,
    this.kilometer,
    required this.latitude,
    required this.longitude,
    this.googlePlaceId,
  });

  final String id;
  final String cityId;

  /// `Urban | Rural`.
  final String type;
  final String? label;

  // Urbano.
  final String? street;
  final String? number;
  final String? district;
  final String? complement;
  final String? zipCode;

  // Ambos (rural exige, urbano opcional como complemento livre).
  final String? reference;

  // Rural.
  final String? propertyName;
  final String? road;
  final String? kilometer;

  final double latitude;
  final double longitude;
  final String? googlePlaceId;

  bool get isUrban => type == 'Urban';
  bool get isRural => type == 'Rural';

  /// Resumo de uma linha para exibição em listas/seletores.
  String get summary {
    if (isUrban) {
      final parts = [
        if (street != null && street!.isNotEmpty) street!,
        if (number != null && number!.isNotEmpty) number!,
        if (district != null && district!.isNotEmpty) district!,
      ];
      return parts.isEmpty ? (label ?? 'Endereço') : parts.join(', ');
    }
    final parts = [
      if (propertyName != null && propertyName!.isNotEmpty) propertyName!,
      if (road != null && road!.isNotEmpty) road!,
      if (kilometer != null && kilometer!.isNotEmpty) 'Km $kilometer',
    ];
    return parts.isEmpty ? (label ?? 'Endereço rural') : parts.join(', ');
  }

  factory Address.fromJson(Map<String, dynamic> json) {
    return Address(
      id: json['id'] as String,
      cityId: json['cityId'] as String,
      type: json['type'] as String,
      label: json['label'] as String?,
      street: json['street'] as String?,
      number: json['number'] as String?,
      district: json['district'] as String?,
      complement: json['complement'] as String?,
      reference: json['reference'] as String?,
      zipCode: json['zipCode'] as String?,
      propertyName: json['propertyName'] as String?,
      road: json['road'] as String?,
      kilometer: json['kilometer'] as String?,
      latitude: (json['latitude'] as num).toDouble(),
      longitude: (json['longitude'] as num).toDouble(),
      googlePlaceId: json['googlePlaceId'] as String?,
    );
  }

  @override
  List<Object?> get props => [
    id,
    cityId,
    type,
    label,
    street,
    number,
    district,
    complement,
    reference,
    zipCode,
    propertyName,
    road,
    kilometer,
    latitude,
    longitude,
    googlePlaceId,
  ];
}
