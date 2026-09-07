import { api } from "@/lib/api/client";
import type { DriverPaymentBatch, DriverReceivable } from "@/lib/types";

export function listPendingReceivables(
  establishmentId: string,
  driverId?: string,
) {
  const query = driverId ? `?driverId=${driverId}` : "";
  return api.get<DriverReceivable[]>(
    `/merchant/establishments/${establishmentId}/driver-payments/receivables/pending${query}`,
  );
}

export function createPaymentBatch(
  establishmentId: string,
  body: { driverId: string; receivableIds: string[] },
) {
  return api.post<DriverPaymentBatch>(
    `/merchant/establishments/${establishmentId}/driver-payments/batches`,
    body,
  );
}

export function reportPayment(establishmentId: string, batchId: string) {
  return api.post<DriverPaymentBatch>(
    `/merchant/establishments/${establishmentId}/driver-payments/batches/${batchId}/report`,
  );
}
