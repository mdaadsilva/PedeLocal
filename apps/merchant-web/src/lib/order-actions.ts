export type OrderType = "Delivery" | "Pickup";

export type MerchantOrderAction =
  | "accept"
  | "preparing"
  | "ready"
  | "complete-pickup"
  | "cancel";

export function merchantActionsFor(status: string, type: string): MerchantOrderAction[] {
  const actions: MerchantOrderAction[] = [];

  if (status === "Pending") {
    actions.push("accept", "cancel");
  }
  if (status === "Accepted") {
    actions.push("preparing", "cancel");
  }
  if (status === "Preparing") {
    actions.push("ready", "cancel");
  }
  if (status === "Ready" && type === "Pickup") {
    actions.push("complete-pickup", "cancel");
  }
  if (status === "Ready" && type === "Delivery") {
    actions.push("cancel");
  }
  if (status === "WaitingDriver") {
    actions.push("cancel");
  }

  return actions;
}

export const ACTION_LABELS: Record<MerchantOrderAction, string> = {
  accept: "Aceitar pedido",
  preparing: "Marcar em preparo",
  ready: "Marcar pronto",
  "complete-pickup": "Confirmar retirada",
  cancel: "Cancelar pedido",
};
