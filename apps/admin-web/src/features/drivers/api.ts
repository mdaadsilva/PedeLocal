import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { Driver, DriverDetail, PagedResult } from "@/lib/types";

/** `GET /api/admin/drivers?onboardingStatus=` — `docs/DRIVER_ONBOARDING.md`. */
export function listDrivers(params: {
  onboardingStatus?: string;
  page?: number;
  pageSize?: number;
} = {}) {
  return api.get<PagedResult<Driver>>(
    `/admin/drivers${toQuery({
      onboardingStatus: params.onboardingStatus,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}

/** `GET /api/admin/drivers/{id}` — perfil + CNH + veículo enviados até agora. */
export function getDriver(id: string) {
  return api.get<DriverDetail>(`/admin/drivers/${id}`);
}

/**
 * Falha com `422`-like `AppException` (`DRIVER_LICENSE_REQUIRED`,
 * `DRIVER_VEHICLE_REQUIRED`, `DRIVER_DOCUMENT_EXPIRED`) se o entregador
 * ainda não enviou os dois documentos ou a CNH já venceu.
 */
export function approveDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/approve`);
}

export function rejectDriver(id: string, reason?: string) {
  return api.post<Driver>(`/admin/drivers/${id}/reject`, { reason });
}

/** Suspende um entregador já aprovado — não existe estado "Suspended" separado. */
export function blockDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/block`);
}

export function unblockDriver(id: string) {
  return api.post<Driver>(`/admin/drivers/${id}/unblock`);
}
