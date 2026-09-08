/** Nomes e payloads do hub (`docs/REALTIME_EVENTS.md`). Não inventar eventos. */
export const HubEvents = {
  OrderCreated: "OrderCreated",
  OrderStatusChanged: "OrderStatusChanged",
  DeliveryAvailable: "DeliveryAvailable",
  DeliveryAccepted: "DeliveryAccepted",
  DeliveryCompleted: "DeliveryCompleted",
  EstablishmentSuspended: "EstablishmentSuspended",
} as const;

export type OrderCreatedPayload = { orderId: string };
export type OrderStatusChangedPayload = { orderId: string; status: string };
export type DeliveryAvailablePayload = { deliveryId: string };
export type DeliveryAcceptedPayload = { deliveryId: string; driverId?: string };
export type DeliveryCompletedPayload = { orderId: string };
export type EstablishmentSuspendedPayload = { establishmentId: string };

export function hubUrl() {
  return (
    process.env.NEXT_PUBLIC_PEDELOCAL_HUB_URL ??
    "http://localhost:8080/hubs/pedelocal"
  );
}
