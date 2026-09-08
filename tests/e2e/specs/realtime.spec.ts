import { e2eConfig } from "../src/config";
import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody } from "../src/fixtures/world";
import { isReachable, loginPanel } from "../src/pages/web";

test.describe("Realtime complementar", () => {
  test.beforeAll(async () => {
    test.skip(
      !e2eConfig.realtimeEnabled,
      "Defina PEDELOCAL_E2E_REALTIME=true e Cors:AllowedOrigins para o hub.",
    );
    test.skip(
      !(await isReachable(e2eConfig.merchantUrl)),
      `merchant-web indisponível em ${e2eConfig.merchantUrl}`,
    );
  });

  test("merchant recebe atualização de novo pedido sem reload", async ({
    page,
    api,
    shop,
  }) => {
    await loginPanel(page, e2eConfig.merchantUrl, shop.merchant.email, shop.merchant.password);
    await page.getByRole("link", { name: "Pedidos", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Pedidos", exact: true })).toBeVisible();

    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );

    await expect(page.getByText("Novo pedido recebido")).toBeVisible({ timeout: 15_000 });
    await expect(
      page.getByRole("link", { name: new RegExp(`#${order.id.slice(0, 8)}`) }),
    ).toBeVisible();
  });
});
