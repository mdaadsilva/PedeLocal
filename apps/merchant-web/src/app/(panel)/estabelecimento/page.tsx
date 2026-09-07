"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import {
  getDeliveryPricing,
  getEstablishment,
  updateDeliveryPricing,
} from "@/features/establishments/api";
import { ApiError } from "@/lib/api/errors";
import type { DeliveryPricing } from "@/lib/types";

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
  const [pricingDraft, setPricingDraft] = useState<DeliveryPricing | null>(null);

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
  const pricing = pricingDraft ?? pricingQuery.data ?? null;

  const save = useMutation({
    mutationFn: () => updateDeliveryPricing(establishment!.id, pricing!),
    onSuccess: () => toast.push("Taxas de entrega atualizadas.", "success"),
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
      {establishmentQuery.data ? (
        <Card className="grid gap-3 sm:grid-cols-2 text-sm">
          <p><span className="text-slate-500">Nome:</span> {establishmentQuery.data.name}</p>
          <p><span className="text-slate-500">Telefone:</span> {establishmentQuery.data.phone}</p>
          <p><span className="text-slate-500">E-mail:</span> {establishmentQuery.data.email}</p>
          <p><span className="text-slate-500">Documento:</span> {establishmentQuery.data.document}</p>
          <p>
            <span className="text-slate-500">Operacional:</span>{" "}
            {STATUS_LABEL[establishmentQuery.data.operationalStatus] ??
              establishmentQuery.data.operationalStatus}
          </p>
          <p>
            <span className="text-slate-500">Comercial:</span>{" "}
            {STATUS_LABEL[establishmentQuery.data.commercialStatus] ??
              establishmentQuery.data.commercialStatus}
          </p>
          <p className="sm:col-span-2 text-xs text-slate-500">
            Endereço completo não vem no EstablishmentResponse — só addressId.
          </p>
        </Card>
      ) : null}

      <Card className="space-y-4">
        <h2 className="font-semibold">Taxas de entrega</h2>
        {pricingQuery.isLoading ? <Skeleton className="h-32" /> : null}
        {pricing ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Modo urbano">
              <Select
                value={pricing.urbanMode}
                onChange={(event) => setPricingDraft({ ...pricing, urbanMode: event.target.value })}
              >
                <option value="Fixed">Fixo</option>
                <option value="PerKm">Por km</option>
              </Select>
            </Field>
            <Field label="Modo rural">
              <Select
                value={pricing.ruralMode}
                onChange={(event) => setPricingDraft({ ...pricing, ruralMode: event.target.value })}
              >
                <option value="Fixed">Fixo</option>
                <option value="PerKm">Por km</option>
              </Select>
            </Field>
            <Field label="Taxa urbana fixa">
              <Input
                type="number"
                value={pricing.urbanFixedFee}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, urbanFixedFee: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Preço urbano / km">
              <Input
                type="number"
                value={pricing.urbanPricePerKm}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, urbanPricePerKm: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Mínimo urbano">
              <Input
                type="number"
                value={pricing.urbanMinFee}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, urbanMinFee: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Taxa rural fixa">
              <Input
                type="number"
                value={pricing.ruralFixedFee}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, ruralFixedFee: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Preço rural / km">
              <Input
                type="number"
                value={pricing.ruralPricePerKm}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, ruralPricePerKm: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Mínimo rural">
              <Input
                type="number"
                value={pricing.ruralMinFee}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, ruralMinFee: Number(event.target.value) })
                }
              />
            </Field>
            <Field label="Distância máxima (km)">
              <Input
                type="number"
                value={pricing.maxDistanceKm}
                onChange={(event) =>
                  setPricingDraft({ ...pricing, maxDistanceKm: Number(event.target.value) })
                }
              />
            </Field>
          </div>
        ) : null}
        <Button loading={save.isPending} disabled={!pricing} onClick={() => save.mutate()}>
          Salvar taxas
        </Button>
      </Card>
    </div>
  );
}
