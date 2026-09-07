import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/features/establishments/domain/establishment.dart';

Establishment build({
  String operationalStatus = 'Open',
  String commercialStatus = 'Active',
}) {
  return Establishment(
    id: 'est-1',
    cityId: 'city-1',
    name: 'Padaria',
    slug: 'padaria',
    categoryId: 'cat-1',
    operationalStatus: operationalStatus,
    commercialStatus: commercialStatus,
    createdAtUtc: DateTime.utc(2026, 1, 1),
  );
}

void main() {
  test('aberto e ativo pode receber pedidos', () {
    expect(build().canReceiveNewOrders, isTrue);
  });

  test('fechado operacionalmente não recebe pedidos', () {
    expect(
      build(operationalStatus: 'ClosedManual').canReceiveNewOrders,
      isFalse,
    );
    expect(build(operationalStatus: 'Paused').canReceiveNewOrders, isFalse);
  });

  test('suspenso comercialmente não recebe pedidos mesmo se aberto', () {
    expect(
      build(commercialStatus: 'SuspendedPayment').canReceiveNewOrders,
      isFalse,
    );
    expect(
      build(commercialStatus: 'SuspendedAdmin').canReceiveNewOrders,
      isFalse,
    );
    expect(build(commercialStatus: 'Inactive').canReceiveNewOrders, isFalse);
  });

  test('OverdueGrace continua podendo receber pedidos', () {
    expect(build(commercialStatus: 'OverdueGrace').canReceiveNewOrders, isTrue);
  });
}
