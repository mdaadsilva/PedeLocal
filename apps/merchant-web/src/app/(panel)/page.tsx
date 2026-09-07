"use client";

import { useQuery } from "@tanstack/react-query";
import { StatCard } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useSession } from "@/features/auth/session-context";
import { listOrders } from "@/features/orders/api";
import { OrderTable } from "@/features/orders/order-table";
import { formatCurrency, isSameSaoPauloDay } from "@/lib/formatters";

const COMPLETED = new Set(["Delivered", "PickupCompleted"]);

export default function DashboardPage() {
  const { establishment } = useSession();
  const ordersQuery = useQuery({
    queryKey: ["orders", establishment?.id, "dashboard"],
    enabled: Boolean(establishment?.id),
    queryFn: () =>
      listOrders(establishment!.id, { page: 1, pageSize: 100 }),
  });

  if (!establishment) {
    return (
      <EmptyState
        title="Nenhum estabelecimento vinculado"
        message="O backend ainda não expõe GET /api/me/establishments. Não encontramos uma loja acessível para esta conta."
      />
    );
  }

  if (ordersQuery.isLoading) {
    return <Skeleton className="h-72" />;
  }
  if (ordersQuery.error) {
    return <ErrorBox message={ordersQuery.error.message} onRetry={() => ordersQuery.refetch()} />;
  }

  const orders = ordersQuery.data?.items ?? [];
  const today = orders.filter((order) => isSameSaoPauloDay(order.createdAtUtc));
  const pending = today.filter((order) => order.status === "Pending").length;
  const preparing = today.filter((order) =>
    ["Accepted", "Preparing"].includes(order.status),
  ).length;
  const ready = today.filter((order) => order.status === "Ready").length;
  const completed = today.filter((order) => COMPLETED.has(order.status));
  const revenue = completed.reduce((sum, order) => sum + order.total, 0);
  const ticket = completed.length ? revenue / completed.length : 0;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900">Dashboard</h1>
        <p className="text-sm text-slate-500">
          Indicadores do dia, calculados a partir de GET /api/merchant/.../orders.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Pedidos de hoje" value={String(today.length)} />
        <StatCard label="Pendentes" value={String(pending)} />
        <StatCard label="Em preparação" value={String(preparing)} />
        <StatCard label="Prontos" value={String(ready)} />
        <StatCard label="Concluídos hoje" value={String(completed.length)} />
        <StatCard label="Faturamento do dia" value={formatCurrency(revenue)} hint="Pedidos entregues ou retirados" />
        <StatCard label="Ticket médio" value={formatCurrency(ticket)} hint="Sobre pedidos concluídos hoje" />
      </div>
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Últimos pedidos</h2>
        {orders.length === 0 ? (
          <EmptyState title="Nenhum pedido" message="Quando os clientes pedirem, a lista aparece aqui." />
        ) : (
          <OrderTable orders={orders.slice(0, 8)} />
        )}
      </section>
    </div>
  );
}
