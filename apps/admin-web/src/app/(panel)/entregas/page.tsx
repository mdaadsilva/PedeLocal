"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Select } from "@/components/ui/input";
import { Pagination } from "@/components/ui/pagination";
import { listCities } from "@/features/cities/api";
import { listAdminDeliveries } from "@/features/deliveries/api";
import { listAllEstablishments } from "@/features/establishments/api";
import { DELIVERY_STATUS_LABEL, labelOf } from "@/features/orders/labels";
import { formatDateTime, shortId } from "@/lib/formatters";

export default function DeliveriesPage() {
  const [cityId, setCityId] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const establishmentsQuery = useQuery({
    queryKey: ["admin", "establishments", "all"],
    queryFn: listAllEstablishments,
  });
  const query = useQuery({
    queryKey: ["admin", "deliveries", cityId, status, page],
    queryFn: () =>
      listAdminDeliveries({
        cityId: cityId || undefined,
        status: status || undefined,
        page,
        pageSize: 20,
      }),
  });

  const cityNames = new Map((citiesQuery.data ?? []).map((city) => [city.id, `${city.name}/${city.state}`]));
  const establishmentNames = new Map((establishmentsQuery.data ?? []).map((item) => [item.id, item.name]));

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Entregas</h1>
        <p className="text-sm text-slate-500">
          GET /api/admin/deliveries?cityId=&status=. A resposta traz IDs e status, sem endereço ou rota.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Select
          value={cityId}
          onChange={(event) => {
            setPage(1);
            setCityId(event.target.value);
          }}
        >
          <option value="">Todas as cidades</option>
          {(citiesQuery.data ?? []).map((city) => (
            <option key={city.id} value={city.id}>
              {city.name}/{city.state}
            </option>
          ))}
        </Select>
        <Select
          value={status}
          onChange={(event) => {
            setPage(1);
            setStatus(event.target.value);
          }}
        >
          <option value="">Todos os status</option>
          {Object.entries(DELIVERY_STATUS_LABEL).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
      </div>

      {query.isLoading ? (
        <Skeleton className="h-80" />
      ) : query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <EmptyState title="Nenhuma entrega" message="Quando houver entregas, a lista aparece aqui." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Entrega</th>
                  <th className="px-4 py-3">Pedido</th>
                  <th className="px-4 py-3">Estabelecimento</th>
                  <th className="px-4 py-3">Cidade</th>
                  <th className="px-4 py-3">Entregador</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Atribuída</th>
                </tr>
              </thead>
              <tbody>
                {query.data!.items.map((delivery) => (
                  <tr key={delivery.id} className="border-t border-slate-100">
                    <td className="px-4 py-3 font-medium">#{shortId(delivery.id)}</td>
                    <td className="px-4 py-3">#{shortId(delivery.orderId)}</td>
                    <td className="px-4 py-3">
                      {establishmentNames.get(delivery.establishmentId) ?? shortId(delivery.establishmentId)}
                    </td>
                    <td className="px-4 py-3">{cityNames.get(delivery.cityId) ?? "—"}</td>
                    <td className="px-4 py-3">
                      {delivery.driverId ? shortId(delivery.driverId) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <Badge value={delivery.status} label={labelOf(DELIVERY_STATUS_LABEL, delivery.status)} />
                    </td>
                    <td className="px-4 py-3 text-slate-600">
                      {delivery.assignedAtUtc ? formatDateTime(delivery.assignedAtUtc) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={query.data!.page}
            totalPages={query.data!.totalPages}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}
