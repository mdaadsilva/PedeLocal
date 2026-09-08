import { e2eConfig } from "../src/config";
import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody } from "../src/fixtures/world";
import { isReachable, loginPanel } from "../src/pages/web";
import { MerchantOrdersPage } from "../src/pages/panel.pages";
import type { Order } from "../src/api/types";

test.describe("Fluxo 2 — merchant UI", () => {
  test.beforeAll(async () => {
    test.skip(
      !(await isReachable(e2eConfig.merchantUrl)),
      `merchant-web indisponível em ${e2eConfig.merchantUrl}`,
    );
  });

  test("login, localiza o pedido e avança accept → preparing → ready", async ({
    page,
    api,
    shop,
  }) => {
    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );

    await loginPanel(page, e2eConfig.merchantUrl, shop.merchant.email, shop.merchant.password);
    await page.getByRole("link", { name: "Pedidos", exact: true }).click();

    const orders = new MerchantOrdersPage(page);
    await expect(page.getByRole("heading", { name: "Pedidos", exact: true })).toBeVisible();
    await orders.openOrder(order.id);

    const main = page.getByRole("main");
    await expect(main.getByText("Entrega", { exact: true })).toBeVisible();
    await expect(main.getByText(/Pagamento:\s*Pix/)).toBeVisible();
    await expect(main.locator("p").filter({ hasText: "Subtotal" })).toContainText(/40,00/);
    await expect(main.locator("p").filter({ hasText: "Taxa de entrega" })).toContainText(/5,00/);
    await expect(main.locator("p.font-semibold")).toContainText(/45,00/);

    await orders.runAction("Aceitar pedido");
    await expect(main.getByText("Aceito").first()).toBeVisible();

    await orders.runAction("Marcar em preparo");
    await expect(main.getByText("Em preparo").first()).toBeVisible();

    await orders.runAction("Marcar pronto");
    await expect(main.getByText("Aguardando entregador").first()).toBeVisible();

    const persisted = await api.getMerchantOrder(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
    );
    expect((await api.readJson<Order>(persisted)).status).toBe("WaitingDriver");
  });

  test("CUSTOMER autenticado no painel merchant vê acesso restrito", async ({
    page,
    shop,
  }) => {
    await loginPanel(page, e2eConfig.merchantUrl, shop.customer.email, shop.customer.password);
    await expect(page.getByText("Acesso restrito")).toBeVisible();
  });
});
