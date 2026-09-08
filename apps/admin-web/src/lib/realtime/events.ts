/** Nomes do hub (`docs/REALTIME_EVENTS.md`). SUPER_ADMIN não tem grupo próprio. */
export const HubEvents = {
  OrderCreated: "OrderCreated",
  OrderStatusChanged: "OrderStatusChanged",
  DeliveryAvailable: "DeliveryAvailable",
  DeliveryAccepted: "DeliveryAccepted",
  DeliveryCompleted: "DeliveryCompleted",
  EstablishmentSuspended: "EstablishmentSuspended",
} as const;

export function hubUrl() {
  return (
    process.env.NEXT_PUBLIC_PEDELOCAL_HUB_URL ??
    "http://localhost:8080/hubs/pedelocal"
  );
}
