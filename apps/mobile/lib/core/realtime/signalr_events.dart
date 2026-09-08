/// Nomes de eventos do hub SignalR (`docs/REALTIME_EVENTS.md`).
///
/// O servidor decide sozinho em quais grupos a conexão entra a partir do
/// token — não existe (e não deve ser criado) um método `Subscribe` no
/// cliente. Esta classe só centraliza os nomes para `connection.on`.
abstract class SignalREvents {
  SignalREvents._();

  /// `{ orderId }` — grupo `establishment:{id}`.
  static const orderCreated = 'OrderCreated';

  /// `{ orderId, status }` — `customer:{userId}` + `establishment:{id}`.
  static const orderStatusChanged = 'OrderStatusChanged';

  /// `{ deliveryId }` — `city-drivers:{cityId}`.
  static const deliveryAvailable = 'DeliveryAvailable';

  /// Staff: `{ deliveryId, driverId }`. Demais entregadores da cidade:
  /// `{ deliveryId }` sem `driverId` (`docs/REALTIME_EVENTS.md`).
  static const deliveryAccepted = 'DeliveryAccepted';

  /// `{ orderId }` — `customer:{userId}` + `establishment:{id}`.
  static const deliveryCompleted = 'DeliveryCompleted';

  /// `{ establishmentId }` — `establishment:{id}`.
  static const establishmentSuspended = 'EstablishmentSuspended';
}
