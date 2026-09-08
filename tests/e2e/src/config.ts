import path from "node:path";
import dotenv from "dotenv";

dotenv.config({ path: path.join(__dirname, "..", ".env") });

function required(name: string, fallback: string): string {
  const value = process.env[name]?.trim();
  return value && value.length > 0 ? value : fallback;
}

export const e2eConfig = {
  apiUrl: required("PEDELOCAL_API_URL", "http://localhost:8080").replace(/\/$/, ""),
  merchantUrl: required("PEDELOCAL_MERCHANT_URL", "http://localhost:3000").replace(
    /\/$/,
    "",
  ),
  adminUrl: required("PEDELOCAL_ADMIN_URL", "http://localhost:3001").replace(
    /\/$/,
    "",
  ),
  adminEmail: required("PEDELOCAL_ADMIN_EMAIL", "admin@pedelocal.dev"),
  adminPassword: required("PEDELOCAL_ADMIN_PASSWORD", "Senha123!"),
  testPassword: required("PEDELOCAL_TEST_PASSWORD", "Senha123!"),
  realtimeEnabled: process.env.PEDELOCAL_E2E_REALTIME === "true",
};

export function hubUrl(): string {
  return `${e2eConfig.apiUrl}/hubs/pedelocal`;
}
