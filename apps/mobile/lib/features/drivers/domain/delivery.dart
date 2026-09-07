import 'package:equatable/equatable.dart';

/// Espelha `PedeLocal.Contracts.Deliveries.DeliveryResponse`
/// (`docs/DELIVERY_FLOW.md`).
class Delivery extends Equatable {
  const Delivery({
    required this.id,
    required this.orderId,
    required this.establishmentId,
    required this.cityId,
    required this.driverId,
    required this.status,
    required this.assignedAtUtc,
    required this.pickedUpAtUtc,
    required this.outForDeliveryAtUtc,
    required this.completedAtUtc,
  });

  final String id;
  final String orderId;
  final String establishmentId;
  final String cityId;
  final String? driverId;

  /// `Available | Assigned | PickedUp | OutForDelivery | Completed | Cancelled`.
  final String status;
  final DateTime? assignedAtUtc;
  final DateTime? pickedUpAtUtc;
  final DateTime? outForDeliveryAtUtc;
  final DateTime? completedAtUtc;

  factory Delivery.fromJson(Map<String, dynamic> json) {
    return Delivery(
      id: json['id'] as String,
      orderId: json['orderId'] as String,
      establishmentId: json['establishmentId'] as String,
      cityId: json['cityId'] as String,
      driverId: json['driverId'] as String?,
      status: json['status'] as String,
      assignedAtUtc: json['assignedAtUtc'] != null
          ? DateTime.parse(json['assignedAtUtc'] as String)
          : null,
      pickedUpAtUtc: json['pickedUpAtUtc'] != null
          ? DateTime.parse(json['pickedUpAtUtc'] as String)
          : null,
      outForDeliveryAtUtc: json['outForDeliveryAtUtc'] != null
          ? DateTime.parse(json['outForDeliveryAtUtc'] as String)
          : null,
      completedAtUtc: json['completedAtUtc'] != null
          ? DateTime.parse(json['completedAtUtc'] as String)
          : null,
    );
  }

  @override
  List<Object?> get props => [
    id,
    orderId,
    establishmentId,
    cityId,
    driverId,
    status,
  ];
}
