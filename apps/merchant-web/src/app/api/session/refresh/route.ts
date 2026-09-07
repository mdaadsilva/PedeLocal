import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/api/backend";
import { parseApiError } from "@/lib/api/errors";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";

export async function refreshSession() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    return { ok: false as const, status: 401 };
  }

  const response = await backendFetch("/api/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
  });
  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    return { ok: false as const, status: response.status, payload };
  }

  const tokens = payload as {
    accessToken: string;
    refreshToken: string;
  };
  return { ok: true as const, tokens };
}

export async function POST() {
  const refreshed = await refreshSession();
  if (!refreshed.ok) {
    const error = parseApiError(refreshed.payload, refreshed.status);
    const result = NextResponse.json(
      { code: error.code, message: error.message },
      { status: refreshed.status },
    );
    result.cookies.set(ACCESS_COOKIE, "", { ...cookieOptions(0), maxAge: 0 });
    result.cookies.set(REFRESH_COOKIE, "", { ...cookieOptions(0), maxAge: 0 });
    return result;
  }

  const result = NextResponse.json({ ok: true });
  result.cookies.set(ACCESS_COOKIE, refreshed.tokens.accessToken, cookieOptions(15 * 60));
  result.cookies.set(REFRESH_COOKIE, refreshed.tokens.refreshToken, cookieOptions(30 * 24 * 60 * 60));
  return result;
}
