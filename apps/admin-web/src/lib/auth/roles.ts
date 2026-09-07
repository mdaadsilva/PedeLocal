export const SUPER_ADMIN_ROLE = "SUPER_ADMIN";

export function canAccessAdminPanel(roles: string[]) {
  return roles.includes(SUPER_ADMIN_ROLE);
}
