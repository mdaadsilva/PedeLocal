import { describe, expect, it } from "vitest";
import { merchantActionsFor } from "./order-actions";

describe("merchantActionsFor", () => {
  it("permite aceitar e cancelar um pedido pendente", () => {
    expect(merchantActionsFor("Pending", "Delivery")).toEqual(["accept", "cancel"]);
  });

  it("permite concluir retirada só em Pickup Ready", () => {
    expect(merchantActionsFor("Ready", "Pickup")).toEqual([
      "complete-pickup",
      "cancel",
    ]);
    expect(merchantActionsFor("Ready", "Delivery")).toEqual(["cancel"]);
  });

  it("não oferece ações de motorista", () => {
    expect(merchantActionsFor("DriverAssigned", "Delivery")).toEqual([]);
    expect(merchantActionsFor("OutForDelivery", "Delivery")).toEqual([]);
    expect(merchantActionsFor("Delivered", "Delivery")).toEqual([]);
  });
});
