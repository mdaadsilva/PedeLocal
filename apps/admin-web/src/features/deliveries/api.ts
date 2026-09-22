import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Delivery, DeliveryOccurrence, PagedResult } from "@/lib/types";

export function listAdminDeliveries(params: {
  cityId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
} = {}) {
  return api.get<PagedResult<Delivery>>(
    `/admin/deliveries${toQuery({
      cityId: params.cityId,
      status: params.status,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}

/** `GET /api/admin/deliveries/{id}/occurrences` — ocorrências relatadas pelo entregador. */
export function getDeliveryOccurrences(deliveryId: string) {
  return api.get<DeliveryOccurrence[]>(`/admin/deliveries/${deliveryId}/occurrences`);
}
