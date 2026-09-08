import 'package:flutter_test/flutter_test.dart';
import 'package:pedelocal_mobile/core/realtime/hub_payload.dart';
import 'package:pedelocal_mobile/core/realtime/order_status_progression.dart';

void main() {
  group('shouldApplyOrderStatus', () {
    test('avança o ciclo e ignora regressão', () {
      expect(shouldApplyOrderStatus('Pending', 'Accepted'), isTrue);
      expect(shouldApplyOrderStatus('Accepted', 'Pending'), isFalse);
      expect(shouldApplyOrderStatus('Delivered', 'Preparing'), isFalse);
      expect(shouldApplyOrderStatus('Ready', 'Ready'), isFalse);
    });

    test('cancelamento não sobrescreve pedido já concluído', () {
      expect(shouldApplyOrderStatus('Preparing', 'Cancelled'), isTrue);
      expect(shouldApplyOrderStatus('Delivered', 'Cancelled'), isFalse);
      expect(shouldApplyOrderStatus('PickupCompleted', 'Cancelled'), isFalse);
      expect(shouldApplyOrderStatus('Cancelled', 'Accepted'), isFalse);
    });
  });

  group('readHubPayload', () {
    test('lê o primeiro argumento camelCase', () {
      final payload = readHubPayload([
        {'orderId': 'o-1', 'status': 'Accepted'},
      ]);
      expect(readHubString(payload!, 'orderId'), 'o-1');
      expect(readHubString(payload, 'status'), 'Accepted');
    });

    test('ignora argumentos vazios ou inválidos', () {
      expect(readHubPayload(null), isNull);
      expect(readHubPayload(const []), isNull);
      expect(readHubPayload(const ['texto']), isNull);
    });
  });
}
