import { api } from "@/lib/api/client";
import type { City } from "@/lib/types";

export function listCities() {
  return api.get<City[]>("/admin/cities");
}

export function createCity(body: { name: string; state: string }) {
  return api.post<City>("/admin/cities", body);
}

export function updateCity(id: string, body: { name: string; state: string }) {
  return api.put<City>(`/admin/cities/${id}`, body);
}

export function setCityStatus(id: string, isActive: boolean) {
  return api.patch<City>(`/admin/cities/${id}/status`, { isActive });
}
