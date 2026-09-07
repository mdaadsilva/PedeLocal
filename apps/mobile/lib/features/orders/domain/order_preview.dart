import 'package:equatable/equatable.dart';

import 'order.dart';

/// Espelha `OrderPreviewResponse` (`POST /api/orders/preview` —
/// `docs/ORDER_FLOW.md#preview`). Mesma validação/cálculo da criação,
/// sem persistir. Não cachear além da tela de checkout.
class OrderPreview extends Equatable {
  const OrderPreview({
    required this.establishmentId,
    required this.type,
    this.addressId,
    required this.subtotal,
    required this.deliveryFee,
    required this.total,
    this.distanceKm,
    required this.items,
  });

  final String establishmentId;
  final String type;
  final String? addressId;
  final double subtotal;
  final double deliveryFee;
  final double total;
  final double? distanceKm;
  final List<OrderItem> items;

  bool get isPickup => type == 'Pickup';

  factory OrderPreview.fromJson(Map<String, dynamic> json) {
    return OrderPreview(
      establishmentId: json['establishmentId'] as String,
      type: json['type'] as String,
      addressId: json['addressId'] as String?,
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      total: (json['total'] as num).toDouble(),
      distanceKm: (json['distanceKm'] as num?)?.toDouble(),
      items: (json['items'] as List<dynamic>? ?? const [])
          .map((e) => OrderItem.fromJson(e as Map<String, dynamic>))
          .toList(growable: false),
    );
  }

  @override
  List<Object?> get props => [
    establishmentId,
    type,
    addressId,
    subtotal,
    deliveryFee,
    total,
    distanceKm,
    items,
  ];
}
