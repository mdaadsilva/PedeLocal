"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { listAllEstablishments } from "@/features/establishments/api";
import { SUBSCRIPTION_STATUS_LABEL, labelOf } from "@/features/orders/labels";
import { getSubscriptionOrNull } from "@/features/subscriptions/api";
import { addDaysToDateOnly, formatCurrency, formatDateOnly } from "@/lib/formatters";
import { mapPool } from "@/lib/pool";

export default function SubscriptionsPage() {
  const query = useQuery({
    queryKey: ["admin", "subscriptions", "overview"],
    queryFn: async () => {
      const establishments = await listAllEstablishments();
      const subscriptions = await mapPool(establishments, 4, async (item) => ({
        establishment: item,
        subscription: await getSubscriptionOrNull(item.id),
      }));
      return subscriptions;
    },
  });

  if (query.isLoading) return <Skeleton className="h-80" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;

  const rows = query.data ?? [];

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Assinaturas</h1>
        <p className="text-sm text-slate-500">
          Não existe listagem global. Cada linha vem de GET /api/admin/establishments/{"{id}"}/subscription.
          Após o vencimento, o backend usa 5 dias de graça por padrão e então bloqueia novos pedidos sem apagar dados.
        </p>
      </div>

      {rows.length === 0 ? (
        <EmptyState title="Nenhum estabelecimento" message="Cadastre lojas para configurar assinaturas." />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Estabelecimento</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Valor</th>
                <th className="px-4 py-3">Vencimento</th>
                <th className="px-4 py-3">Fim da graça</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(({ establishment, subscription }) => (
                <tr key={establishment.id} className="border-t border-slate-100">
                  <td className="px-4 py-3">
                    <Link
                      className="font-medium text-brand-700 hover:underline"
                      href={`/estabelecimentos/${establishment.id}`}
                    >
                      {establishment.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3">
                    {subscription ? (
                      <Badge
                        value={subscription.paymentStatus}
                        label={labelOf(SUBSCRIPTION_STATUS_LABEL, subscription.paymentStatus)}
                      />
                    ) : (
                      <span className="text-slate-500">Não configurada</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {subscription ? formatCurrency(subscription.monthlyAmount) : "—"}
                  </td>
                  <td className="px-4 py-3">
                    {subscription ? formatDateOnly(subscription.dueDate) : "—"}
                  </td>
                  <td className="px-4 py-3">
                    {subscription
                      ? formatDateOnly(addDaysToDateOnly(subscription.dueDate, subscription.graceDays))
                      : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
