import { api } from "@/lib/api/client";
import type { MenuCategory, Product } from "@/lib/types";

export function listCategories(establishmentId: string) {
  return api.get<MenuCategory[]>(
    `/merchant/establishments/${establishmentId}/menu-categories`,
  );
}

export function createCategory(
  establishmentId: string,
  body: { name: string; sortOrder: number },
) {
  return api.post<MenuCategory>(
    `/merchant/establishments/${establishmentId}/menu-categories`,
    body,
  );
}

export function updateCategory(
  establishmentId: string,
  categoryId: string,
  body: { name: string; sortOrder: number },
) {
  return api.put<MenuCategory>(
    `/merchant/establishments/${establishmentId}/menu-categories/${categoryId}`,
    body,
  );
}

export function listProducts(establishmentId: string) {
  return api.get<Product[]>(
    `/merchant/establishments/${establishmentId}/products`,
  );
}

export function createProduct(
  establishmentId: string,
  body: Record<string, unknown>,
) {
  return api.post<Product>(
    `/merchant/establishments/${establishmentId}/products`,
    body,
  );
}

export function updateProduct(
  establishmentId: string,
  productId: string,
  body: Record<string, unknown>,
) {
  return api.put<Product>(
    `/merchant/establishments/${establishmentId}/products/${productId}`,
    body,
  );
}

export function setProductAvailability(
  establishmentId: string,
  productId: string,
  isAvailable: boolean,
) {
  return api.patch<Product>(
    `/merchant/establishments/${establishmentId}/products/${productId}/availability`,
    { isAvailable },
  );
}

export async function uploadImage(file: File) {
  const data = new FormData();
  data.append("file", file);
  return api.post<{ url: string }>("/uploads/image", data);
}
