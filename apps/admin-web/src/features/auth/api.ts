import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";
import type { Profile } from "@/lib/types";

export async function login(email: string, password: string) {
  const response = await fetch("/api/session/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    throw new ApiError(
      payload?.code ?? "INVALID_CREDENTIALS",
      payload?.message ?? "E-mail ou senha incorretos.",
      response.status,
    );
  }
}

export async function logout() {
  await fetch("/api/session/logout", { method: "POST" });
}

export function getProfile() {
  return api.get<Profile>("/me/profile");
}
