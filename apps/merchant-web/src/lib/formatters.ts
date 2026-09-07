const currency = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
});

const dateTime = new Intl.DateTimeFormat("pt-BR", {
  timeZone: "America/Sao_Paulo",
  dateStyle: "short",
  timeStyle: "short",
});

const dateOnly = new Intl.DateTimeFormat("pt-BR", {
  timeZone: "America/Sao_Paulo",
  dateStyle: "short",
});

export function formatCurrency(value: number) {
  return currency.format(value);
}

export function formatDateTime(iso: string) {
  return dateTime.format(new Date(iso));
}

export function formatDate(iso: string) {
  return dateOnly.format(new Date(iso));
}

export function isSameSaoPauloDay(iso: string, now = new Date()) {
  return dateOnly.format(new Date(iso)) === dateOnly.format(now);
}

export function shortId(id: string) {
  return id.slice(0, 8);
}
