/// Payload `data` real do FCM (`docs/PUSH_NOTIFICATIONS.md#payloads`).
///
/// O backend envia só identificadores — nunca PII de terceiros. O app
/// usa esses ids para buscar o resto via REST. Não confiar em `userId`,
/// `role` ou qualquer outro campo extra mesmo que apareça no mapa.
class NotificationPayload {
  const NotificationPayload({
    this.orderId,
    this.deliveryId,
    this.eventType,
    this.batchId,
  });

  const NotificationPayload.empty()
    : orderId = null,
      deliveryId = null,
      eventType = null,
      batchId = null;

  /// Eventos de pedido/entrega para o cliente
  /// (`OrderAccepted`, `DeliveryAccepted`, ...).
  final String? orderId;

  /// Entrega — presente em eventos de entrega do cliente e em
  /// `DeliveryAvailable` para o entregador.
  final String? deliveryId;

  /// Nome exato de `NotificationEventType` (`OrderAccepted`,
  /// `DeliveryAvailable`, `PaymentReported`, ...).
  final String? eventType;

  /// Lote financeiro — só em `PaymentReported`.
  final String? batchId;

  bool get isEmpty =>
      orderId == null &&
      deliveryId == null &&
      eventType == null &&
      batchId == null;

  /// Parse tolerante: mapa ausente, vazio ou malformado nunca lança.
  /// Valores não-string são convertidos com `toString()`; strings vazias
  /// viram `null`. Campos desconhecidos (incluindo `userId`/`role`) são
  /// ignorados.
  static NotificationPayload parse(Map<String, dynamic>? data) {
    if (data == null || data.isEmpty) {
      return const NotificationPayload.empty();
    }

    return NotificationPayload(
      orderId: _read(data, 'orderId'),
      deliveryId: _read(data, 'deliveryId'),
      eventType: _read(data, 'eventType'),
      batchId: _read(data, 'batchId'),
    );
  }

  static String? _read(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value == null) return null;
    final text = value.toString().trim();
    return text.isEmpty ? null : text;
  }
}

/// Nomes exatos enviados pelo backend (`NotificationEventType.ToString()`).
abstract class NotificationEventTypes {
  NotificationEventTypes._();

  static const orderAccepted = 'OrderAccepted';
  static const orderPreparing = 'OrderPreparing';
  static const orderReady = 'OrderReady';
  static const orderOutForDelivery = 'OrderOutForDelivery';
  static const deliveryAccepted = 'DeliveryAccepted';
  static const orderDelivered = 'OrderDelivered';
  static const orderCancelled = 'OrderCancelled';
  static const deliveryAvailable = 'DeliveryAvailable';
  static const paymentReported = 'PaymentReported';
}
