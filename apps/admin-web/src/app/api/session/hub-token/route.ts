import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";
import { refreshSession } from "@/app/api/session/refresh/route";

export async function GET() {
  const jar = await cookies();
  const access = jar.get(ACCESS_COOKIE)?.value;
  if (access) {
    return NextResponse.json({ accessToken: access });
  }

  const refreshed = await refreshSession();
  if (!refreshed.ok) {
    return NextResponse.json({ code: "UNAUTHENTICATED" }, { status: 401 });
  }

  const result = NextResponse.json({ accessToken: refreshed.tokens.accessToken });
  result.cookies.set(ACCESS_COOKIE, refreshed.tokens.accessToken, cookieOptions(15 * 60));
  result.cookies.set(
    REFRESH_COOKIE,
    refreshed.tokens.refreshToken,
    cookieOptions(30 * 24 * 60 * 60),
  );
  return result;
}
