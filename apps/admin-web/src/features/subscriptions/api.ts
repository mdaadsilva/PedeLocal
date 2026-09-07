import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";
import type { Subscription, SubscriptionPayment } from "@/lib/types";

export function getSubscription(establishmentId: string) {
  return api.get<Subscription>(`/admin/establishments/${establishmentId}/subscription`);
}

export async function getSubscriptionOrNull(establishmentId: string) {
  try {
    return await getSubscription(establishmentId);
  } catch (error) {
    if (error instanceof ApiError && error.code === "SUBSCRIPTION_NOT_FOUND") {
      return null;
    }
    throw error;
  }
}

export function upsertSubscription(
  establishmentId: string,
  body: {
    monthlyAmount: number;
    billingDay: number;
    dueDate: string;
    graceDays: number;
  },
) {
  return api.put<Subscription>(
    `/admin/establishments/${establishmentId}/subscription`,
    body,
  );
}

export function listSubscriptionPayments(establishmentId: string) {
  return api.get<SubscriptionPayment[]>(
    `/admin/establishments/${establishmentId}/subscription/payments`,
  );
}

export function registerSubscriptionPayment(
  establishmentId: string,
  body: {
    amount: number;
    paymentMethod: string;
    paidAtUtc: string;
    notes?: string | null;
    receiptUrl?: string | null;
  },
) {
  return api.post<SubscriptionPayment>(
    `/admin/establishments/${establishmentId}/subscription/payments`,
    body,
  );
}
