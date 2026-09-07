import { isSameSaoPauloDay } from "@/lib/formatters";
import type { City, Establishment, Order, Subscription } from "@/lib/types";

const ACTIVE_COMMERCIAL = "Active";
const BLOCKED_COMMERCIAL = new Set([
  "SuspendedPayment",
  "SuspendedAdmin",
  "Inactive",
]);

export function isEstablishmentBlocked(status: string) {
  return BLOCKED_COMMERCIAL.has(status);
}

export function countEstablishmentsByCity(establishments: Establishment[]) {
  const counts = new Map<string, number>();
  for (const item of establishments) {
    counts.set(item.cityId, (counts.get(item.cityId) ?? 0) + 1);
  }
  return counts;
}

export function deriveDashboard(input: {
  cities: City[];
  establishments: Establishment[];
  driversTotal: number;
  customersTotal: number;
  orders: Order[];
  ordersTotal: number;
  subscriptions: Array<Subscription | null>;
  now?: Date;
}) {
  const now = input.now ?? new Date();
  const activeEstablishments = input.establishments.filter(
    (item) => item.commercialStatus === ACTIVE_COMMERCIAL,
  ).length;
  const blockedEstablishments = input.establishments.filter((item) =>
    isEstablishmentBlocked(item.commercialStatus),
  ).length;
  const todayOrders = input.orders.filter((order) =>
    isSameSaoPauloDay(order.createdAtUtc, now),
  );
  const listedVolume = input.orders.reduce((sum, order) => sum + order.total, 0);
  const recentEstablishments = [...input.establishments]
    .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))
    .slice(0, 5);
  const recentOrders = [...input.orders]
    .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))
    .slice(0, 8);

  const configured = input.subscriptions.filter((item): item is Subscription => item !== null);

  return {
    citiesTotal: input.cities.length,
    establishmentsTotal: input.establishments.length,
    activeEstablishments,
    blockedEstablishments,
    driversTotal: input.driversTotal,
    customersTotal: input.customersTotal,
    ordersToday: todayOrders.length,
    ordersListed: input.orders.length,
    ordersTotal: input.ordersTotal,
    listedVolume,
    subscriptionsActive: configured.filter((item) => item.paymentStatus === "Active").length,
    subscriptionsGrace: configured.filter((item) => item.paymentStatus === "OverdueGrace").length,
    subscriptionsBlocked: configured.filter((item) => item.paymentStatus === "SuspendedPayment").length,
    subscriptionsMissing: input.subscriptions.length - configured.length,
    recentEstablishments,
    recentOrders,
  };
}
