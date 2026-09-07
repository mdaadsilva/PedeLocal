import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type {
  CreateEstablishmentBody,
  Establishment,
  EstablishmentCategory,
  PagedResult,
  UpdateEstablishmentBody,
} from "@/lib/types";

export function listEstablishments(params: { page?: number; pageSize?: number } = {}) {
  return api.get<PagedResult<Establishment>>(
    `/admin/establishments${toQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}

export async function listAllEstablishments() {
  const first = await listEstablishments({ page: 1, pageSize: 100 });
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await listEstablishments({ page, pageSize: 100 });
    items.push(...next.items);
  }
  return items;
}

export function getEstablishment(id: string) {
  return api.get<Establishment>(`/admin/establishments/${id}`);
}

export function createEstablishment(body: CreateEstablishmentBody) {
  return api.post<Establishment>("/admin/establishments", body);
}

export function updateEstablishment(id: string, body: UpdateEstablishmentBody) {
  return api.put<Establishment>(`/admin/establishments/${id}`, body);
}

export function setEstablishmentStatus(
  id: string,
  body: { operationalStatus?: string; commercialStatus?: string },
) {
  return api.patch<Establishment>(`/admin/establishments/${id}/status`, body);
}

export function listEstablishmentCategories() {
  return api.get<EstablishmentCategory[]>("/establishment-categories");
}
