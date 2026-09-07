"use client";

import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Input, Select } from "@/components/ui/input";
import { Pagination } from "@/components/ui/pagination";
import { listCities } from "@/features/cities/api";
import { listAllEstablishments } from "@/features/establishments/api";
import { listAdminOrders } from "@/features/orders/api";
import { ORDER_STATUS_LABEL } from "@/features/orders/labels";
import { OrderTable } from "@/features/orders/order-table";

export default function OrdersPage() {
  const [establishmentId, setEstablishmentId] = useState("");
  const [status, setStatus] = useState("");
  const [cityId, setCityId] = useState("");
  const [type, setType] = useState("");
  const [date, setDate] = useState("");
  const [page, setPage] = useState(1);

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const establishmentsQuery = useQuery({
    queryKey: ["admin", "establishments", "all"],
    queryFn: listAllEstablishments,
  });
  const query = useQuery({
    queryKey: ["admin", "orders", establishmentId, status, page],
    queryFn: () =>
      listAdminOrders({
        establishmentId: establishmentId || undefined,
        status: status || undefined,
        page,
        pageSize: 20,
      }),
  });

  const cityOfEstablishment = useMemo(
    () => new Map((establishmentsQuery.data ?? []).map((item) => [item.id, item.cityId])),
    [establishmentsQuery.data],
  );

  const items = (query.data?.items ?? []).filter((order) => {
    if (cityId && (order.cityId ?? cityOfEstablishment.get(order.establishmentId)) !== cityId) {
      return false;
    }
    if (type && order.type !== type) return false;
    if (date && !order.createdAtUtc.startsWith(date)) return false;
    return true;
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Pedidos</h1>
        <p className="text-sm text-slate-500">
          Consulta via GET /api/admin/orders (establishmentId e status). Cidade, tipo e data são
          filtros locais da página. Sem ações de fluxo — o Super Admin não altera o pedido aqui.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        <Select
          value={establishmentId}
          onChange={(event) => {
            setPage(1);
            setEstablishmentId(event.target.value);
          }}
        >
          <option value="">Todos os estabelecimentos</option>
          {(establishmentsQuery.data ?? []).map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
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
          {Object.entries(ORDER_STATUS_LABEL).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
        <Select value={cityId} onChange={(event) => setCityId(event.target.value)}>
          <option value="">Todas as cidades (local)</option>
          {(citiesQuery.data ?? []).map((city) => (
            <option key={city.id} value={city.id}>
              {city.name}/{city.state}
            </option>
          ))}
        </Select>
        <Select value={type} onChange={(event) => setType(event.target.value)}>
          <option value="">Delivery e Pickup</option>
          <option value="Delivery">Entrega</option>
          <option value="Pickup">Retirada</option>
        </Select>
        <Input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
      </div>

      {query.isLoading ? (
        <Skeleton className="h-80" />
      ) : query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState title="Nenhum pedido" message="Ajuste os filtros ou aguarde novos pedidos." />
      ) : (
        <>
          <OrderTable orders={items} establishments={establishmentsQuery.data ?? []} />
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
