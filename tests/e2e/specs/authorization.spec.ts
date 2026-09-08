import { waitForAvailableDelivery } from "../src/api/client";
import { expect, test } from "../src/fixtures/test";
import {
  bootstrapRivalMerchant,
  deliveryOrderBody,
  registerApprovedDriver,
  registerCustomerActor,
} from "../src/fixtures/world";

test.describe("Fluxo 6 — autorização / IDOR", () => {
  test("sem token, endpoints protegidos retornam 401", async ({ api }) => {
    const paths = [
      "/api/me/profile",
      "/api/orders",
      "/api/admin/cities",
      "/api/driver/me",
      "/api/merchant/establishments/00000000-0000-0000-0000-000000000001/orders",
    ];
    for (const path of paths) {
      const response = await api.rawGet(path);
      expect(response.status(), path).toBe(401);
    }
  });

  test("CUSTOMER não acessa pedido de outro cliente", async ({ api, shop }) => {
    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );
    const stranger = await registerCustomerActor(api);
    const response = await api.getOrder(stranger.tokens.accessToken, order.id);
    expect(response.status()).toBe(403);
    expect((await api.readError(response)).code).toBe("ORDER_ACCESS_DENIED");
  });

  test("ESTABLISHMENT_ADMIN não acessa pedido de outro estabelecimento", async ({
    api,
    shop,
  }) => {
    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );
    const other = await bootstrapRivalMerchant(api, shop);
    const response = await api.getMerchantOrder(
      other.tokens.accessToken,
      shop.establishment.id,
      order.id,
    );
    expect(response.status()).toBe(403);
  });

  test("DRIVER não acessa entrega privada de outro motorista", async ({
    api,
    shop,
  }) => {
    const owner = await registerApprovedDriver(
      api,
      shop.adminToken,
      shop.city.id,
      shop.suffix,
      "y",
    );
    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );
    for (const action of ["accept", "preparing", "ready"] as const) {
      await api.expectStatus(
        await api.merchantAction(
          shop.merchant.tokens.accessToken,
          shop.establishment.id,
          order.id,
          action,
        ),
        200,
      );
    }

    const delivery = await waitForAvailableDelivery(
      api,
      owner.tokens.accessToken,
      order.id,
    );
    await api.expectStatus(
      await api.acceptDelivery(owner.tokens.accessToken, delivery.id),
      200,
    );

    const otherDriver = await registerApprovedDriver(
      api,
      shop.adminToken,
      shop.city.id,
      `${shop.suffix}x`,
      "z",
    );
    const forbidden = await api.getDeliveryDetails(
      otherDriver.tokens.accessToken,
      delivery.id,
    );
    expect(forbidden.status()).toBe(403);
  });

  test("role incorreta retorna 403", async ({ api, shop }) => {
    const customerToken = shop.customer.tokens.accessToken;
    const driverForbidden = await api.rawGet(
      "/api/driver/deliveries/available",
      customerToken,
    );
    expect(driverForbidden.status()).toBe(403);

    const adminForbidden = await api.rawGet("/api/admin/cities", customerToken);
    expect(adminForbidden.status()).toBe(403);

    const merchantForbidden = await api.rawGet(
      `/api/merchant/establishments/${shop.establishment.id}/orders`,
      customerToken,
    );
    expect(merchantForbidden.status()).toBe(403);
  });
});
