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
  listDrivers,
  rejectDriver,
  unblockDriver,
} from "@/features/drivers/api";
import {
  DELIVERY_STATUS_LABEL,
  DRIVER_APPROVAL_LABEL,
  DRIVER_STATUS_LABEL,
  labelOf,
} from "@/features/orders/labels";
import { ApiError } from "@/lib/api/errors";
import type { Driver } from "@/lib/types";

type PendingAction = { type: "approve" | "block" | "unblock"; driver: Driver };

export default function DriversPage() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [approvalStatus, setApprovalStatus] = useState("");
  const [cityId, setCityId] = useState("");
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [rejecting, setRejecting] = useState<Driver | null>(null);
  const [reason, setReason] = useState("");

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const query = useQuery({
    queryKey: ["admin", "drivers", approvalStatus, page],
    queryFn: () =>
      listDrivers({
        approvalStatus: approvalStatus || undefined,
        page,
        pageSize: 20,
      }),
  });
  const deliveriesQuery = useQuery({
    queryKey: ["admin", "deliveries", "busy"],
    queryFn: () => listAdminDeliveries({ page: 1, pageSize: 100 }),
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

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Entregadores</h1>
        <p className="text-sm text-slate-500">
          GET /api/admin/drivers aceita approvalStatus. Cidade e entrega atual são cruzadas no cliente.
          CPF e chave Pix não são exibidos.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Select
          value={approvalStatus}
          onChange={(event) => {
            setPage(1);
            setApprovalStatus(event.target.value);
          }}
        >
          <option value="">Todos os status de aprovação</option>
          {Object.entries(DRIVER_APPROVAL_LABEL).map(([value, label]) => (
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
                  <th className="px-4 py-3">Disponibilidade</th>
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
                        <p className="font-medium">{driver.name}</p>
                        <p className="text-xs text-slate-500">{driver.vehicleType}</p>
                      </td>
                      <td className="px-4 py-3">{cityNames.get(driver.cityId) ?? "—"}</td>
                      <td className="px-4 py-3">
                        <Badge
                          value={driver.approvalStatus}
                          label={labelOf(DRIVER_APPROVAL_LABEL, driver.approvalStatus)}
                        />
                      </td>
                      <td className="px-4 py-3">
                        <Badge value={driver.status} label={labelOf(DRIVER_STATUS_LABEL, driver.status)} />
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {current ? labelOf(DELIVERY_STATUS_LABEL, current) : "—"}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap justify-end gap-2">
                          {driver.approvalStatus === "PendingApproval" ? (
                            <>
                              <Button variant="secondary" onClick={() => setPending({ type: "approve", driver })}>
                                Aprovar
                              </Button>
                              <Button variant="ghost" onClick={() => setRejecting(driver)}>
                                Recusar
                              </Button>
                            </>
                          ) : null}
                          {driver.status === "Blocked" ? (
                            <Button variant="secondary" onClick={() => setPending({ type: "unblock", driver })}>
                              Desbloquear
                            </Button>
                          ) : (
                            <Button variant="ghost" onClick={() => setPending({ type: "block", driver })}>
                              Bloquear
                            </Button>
                          )}
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
                ? "Bloquear entregador"
                : "Desbloquear entregador"
          }
          message={`Confirma a ação para ${pending.driver.name}?`}
          danger={pending.type === "block"}
          loading={actionMutation.isPending}
          onConfirm={() => actionMutation.mutate()}
          onClose={() => setPending(null)}
        />
      ) : null}

      {rejecting ? (
        <Modal title={`Recusar ${rejecting.name}`} onClose={() => setRejecting(null)}>
          <Field label="Motivo (opcional)">
            <Textarea value={reason} onChange={(event) => setReason(event.target.value)} />
          </Field>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setRejecting(null)}>
              Cancelar
            </Button>
            <Button variant="danger" loading={actionMutation.isPending} onClick={() => actionMutation.mutate()}>
              Recusar
            </Button>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
