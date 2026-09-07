import { describe, expect, it } from "vitest";
import { canAccessAdminPanel } from "./roles";

describe("canAccessAdminPanel", () => {
  it("libera somente SUPER_ADMIN", () => {
    expect(canAccessAdminPanel(["SUPER_ADMIN"])).toBe(true);
    expect(canAccessAdminPanel(["SUPER_ADMIN", "CUSTOMER"])).toBe(true);
  });

  it("bloqueia papéis de loja, cliente e entregador", () => {
    expect(canAccessAdminPanel(["ESTABLISHMENT_ADMIN"])).toBe(false);
    expect(canAccessAdminPanel(["ESTABLISHMENT_MANAGER"])).toBe(false);
    expect(canAccessAdminPanel(["CUSTOMER"])).toBe(false);
    expect(canAccessAdminPanel(["DRIVER"])).toBe(false);
    expect(canAccessAdminPanel([])).toBe(false);
  });
});
