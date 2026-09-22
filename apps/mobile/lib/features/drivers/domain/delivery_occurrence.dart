import 'package:equatable/equatable.dart';

/// Espelha `DeliveryOccurrenceResponse`
/// (`docs/DELIVERY_FLOW.md#ocorrências-na-entrega`). Registro imutável —
/// sem edição/exclusão, é histórico do que foi relatado no momento.
class DeliveryOccurrence extends Equatable {
  const DeliveryOccurrence({
    required this.id,
    required this.deliveryId,
    required this.driverId,
    required this.type,
    required this.note,
    required this.createdAtUtc,
  });

  final String id;
  final String deliveryId;
  final String driverId;

  /// `CustomerNotFound | CustomerNotResponding | WrongAddress |
  /// PaymentProblem | VehicleProblem | Other`.
  final String type;
  final String? note;
  final DateTime createdAtUtc;

  factory DeliveryOccurrence.fromJson(Map<String, dynamic> json) {
    return DeliveryOccurrence(
      id: json['id'] as String,
      deliveryId: json['deliveryId'] as String,
      driverId: json['driverId'] as String,
      type: json['type'] as String,
      note: json['note'] as String?,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }

  @override
  List<Object?> get props => [id, deliveryId, driverId, type, note, createdAtUtc];
}

/// `type` válidos e rótulo amigável — nunca mostrar o enum técnico cru
/// (`docs/DELIVERY_FLOW.md`).
const occurrenceTypeLabels = {
  'CustomerNotFound': 'Cliente não encontrado',
  'CustomerNotResponding': 'Cliente não responde',
  'WrongAddress': 'Endereço incorreto',
  'PaymentProblem': 'Problema de pagamento',
  'VehicleProblem': 'Problema no veículo',
  'Other': 'Outro',
};
