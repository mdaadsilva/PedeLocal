"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import { listProducts } from "@/features/catalog/api";
import { adjustStock, listMovements } from "@/features/inventory/api";
import { ApiError } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/formatters";

export default function InventoryPage() {
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [productId, setProductId] = useState("");
  const [type, setType] = useState("Entry");
  const [quantity, setQuantity] = useState("1");
  const [reason, setReason] = useState("");

  const productsQuery = useQuery({
    queryKey: ["products", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => listProducts(establishment!.id),
  });

  const controlled = (productsQuery.data ?? []).filter(
    (product) => product.stockMode === "Controlled",
  );
  const selectedId = productId || controlled[0]?.id || "";

  const movementsQuery = useQuery({
    queryKey: ["movements", establishment?.id, selectedId],
    enabled: Boolean(establishment?.id && selectedId),
    queryFn: () => listMovements(establishment!.id, selectedId),
  });

  const mutation = useMutation({
    mutationFn: () =>
      adjustStock(establishment!.id, selectedId, {
        type,
        quantity: Number(quantity),
        reason: reason || undefined,
      }),
    onSuccess: () => {
      toast.push("Estoque atualizado.", "success");
      queryClient.invalidateQueries({ queryKey: ["products"] });
      queryClient.invalidateQueries({ queryKey: ["movements"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const selected = controlled.find((product) => product.id === selectedId);

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Estoque</h1>
        <p className="text-sm text-slate-500">
          Só produtos Controlled aceitam ajuste. Uncontrolled não tem quantidade.
          Tipos manuais: Entry, Loss, Return, Adjustment.
        </p>
      </div>

      {productsQuery.isLoading ? <Skeleton className="h-40" /> : null}
      {controlled.length === 0 && !productsQuery.isLoading ? (
        <EmptyState
          title="Nenhum produto com estoque controlado"
          message="Cadastre um produto com stockMode Controlled para movimentar quantidade."
        />
      ) : null}

      {controlled.length > 0 ? (
        <div className="grid gap-4 lg:grid-cols-[20rem_1fr]">
          <Card className="space-y-3">
            {controlled.map((product) => {
              const low =
                product.stockQuantity != null && product.stockQuantity <= 5;
              return (
                <button
                  key={product.id}
                  type="button"
                  onClick={() => setProductId(product.id)}
                  className={`w-full rounded-xl border px-3 py-3 text-left ${
                    selectedId === product.id
                      ? "border-brand-600 bg-brand-50"
                      : "border-slate-200"
                  }`}
                >
                  <p className="font-medium">{product.name}</p>
                  <p className={`text-sm ${low ? "text-red-600" : "text-slate-500"}`}>
                    {product.stockQuantity ?? 0} un.
                    {low ? " · estoque baixo" : ""}
                  </p>
                </button>
              );
            })}
          </Card>

          <div className="space-y-4">
            <Card className="space-y-3">
              <h2 className="font-semibold">Movimentar {selected?.name}</h2>
              <Field label="Tipo">
                <Select value={type} onChange={(event) => setType(event.target.value)}>
                  <option value="Entry">Entrada</option>
                  <option value="Loss">Perda</option>
                  <option value="Return">Devolução</option>
                  <option value="Adjustment">Ajuste (define o saldo)</option>
                </Select>
              </Field>
              <Field label="Quantidade">
                <Input
                  type="number"
                  min={0}
                  value={quantity}
                  onChange={(event) => setQuantity(event.target.value)}
                />
              </Field>
              <Field label="Motivo">
                <Input value={reason} onChange={(event) => setReason(event.target.value)} />
              </Field>
              <Button loading={mutation.isPending} onClick={() => mutation.mutate()}>
                Registrar movimento
              </Button>
            </Card>

            <Card>
              <h2 className="mb-3 font-semibold">Histórico</h2>
              {movementsQuery.isLoading ? <Skeleton className="h-32" /> : null}
              {movementsQuery.error ? (
                <ErrorBox message={movementsQuery.error.message} />
              ) : null}
              {movementsQuery.data?.items.length === 0 ? (
                <p className="text-sm text-slate-500">Sem movimentações ainda.</p>
              ) : null}
              <ul className="space-y-2 text-sm">
                {movementsQuery.data?.items.map((movement) => (
                  <li key={movement.id} className="flex justify-between gap-3">
                    <span>
                      {movement.type} · {movement.previousQuantity} → {movement.newQuantity}
                      {movement.reason ? ` · ${movement.reason}` : ""}
                    </span>
                    <span className="text-slate-500">{formatDateTime(movement.createdAtUtc)}</span>
                  </li>
                ))}
              </ul>
            </Card>
          </div>
        </div>
      ) : null}
    </div>
  );
}
