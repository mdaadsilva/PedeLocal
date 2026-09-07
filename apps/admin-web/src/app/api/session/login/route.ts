import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/api/backend";
import { parseApiError } from "@/lib/api/errors";
import { canAccessAdminPanel } from "@/lib/auth/roles";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";

type Tokens = {
  accessToken: string;
  refreshToken: string;
};

async function revokeRefresh(refreshToken: string) {
  await backendFetch("/api/auth/logout", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
  }).catch(() => undefined);
}

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

  const tokens = payload as Tokens;
  const profileResponse = await backendFetch("/api/me/profile", {}, tokens.accessToken);
  const profile = (await profileResponse.json().catch(() => null)) as {
    roles?: string[];
  } | null;

  if (!profileResponse.ok || !canAccessAdminPanel(profile?.roles ?? [])) {
    await revokeRefresh(tokens.refreshToken);
    return NextResponse.json(
      {
        code: "FORBIDDEN_ROLE",
        message: "Esta área é exclusiva do Super Admin.",
      },
      { status: 403 },
    );
  }

  const result = NextResponse.json({ ok: true });
  result.cookies.set(ACCESS_COOKIE, tokens.accessToken, cookieOptions(15 * 60));
  result.cookies.set(REFRESH_COOKIE, tokens.refreshToken, cookieOptions(30 * 24 * 60 * 60));
  return result;
}
