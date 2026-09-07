import { describe, expect, it } from "vitest";
import { canAccessMerchantPanel } from "./roles";

describe("canAccessMerchantPanel", () => {
  it("libera ESTABLISHMENT_ADMIN", () => {
    expect(canAccessMerchantPanel(["ESTABLISHMENT_ADMIN"])).toBe(true);
  });

  it("bloqueia cliente e entregador", () => {
    expect(canAccessMerchantPanel(["CUSTOMER"])).toBe(false);
    expect(canAccessMerchantPanel(["DRIVER"])).toBe(false);
  });
});
