import { describe, expect, it } from "vitest";
import { addDaysToDateOnly, formatCurrency, formatDateOnly, shortId } from "./formatters";

describe("formatters", () => {
  it("formata reais", () => {
    expect(formatCurrency(20)).toContain("20");
  });

  it("abrevia ids", () => {
    expect(shortId("12345678-aaaa")).toBe("12345678");
  });

  it("formata DateOnly sem deslocar fuso", () => {
    expect(formatDateOnly("2026-09-07")).toBe("07/09/2026");
  });

  it("soma dias em DateOnly", () => {
    expect(addDaysToDateOnly("2026-09-07", 5)).toBe("2026-09-12");
  });
});
