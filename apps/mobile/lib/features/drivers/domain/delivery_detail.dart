import 'package:equatable/equatable.dart';

import '../../addresses/domain/address.dart';
import '../../orders/domain/order.dart';

/// Espelha `DeliveryDetailResponse`
/// (`GET /api/driver/deliveries/{id}/details` — `docs/DELIVERY_FLOW.md`).
class DeliveryDetail extends Equatable {
  const DeliveryDetail({
    required this.id,
    required this.status,
    required this.order,
    required this.establishment,
    required this.deliveryAddress,
  });

  final String id;
  final String status;
  final DeliveryDetailOrder order;
  final DeliveryDetailEstablishment establishment;
  final Address deliveryAddress;

  factory DeliveryDetail.fromJson(Map<String, dynamic> json) {
    return DeliveryDetail(
      id: json['id'] as String,
      status: json['status'] as String,
      order: DeliveryDetailOrder.fromJson(
        json['order'] as Map<String, dynamic>,
      ),
      establishment: DeliveryDetailEstablishment.fromJson(
        json['establishment'] as Map<String, dynamic>,
      ),
      deliveryAddress: Address.fromJson(
        json['deliveryAddress'] as Map<String, dynamic>,
      ),
    );
  }

  @override
  List<Object?> get props => [id, status, order, establishment, deliveryAddress];
}

class DeliveryDetailOrder extends Equatable {
  const DeliveryDetailOrder({
    required this.id,
    required this.paymentMethod,
    required this.paymentStatus,
    this.changeForAmount,
    required this.subtotal,
    required this.deliveryFee,
    required this.total,
    this.notes,
    required this.items,
  });

  final String id;
  final String paymentMethod;
  final String paymentStatus;
  final double? changeForAmount;
  final double subtotal;
  final double deliveryFee;
  final double total;
  final String? notes;
  final List<OrderItem> items;

  factory DeliveryDetailOrder.fromJson(Map<String, dynamic> json) {
    return DeliveryDetailOrder(
      id: json['id'] as String,
      paymentMethod: json['paymentMethod'] as String,
      paymentStatus: json['paymentStatus'] as String,
      changeForAmount: (json['changeForAmount'] as num?)?.toDouble(),
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      total: (json['total'] as num).toDouble(),
      notes: json['notes'] as String?,
      items: (json['items'] as List<dynamic>? ?? const [])
          .map((e) => OrderItem.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [
    id,
    paymentMethod,
    paymentStatus,
    changeForAmount,
    subtotal,
    deliveryFee,
    total,
    notes,
    items,
  ];
}

class DeliveryDetailEstablishment extends Equatable {
  const DeliveryDetailEstablishment({
    required this.id,
    required this.name,
    required this.phone,
    required this.address,
  });

  final String id;
  final String name;
  final String phone;
  final Address address;

  factory DeliveryDetailEstablishment.fromJson(Map<String, dynamic> json) {
    return DeliveryDetailEstablishment(
      id: json['id'] as String,
      name: json['name'] as String,
      phone: json['phone'] as String,
      address: Address.fromJson(json['address'] as Map<String, dynamic>),
    );
  }

  @override
  List<Object?> get props => [id, name, phone, address];
}
