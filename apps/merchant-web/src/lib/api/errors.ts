export class ApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
    public readonly traceId?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function parseApiError(payload: unknown, status: number) {
  if (payload && typeof payload === "object") {
    const body = payload as {
      code?: string;
      message?: string;
      traceId?: string;
    };
    return new ApiError(
      body.code ?? "INTERNAL_ERROR",
      body.message ?? "Não foi possível concluir a operação.",
      status,
      body.traceId,
    );
  }
  return new ApiError(
    "INTERNAL_ERROR",
    "Não foi possível concluir a operação.",
    status,
  );
}
