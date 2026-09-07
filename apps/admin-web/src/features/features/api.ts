import { api } from "@/lib/api/client";
import type { EstablishmentFeature } from "@/lib/types";

export const KNOWN_FEATURE_KEYS = [
  "CUSTOM_REPORT",
  "WHOLESALE_MODE",
  "ADVANCED_STOCK",
  "SPECIAL_ORDER_FLOW",
] as const;

export const FEATURE_KEY_LABEL: Record<string, string> = {
  CUSTOM_REPORT: "Relatório personalizado",
  WHOLESALE_MODE: "Modo atacado",
  ADVANCED_STOCK: "Estoque avançado",
  SPECIAL_ORDER_FLOW: "Fluxo especial de pedido",
};

export function listFeatures(establishmentId: string) {
  return api.get<EstablishmentFeature[]>(
    `/admin/establishments/${establishmentId}/features`,
  );
}

export function setFeature(
  establishmentId: string,
  featureKey: string,
  body: { enabled: boolean; settingsJson?: string | null },
) {
  return api.put<EstablishmentFeature>(
    `/admin/establishments/${establishmentId}/features/${encodeURIComponent(featureKey)}`,
    body,
  );
}
