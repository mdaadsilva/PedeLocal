import { expect, test } from "../src/fixtures/test";
import { loginAsAdmin } from "../src/fixtures/world";

test.describe("Fluxo 7 — super admin (API)", () => {
  test("SUPER_ADMIN autentica e lista entidades suportadas", async ({ api }) => {
    const tokens = await loginAsAdmin(api);

    const cities = await api.rawGet("/api/admin/cities", tokens.accessToken);
    await api.expectStatus(cities, 200);
    expect(Array.isArray(await cities.json())).toBeTruthy();

    const establishments = await api.rawGet(
      "/api/admin/establishments?page=1&pageSize=20",
      tokens.accessToken,
    );
    await api.expectStatus(establishments, 200);

    const customers = await api.rawGet(
      "/api/admin/customers?page=1&pageSize=20",
      tokens.accessToken,
    );
    await api.expectStatus(customers, 200);

    const drivers = await api.rawGet(
      "/api/admin/drivers?page=1&pageSize=20",
      tokens.accessToken,
    );
    await api.expectStatus(drivers, 200);

    const orders = await api.rawGet(
      "/api/admin/orders?page=1&pageSize=20",
      tokens.accessToken,
    );
    await api.expectStatus(orders, 200);
  });

  test("CUSTOMER não entra em /api/admin", async ({ api, shop }) => {
    const response = await api.rawGet(
      "/api/admin/customers?page=1&pageSize=1",
      shop.customer.tokens.accessToken,
    );
    expect(response.status()).toBe(403);
  });
});
