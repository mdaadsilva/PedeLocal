import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Customer, PagedResult } from "@/lib/types";

export function listCustomers(params: { page?: number; pageSize?: number } = {}) {
  return api.get<PagedResult<Customer>>(
    `/admin/customers${toQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}
