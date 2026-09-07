import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Driver, PagedResult } from "@/lib/types";

export function listDrivers(params: {
  approvalStatus?: string;
  page?: number;
  pageSize?: number;
} = {}) {
  return api.get<PagedResult<Driver>>(
    `/admin/drivers${toQuery({
      approvalStatus: params.approvalStatus,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}

export function approveDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/approve`);
}

export function rejectDriver(id: string, reason?: string) {
  return api.post<Driver>(`/admin/drivers/${id}/reject`, { reason });
}

export function blockDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/block`);
}

export function unblockDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/unblock`);
}
