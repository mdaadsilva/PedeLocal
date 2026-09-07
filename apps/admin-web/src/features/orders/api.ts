import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Order, PagedResult } from "@/lib/types";

export function listAdminOrders(params: {
  establishmentId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
} = {}) {
  return api.get<PagedResult<Order>>(
    `/admin/orders${toQuery({
      establishmentId: params.establishmentId,
      status: params.status,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}

export function getMerchantOrder(establishmentId: string, orderId: string) {
  return api.get<Order>(`/merchant/establishments/${establishmentId}/orders/${orderId}`);
}
