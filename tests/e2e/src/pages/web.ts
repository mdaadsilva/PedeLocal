import type { Page } from "@playwright/test";

export async function isReachable(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, { method: "GET" });
    return response.status < 500;
  } catch {
    return false;
  }
}

export async function loginPanel(page: Page, baseUrl: string, email: string, password: string) {
  await page.goto(`${baseUrl}/login`);
  await page.getByRole("heading").waitFor();
  await page.getByLabel("E-mail").fill(email);
  await page.getByLabel("Senha").fill(password);

  for (let attempt = 0; attempt < 4; attempt++) {
    await page.getByRole("button", { name: "Entrar" }).click();

    const outcome = await Promise.race([
      page
        .waitForURL((url) => !url.pathname.endsWith("/login"), { timeout: 15_000 })
        .then(() => "navigated" as const),
      page
        .getByText("Esta área é exclusiva do Super Admin.")
        .waitFor({ timeout: 15_000 })
        .then(() => "forbidden" as const),
      page
        .getByText("Acesso restrito")
        .waitFor({ timeout: 15_000 })
        .then(() => "restricted" as const),
    ]).catch(() => "retry" as const);

    if (outcome !== "retry") return;

    await new Promise((resolve) =>
      setTimeout(resolve, Math.min(15_000 * (attempt + 1), 45_000)),
    );
  }
}
