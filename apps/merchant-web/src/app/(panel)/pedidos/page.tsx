"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useSession } from "@/features/auth/session-context";
import { listOrders } from "@/features/orders/api";
import { ORDER_STATUS_LABEL } from "@/features/orders/labels";
import { OrderTable } from "@/features/orders/order-table";

export default function OrdersPage() {
  const { establishment } = useSession();
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["orders", establishment?.id, status, page],
    enabled: Boolean(establishment?.id),
    queryFn: () =>
      listOrders(establishment!.id, {
        status: status || undefined,
        page,
        pageSize: 20,
      }),
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Pedidos</h1>
          <p className="text-sm text-slate-500">Filtre por status e abra o detalhe para avançar o fluxo.</p>
        </div>
        <Select
          value={status}
          onChange={(event) => {
            setStatus(event.target.value);
            setPage(1);
          }}
        >
          <option value="">Todos os status</option>
          {Object.entries(ORDER_STATUS_LABEL).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
      </div>

      {query.isLoading ? <Skeleton className="h-80" /> : null}
      {query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : null}
      {query.data && query.data.items.length === 0 ? (
        <EmptyState title="Nenhum pedido neste filtro" message="Ajuste o status ou aguarde novos pedidos." />
      ) : null}
      {query.data && query.data.items.length > 0 ? (
        <>
          <OrderTable orders={query.data.items} />
          <div className="flex items-center justify-between text-sm text-slate-500">
            <span>
              Página {query.data.page} de {query.data.totalPages || 1}
            </span>
            <div className="flex gap-2">
              <Button
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage((current) => current - 1)}
              >
                Anterior
              </Button>
              <Button
                variant="secondary"
                disabled={page >= (query.data.totalPages || 1)}
                onClick={() => setPage((current) => current + 1)}
              >
                Próxima
              </Button>
            </div>
          </div>
        </>
      ) : null}
    </div>
  );
}
