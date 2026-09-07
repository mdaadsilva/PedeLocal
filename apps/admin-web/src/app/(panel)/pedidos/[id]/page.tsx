"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { getEstablishment } from "@/features/establishments/api";
import { getMerchantOrder, listAdminOrders } from "@/features/orders/api";
import { orderStatusLabel, paymentLabel } from "@/features/orders/labels";
import { formatCurrency, formatDateTime, shortId } from "@/lib/formatters";
import type { Order } from "@/lib/types";

async function loadAdminOrder(orderId: string, establishmentId?: string | null): Promise<Order> {
  if (establishmentId) {
    return getMerchantOrder(establishmentId, orderId);
  }

  const page = await listAdminOrders({ page: 1, pageSize: 100 });
  const found = page.items.find((item) => item.id === orderId);
  if (!found) {
    throw new Error(
      "Pedido não encontrado na primeira página da listagem admin. Não existe GET /api/admin/orders/{id}. Abra o pedido a partir da lista.",
    );
  }
  return getMerchantOrder(found.establishmentId, found.id);
}

function OrderDetail() {
  const params = useParams<{ id: string }>();
  const search = useSearchParams();
  const establishmentId = search.get("establishmentId");

  const query = useQuery({
    queryKey: ["admin", "order", params.id, establishmentId],
    queryFn: () => loadAdminOrder(params.id, establishmentId),
  });
  const establishmentQuery = useQuery({
    queryKey: ["admin", "establishment", query.data?.establishmentId],
    enabled: Boolean(query.data?.establishmentId),
    queryFn: () => getEstablishment(query.data!.establishmentId),
  });

  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;
  const order = query.data;
  if (!order) return null;

  return (
    <div className="space-y-5">
      <div>
        <Link className="text-sm text-brand-700" href="/pedidos">
          ← Pedidos
        </Link>
        <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-sm text-slate-500">Pedido #{shortId(order.id)}</p>
            <h1 className="text-2xl font-semibold">{formatDateTime(order.createdAtUtc)}</h1>
            <p className="text-sm text-slate-500">
              {establishmentQuery.data?.name ?? shortId(order.establishmentId)}
            </p>
          </div>
          <div className="flex gap-2">
            <Badge value={order.type} label={order.type === "Pickup" ? "Retirada" : "Entrega"} />
            <Badge value={order.status} label={orderStatusLabel(order.status)} />
          </div>
        </div>
      </div>

      <EmptyState
        title="Consulta apenas"
        message="O painel Super Admin não executa transições de pedido. O detalhe usa GET merchant, permitido para SUPER_ADMIN pelo EstablishmentAccessGuard."
      />

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="space-y-3 lg:col-span-2">
          <h2 className="font-semibold">Itens</h2>
          {order.items.map((item) => (
            <div key={item.id} className="flex justify-between gap-4 text-sm">
              <div>
                <p className="font-medium">
                  {item.quantity}x {item.productName}
                </p>
                {item.options.length > 0 ? (
                  <p className="text-slate-500">
                    {item.options.map((option) => option.optionName).join(", ")}
                  </p>
                ) : null}
              </div>
              <p>{formatCurrency(item.lineTotal)}</p>
            </div>
          ))}
          <div className="border-t border-slate-100 pt-3 text-sm">
            <p className="flex justify-between">
              <span>Subtotal</span>
              <span>{formatCurrency(order.subtotal)}</span>
            </p>
            <p className="flex justify-between">
              <span>Taxa de entrega</span>
              <span>{formatCurrency(order.deliveryFee)}</span>
            </p>
            <p className="mt-1 flex justify-between font-semibold">
              <span>Total</span>
              <span>{formatCurrency(order.total)}</span>
            </p>
          </div>
        </Card>

        <Card className="space-y-2 text-sm">
          <h2 className="font-semibold">Pagamento e entrega</h2>
          <p>Cliente: {shortId(order.customerUserId)}</p>
          <p>Pagamento: {paymentLabel(order.paymentMethod)}</p>
          <p>Status do pagamento: {order.paymentStatus}</p>
          {order.changeForAmount ? <p>Troco para {formatCurrency(order.changeForAmount)}</p> : null}
          {order.notes ? <p>Obs.: {order.notes}</p> : null}
          {order.type === "Delivery" ? (
            <p>Endereço: {order.addressId ? `#${shortId(order.addressId)}` : "não informado"}</p>
          ) : (
            <p>Retirada no estabelecimento.</p>
          )}
          {order.calculatedDistanceKm != null ? (
            <p>Distância calculada: {order.calculatedDistanceKm} km</p>
          ) : null}
        </Card>
      </div>

      <Card>
        <h2 className="mb-3 font-semibold">Histórico</h2>
        <ol className="space-y-2 text-sm">
          {order.statusHistory.map((entry) => (
            <li key={`${entry.status}-${entry.changedAtUtc}`}>
              <span className="font-medium">{orderStatusLabel(entry.status)}</span>
              <span className="text-slate-500"> · {formatDateTime(entry.changedAtUtc)}</span>
              {entry.notes ? <p className="text-slate-500">{entry.notes}</p> : null}
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
}

export default function AdminOrderDetailPage() {
  return (
    <Suspense fallback={<Skeleton className="h-96" />}>
      <OrderDetail />
    </Suspense>
  );
}
