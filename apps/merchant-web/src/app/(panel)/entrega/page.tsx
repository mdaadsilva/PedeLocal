"use client";

import { useQuery } from "@tanstack/react-query";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useSession } from "@/features/auth/session-context";
import { listOrders } from "@/features/orders/api";
import { OrderTable } from "@/features/orders/order-table";

const DELIVERY_STATUSES = [
  "WaitingDriver",
  "DriverAssigned",
  "PickedUp",
  "OutForDelivery",
];

export default function DeliveryPage() {
  const { establishment } = useSession();
  const query = useQuery({
    queryKey: ["orders", establishment?.id, "delivery"],
    enabled: Boolean(establishment?.id),
    queryFn: () => listOrders(establishment!.id, { page: 1, pageSize: 100 }),
  });

  const deliveries = (query.data?.items ?? []).filter(
    (order) => order.type === "Delivery" && DELIVERY_STATUSES.includes(order.status),
  );

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Entregas em andamento</h1>
        <p className="text-sm text-slate-500">
          Acompanhamento pelo status do pedido. Não há endpoint merchant de detalhes do
          entregador nem ações de motorista (aceitar/retirar/confirmar).
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
      {deliveries.length > 0 ? <OrderTable orders={deliveries} /> : null}
    </div>
  );
}
