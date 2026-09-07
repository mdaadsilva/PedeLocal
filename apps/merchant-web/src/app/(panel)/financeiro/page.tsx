"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, StatCard } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import {
  createPaymentBatch,
  listPendingReceivables,
  reportPayment,
} from "@/features/finance/api";
import { listOrders } from "@/features/orders/api";
import { paymentLabel } from "@/features/orders/labels";
import { ApiError } from "@/lib/api/errors";
import { formatCurrency, formatDateTime, shortId } from "@/lib/formatters";

export default function FinancePage() {
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [selected, setSelected] = useState<string[]>([]);
  const [lastBatchId, setLastBatchId] = useState<string | null>(null);

  const ordersQuery = useQuery({
    queryKey: ["orders", establishment?.id, "finance"],
    enabled: Boolean(establishment?.id),
    queryFn: () => listOrders(establishment!.id, { page: 1, pageSize: 100 }),
  });
  const receivablesQuery = useQuery({
    queryKey: ["receivables", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => listPendingReceivables(establishment!.id),
  });

  const summary = useMemo(() => {
    const orders = ordersQuery.data?.items ?? [];
    const paid = orders.filter((order) => order.paymentStatus === "Paid");
    const byMethod = new Map<string, number>();
    for (const order of orders) {
      byMethod.set(order.paymentMethod, (byMethod.get(order.paymentMethod) ?? 0) + order.total);
    }
    return {
      total: orders.reduce((sum, order) => sum + order.total, 0),
      paid: paid.reduce((sum, order) => sum + order.total, 0),
      byMethod,
    };
  }, [ordersQuery.data]);

  const grouped = useMemo(() => {
    const items = receivablesQuery.data ?? [];
    const map = new Map<string, typeof items>();
    for (const item of items) {
      const list = map.get(item.driverId) ?? [];
      list.push(item);
      map.set(item.driverId, list);
    }
    return map;
  }, [receivablesQuery.data]);

  const createBatch = useMutation({
    mutationFn: () => {
      const first = (receivablesQuery.data ?? []).find((item) =>
        selected.includes(item.id),
      );
      if (!first) throw new Error("Selecione recebíveis do mesmo entregador.");
      return createPaymentBatch(establishment!.id, {
        driverId: first.driverId,
        receivableIds: selected,
      });
    },
    onSuccess: (batch) => {
      toast.push("Lote criado.", "success");
      setLastBatchId(batch.id);
      setSelected([]);
      queryClient.invalidateQueries({ queryKey: ["receivables"] });
    },
    onError: (error: Error) =>
      toast.push(error instanceof ApiError ? error.message : error.message, "error"),
  });

  const report = useMutation({
    mutationFn: () => reportPayment(establishment!.id, lastBatchId!),
    onSuccess: () => toast.push("Pagamento reportado ao entregador.", "success"),
    onError: (error: Error) =>
      toast.push(error instanceof ApiError ? error.message : error.message, "error"),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Financeiro</h1>
        <p className="text-sm text-slate-500">
          Resumo derivado dos pedidos. Não há gateway de pagamento. Lotes históricos do
          comércio não têm endpoint de listagem — só criar e reportar.
        </p>
      </div>

      {ordersQuery.isLoading ? <Skeleton className="h-32" /> : null}
      {ordersQuery.error ? <ErrorBox message={ordersQuery.error.message} /> : null}
      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard label="Total listado" value={formatCurrency(summary.total)} />
        <StatCard label="Pedidos pagos (Pix confirmado)" value={formatCurrency(summary.paid)} />
        <StatCard
          label="Recebíveis pendentes"
          value={String(receivablesQuery.data?.length ?? 0)}
        />
      </div>

      <Card>
        <h2 className="mb-3 font-semibold">Por forma de pagamento</h2>
        <ul className="space-y-1 text-sm">
          {[...summary.byMethod.entries()].map(([method, amount]) => (
            <li key={method} className="flex justify-between">
              <span>{paymentLabel(method)}</span>
              <span>{formatCurrency(amount)}</span>
            </li>
          ))}
        </ul>
      </Card>

      <Card className="space-y-3">
        <h2 className="font-semibold">Repasse a entregadores</h2>
        {receivablesQuery.isLoading ? <Skeleton className="h-24" /> : null}
        {receivablesQuery.data?.length === 0 ? (
          <EmptyState title="Nenhum recebível pendente" message="Quando houver entregas concluídas, elas aparecem aqui." />
        ) : null}
        {[...grouped.entries()].map(([driverId, items]) => (
          <div key={driverId} className="rounded-xl border border-slate-100 p-3">
            <p className="mb-2 text-sm font-medium">Entregador {shortId(driverId)}</p>
            {items?.map((item) => (
              <label key={item.id} className="flex items-center justify-between gap-3 text-sm">
                <span>
                  <input
                    type="checkbox"
                    className="mr-2"
                    checked={selected.includes(item.id)}
                    onChange={(event) => {
                      setSelected((current) =>
                        event.target.checked
                          ? [...current, item.id]
                          : current.filter((id) => id !== item.id),
                      );
                    }}
                  />
                  {formatCurrency(item.amount)} · {formatDateTime(item.createdAtUtc)}
                </span>
                <span className="text-slate-500">{item.status}</span>
              </label>
            ))}
          </div>
        ))}
        <div className="flex flex-wrap gap-2">
          <Button disabled={selected.length === 0} loading={createBatch.isPending} onClick={() => createBatch.mutate()}>
            Criar lote
          </Button>
          <Button
            variant="secondary"
            disabled={!lastBatchId}
            loading={report.isPending}
            onClick={() => report.mutate()}
          >
            Reportar pagamento do último lote
          </Button>
        </div>
      </Card>
    </div>
  );
}
