"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Field, Input, Select, Textarea } from "@/components/ui/input";
import { useToast } from "@/components/ui/toast";
import { SUBSCRIPTION_STATUS_LABEL, labelOf } from "@/features/orders/labels";
import {
  getSubscriptionOrNull,
  listSubscriptionPayments,
  registerSubscriptionPayment,
  upsertSubscription,
} from "@/features/subscriptions/api";
import { ApiError } from "@/lib/api/errors";
import { addDaysToDateOnly, formatCurrency, formatDateOnly, formatDateTime, shortId } from "@/lib/formatters";

export function SubscriptionPanel({ establishmentId }: { establishmentId: string }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const subscriptionQuery = useQuery({
    queryKey: ["admin", "subscription", establishmentId],
    queryFn: () => getSubscriptionOrNull(establishmentId),
  });
  const paymentsQuery = useQuery({
    queryKey: ["admin", "subscription-payments", establishmentId],
    queryFn: () => listSubscriptionPayments(establishmentId),
    enabled: subscriptionQuery.data !== null,
  });

  const subscription = subscriptionQuery.data;
  const [monthlyAmount, setMonthlyAmount] = useState("");
  const [billingDay, setBillingDay] = useState("10");
  const [dueDate, setDueDate] = useState("");
  const [graceDays, setGraceDays] = useState("5");
  const [amount, setAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("Pix");
  const [notes, setNotes] = useState("");

  const hydrated = subscription
    ? `${subscription.id}:${subscription.dueDate}:${subscription.monthlyAmount}`
    : "none";
  const [lastHydrated, setLastHydrated] = useState("");
  if (hydrated !== lastHydrated) {
    setLastHydrated(hydrated);
    if (subscription) {
      setMonthlyAmount(String(subscription.monthlyAmount));
      setBillingDay(String(subscription.billingDay));
      setDueDate(subscription.dueDate);
      setGraceDays(String(subscription.graceDays));
    }
  }

  const saveMutation = useMutation({
    mutationFn: () =>
      upsertSubscription(establishmentId, {
        monthlyAmount: Number(monthlyAmount),
        billingDay: Number(billingDay),
        dueDate,
        graceDays: Number(graceDays),
      }),
    onSuccess: () => {
      toast.push("Assinatura salva.", "success");
      queryClient.invalidateQueries({ queryKey: ["admin", "subscription", establishmentId] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const payMutation = useMutation({
    mutationFn: () =>
      registerSubscriptionPayment(establishmentId, {
        amount: Number(amount),
        paymentMethod,
        paidAtUtc: new Date().toISOString(),
        notes: notes || null,
      }),
    onSuccess: () => {
      toast.push("Pagamento registrado. A assinatura foi regularizada no backend.", "success");
      setAmount("");
      setNotes("");
      queryClient.invalidateQueries({ queryKey: ["admin", "subscription", establishmentId] });
      queryClient.invalidateQueries({ queryKey: ["admin", "subscription-payments", establishmentId] });
      queryClient.invalidateQueries({ queryKey: ["admin", "establishment", establishmentId] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  if (subscriptionQuery.isLoading) return <Skeleton className="h-64" />;
  if (subscriptionQuery.error) {
    return (
      <ErrorBox
        message={subscriptionQuery.error.message}
        onRetry={() => subscriptionQuery.refetch()}
      />
    );
  }

  const graceUntil = subscription
    ? addDaysToDateOnly(subscription.dueDate, subscription.graceDays)
    : null;

  return (
    <div className="space-y-4">
      <Card className="space-y-3">
        {subscription ? (
          <div className="flex flex-wrap items-center gap-3">
            <Badge
              value={subscription.paymentStatus}
              label={labelOf(SUBSCRIPTION_STATUS_LABEL, subscription.paymentStatus)}
            />
            <p className="text-sm text-slate-600">
              Vencimento {formatDateOnly(subscription.dueDate)} · graça até{" "}
              {graceUntil ? formatDateOnly(graceUntil) : "—"} ({subscription.graceDays} dias)
            </p>
          </div>
        ) : (
          <EmptyState
            title="Assinatura não configurada"
            message="O backend retorna SUBSCRIPTION_NOT_FOUND até o primeiro PUT."
          />
        )}
        <p className="text-sm text-slate-500">
          Regularizar = POST /subscription/payments. Isso marca a assinatura como Active, avança o
          vencimento em um mês e reativa o estabelecimento se estiver suspenso por pagamento.
        </p>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="Valor mensal">
            <Input
              type="number"
              min="0"
              step="0.01"
              value={monthlyAmount}
              onChange={(event) => setMonthlyAmount(event.target.value)}
              required
            />
          </Field>
          <Field label="Dia de cobrança">
            <Input
              type="number"
              min="1"
              max="28"
              value={billingDay}
              onChange={(event) => setBillingDay(event.target.value)}
              required
            />
          </Field>
          <Field label="Vencimento">
            <Input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} required />
          </Field>
          <Field label="Dias de graça">
            <Input
              type="number"
              min="0"
              value={graceDays}
              onChange={(event) => setGraceDays(event.target.value)}
              required
            />
          </Field>
        </div>
        <div className="flex justify-end">
          <Button
            loading={saveMutation.isPending}
            onClick={() => saveMutation.mutate()}
            disabled={!dueDate || !monthlyAmount}
          >
            Salvar assinatura
          </Button>
        </div>
      </Card>

      {subscription ? (
        <Card className="space-y-3">
          <h3 className="font-semibold">Registrar pagamento</h3>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Valor">
              <Input
                type="number"
                min="0"
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </Field>
            <Field label="Método">
              <Select value={paymentMethod} onChange={(event) => setPaymentMethod(event.target.value)}>
                <option value="Pix">Pix</option>
                <option value="Transfer">Transferência</option>
                <option value="Cash">Dinheiro</option>
              </Select>
            </Field>
            <div className="md:col-span-2">
              <Field label="Observações">
                <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} />
              </Field>
            </div>
          </div>
          <div className="flex justify-end">
            <Button
              loading={payMutation.isPending}
              disabled={!amount}
              onClick={() => payMutation.mutate()}
            >
              Regularizar com pagamento
            </Button>
          </div>
        </Card>
      ) : null}

      <Card>
        <h3 className="mb-3 font-semibold">Histórico de pagamentos</h3>
        {paymentsQuery.isLoading ? (
          <Skeleton className="h-24" />
        ) : paymentsQuery.error ? (
          <ErrorBox message={paymentsQuery.error.message} onRetry={() => paymentsQuery.refetch()} />
        ) : (paymentsQuery.data ?? []).length === 0 ? (
          <EmptyState title="Sem pagamentos" message="Os registros de POST /payments aparecem aqui." />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="py-2">Quando</th>
                  <th className="py-2">Valor</th>
                  <th className="py-2">Método</th>
                  <th className="py-2">Registrado por</th>
                  <th className="py-2">Obs.</th>
                </tr>
              </thead>
              <tbody>
                {(paymentsQuery.data ?? []).map((payment) => (
                  <tr key={payment.id} className="border-t border-slate-100">
                    <td className="py-2">{formatDateTime(payment.paidAtUtc)}</td>
                    <td className="py-2">{formatCurrency(payment.amount)}</td>
                    <td className="py-2">{payment.paymentMethod}</td>
                    <td className="py-2 text-slate-500">{shortId(payment.registeredByUserId)}</td>
                    <td className="py-2 text-slate-500">{payment.notes ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}
