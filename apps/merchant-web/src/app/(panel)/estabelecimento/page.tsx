"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import {
  getDeliveryPricing,
  getEstablishment,
  pauseEstablishment,
  resumeEstablishment,
  updateDeliveryPricing,
} from "@/features/establishments/api";
import { PAUSE_REASON_LABEL, labelOf } from "@/features/orders/labels";
import { ApiError } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/formatters";

const STATUS_LABEL: Record<string, string> = {
  Open: "Aberto",
  ClosedManual: "Fechado manualmente",
  ClosedSchedule: "Fechado por horário",
  Paused: "Pausado",
  Active: "Ativo",
  OverdueGrace: "Inadimplente (carência)",
  SuspendedPayment: "Suspenso por pagamento",
  SuspendedAdmin: "Suspenso pelo admin",
  Inactive: "Inativo",
};

export default function EstablishmentPage() {
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [fee, setFee] = useState<number | null>(null);
  const [radiusKm, setRadiusKm] = useState<number | null>(null);
  const [pauseOpen, setPauseOpen] = useState(false);
  const [pauseReason, setPauseReason] = useState("HighDemand");
  const [pauseUntil, setPauseUntil] = useState("");

  const establishmentQuery = useQuery({
    queryKey: ["establishment", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => getEstablishment(establishment!.id),
  });
  const pricingQuery = useQuery({
    queryKey: ["pricing", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => getDeliveryPricing(establishment!.id),
  });

  const currentFee = fee ?? pricingQuery.data?.deliveryFee ?? null;
  const currentRadius = radiusKm ?? pricingQuery.data?.deliveryRadiusKm ?? null;

  const savePricing = useMutation({
    mutationFn: () =>
      updateDeliveryPricing(establishment!.id, {
        deliveryFee: currentFee!,
        deliveryRadiusKm: currentRadius!,
      }),
    onSuccess: () => {
      toast.push("Taxa de entrega atualizada.", "success");
      queryClient.invalidateQueries({ queryKey: ["pricing", establishment?.id] });
    },
    onError: (error: Error) =>
      toast.push(error instanceof ApiError ? error.message : error.message, "error"),
  });

  const pause = useMutation({
    mutationFn: () =>
      pauseEstablishment(establishment!.id, {
        reason: pauseReason,
        pausedUntilUtc: pauseUntil ? new Date(pauseUntil).toISOString() : null,
      }),
    onSuccess: () => {
      toast.push("Estabelecimento pausado.", "success");
      setPauseOpen(false);
      queryClient.invalidateQueries({ queryKey: ["establishment", establishment?.id] });
    },
    onError: (error: Error) =>
      toast.push(error instanceof ApiError ? error.message : error.message, "error"),
  });

  const resume = useMutation({
    mutationFn: () => resumeEstablishment(establishment!.id),
    onSuccess: () => {
      toast.push("Estabelecimento reativado — voltando a aceitar pedidos.", "success");
      queryClient.invalidateQueries({ queryKey: ["establishment", establishment?.id] });
    },
    onError: (error: Error) =>
      toast.push(error instanceof ApiError ? error.message : error.message, "error"),
  });

  if (!establishment) {
    return (
      <EmptyState
        title="Estabelecimento não encontrado"
        message="Não há contrato para listar as lojas do usuário autenticado."
      />
    );
  }

  const data = establishmentQuery.data;
  const isPaused = data?.operationalStatus === "Paused";

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Estabelecimento</h1>
        <p className="text-sm text-slate-500">
          Nome, telefone e status são somente leitura: o PUT/PATCH do estabelecimento é admin.
          Não existem horários nem descrição no contrato atual.
        </p>
      </div>

      {establishmentQuery.isLoading ? <Skeleton className="h-40" /> : null}
      {establishmentQuery.error ? (
        <ErrorBox message={establishmentQuery.error.message} />
      ) : null}
      {data ? (
        <Card className="grid gap-3 sm:grid-cols-2 text-sm">
          <p><span className="text-slate-500">Nome:</span> {data.name}</p>
          <p><span className="text-slate-500">Telefone:</span> {data.phone}</p>
          <p><span className="text-slate-500">E-mail:</span> {data.email}</p>
          <p><span className="text-slate-500">Documento:</span> {data.document}</p>
          <p>
            <span className="text-slate-500">Operacional:</span>{" "}
            <Badge value={data.operationalStatus} label={STATUS_LABEL[data.operationalStatus] ?? data.operationalStatus} />
          </p>
          <p>
            <span className="text-slate-500">Comercial:</span>{" "}
            <Badge value={data.commercialStatus} label={STATUS_LABEL[data.commercialStatus] ?? data.commercialStatus} />
          </p>
          <p className="sm:col-span-2 text-xs text-slate-500">
            Endereço completo não vem no EstablishmentResponse — só addressId.
          </p>
        </Card>
      ) : null}

      <Card className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="font-semibold">Aceitar pedidos</h2>
            <p className="text-sm text-slate-500">
              Pausar impede novos pedidos (entrega e retirada); pedidos já em andamento seguem normalmente.
            </p>
          </div>
          {isPaused ? (
            <Button variant="secondary" loading={resume.isPending} onClick={() => resume.mutate()}>
              Reativar pedidos
            </Button>
          ) : (
            <Button variant="danger" onClick={() => setPauseOpen(true)}>
              Pausar pedidos
            </Button>
          )}
        </div>
        {isPaused ? (
          <div className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">
            <p>
              <span className="font-medium">Motivo:</span>{" "}
              {data?.pauseReason ? labelOf(PAUSE_REASON_LABEL, data.pauseReason) : "—"}
            </p>
            {data?.pausedAtUtc ? <p>Pausado em {formatDateTime(data.pausedAtUtc)}</p> : null}
            <p>
              {data?.pausedUntilUtc
                ? `Retoma automaticamente em ${formatDateTime(data.pausedUntilUtc)}`
                : "Sem previsão automática — só termina com reativação manual."}
            </p>
          </div>
        ) : null}
        {pauseOpen ? (
          <div className="space-y-3 rounded-lg border border-slate-200 p-3">
            <Field label="Motivo da pausa">
              <Select value={pauseReason} onChange={(event) => setPauseReason(event.target.value)}>
                {Object.entries(PAUSE_REASON_LABEL).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Retomar automaticamente em (opcional)">
              <Input
                type="datetime-local"
                value={pauseUntil}
                onChange={(event) => setPauseUntil(event.target.value)}
              />
            </Field>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setPauseOpen(false)}>
                Cancelar
              </Button>
              <Button variant="danger" loading={pause.isPending} onClick={() => pause.mutate()}>
                Confirmar pausa
              </Button>
            </div>
          </div>
        ) : null}
      </Card>

      <Card className="space-y-4">
        <h2 className="font-semibold">Taxa de entrega</h2>
        <p className="text-sm text-slate-500">
          Taxa fixa por pedido dentro do raio máximo — não há mais cálculo por km
          (GET/PUT /api/merchant/establishments/{"{id}"}/delivery-pricing).
        </p>
        {pricingQuery.isLoading ? <Skeleton className="h-24" /> : null}
        {pricingQuery.error ? (
          <ErrorBox message={pricingQuery.error.message} onRetry={() => pricingQuery.refetch()} />
        ) : null}
        {currentFee !== null && currentRadius !== null ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Taxa de entrega (R$)">
              <Input
                type="number"
                min={0}
                step="0.01"
                value={currentFee}
                onChange={(event) => setFee(Number(event.target.value))}
              />
            </Field>
            <Field label="Raio máximo de entrega (km)">
              <Input
                type="number"
                min={0}
                step="0.1"
                value={currentRadius}
                onChange={(event) => setRadiusKm(Number(event.target.value))}
              />
            </Field>
          </div>
        ) : null}
        <Button
          loading={savePricing.isPending}
          disabled={currentFee === null || currentRadius === null}
          onClick={() => savePricing.mutate()}
        >
          Salvar taxa de entrega
        </Button>
      </Card>
    </div>
  );
}
