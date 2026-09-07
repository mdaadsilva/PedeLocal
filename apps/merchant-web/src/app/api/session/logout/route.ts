import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/api/backend";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";

export async function POST() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;

  if (refreshToken) {
    await backendFetch("/api/auth/logout", {
      method: "POST",
      body: JSON.stringify({ refreshToken }),
    }).catch(() => undefined);
  }

  const result = NextResponse.json({ ok: true });
  result.cookies.set(ACCESS_COOKIE, "", { ...cookieOptions(0), maxAge: 0 });
  result.cookies.set(REFRESH_COOKIE, "", { ...cookieOptions(0), maxAge: 0 });
  return result;
}
