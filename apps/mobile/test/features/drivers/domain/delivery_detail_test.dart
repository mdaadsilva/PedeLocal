import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/drivers/domain/delivery_detail.dart';

void main() {
  test('fromJson mapeia estabelecimento, itens, endereço e pagamento', () {
    final details = DeliveryDetail.fromJson({
      'id': 'delivery-1',
      'status': 'Assigned',
      'order': {
        'id': 'order-1',
        'paymentMethod': 'Cash',
        'paymentStatus': 'NotApplicable',
        'changeForAmount': 50,
        'subtotal': 20,
        'deliveryFee': 5,
        'total': 25,
        'notes': 'Portão azul',
        'items': [
          {
            'id': 'item-1',
            'productId': 'prod-1',
            'productName': 'X-Burger',
            'unitPrice': 20,
            'quantity': 1,
            'lineTotal': 20,
            'options': <dynamic>[],
          },
        ],
      },
      'establishment': {
        'id': 'est-1',
        'name': 'Lanchonete Exemplo',
        'phone': '14999900004',
        'address': {
          'id': 'addr-loja',
          'cityId': 'city-1',
          'type': 'Urban',
          'label': 'Loja',
          'street': 'Rua Principal',
          'number': '100',
          'district': 'Centro',
          'complement': null,
          'reference': null,
          'zipCode': '17370000',
          'propertyName': null,
          'road': null,
          'kilometer': null,
          'latitude': -21.85,
          'longitude': -49.9,
          'googlePlaceId': null,
        },
      },
      'deliveryAddress': {
        'id': 'addr-cliente',
        'cityId': 'city-1',
        'type': 'Urban',
        'label': 'Casa',
        'street': 'Rua B',
        'number': '20',
        'district': 'Centro',
        'complement': null,
        'reference': null,
        'zipCode': '17370000',
        'propertyName': null,
        'road': null,
        'kilometer': null,
        'latitude': -21.86,
        'longitude': -49.91,
        'googlePlaceId': null,
      },
    });

    expect(details.establishment.name, 'Lanchonete Exemplo');
    expect(details.establishment.address.latitude, -21.85);
    expect(details.deliveryAddress.summary, contains('Rua B'));
    expect(details.order.items.single.productName, 'X-Burger');
    expect(details.order.notes, 'Portão azul');
    expect(details.order.paymentMethod, 'Cash');
    expect(details.order.total, 25);
  });
}
