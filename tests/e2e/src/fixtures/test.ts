import { test as base, request as playwrightRequest } from "@playwright/test";
import { ApiClient } from "../api/client";
import { e2eConfig } from "../config";
import {
  bootstrapShop,
  cleanupShop,
  registerApprovedDriver,
  type IsolatedShop,
} from "./world";

type ShopWithDriver = IsolatedShop & {
  driver: Awaited<ReturnType<typeof registerApprovedDriver>>;
};

type E2EFixtures = {
  api: ApiClient;
};

type WorkerFixtures = {
  shop: IsolatedShop;
  shopWithDriver: ShopWithDriver;
};

export const test = base.extend<E2EFixtures, WorkerFixtures>({
  api: async ({ request }, use) => {
    await use(new ApiClient(request, e2eConfig.apiUrl));
  },
  shop: [
    async ({}, use) => {
      const context = await playwrightRequest.newContext();
      const api = new ApiClient(context, e2eConfig.apiUrl);
      const shop = await bootstrapShop(api);
      await use(shop);
      await cleanupShop(api, shop);
      await context.dispose();
    },
    { scope: "worker" },
  ],
  shopWithDriver: [
    async ({ shop }, use) => {
      const context = await playwrightRequest.newContext();
      const api = new ApiClient(context, e2eConfig.apiUrl);
      const driver = await registerApprovedDriver(
        api,
        shop.adminToken,
        shop.city.id,
        shop.suffix,
        "w",
      );
      await use({ ...shop, driver });
      await context.dispose();
    },
    { scope: "worker" },
  ],
});

export { expect } from "@playwright/test";
