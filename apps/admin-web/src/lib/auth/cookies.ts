export const ACCESS_COOKIE = "pl_admin_access";
export const REFRESH_COOKIE = "pl_admin_refresh";

export function cookieOptions(maxAgeSeconds: number) {
  return {
    httpOnly: true as const,
    sameSite: "lax" as const,
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: maxAgeSeconds,
  };
}
