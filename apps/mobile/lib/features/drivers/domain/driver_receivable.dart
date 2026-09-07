import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Finance.DriverReceivableResponse`
/// (`docs/API_CONTRACT.md`, `docs/DELIVERY_FLOW.md#financeiro-do-
/// entregador`). Cada recebível é criado automaticamente quando uma entrega
/// é confirmada (`delivery = COMPLETED`) — na ausência de um endpoint
/// dedicado de "histórico de entregas", esta é a única fonte real de dados
/// sobre entregas concluídas pelo entregador.
class DriverReceivable extends Equatable {
  const DriverReceivable({
    required this.id,
    required this.deliveryId,
    required this.driverId,
    required this.establishmentId,
    required this.amount,
    required this.status,
    required this.paymentBatchId,
    required this.createdAtUtc,
  });

  final String id;
  final String deliveryId;
  final String driverId;
  final String establishmentId;
  final double amount;

  /// `Pending | PaymentReported | Received` (acompanha o lote de pagamento).
  final String status;
  final String? paymentBatchId;
  final DateTime createdAtUtc;

  factory DriverReceivable.fromJson(Map<String, dynamic> json) {
    return DriverReceivable(
      id: json['id'] as String,
      deliveryId: json['deliveryId'] as String,
      driverId: json['driverId'] as String,
      establishmentId: json['establishmentId'] as String,
      amount: (json['amount'] as num).toDouble(),
      status: json['status'] as String,
      paymentBatchId: json['paymentBatchId'] as String?,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }

  @override
  List<Object?> get props => [
    id,
    deliveryId,
    driverId,
    establishmentId,
    amount,
    status,
    paymentBatchId,
    createdAtUtc,
  ];
}
