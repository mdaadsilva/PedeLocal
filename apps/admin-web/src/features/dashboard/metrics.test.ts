import { describe, expect, it } from "vitest";
import { deriveDashboard, isEstablishmentBlocked } from "./metrics";
import type { City, Establishment, Order, Subscription } from "@/lib/types";

function city(id: string): City {
  return { id, name: id, state: "MG", isActive: true };
}

function establishment(
  id: string,
  commercialStatus: string,
  createdAtUtc = "2026-01-01T12:00:00Z",
): Establishment {
  return {
    id,
    cityId: "city-1",
    name: id,
    slug: id,
    document: "00",
    phone: "1",
    email: "a@a.com",
    categoryId: "cat",
    addressId: "addr",
    operationalStatus: "Open",
    commercialStatus,
    createdAtUtc,
  };
}

function order(id: string, createdAtUtc: string, total = 10): Order {
  return {
    id,
    customerUserId: "c",
    establishmentId: "e",
    cityId: "city-1",
    addressId: null,
    status: "Pending",
    type: "Pickup",
    paymentMethod: "Pix",
    paymentStatus: "Paid",
    changeForAmount: null,
    subtotal: total,
    deliveryFee: 0,
    total,
    calculatedDistanceKm: null,
    notes: null,
    createdAtUtc,
    items: [],
    statusHistory: [],
  };
}

function subscription(status: string): Subscription {
  return {
    id: status,
    establishmentId: "e",
    monthlyAmount: 99,
    billingDay: 10,
    dueDate: "2026-09-10",
    graceDays: 5,
    paymentStatus: status,
  };
}

describe("isEstablishmentBlocked", () => {
  it("considera bloqueio comercial, não período de graça", () => {
    expect(isEstablishmentBlocked("SuspendedPayment")).toBe(true);
    expect(isEstablishmentBlocked("SuspendedAdmin")).toBe(true);
    expect(isEstablishmentBlocked("Inactive")).toBe(true);
    expect(isEstablishmentBlocked("OverdueGrace")).toBe(false);
    expect(isEstablishmentBlocked("Active")).toBe(false);
  });
});

describe("deriveDashboard", () => {
  it("agrega apenas métricas derivadas dos contratos reais", () => {
    const now = new Date("2026-09-07T15:00:00-03:00");
    const stats = deriveDashboard({
      cities: [city("a"), city("b")],
      establishments: [
        establishment("loja-1", "Active", "2026-09-01T00:00:00Z"),
        establishment("loja-2", "SuspendedPayment", "2026-08-01T00:00:00Z"),
        establishment("loja-3", "OverdueGrace", "2026-07-01T00:00:00Z"),
      ],
      driversTotal: 4,
      customersTotal: 12,
      orders: [
        order("o1", "2026-09-07T12:00:00-03:00", 20),
        order("o2", "2026-09-06T12:00:00-03:00", 30),
      ],
      ordersTotal: 40,
      subscriptions: [
        subscription("Active"),
        subscription("OverdueGrace"),
        subscription("SuspendedPayment"),
        null,
      ],
      now,
    });

    expect(stats.citiesTotal).toBe(2);
    expect(stats.establishmentsTotal).toBe(3);
    expect(stats.activeEstablishments).toBe(1);
    expect(stats.blockedEstablishments).toBe(1);
    expect(stats.ordersToday).toBe(1);
    expect(stats.listedVolume).toBe(50);
    expect(stats.ordersTotal).toBe(40);
    expect(stats.subscriptionsActive).toBe(1);
    expect(stats.subscriptionsGrace).toBe(1);
    expect(stats.subscriptionsBlocked).toBe(1);
    expect(stats.subscriptionsMissing).toBe(1);
    expect(stats.recentEstablishments[0].id).toBe("loja-1");
  });
});
