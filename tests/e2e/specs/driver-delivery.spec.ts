import { waitForAvailableDelivery, waitForOrderStatus, type ApiClient } from "../src/api/client";
import type { Delivery, DeliveryCode, DeliveryDetails, Order } from "../src/api/types";
import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody, type IsolatedShop } from "../src/fixtures/world";

async function prepareDeliveryOrder(api: ApiClient, shop: IsolatedShop): Promise<Order> {
  const order = await api.createOrder(
    shop.customer.tokens.accessToken,
    deliveryOrderBody(shop),
  );
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
  await api.expectStatus(
    await api.merchantAction(
      shop.merchant.tokens.accessToken,
      shop.establishment.id,
      order.id,
      "ready",
    ),
    200,
  );
  return order;
}

test.describe("Fluxo 3 — driver delivery", () => {
  test("aceita a entrega, consulta detalhes, usa o código e conclui", async ({
    api,
    shopWithDriver,
  }) => {
    const shop = shopWithDriver;
    const driverToken = shop.driver.tokens.accessToken;
    const order = await prepareDeliveryOrder(api, shop);

    const delivery = await waitForAvailableDelivery(api, driverToken, order.id);

    const accept = await api.acceptDelivery(driverToken, delivery.id);
    await api.expectStatus(accept, 200);
    const assigned = await api.readJson<Delivery>(accept);
    expect(assigned.status).toBe("Assigned");
    expect(assigned.driverId).toBe(shop.driver.driverId);

    const detailsResponse = await api.getDeliveryDetails(driverToken, delivery.id);
    await api.expectStatus(detailsResponse, 200);
    const details = await api.readJson<DeliveryDetails>(detailsResponse);
    expect(details.order.id).toBe(order.id);
    expect(details.order.items).toHaveLength(1);
    expect(details.order.paymentMethod).toBe("Pix");
    expect(details.deliveryAddress.street).toBeTruthy();
    expect(details.establishment.name).toContain("Lanchonete E2E");

    const pickup = await api.pickupDelivery(driverToken, delivery.id);
    await api.expectStatus(pickup, 200);
    expect((await api.readJson<Delivery>(pickup)).status).toBe("PickedUp");

    const out = await api.outForDelivery(driverToken, delivery.id);
    await api.expectStatus(out, 200);
    expect((await api.readJson<Delivery>(out)).status).toBe("OutForDelivery");

    const codeResponse = await api.getDeliveryCode(
      shop.customer.tokens.accessToken,
      order.id,
    );
    await api.expectStatus(codeResponse, 200);
    const code = await api.readJson<DeliveryCode>(codeResponse);
    expect(code.code).toHaveLength(6);

    const wrong = await api.confirmDelivery(driverToken, delivery.id, "000000");
    expect(wrong.status()).toBe(400);

    const confirm = await api.confirmDelivery(driverToken, delivery.id, code.code);
    await api.expectStatus(confirm, 200);
    expect((await api.readJson<Delivery>(confirm)).status).toBe("Completed");

    const finalOrder = await waitForOrderStatus(
      api,
      shop.customer.tokens.accessToken,
      order.id,
      "Delivered",
    );
    expect(finalOrder.status).toBe("Delivered");

    const current = await api.getCurrentDelivery(driverToken);
    expect(current.status()).toBe(204);
  });
});
