import { describe, expect, it } from "vitest";
import { formatCurrency, shortId } from "./formatters";

describe("formatters", () => {
  it("formata reais", () => {
    expect(formatCurrency(20)).toContain("20");
  });

  it("abrevia ids", () => {
    expect(shortId("12345678-aaaa")).toBe("12345678");
  });
});
