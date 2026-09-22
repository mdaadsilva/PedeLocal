"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { useSession } from "@/features/auth/session-context";
import { getOrderOccurrences, listOrders } from "@/features/orders/api";
import { OCCURRENCE_TYPE_LABEL, ORDER_STATUS_LABEL, labelOf } from "@/features/orders/labels";
import { formatDateTime, shortId } from "@/lib/formatters";

const DELIVERY_STATUSES = [
  "WaitingDriver",
  "DriverAssigned",
  "PickedUp",
  "OutForDelivery",
];

export default function DeliveryPage() {
  const { establishment } = useSession();
  const [occurrencesFor, setOccurrencesFor] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["orders", establishment?.id, "delivery"],
    enabled: Boolean(establishment?.id),
    queryFn: () => listOrders(establishment!.id, { page: 1, pageSize: 100 }),
  });

  const occurrencesQuery = useQuery({
    queryKey: ["orders", establishment?.id, "occurrences", occurrencesFor],
    queryFn: () => getOrderOccurrences(establishment!.id, occurrencesFor!),
    enabled: Boolean(establishment?.id && occurrencesFor),
  });

  const deliveries = (query.data?.items ?? []).filter(
    (order) => order.type === "Delivery" && DELIVERY_STATUSES.includes(order.status),
  );

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Entregas em andamento</h1>
        <p className="text-sm text-slate-500">
          Acompanhamento pelo status do pedido: motorista aceitou, retirou o pedido, está em
          trânsito ou concluiu. Ações de motorista (aceitar/retirar/confirmar) ficam no app do
          entregador.
        </p>
      </div>
      {query.isLoading ? <Skeleton className="h-64" /> : null}
      {query.error ? <ErrorBox message={query.error.message} onRetry={() => query.refetch()} /> : null}
      {query.data && deliveries.length === 0 ? (
        <EmptyState
          title="Nenhuma entrega ativa"
          message="Pedidos Delivery em WaitingDriver ou depois aparecem aqui."
        />
      ) : null}
      {deliveries.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Pedido</th>
                <th className="px-4 py-3">Quando</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Total</th>
                <th className="px-4 py-3 text-right">Ocorrências</th>
              </tr>
            </thead>
            <tbody>
              {deliveries.map((order) => (
                <tr key={order.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 font-semibold">#{shortId(order.id)}</td>
                  <td className="px-4 py-3 text-slate-600">{formatDateTime(order.createdAtUtc)}</td>
                  <td className="px-4 py-3">
                    <Badge value={order.status} label={labelOf(ORDER_STATUS_LABEL, order.status)} />
                  </td>
                  <td className="px-4 py-3 text-right font-medium">
                    {new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(order.total)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="ghost" onClick={() => setOccurrencesFor(order.id)}>
                      Ver
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {occurrencesFor ? (
        <Modal title={`Ocorrências — pedido #${shortId(occurrencesFor)}`} onClose={() => setOccurrencesFor(null)}>
          {occurrencesQuery.isLoading ? (
            <Skeleton className="h-32" />
          ) : occurrencesQuery.error ? (
            <ErrorBox message={occurrencesQuery.error.message} onRetry={() => occurrencesQuery.refetch()} />
          ) : (occurrencesQuery.data?.length ?? 0) === 0 ? (
            <EmptyState
              title="Nenhuma ocorrência"
              message="O entregador não relatou nenhum problema nesta entrega."
            />
          ) : (
            <ul className="space-y-3 text-sm">
              {occurrencesQuery.data!.map((occurrence) => (
                <li key={occurrence.id} className="rounded-lg border border-slate-200 p-3">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{labelOf(OCCURRENCE_TYPE_LABEL, occurrence.type)}</span>
                    <span className="text-xs text-slate-500">{formatDateTime(occurrence.createdAtUtc)}</span>
                  </div>
                  {occurrence.note ? <p className="mt-2 text-slate-700">{occurrence.note}</p> : null}
                </li>
              ))}
            </ul>
          )}
        </Modal>
      ) : null}
    </div>
  );
}
