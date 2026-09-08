"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { StatCard } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { listCities } from "@/features/cities/api";
import { deriveDashboard } from "@/features/dashboard/metrics";
import { listCustomers } from "@/features/users/api";
import { listDrivers } from "@/features/drivers/api";
import { listAllEstablishments } from "@/features/establishments/api";
import { listAdminOrders } from "@/features/orders/api";
import { OrderTable } from "@/features/orders/order-table";
import { getSubscriptionOrNull } from "@/features/subscriptions/api";
import { formatCurrency, formatDateTime } from "@/lib/formatters";
import { mapPool } from "@/lib/pool";

export default function DashboardPage() {
  const query = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: async () => {
      const [cities, establishments, drivers, customers, orders] = await Promise.all([
        listCities(),
        listAllEstablishments(),
        listDrivers({ page: 1, pageSize: 1 }),
        listCustomers({ page: 1, pageSize: 1 }),
        listAdminOrders({ page: 1, pageSize: 100 }),
      ]);
      const subscriptions = await mapPool(establishments, 4, (item) =>
        getSubscriptionOrNull(item.id),
      );
      return { cities, establishments, drivers, customers, orders, subscriptions };
    },
  });

  if (query.isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-10 w-56" />
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, index) => (
            <Skeleton key={index} className="h-28" />
          ))}
        </div>
      </div>
    );
  }

  if (query.error) {
    return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;
  }

  const data = query.data;
  if (!data) return null;

  const stats = deriveDashboard({
    cities: data.cities,
    establishments: data.establishments,
    driversTotal: data.drivers.totalCount,
    customersTotal: data.customers.totalCount,
    orders: data.orders.items,
    ordersTotal: data.orders.totalCount,
    subscriptions: data.subscriptions,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900">Dashboard</h1>
        <p className="text-sm text-slate-500">
          Indicadores derivados das listagens admin existentes. Não há endpoint de métricas.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Cidades" value={String(stats.citiesTotal)} />
        <StatCard label="Estabelecimentos" value={String(stats.establishmentsTotal)} />
        <StatCard label="Estabelecimentos ativos" value={String(stats.activeEstablishments)} hint="commercialStatus = Active" />
        <StatCard label="Estabelecimentos bloqueados" value={String(stats.blockedEstablishments)} hint="SuspendedPayment, SuspendedAdmin ou Inactive" />
        <StatCard label="Entregadores" value={String(stats.driversTotal)} />
        <StatCard label="Clientes" value={String(stats.customersTotal)} hint="GET /api/admin/customers" />
        <StatCard label="Pedidos hoje" value={String(stats.ordersToday)} hint="Entre os até 100 mais recentes" />
        <StatCard label="Pedidos no recorte" value={String(stats.ordersListed)} hint="Página atual da listagem admin (máx. 100)" />
        <StatCard label="Volume total de pedidos" value={String(stats.ordersTotal)} hint="totalCount de GET /api/admin/orders" />
        <StatCard
          label="Volume financeiro listado"
          value={formatCurrency(stats.listedVolume)}
          hint="Soma dos totais dos pedidos carregados. Não é faturamento da plataforma."
        />
        <StatCard label="Assinaturas ativas" value={String(stats.subscriptionsActive)} />
        <StatCard label="Em período de graça" value={String(stats.subscriptionsGrace)} />
        <StatCard label="Assinaturas bloqueadas" value={String(stats.subscriptionsBlocked)} />
        {stats.subscriptionsMissing > 0 ? (
          <StatCard
            label="Sem assinatura configurada"
            value={String(stats.subscriptionsMissing)}
            hint="GET subscription retornou SUBSCRIPTION_NOT_FOUND"
          />
        ) : null}
      </div>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Últimos estabelecimentos</h2>
          <Link className="text-sm font-medium text-brand-700" href="/estabelecimentos">
            Ver todos
          </Link>
        </div>
        {stats.recentEstablishments.length === 0 ? (
          <EmptyState title="Nenhum estabelecimento" message="Cadastre o primeiro pelo painel." />
        ) : (
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Nome</th>
                  <th className="px-4 py-3">Status comercial</th>
                  <th className="px-4 py-3">Cadastro</th>
                </tr>
              </thead>
              <tbody>
                {stats.recentEstablishments.map((item) => (
                  <tr key={item.id} className="border-t border-slate-100">
                    <td className="px-4 py-3">
                      <Link className="font-medium text-brand-700 hover:underline" href={`/estabelecimentos/${item.id}`}>
                        {item.name}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{item.commercialStatus}</td>
                    <td className="px-4 py-3 text-slate-600">{formatDateTime(item.createdAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Últimos pedidos</h2>
          <Link className="text-sm font-medium text-brand-700" href="/pedidos">
            Ver todos
          </Link>
        </div>
        {stats.recentOrders.length === 0 ? (
          <EmptyState title="Nenhum pedido" message="Os pedidos da plataforma aparecem aqui." />
        ) : (
          <OrderTable orders={stats.recentOrders} establishments={data.establishments} />
        )}
      </section>
    </div>
  );
}
