/// Nomes de eventos do hub SignalR (`docs/FRONTEND_GUIDE.md#signalr`).
///
/// O servidor decide sozinho em quais grupos a conexão entra a partir do
/// token — não existe (e não deve ser criado) um método `Subscribe` client
/// side. Esta classe só centraliza os nomes de evento para
/// `connection.on(event, handler)`.
abstract class SignalREvents {
  SignalREvents._();

  /// Payload: `{ orderId }`. Recebido por: staff do estabelecimento.
  static const orderCreated = 'OrderCreated';

  /// Payload: `{ orderId, status }`. Recebido por: cliente do pedido + staff.
  static const orderStatusChanged = 'OrderStatusChanged';

  /// Payload: `{ deliveryId }`. Recebido por: entregadores online da cidade.
  static const deliveryAvailable = 'DeliveryAvailable';

  /// Payload: `{ deliveryId, driverId }`. Recebido por: staff do estabelecimento.
  static const deliveryAccepted = 'DeliveryAccepted';

  /// Payload: `{ orderId }`. Recebido por: cliente do pedido + staff.
  static const deliveryCompleted = 'DeliveryCompleted';

  /// Payload: `{ establishmentId }`. Recebido por: staff do estabelecimento.
  static const establishmentSuspended = 'EstablishmentSuspended';
}
