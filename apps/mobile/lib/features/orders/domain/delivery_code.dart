import 'package:equatable/equatable.dart';

/// Espelha `DeliveryCodeResponse` (`GET /api/orders/{id}/delivery-code`).
/// Só o cliente dono do pedido. Nunca persistir em disco.
class DeliveryCode extends Equatable {
  const DeliveryCode({required this.code, required this.expiresAtUtc});

  final String code;
  final DateTime expiresAtUtc;

  factory DeliveryCode.fromJson(Map<String, dynamic> json) {
    return DeliveryCode(
      code: json['code'] as String,
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
    );
  }

  @override
  List<Object?> get props => [code, expiresAtUtc];

  @override
  String toString() => 'DeliveryCode(expiresAtUtc: $expiresAtUtc)';
}
