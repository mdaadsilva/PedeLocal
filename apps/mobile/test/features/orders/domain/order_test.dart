import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/orders/domain/order.dart';

Order build(String status) {
  return Order(
    id: 'order-1',
    customerUserId: 'user-1',
    establishmentId: 'est-1',
    cityId: 'city-1',
    addressId: 'addr-1',
    status: status,
    paymentMethod: 'Pix',
    paymentStatus: 'Pending',
    subtotal: 20,
    deliveryFee: 5,
    total: 25,
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
}
