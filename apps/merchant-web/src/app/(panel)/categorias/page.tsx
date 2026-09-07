"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import { createCategory, listCategories, updateCategory } from "@/features/catalog/api";
import { ApiError } from "@/lib/api/errors";
import type { MenuCategory } from "@/lib/types";

export default function CategoriesPage() {
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<MenuCategory | null>(null);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [sortOrder, setSortOrder] = useState(1);

  const query = useQuery({
    queryKey: ["categories", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => listCategories(establishment!.id),
  });

  const save = useMutation({
    mutationFn: async () => {
      if (!establishment) throw new Error("Sem estabelecimento");
      if (editing) {
        return updateCategory(establishment.id, editing.id, { name, sortOrder });
      }
      return createCategory(establishment.id, { name, sortOrder });
    },
    onSuccess: () => {
      toast.push("Categoria salva.", "success");
      setCreating(false);
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ["categories"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  function openCreate() {
    setName("");
    setSortOrder((query.data?.length ?? 0) + 1);
    setEditing(null);
    setCreating(true);
  }

  function openEdit(category: MenuCategory) {
    setName(category.name);
    setSortOrder(category.sortOrder);
    setEditing(category);
    setCreating(true);
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Categorias do cardápio</h1>
          <p className="text-sm text-slate-500">
            O backend não tem ativar/desativar nem excluir categoria — só criar e editar nome/ordem.
          </p>
        </div>
        <Button onClick={openCreate}>Nova categoria</Button>
      </div>

      {query.isLoading ? <Skeleton className="h-64" /> : null}
      {query.error ? <ErrorBox message={query.error.message} onRetry={() => query.refetch()} /> : null}
      {query.data?.length === 0 ? (
        <EmptyState title="Nenhuma categoria" message="Crie a primeira seção do cardápio." />
      ) : null}
      {query.data && query.data.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Nome</th>
                <th className="px-4 py-3">Ordem</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {query.data.map((category) => (
                <tr key={category.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 font-medium">{category.name}</td>
                  <td className="px-4 py-3">{category.sortOrder}</td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="ghost" onClick={() => openEdit(category)}>
                      Editar
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {creating ? (
        <Modal title={editing ? "Editar categoria" : "Nova categoria"} onClose={() => setCreating(false)}>
          <div className="space-y-3">
            <Field label="Nome">
              <Input value={name} onChange={(event) => setName(event.target.value)} />
            </Field>
            <Field label="Ordem">
              <Input
                type="number"
                value={sortOrder}
                onChange={(event) => setSortOrder(Number(event.target.value))}
              />
            </Field>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setCreating(false)}>
                Cancelar
              </Button>
              <Button loading={save.isPending} onClick={() => save.mutate()}>
                Salvar
              </Button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
