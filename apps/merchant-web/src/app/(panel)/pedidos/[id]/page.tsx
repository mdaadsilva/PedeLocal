"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { Textarea } from "@/components/ui/input";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import { getOrder, getOrderOccurrences, runOrderAction } from "@/features/orders/api";
import { OCCURRENCE_TYPE_LABEL, labelOf, orderStatusLabel, paymentLabel } from "@/features/orders/labels";
import { ACTION_LABELS, merchantActionsFor, type MerchantOrderAction } from "@/lib/order-actions";
import { formatCurrency, formatDateTime, shortId } from "@/lib/formatters";
import { ApiError } from "@/lib/api/errors";

export default function OrderDetailPage() {
  const params = useParams<{ id: string }>();
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [occurrencesOpen, setOccurrencesOpen] = useState(false);

  const query = useQuery({
    queryKey: ["order", establishment?.id, params.id],
    enabled: Boolean(establishment?.id && params.id),
    queryFn: () => getOrder(establishment!.id, params.id),
  });

  const occurrencesQuery = useQuery({
    queryKey: ["order", establishment?.id, params.id, "occurrences"],
    enabled: Boolean(establishment?.id && params.id && occurrencesOpen),
    queryFn: () => getOrderOccurrences(establishment!.id, params.id),
  });

  const mutation = useMutation({
    mutationFn: (action: MerchantOrderAction) =>
      runOrderAction(establishment!.id, params.id, action, reason || undefined),
    onSuccess: () => {
      toast.push("Status atualizado.", "success");
      setCancelOpen(false);
      queryClient.invalidateQueries({ queryKey: ["order", establishment?.id, params.id] });
      queryClient.invalidateQueries({ queryKey: ["orders"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;
  const order = query.data;
  if (!order) return null;

  const actions = merchantActionsFor(order.status, order.type);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm text-slate-500">Pedido #{shortId(order.id)}</p>
          <h1 className="text-2xl font-semibold">{formatDateTime(order.createdAtUtc)}</h1>
        </div>
        <div className="flex gap-2">
          <Badge value={order.type} label={order.type === "Pickup" ? "Retirada" : "Entrega"} />
          <Badge value={order.status} label={orderStatusLabel(order.status)} />
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2 space-y-3">
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
            <p className="flex justify-between"><span>Subtotal</span><span>{formatCurrency(order.subtotal)}</span></p>
            <p className="flex justify-between"><span>Taxa de entrega</span><span>{formatCurrency(order.deliveryFee)}</span></p>
            <p className="mt-1 flex justify-between font-semibold"><span>Total</span><span>{formatCurrency(order.total)}</span></p>
          </div>
        </Card>

        <div className="space-y-4">
          <Card className="space-y-2 text-sm">
            <h2 className="font-semibold">Cliente e pagamento</h2>
            <p>Cliente: {shortId(order.customerUserId)}</p>
            <p className="text-xs text-slate-500">
              O contrato do pedido não traz nome/telefone do cliente.
            </p>
            <p>Pagamento: {paymentLabel(order.paymentMethod)}</p>
            <p>Status do pagamento: {order.paymentStatus}</p>
            {order.changeForAmount ? <p>Troco para {formatCurrency(order.changeForAmount)}</p> : null}
            {order.notes ? <p>Obs.: {order.notes}</p> : null}
            {order.type === "Delivery" ? (
              <>
                <p>Endereço: {order.addressId ? `#${shortId(order.addressId)}` : "não informado"}</p>
                <Button variant="ghost" onClick={() => setOccurrencesOpen(true)}>
                  Ver ocorrências relatadas pelo entregador
                </Button>
              </>
            ) : (
              <p>Retirada no estabelecimento. Sem entregador.</p>
            )}
          </Card>

          <Card className="space-y-2">
            <h2 className="font-semibold">Ações</h2>
            {actions.length === 0 ? (
              <p className="text-sm text-slate-500">
                Nenhuma ação do comércio neste status. Transições do entregador ficam no app do motorista.
              </p>
            ) : (
              <div className="flex flex-col gap-2">
                {actions.map((action) =>
                  action === "cancel" ? (
                    <Button key={action} variant="danger" onClick={() => setCancelOpen(true)}>
                      {ACTION_LABELS[action]}
                    </Button>
                  ) : (
                    <Button
                      key={action}
                      variant={action === "complete-pickup" ? "primary" : "secondary"}
                      loading={mutation.isPending}
                      onClick={() => mutation.mutate(action)}
                    >
                      {ACTION_LABELS[action]}
                    </Button>
                  ),
                )}
              </div>
            )}
          </Card>
        </div>
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

      {cancelOpen ? (
        <Modal title="Cancelar pedido" onClose={() => setCancelOpen(false)}>
          <Textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Motivo do cancelamento"
          />
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCancelOpen(false)}>
              Voltar
            </Button>
            <Button
              variant="danger"
              loading={mutation.isPending}
              onClick={() => mutation.mutate("cancel")}
            >
              Confirmar cancelamento
            </Button>
          </div>
        </Modal>
      ) : null}

      {occurrencesOpen ? (
        <Modal title="Ocorrências da entrega" onClose={() => setOccurrencesOpen(false)}>
          {occurrencesQuery.isLoading ? (
            <Skeleton className="h-32" />
          ) : occurrencesQuery.error ? (
            <ErrorBox message={occurrencesQuery.error.message} onRetry={() => occurrencesQuery.refetch()} />
          ) : (occurrencesQuery.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-slate-500">O entregador não relatou nenhum problema nesta entrega.</p>
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
