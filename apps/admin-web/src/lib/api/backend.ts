const DEFAULT_API_URL = "http://localhost:8080";

export function backendUrl() {
  return (process.env.PEDELOCAL_API_URL ?? DEFAULT_API_URL).replace(/\/$/, "");
}

export async function backendFetch(
  path: string,
  init: RequestInit = {},
  accessToken?: string,
) {
  const headers = new Headers(init.headers);
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }
  if (init.body && !headers.has("Content-Type") && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  return fetch(`${backendUrl()}${path}`, {
    ...init,
    headers,
    cache: "no-store",
  });
}
