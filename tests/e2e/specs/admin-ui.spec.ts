import { e2eConfig } from "../src/config";
import { expect, test } from "../src/fixtures/test";
import { isReachable, loginPanel } from "../src/pages/web";
import { AdminPanelPage } from "../src/pages/panel.pages";

test.describe("Fluxo 7 — super admin UI", () => {
  test.beforeAll(async () => {
    test.skip(
      !(await isReachable(e2eConfig.adminUrl)),
      `admin-web indisponível em ${e2eConfig.adminUrl}`,
    );
  });

  test("login SUPER_ADMIN e vê entidades suportadas", async ({ page }) => {
    await loginPanel(page, e2eConfig.adminUrl, e2eConfig.adminEmail, e2eConfig.adminPassword);
    const panel = new AdminPanelPage(page);
    await panel.expectDashboard();
    await expect(page.getByRole("main").getByText("Cidades", { exact: true })).toBeVisible();
    await expect(page.getByRole("main").getByText("Estabelecimentos", { exact: true })).toBeVisible();

    await panel.openNav("Cidades");
    await expect(page.getByRole("heading", { name: "Cidades", exact: true })).toBeVisible();

    await panel.openNav("Estabelecimentos");
    await expect(page.getByRole("heading", { name: "Estabelecimentos", exact: true })).toBeVisible();

    await panel.openNav("Usuários");
    await expect(page.getByRole("heading", { name: "Usuários", exact: true })).toBeVisible();

    await panel.openNav("Entregadores");
    await expect(page.getByRole("heading", { name: "Entregadores", exact: true })).toBeVisible();
  });

  test("role não autorizada é bloqueada no login do admin", async ({ page, shop }) => {
    await loginPanel(page, e2eConfig.adminUrl, shop.customer.email, shop.customer.password);
    await expect(page.getByText("Esta área é exclusiva do Super Admin.")).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });
});
