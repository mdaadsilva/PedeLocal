import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/api/backend";
import { parseApiError } from "@/lib/api/errors";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";

export async function POST(request: Request) {
  const body = await request.json();
  const response = await backendFetch("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(body),
  });

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const error = parseApiError(payload, response.status);
    return NextResponse.json(
      { code: error.code, message: error.message, traceId: error.traceId },
      { status: response.status },
    );
  }

  const tokens = payload as {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAtUtc: string;
  };

  const result = NextResponse.json({ ok: true });
  result.cookies.set(ACCESS_COOKIE, tokens.accessToken, cookieOptions(15 * 60));
  result.cookies.set(REFRESH_COOKIE, tokens.refreshToken, cookieOptions(30 * 24 * 60 * 60));
  return result;
}
