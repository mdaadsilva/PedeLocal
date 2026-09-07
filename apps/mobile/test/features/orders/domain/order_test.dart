import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/orders/domain/order.dart';

Order build(
  String status, {
  String type = 'Delivery',
  String? addressId = 'addr-1',
}) {
  return Order(
    id: 'order-1',
    customerUserId: 'user-1',
    establishmentId: 'est-1',
    cityId: 'city-1',
    addressId: addressId,
    status: status,
    type: type,
    paymentMethod: 'Pix',
    paymentStatus: 'Pending',
    subtotal: 20,
    deliveryFee: type == 'Pickup' ? 0 : 5,
    total: type == 'Pickup' ? 20 : 25,
    createdAtUtc: DateTime.utc(2026, 9, 7),
    items: const [],
    statusHistory: const [],
  );
}

void main() {
  test('aviso do código aparece a partir de Ready até OutForDelivery', () {
    expect(build('Pending').shouldShowDeliveryCodeNotice, isFalse);
    expect(build('Accepted').shouldShowDeliveryCodeNotice, isFalse);
    expect(build('Ready').shouldShowDeliveryCodeNotice, isTrue);
    expect(build('WaitingDriver').shouldShowDeliveryCodeNotice, isTrue);
    expect(build('OutForDelivery').shouldShowDeliveryCodeNotice, isTrue);
    expect(build('Delivered').shouldShowDeliveryCodeNotice, isFalse);
    expect(build('Cancelled').shouldShowDeliveryCodeNotice, isFalse);
  });

  test('Pickup nunca mostra código de entrega nem fluxo de motorista', () {
    expect(build('Ready', type: 'Pickup').shouldShowDeliveryCodeNotice, isFalse);
    expect(build('Ready', type: 'Pickup').shouldShowPickupReadyNotice, isTrue);
    expect(build('Ready', type: 'Pickup').lifecycle, Order.pickupLifecycle);
    expect(build('PickupCompleted', type: 'Pickup').isCompleted, isTrue);
    expect(build('PickupCompleted', type: 'Pickup').isActive, isFalse);
  });

  test('fromJson aceita addressId nulo e type Pickup', () {
    final order = Order.fromJson({
      'id': 'order-1',
      'customerUserId': 'user-1',
      'establishmentId': 'est-1',
      'cityId': 'city-1',
      'addressId': null,
      'status': 'Pending',
      'type': 'Pickup',
      'paymentMethod': 'Pix',
      'paymentStatus': 'Pending',
      'subtotal': 20,
      'deliveryFee': 0,
      'total': 20,
      'calculatedDistanceKm': null,
      'notes': 'Retiro às 19h',
      'createdAtUtc': '2026-09-07T18:30:00Z',
      'items': <dynamic>[],
      'statusHistory': <dynamic>[],
    });

    expect(order.isPickup, isTrue);
    expect(order.addressId, isNull);
    expect(order.deliveryFee, 0);
    expect(order.notes, 'Retiro às 19h');
  });
}
