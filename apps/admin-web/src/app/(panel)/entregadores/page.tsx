"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Field, Select, Textarea } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Pagination } from "@/components/ui/pagination";
import { useToast } from "@/components/ui/toast";
import { listCities } from "@/features/cities/api";
import { listAdminDeliveries } from "@/features/deliveries/api";
import {
  approveDriver,
  blockDriver,
  getDriver,
  listDrivers,
  rejectDriver,
  unblockDriver,
} from "@/features/drivers/api";
import {
  DELIVERY_STATUS_LABEL,
  DRIVER_ELIGIBILITY_LABEL,
  DRIVER_ONBOARDING_LABEL,
  DRIVER_STATUS_LABEL,
  PIX_KEY_TYPE_LABEL,
  VEHICLE_TYPE_LABEL,
  labelOf,
} from "@/features/orders/labels";
import { ApiError } from "@/lib/api/errors";
import { formatDateOnly, formatDateTime } from "@/lib/formatters";
import type { Driver } from "@/lib/types";

type PendingAction = { type: "approve" | "block" | "unblock"; driver: Driver };

function maskCpf(cpf: string) {
  const digits = cpf.replace(/\D/g, "");
  if (digits.length !== 11) return cpf;
  return `${digits.slice(0, 3)}.***.***-${digits.slice(9)}`;
}

