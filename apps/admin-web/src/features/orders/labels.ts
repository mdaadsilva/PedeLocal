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

export const COMMERCIAL_STATUS_LABEL: Record<string, string> = {
  Active: "Ativo",
  OverdueGrace: "Período de graça",
  SuspendedPayment: "Bloqueado por pagamento",
  SuspendedAdmin: "Bloqueado pelo admin",
  Inactive: "Inativo",
};

export const OPERATIONAL_STATUS_LABEL: Record<string, string> = {
  Open: "Aberto",
  ClosedManual: "Fechado",
  ClosedSchedule: "Fechado (horário)",
  Paused: "Pausado",
};

export const SUBSCRIPTION_STATUS_LABEL: Record<string, string> = {
  Active: "Ativa",
  OverdueGrace: "Período de graça",
  SuspendedPayment: "Bloqueada",
};

export const DRIVER_ONBOARDING_LABEL: Record<string, string> = {
  PendingDocuments: "Documentação pendente",
  UnderReview: "Em análise",
  Approved: "Aprovado",
  Rejected: "Reprovado",
};

export const DRIVER_ELIGIBILITY_LABEL: Record<string, string> = {
  Eligible: "Apto a trabalhar",
  PendingDocuments: "Documentação pendente",
  UnderReview: "Em análise",
  Rejected: "Reprovado",
  Blocked: "Suspenso",
  DocumentExpired: "CNH vencida",
};

export const DRIVER_STATUS_LABEL: Record<string, string> = {
  Offline: "Offline",
  Available: "Disponível",
  Busy: "Ocupado",
  Blocked: "Suspenso",
};

export const VEHICLE_TYPE_LABEL: Record<string, string> = {
  Bike: "Bicicleta",
  Motorcycle: "Moto",
  Car: "Carro",
};

export const PIX_KEY_TYPE_LABEL: Record<string, string> = {
  Cpf: "CPF",
  Email: "E-mail",
  Phone: "Telefone",
  Random: "Aleatória",
};

export const OCCURRENCE_TYPE_LABEL: Record<string, string> = {
  CustomerNotFound: "Cliente não encontrado",
  CustomerNotResponding: "Cliente não responde",
  WrongAddress: "Endereço incorreto",
  PaymentProblem: "Problema de pagamento",
  VehicleProblem: "Problema no veículo",
  Other: "Outro",
};

export const DELIVERY_STATUS_LABEL: Record<string, string> = {
  Available: "Disponível",
  Assigned: "Motorista aceitou",
  PickedUp: "Motorista retirou o pedido",
  OutForDelivery: "Em trânsito",
  Completed: "Concluída",
  Cancelled: "Cancelada",
};

export function orderStatusLabel(status: string) {
  return ORDER_STATUS_LABEL[status] ?? status;
}

export function paymentLabel(method: string) {
  return PAYMENT_LABEL[method] ?? method;
}

export function labelOf(map: Record<string, string>, value: string) {
  return map[value] ?? value;
}
