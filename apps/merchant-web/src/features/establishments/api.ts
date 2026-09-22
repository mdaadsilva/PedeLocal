import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";
import type {
  City,
  DeliveryPricing,
  Establishment,
  MenuCategory,
  PagedResult,
} from "@/lib/types";

export function getCities() {
  return api.get<City[]>("/cities");
}

export function getEstablishment(id: string) {
  return api.get<Establishment>(`/establishments/${id}`);
}

export function getDeliveryPricing(establishmentId: string) {
  return api.get<DeliveryPricing>(
    `/merchant/establishments/${establishmentId}/delivery-pricing`,
  );
}

export function updateDeliveryPricing(
  establishmentId: string,
  body: { deliveryFee: number; deliveryRadiusKm: number },
) {
  return api.put<DeliveryPricing>(
    `/merchant/establishments/${establishmentId}/delivery-pricing`,
    body,
  );
}

/**
 * Pausa o recebimento de novos pedidos (Delivery e Pickup) — pedidos em
 * andamento seguem normalmente. `pausedUntilUtc` omitido = pausa
 * indefinida, só termina com `resumeEstablishment` manual.
 */
export function pauseEstablishment(
  establishmentId: string,
  body: { reason: string; pausedUntilUtc?: string | null },
) {
  return api.post<Establishment>(
    `/merchant/establishments/${establishmentId}/pause`,
    body,
  );
}

export function resumeEstablishment(establishmentId: string) {
  return api.post<Establishment>(
    `/merchant/establishments/${establishmentId}/resume`,
  );
}

/**
 * O backend não expõe GET /api/me/establishments. Descobrimos os
 * estabelecimentos do staff tentando o catálogo merchant nas lojas
 * públicas de cada cidade (403 = não é staff).
 */
export async function resolveMyEstablishments() {
  const cities = await getCities();
  const found: Establishment[] = [];

  for (const city of cities) {
    const page = await api.get<PagedResult<Establishment>>(
      `/establishments?cityId=${city.id}&page=1&pageSize=100`,
    );
    for (const establishment of page.items) {
      try {
        await api.get<MenuCategory[]>(
          `/merchant/establishments/${establishment.id}/menu-categories`,
        );
        found.push(establishment);
      } catch (error) {
        if (error instanceof ApiError && error.status === 403) {
          continue;
        }
        if (error instanceof ApiError && error.code === "ESTABLISHMENT_ACCESS_DENIED") {
          continue;
        }
        throw error;
      }
    }
  }

  return found;
}
