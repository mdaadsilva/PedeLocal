import { e2eConfig } from "./config";

export default async function globalSetup() {
  const url = `${e2eConfig.apiUrl}/api/cities`;
  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  } catch (error) {
    throw new Error(
      `API indisponível em ${e2eConfig.apiUrl} (${url}): ${String(error)}. ` +
        "Suba o backend (Development, com seed) antes de rodar o Playwright. Ver docs/E2E_TESTING.md.",
    );
  }
}