export default function DriversPage() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [onboardingStatus, setOnboardingStatus] = useState("");
  const [cityId, setCityId] = useState("");
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [rejecting, setRejecting] = useState<Driver | null>(null);
  const [reason, setReason] = useState("");
  const [detailsId, setDetailsId] = useState<string | null>(null);

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const query = useQuery({
    queryKey: ["admin", "drivers", onboardingStatus, page],
    queryFn: () =>
      listDrivers({
        onboardingStatus: onboardingStatus || undefined,
        page,
        pageSize: 20,
      }),
  });
  const deliveriesQuery = useQuery({
    queryKey: ["admin", "deliveries", "busy"],
    queryFn: () => listAdminDeliveries({ page: 1, pageSize: 100 }),
  });
  const detailsQuery = useQuery({
    queryKey: ["admin", "drivers", "detail", detailsId],
    queryFn: () => getDriver(detailsId!),
    enabled: Boolean(detailsId),
  });

  const cityNames = new Map((citiesQuery.data ?? []).map((city) => [city.id, `${city.name}/${city.state}`]));
  const currentByDriver = useMemo(() => {
    const map = new Map<string, string>();
    for (const delivery of deliveriesQuery.data?.items ?? []) {
      if (!delivery.driverId) continue;
      if (["Assigned", "PickedUp", "OutForDelivery"].includes(delivery.status)) {
        map.set(delivery.driverId, delivery.status);
      }
    }
    return map;
  }, [deliveriesQuery.data]);

  const filtered = (query.data?.items ?? []).filter((driver) => !cityId || driver.cityId === cityId);

  const actionMutation = useMutation({
    mutationFn: async () => {
      if (pending?.type === "approve") return approveDriver(pending.driver.id);
      if (pending?.type === "block") return blockDriver(pending.driver.id);
      if (pending?.type === "unblock") return unblockDriver(pending.driver.id);
      if (rejecting) return rejectDriver(rejecting.id, reason || undefined);
      throw new Error("Ação inválida");
    },
    onSuccess: () => {
      toast.push("Entregador atualizado.", "success");
      setPending(null);
      setRejecting(null);
      setReason("");
      queryClient.invalidateQueries({ queryKey: ["admin", "drivers"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const detailDriver = detailsQuery.data;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Entregadores</h1>
        <p className="text-sm text-slate-500">
          GET /api/admin/drivers aceita onboardingStatus. Aprovar/reprovar exige CNH e veículo já
          enviados; CNH vencida bloqueia aprovação (docs/DRIVER_ONBOARDING.md).
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Select
          value={onboardingStatus}
          onChange={(event) => {
            setPage(1);
            setOnboardingStatus(event.target.value);
          }}
        >
          <option value="">Todos os status de aprovação</option>
          {Object.entries(DRIVER_ONBOARDING_LABEL).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
        <Select value={cityId} onChange={(event) => setCityId(event.target.value)}>
          <option value="">Todas as cidades (filtro local)</option>
          {(citiesQuery.data ?? []).map((city) => (
            <option key={city.id} value={city.id}>
              {city.name}/{city.state}
            </option>
          ))}
        </Select>
      </div>

      {query.isLoading ? (
        <Skeleton className="h-80" />
      ) : query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : filtered.length === 0 ? (
        <EmptyState title="Nenhum entregador" message="Ajuste o filtro ou aguarde novos cadastros." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Nome</th>
                  <th className="px-4 py-3">Cidade</th>
                  <th className="px-4 py-3">Aprovação</th>
                  <th className="px-4 py-3">Elegibilidade</th>
                  <th className="px-4 py-3">Entrega atual</th>
                  <th className="px-4 py-3 text-right">Ações</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((driver) => {
                  const current = currentByDriver.get(driver.id);
                  return (
                    <tr key={driver.id} className="border-t border-slate-100">
                      <td className="px-4 py-3">
                        <button
                          className="font-medium text-brand-700 hover:underline"
                          onClick={() => setDetailsId(driver.id)}
                        >
                          {driver.name}
                        </button>
                        <p className="text-xs text-slate-500">
                          {labelOf(VEHICLE_TYPE_LABEL, driver.vehicleType)} · {maskCpf(driver.cpf)}
                        </p>
                      </td>
                      <td className="px-4 py-3">{cityNames.get(driver.cityId) ?? "—"}</td>
                      <td className="px-4 py-3">
                        <Badge
                          value={driver.onboardingStatus}
                          label={labelOf(DRIVER_ONBOARDING_LABEL, driver.onboardingStatus)}
                        />
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-col gap-1">
                          <Badge
                            value={driver.eligibilityStatus}
                            label={labelOf(DRIVER_ELIGIBILITY_LABEL, driver.eligibilityStatus)}
                          />
                          <span className="text-xs text-slate-400">
                            {labelOf(DRIVER_STATUS_LABEL, driver.status)}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {current ? labelOf(DELIVERY_STATUS_LABEL, current) : "—"}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap justify-end gap-2">
                          <Button variant="ghost" onClick={() => setDetailsId(driver.id)}>
                            Detalhes
                          </Button>
                          {driver.onboardingStatus === "UnderReview" ? (
                            <>
                              <Button variant="secondary" onClick={() => setPending({ type: "approve", driver })}>
                                Aprovar
                              </Button>
                              <Button variant="ghost" onClick={() => setRejecting(driver)}>
                                Reprovar
                              </Button>
                            </>
                          ) : null}
                          {driver.onboardingStatus === "Approved" ? (
                            driver.status === "Blocked" ? (
                              <Button variant="secondary" onClick={() => setPending({ type: "unblock", driver })}>
                                Reativar
                              </Button>
                            ) : (
                              <Button variant="ghost" onClick={() => setPending({ type: "block", driver })}>
                                Suspender
                              </Button>
                            )
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  );
                })}
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

      {pending ? (
        <ConfirmDialog
          title={
            pending.type === "approve"
              ? "Aprovar entregador"
              : pending.type === "block"
                ? "Suspender entregador"
                : "Reativar entregador"
          }
          message={`Confirma a ação para ${pending.driver.name}?`}
          danger={pending.type === "block"}
          loading={actionMutation.isPending}
          onConfirm={() => actionMutation.mutate()}
          onClose={() => setPending(null)}
        />
      ) : null}

      {rejecting ? (
        <Modal title={`Reprovar ${rejecting.name}`} onClose={() => setRejecting(null)}>
          <Field label="Motivo (opcional, exibido ao entregador)">
            <Textarea value={reason} onChange={(event) => setReason(event.target.value)} />
          </Field>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setRejecting(null)}>
              Cancelar
            </Button>
            <Button variant="danger" loading={actionMutation.isPending} onClick={() => actionMutation.mutate()}>
              Reprovar
            </Button>
          </div>
        </Modal>
      ) : null}

      {detailsId ? (
        <Modal title="Detalhes do entregador" onClose={() => setDetailsId(null)}>
          {detailsQuery.isLoading ? (
            <Skeleton className="h-64" />
          ) : detailsQuery.error ? (
            <ErrorBox message={detailsQuery.error.message} onRetry={() => detailsQuery.refetch()} />
          ) : detailDriver ? (
            <div className="space-y-4 text-sm">
              <div className="grid grid-cols-2 gap-3">
                <p>
                  <span className="text-slate-500">Nome:</span> {detailDriver.driver.name}
                </p>
                <p>
                  <span className="text-slate-500">CPF:</span> {detailDriver.driver.cpf}
                </p>
                <p>
                  <span className="text-slate-500">Chave Pix:</span>{" "}
                  {labelOf(PIX_KEY_TYPE_LABEL, detailDriver.driver.pixKeyType)} — {detailDriver.driver.pixKey}
                </p>
                <p>
                  <span className="text-slate-500">Aprovação:</span>{" "}
                  <Badge
                    value={detailDriver.driver.onboardingStatus}
                    label={labelOf(DRIVER_ONBOARDING_LABEL, detailDriver.driver.onboardingStatus)}
                  />
                </p>
                <p>
                  <span className="text-slate-500">Elegibilidade:</span>{" "}
                  <Badge
                    value={detailDriver.driver.eligibilityStatus}
                    label={labelOf(DRIVER_ELIGIBILITY_LABEL, detailDriver.driver.eligibilityStatus)}
                  />
                </p>
                {detailDriver.driver.reviewedAtUtc ? (
                  <p>
                    <span className="text-slate-500">Revisado em:</span>{" "}
                    {formatDateTime(detailDriver.driver.reviewedAtUtc)}
                  </p>
                ) : null}
              </div>
              {detailDriver.driver.rejectionReason ? (
                <div className="rounded-lg bg-red-50 p-3 text-red-800">
                  <span className="font-medium">Motivo da reprovação/suspensão:</span>{" "}
                  {detailDriver.driver.rejectionReason}
                </div>
              ) : null}

              <div className="rounded-xl border border-slate-200 p-3">
                <h3 className="mb-2 font-semibold">CNH</h3>
                {detailDriver.license ? (
                  <div className="grid grid-cols-2 gap-2">
                    <p>
                      <span className="text-slate-500">Registro:</span>{" "}
                      {detailDriver.license.registrationNumber}
                    </p>
                    <p>
                      <span className="text-slate-500">Categoria:</span> {detailDriver.license.category}
                    </p>
                    <p>
                      <span className="text-slate-500">Validade:</span>{" "}
                      {formatDateOnly(detailDriver.license.expiresAt)}{" "}
                      {detailDriver.license.daysUntilExpiration < 0 ? (
                        <Badge value="Rejected" label="Vencida" />
                      ) : detailDriver.license.daysUntilExpiration <= 30 ? (
                        <Badge
                          value="UnderReview"
                          label={`Vence em ${detailDriver.license.daysUntilExpiration}d`}
                        />
                      ) : null}
                    </p>
                    <p>
                      <span className="text-slate-500">EAR:</span>{" "}
                      {detailDriver.license.hasEar === null
                        ? "Não se aplica"
                        : detailDriver.license.hasEar
                          ? "Sim"
                          : "Não"}
                    </p>
                    {detailDriver.license.documentImageUrl ? (
                      <a
                        className="col-span-2 text-brand-700 hover:underline"
                        href={detailDriver.license.documentImageUrl}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Ver foto da CNH
                      </a>
                    ) : null}
                  </div>
                ) : (
                  <p className="text-slate-500">CNH ainda não enviada.</p>
                )}
              </div>

              <div className="rounded-xl border border-slate-200 p-3">
                <h3 className="mb-2 font-semibold">Veículo</h3>
                {detailDriver.vehicle ? (
                  <div className="grid grid-cols-2 gap-2">
                    <p>
                      <span className="text-slate-500">Tipo:</span>{" "}
                      {labelOf(VEHICLE_TYPE_LABEL, detailDriver.vehicle.type)}
                    </p>
                    <p>
                      <span className="text-slate-500">Placa:</span> {detailDriver.vehicle.plate ?? "—"}
                    </p>
                    <p>
                      <span className="text-slate-500">Modelo:</span> {detailDriver.vehicle.model}
                    </p>
                    <p>
                      <span className="text-slate-500">Cor:</span> {detailDriver.vehicle.color}
                    </p>
                    {detailDriver.vehicle.documentImageUrl ? (
                      <a
                        className="col-span-2 text-brand-700 hover:underline"
                        href={detailDriver.vehicle.documentImageUrl}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Ver documento do veículo
                      </a>
                    ) : null}
                  </div>
                ) : (
                  <p className="text-slate-500">Veículo ainda não enviado.</p>
                )}
              </div>

              <div className="flex flex-wrap justify-end gap-2">
                {detailDriver.driver.onboardingStatus === "UnderReview" ? (
                  <>
                    <Button
                      variant="secondary"
                      onClick={() => {
                        setDetailsId(null);
                        setPending({ type: "approve", driver: detailDriver.driver });
                      }}
                    >
                      Aprovar
                    </Button>
                    <Button
                      variant="ghost"
                      onClick={() => {
                        setDetailsId(null);
                        setRejecting(detailDriver.driver);
                      }}
                    >
                      Reprovar
                    </Button>
                  </>
                ) : null}
              </div>
            </div>
          ) : null}
        </Modal>
      ) : null}
    </div>
  );
}
