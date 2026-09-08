import { waitForAvailableDelivery, type ApiClient } from "../src/api/client";
import type { Delivery } from "../src/api/types";
import { expect, test } from "../src/fixtures/test";
import {
  deliveryOrderBody,
  registerApprovedDriver,
  type IsolatedShop,
} from "../src/fixtures/world";

async function readyDelivery(api: ApiClient, shop: IsolatedShop) {
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
  return order;
}

test.describe("Fluxo 4 — concorrência entre motoristas", () => {
  test("somente um motorista aceita a mesma entrega (first-accept-wins)", async ({
    api,
    shop,
  }) => {
    const driverA = await registerApprovedDriver(
      api,
      shop.adminToken,
      shop.city.id,
      shop.suffix,
      "a",
    );
    const driverB = await registerApprovedDriver(
      api,
      shop.adminToken,
      shop.city.id,
      shop.suffix,
      "b",
    );

    const order = await readyDelivery(api, shop);
    const delivery = await waitForAvailableDelivery(
      api,
      driverA.tokens.accessToken,
      order.id,
    );

    const [responseA, responseB] = await Promise.all([
      api.acceptDelivery(driverA.tokens.accessToken, delivery.id),
      api.acceptDelivery(driverB.tokens.accessToken, delivery.id),
    ]);

    const statuses = [responseA.status(), responseB.status()].sort();
    expect(statuses).toEqual([200, 409]);

    const winner =
      responseA.status() === 200
        ? await api.readJson<Delivery>(responseA)
        : await api.readJson<Delivery>(responseB);
    const loserToken =
      responseA.status() === 200
        ? driverB.tokens.accessToken
        : driverA.tokens.accessToken;
    const winnerToken =
      responseA.status() === 200
        ? driverA.tokens.accessToken
        : driverB.tokens.accessToken;
    const loserError = await api.readError(
      responseA.status() === 409 ? responseA : responseB,
    );

    expect(winner.driverId).toBeTruthy();
    expect(winner.status).toBe("Assigned");
    expect(loserError.code).toBe("DELIVERY_ALREADY_TAKEN");

    const loserDetails = await api.getDeliveryDetails(loserToken, delivery.id);
    expect(loserDetails.status()).toBe(403);

    const leftover = await api.listAvailableDeliveries(loserToken);
    expect(leftover.some((item) => item.id === delivery.id)).toBeFalsy();

    const winnerCurrent = await api.getCurrentDelivery(winnerToken);
    await api.expectStatus(winnerCurrent, 200);
    expect((await api.readJson<Delivery>(winnerCurrent)).id).toBe(delivery.id);

    const loserCurrent = await api.getCurrentDelivery(loserToken);
    expect(loserCurrent.status()).toBe(204);
  });
});
