import path from "node:path";
import { defineConfig, devices } from "@playwright/test";
import dotenv from "dotenv";

dotenv.config({ path: path.join(__dirname, ".env") });

const merchantUrl = process.env.PEDELOCAL_MERCHANT_URL ?? "http://localhost:3000";
const adminUrl = process.env.PEDELOCAL_ADMIN_URL ?? "http://localhost:3001";

export default defineConfig({
  testDir: "./specs",
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  timeout: 240_000,
  expect: { timeout: 20_000 },
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
  ],
  globalSetup: require.resolve("./src/global-setup.ts"),
  outputDir: "test-results",
  use: {
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "api",
      testMatch: /specs\/(?!merchant-ui|admin-ui|realtime).*\.spec\.ts/,
    },
    {
      name: "merchant",
      testMatch: /specs\/merchant-ui\.spec\.ts/,
      use: { ...devices["Desktop Chrome"], baseURL: merchantUrl },
    },
    {
      name: "admin",
      testMatch: /specs\/admin-ui\.spec\.ts/,
      use: { ...devices["Desktop Chrome"], baseURL: adminUrl },
    },
    {
      name: "realtime",
      testMatch: /specs\/realtime\.spec\.ts/,
      use: { ...devices["Desktop Chrome"], baseURL: merchantUrl },
    },
  ],
});
