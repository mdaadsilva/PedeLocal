import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { backendFetch } from "@/lib/api/backend";
import { ACCESS_COOKIE, REFRESH_COOKIE, cookieOptions } from "@/lib/auth/cookies";

async function tryRefresh() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) return null;

  const response = await backendFetch("/api/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) return null;
  return (await response.json()) as {
    accessToken: string;
    refreshToken: string;
  };
}

async function proxy(request: Request, params: Promise<{ path: string[] }>) {
  const { path } = await params;
  const url = new URL(request.url);
  const targetPath = `/api/${path.join("/")}${url.search}`;
  const jar = await cookies();
  let accessToken = jar.get(ACCESS_COOKIE)?.value;

  const init: RequestInit = {
    method: request.method,
    body: ["GET", "HEAD"].includes(request.method)
      ? undefined
      : await request.arrayBuffer(),
    headers: {
      Accept: request.headers.get("Accept") ?? "application/json",
      ...(request.headers.get("Content-Type")
        ? { "Content-Type": request.headers.get("Content-Type")! }
        : {}),
    },
  };

  let upstream = await backendFetch(targetPath, init, accessToken);

  if (upstream.status === 401) {
    const tokens = await tryRefresh();
    if (tokens) {
      accessToken = tokens.accessToken;
      upstream = await backendFetch(targetPath, init, accessToken);
      const payload = await upstream.arrayBuffer();
      const result = new NextResponse(payload, {
        status: upstream.status,
        headers: {
          "Content-Type":
            upstream.headers.get("Content-Type") ?? "application/json",
        },
      });
      result.cookies.set(ACCESS_COOKIE, tokens.accessToken, cookieOptions(15 * 60));
      result.cookies.set(
        REFRESH_COOKIE,
        tokens.refreshToken,
        cookieOptions(30 * 24 * 60 * 60),
      );
      return result;
    }
  }

  const payload = await upstream.arrayBuffer();
  return new NextResponse(payload, {
    status: upstream.status,
    headers: {
      "Content-Type": upstream.headers.get("Content-Type") ?? "application/json",
    },
  });
}

export async function GET(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  return proxy(request, context.params);
}

export async function POST(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  return proxy(request, context.params);
}

export async function PUT(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  return proxy(request, context.params);
}

export async function PATCH(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  return proxy(request, context.params);
}

export async function DELETE(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  return proxy(request, context.params);
}
