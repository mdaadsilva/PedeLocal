import type { Order } from "../src/api/types";
import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody } from "../src/fixtures/world";

test.describe("Fluxo 5 — pickup", () => {
  test("cria Pickup sem endereço, taxa 0, comércio conclui sem motorista", async ({
    api,
    shop,
  }) => {
    const body = deliveryOrderBody(shop, { orderType: "Pickup", quantity: 1 });
    const preview = await api.previewOrder(shop.customer.tokens.accessToken, body);
    expect(preview.type).toBe("Pickup");
    expect(preview.addressId).toBeNull();
    expect(preview.deliveryFee).toBe(0);
    expect(preview.distanceKm).toBeNull();
    expect(preview.total).toBe(preview.subtotal);

    const order = await api.createOrder(shop.customer.tokens.accessToken, body);
    expect(order.type).toBe("Pickup");
    expect(order.addressId).toBeNull();
    expect(order.deliveryFee).toBe(0);
    expect(order.total).toBe(order.subtotal);
    expect(order.status).toBe("Pending");

    await api.expectStatus(
      await api.merchantAction(
        shop.merchant.tokens.accessToken,
        shop.establishment.id,
        order.id,
        "accept",
      ),
      200,
    );
    await api.expectStatus(
      await api.merchantAction(
        shop.merchant.tokens.accessToken,
        shop.establishment.id,
        order.id,
        "preparing",
      ),
      200,
    );
    const ready = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "ready",
    );
    await api.expectStatus(ready, 200);
    expect((await api.readJson<Order>(ready)).status).toBe("Ready");

    const code = await api.getDeliveryCode(
      shop.customer.tokens.accessToken,
      order.id,
    );
    expect(code.status()).toBe(404);

    const complete = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "complete-pickup",
    );
    await api.expectStatus(complete, 200);
    expect((await api.readJson<Order>(complete)).status).toBe("PickupCompleted");

    const finalResponse = await api.getOrder(
      shop.customer.tokens.accessToken,
      order.id,
    );
    expect((await api.readJson<Order>(finalResponse)).status).toBe("PickupCompleted");
  });

  test("Delivery sem addressId é rejeitado", async ({ api, shop }) => {
    const body = deliveryOrderBody(shop, { orderType: "Delivery", addressId: null });
    const response = await api.postOrder(shop.customer.tokens.accessToken, body);
    expect(response.status()).toBe(400);
    expect((await api.readError(response)).code).toBe("ADDRESS_REQUIRED");
  });
});
