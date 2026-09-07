import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Delivery, PagedResult } from "@/lib/types";

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
