export const MERCHANT_ROLES = [
  "ESTABLISHMENT_ADMIN",
  "ESTABLISHMENT_MANAGER",
  "ATTENDANT",
  "STOCK_OPERATOR",
  "SUPER_ADMIN",
] as const;

export type MerchantRole = (typeof MERCHANT_ROLES)[number];

export function canAccessMerchantPanel(roles: string[]) {
  return roles.some((role) =>
    MERCHANT_ROLES.includes(role as MerchantRole),
  );
}

export function isEstablishmentAdmin(roles: string[]) {
  return roles.includes("ESTABLISHMENT_ADMIN") || roles.includes("SUPER_ADMIN");
}
