export const ORDER_STATUS_LABEL: Record<string, string> = {
  Pending: "Pendente",
  Accepted: "Aceito",
  Preparing: "Em preparo",
  Ready: "Pronto",
  WaitingDriver: "Aguardando entregador",
  DriverAssigned: "Entregador a caminho",
  PickedUp: "Retirado na loja",
  OutForDelivery: "Saiu para entrega",
  Delivered: "Entregue",
  PickupCompleted: "Retirado pelo cliente",
  Cancelled: "Cancelado",
};

export const PAYMENT_LABEL: Record<string, string> = {
  Pix: "Pix",
  Cash: "Dinheiro",
  DebitOnDelivery: "Débito",
  CreditOnDelivery: "Crédito",
};

export const PAYMENT_STATUS_LABEL: Record<string, string> = {
  Pending: "Pendente",
  Paid: "Pago",
  NotApplicable: "Na entrega/retirada",
};

export function orderStatusLabel(status: string) {
  return ORDER_STATUS_LABEL[status] ?? status;
}

export function paymentLabel(method: string) {
  return PAYMENT_LABEL[method] ?? method;
}
