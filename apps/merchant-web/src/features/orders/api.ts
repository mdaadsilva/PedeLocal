import { api } from "@/lib/api/client";
import type { MerchantOrderAction } from "@/lib/order-actions";
import type { Order, PagedResult } from "@/lib/types";

export function listOrders(
  establishmentId: string,
  params: { status?: string; page?: number; pageSize?: number },
) {
  const search = new URLSearchParams();
  if (params.status) search.set("status", params.status);
  search.set("page", String(params.page ?? 1));
  search.set("pageSize", String(params.pageSize ?? 20));
  return api.get<PagedResult<Order>>(
    `/merchant/establishments/${establishmentId}/orders?${search}`,
  );
}

export function getOrder(establishmentId: string, orderId: string) {
  return api.get<Order>(
    `/merchant/establishments/${establishmentId}/orders/${orderId}`,
  );
}

export function runOrderAction(
  establishmentId: string,
  orderId: string,
  action: MerchantOrderAction,
  reason?: string,
) {
  const path = `/merchant/establishments/${establishmentId}/orders/${orderId}/${action}`;
  return api.post<Order>(
    path,
    action === "cancel" ? { reason: reason ?? "Cancelado pelo estabelecimento" } : undefined,
  );
}
