import { api } from "@/lib/api/client";
import type { InventoryMovement, PagedResult } from "@/lib/types";

export function listMovements(
  establishmentId: string,
  productId: string,
  page = 1,
) {
  return api.get<PagedResult<InventoryMovement>>(
    `/merchant/establishments/${establishmentId}/products/${productId}/inventory/movements?page=${page}&pageSize=20`,
  );
}

export function adjustStock(
  establishmentId: string,
  productId: string,
  body: { type: string; quantity: number; reason?: string },
) {
  return api.post<InventoryMovement>(
    `/merchant/establishments/${establishmentId}/products/${productId}/inventory/adjust`,
    body,
  );
}
