"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Field, Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { useToast } from "@/components/ui/toast";
import { createCity, listCities, setCityStatus, updateCity } from "@/features/cities/api";
import { listAllEstablishments } from "@/features/establishments/api";
import { countEstablishmentsByCity } from "@/features/dashboard/metrics";
import { ApiError } from "@/lib/api/errors";
import type { City } from "@/lib/types";

export default function CitiesPage() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<City | null>(null);
  const [creating, setCreating] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<City | null>(null);
  const [name, setName] = useState("");
  const [state, setState] = useState("");

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const establishmentsQuery = useQuery({
    queryKey: ["admin", "establishments", "all"],
    queryFn: listAllEstablishments,
  });

  const counts = useMemo(
    () => countEstablishmentsByCity(establishmentsQuery.data ?? []),
    [establishmentsQuery.data],
  );

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (editing) {
        return updateCity(editing.id, { name, state });
      }
      return createCity({ name, state });
    },
    onSuccess: () => {
      toast.push(editing ? "Cidade atualizada." : "Cidade criada.", "success");
      setEditing(null);
      setCreating(false);
      queryClient.invalidateQueries({ queryKey: ["admin", "cities"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const statusMutation = useMutation({
    mutationFn: () => setCityStatus(pendingStatus!.id, !pendingStatus!.isActive),
    onSuccess: () => {
      toast.push("Status da cidade atualizado.", "success");
      setPendingStatus(null);
      queryClient.invalidateQueries({ queryKey: ["admin", "cities"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  function openCreate() {
    setName("");
    setState("");
    setCreating(true);
  }

  function openEdit(city: City) {
    setName(city.name);
    setState(city.state);
    setEditing(city);
  }

  if (citiesQuery.isLoading) return <Skeleton className="h-80" />;
  if (citiesQuery.error) {
    return <ErrorBox message={citiesQuery.error.message} onRetry={() => citiesQuery.refetch()} />;
  }

  const cities = citiesQuery.data ?? [];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Cidades</h1>
          <p className="text-sm text-slate-500">
            GET/POST /api/admin/cities, PUT /{"{id}"} e PATCH /{"{id}"}/status.
          </p>
        </div>
        <Button onClick={openCreate}>Nova cidade</Button>
      </div>

      {cities.length === 0 ? (
        <EmptyState title="Nenhuma cidade" message="Crie a primeira cidade para habilitar estabelecimentos." />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Cidade</th>
                <th className="px-4 py-3">UF</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Estabelecimentos</th>
                <th className="px-4 py-3 text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {cities.map((city) => (
                <tr key={city.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 font-medium">{city.name}</td>
                  <td className="px-4 py-3">{city.state}</td>
                  <td className="px-4 py-3">
                    <Badge
                      value={city.isActive ? "Active" : "Inactive"}
                      label={city.isActive ? "Ativa" : "Inativa"}
                    />
                  </td>
                  <td className="px-4 py-3 text-slate-600">{counts.get(city.id) ?? 0}</td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex justify-end gap-2">
                      <Button variant="secondary" onClick={() => openEdit(city)}>
                        Editar
                      </Button>
                      <Button variant="ghost" onClick={() => setPendingStatus(city)}>
                        {city.isActive ? "Desativar" : "Ativar"}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {creating || editing ? (
        <Modal
          title={editing ? "Editar cidade" : "Nova cidade"}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        >
          <form
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault();
              saveMutation.mutate();
            }}
          >
            <Field label="Nome">
              <Input value={name} onChange={(event) => setName(event.target.value)} required />
            </Field>
            <Field label="UF">
              <Input
                value={state}
                maxLength={2}
                onChange={(event) => setState(event.target.value.toUpperCase())}
                required
              />
            </Field>
            <div className="flex justify-end gap-2">
              <Button
                variant="secondary"
                type="button"
                onClick={() => {
                  setCreating(false);
                  setEditing(null);
                }}
              >
                Cancelar
              </Button>
              <Button loading={saveMutation.isPending} type="submit">
                Salvar
              </Button>
            </div>
          </form>
        </Modal>
      ) : null}

      {pendingStatus ? (
        <ConfirmDialog
          title={pendingStatus.isActive ? "Desativar cidade" : "Ativar cidade"}
          message={`Confirma ${pendingStatus.isActive ? "desativar" : "ativar"} ${pendingStatus.name}?`}
          loading={statusMutation.isPending}
          onConfirm={() => statusMutation.mutate()}
          onClose={() => setPendingStatus(null)}
        />
      ) : null}
    </div>
  );
}
