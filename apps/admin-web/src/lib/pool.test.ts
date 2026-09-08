import { describe, expect, it } from "vitest";
import { mapPool } from "./pool";

describe("mapPool", () => {
  it("preserva ordem com concorrência menor que o array", async () => {
    const seen: number[] = [];
    const result = await mapPool([1, 2, 3, 4], 2, async (item) => {
      seen.push(item);
      await new Promise((resolve) => setTimeout(resolve, 5));
      return item * 10;
    });
    expect(result).toEqual([10, 20, 30, 40]);
    expect(seen).toHaveLength(4);
  });
});
