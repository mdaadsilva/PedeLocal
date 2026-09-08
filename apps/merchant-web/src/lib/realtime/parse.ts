export function asRecord(value: unknown): Record<string, unknown> | null {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }
  return null;
}

export function readString(record: Record<string, unknown>, key: string) {
  const value = record[key];
  if (typeof value !== "string" || value.length === 0) return null;
  return value;
}
