import { describe, expect, it } from "vitest";
import {
  OCCURRENCE_TYPE_LABEL,
  ORDER_STATUS_LABEL,
  PAUSE_REASON_LABEL,
  labelOf,
} from "./labels";

describe("labelOf", () => {
  it("mapeia um valor conhecido para o rótulo amigável", () => {
    expect(labelOf(ORDER_STATUS_LABEL, "DriverAssigned")).toBe("Motorista aceitou");
    expect(labelOf(ORDER_STATUS_LABEL, "PickedUp")).toBe("Motorista retirou o pedido");
    expect(labelOf(ORDER_STATUS_LABEL, "OutForDelivery")).toBe("Em trânsito");
  });

  it("nunca mostra o enum técnico cru quando existe rótulo", () => {
    for (const value of Object.keys(OCCURRENCE_TYPE_LABEL)) {
      expect(labelOf(OCCURRENCE_TYPE_LABEL, value)).not.toBe(value);
    }
  });

  it("faz fallback para o próprio valor quando desconhecido", () => {
    expect(labelOf(PAUSE_REASON_LABEL, "SomethingNew")).toBe("SomethingNew");
  });
});
