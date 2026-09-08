import { describe, expect, it } from "vitest";
import { asRecord, readString } from "./parse";

describe("hub payload parse", () => {
  it("lê orderId de OrderCreated", () => {
    const record = asRecord({ orderId: "abc" });
    expect(readString(record!, "orderId")).toBe("abc");
  });

  it("rejeita payload inválido", () => {
    expect(asRecord(null)).toBeNull();
    expect(asRecord("OrderCreated")).toBeNull();
    expect(readString({ orderId: "" }, "orderId")).toBeNull();
  });
});
