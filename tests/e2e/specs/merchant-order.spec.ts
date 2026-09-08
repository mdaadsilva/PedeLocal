import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody } from "../src/fixtures/world";
import type { Order } from "../src/api/types";

test.describe("Fluxo 2 — merchant (API)", () => {
  test("localiza o pedido, valida dados e segue transições permitidas", async ({
    api,
    shop,
  }) => {
    const order = await api.createOrder(
      shop.customer.tokens.accessToken,
      deliveryOrderBody(shop),
    );

    const list = await api.listMerchantOrders(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
    );
    expect(list.items.some((item) => item.id === order.id)).toBeTruthy();

    const detailResponse = await api.getMerchantOrder(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
    );
    await api.expectStatus(detailResponse, 200);
    const detail = await api.readJson<Order>(detailResponse);
    expect(detail.customerUserId).toBeTruthy();
    expect(detail.type).toBe("Delivery");
    expect(detail.paymentMethod).toBe("Pix");
    expect(detail.subtotal).toBe(40);
    expect(detail.deliveryFee).toBe(5);
    expect(detail.total).toBe(45);
    expect(detail.items[0]?.quantity).toBe(2);

    const accept = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "accept",
    );
    await api.expectStatus(accept, 200);
    expect((await api.readJson<Order>(accept)).status).toBe("Accepted");

    const preparing = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "preparing",
    );
    await api.expectStatus(preparing, 200);
    expect((await api.readJson<Order>(preparing)).status).toBe("Preparing");

    const ready = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "ready",
    );
    await api.expectStatus(ready, 200);
    expect((await api.readJson<Order>(ready)).status).toBe("WaitingDriver");

    const persisted = await api.getMerchantOrder(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
    );
    expect((await api.readJson<Order>(persisted)).status).toBe("WaitingDriver");

    const invalid = await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "accept",
    );
    expect(invalid.status()).toBe(409);
    expect((await api.readError(invalid)).code).toBe("INVALID_ORDER_TRANSITION");
  });
});
