"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/input";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import {
  createProduct,
  listCategories,
  listProducts,
  setProductAvailability,
  updateProduct,
  uploadImage,
} from "@/features/catalog/api";
import { ApiError } from "@/lib/api/errors";
import { formatCurrency } from "@/lib/formatters";
import type { Product } from "@/lib/types";

const emptyForm = {
  name: "",
  description: "",
  price: "0",
  promotionalPrice: "",
  menuCategoryId: "",
  stockMode: "Uncontrolled",
  stockQuantity: "",
  imageUrl: "",
};

export default function ProductsPage() {
  const { establishment } = useSession();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [uploading, setUploading] = useState(false);

  const productsQuery = useQuery({
    queryKey: ["products", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => listProducts(establishment!.id),
  });
  const categoriesQuery = useQuery({
    queryKey: ["categories", establishment?.id],
    enabled: Boolean(establishment?.id),
    queryFn: () => listCategories(establishment!.id),
  });

  function startCreate() {
    setEditing(null);
    setForm({
      ...emptyForm,
      menuCategoryId: categoriesQuery.data?.[0]?.id ?? "",
    });
    setOpen(true);
  }

  function startEdit(product: Product) {
    setEditing(product);
    setForm({
      name: product.name,
      description: product.description ?? "",
      price: String(product.price),
      promotionalPrice: product.promotionalPrice?.toString() ?? "",
      menuCategoryId: product.menuCategoryId,
      stockMode: product.stockMode,
      stockQuantity: product.stockQuantity?.toString() ?? "",
      imageUrl: product.imageUrl ?? "",
    });
    setOpen(true);
  }

  const save = useMutation({
    mutationFn: async () => {
      if (!establishment) throw new Error("Sem estabelecimento");
      const payload = {
        menuCategoryId: form.menuCategoryId,
        name: form.name,
        description: form.description || null,
        imageUrl: form.imageUrl || null,
        price: Number(form.price),
        promotionalPrice: form.promotionalPrice ? Number(form.promotionalPrice) : null,
        optionGroups: editing?.optionGroups ?? [],
        ...(editing
          ? {}
          : {
              stockMode: form.stockMode,
              stockQuantity:
                form.stockMode === "Controlled" ? Number(form.stockQuantity || 0) : null,
            }),
      };
      return editing
        ? updateProduct(establishment.id, editing.id, payload)
        : createProduct(establishment.id, payload);
    },
    onSuccess: () => {
      toast.push("Produto salvo.", "success");
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ["products"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const toggle = useMutation({
    mutationFn: (product: Product) =>
      setProductAvailability(establishment!.id, product.id, !product.isAvailable),
    onSuccess: () => {
      toast.push("Disponibilidade atualizada.", "success");
      queryClient.invalidateQueries({ queryKey: ["products"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  async function onUpload(file?: File) {
    if (!file) return;
    setUploading(true);
    try {
      const uploaded = await uploadImage(file);
      setForm((current) => ({ ...current, imageUrl: uploaded.url }));
      toast.push("Imagem enviada.", "success");
    } catch (error) {
      toast.push(error instanceof ApiError ? error.message : "Falha no upload.", "error");
    } finally {
      setUploading(false);
    }
  }

  const categories = categoriesQuery.data ?? [];

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Produtos</h1>
          <p className="text-sm text-slate-500">
            Criar, editar, preço e disponibilidade. Exclusão não existe no contrato atual.
          </p>
        </div>
        <Button onClick={startCreate}>Novo produto</Button>
      </div>

      {productsQuery.isLoading ? <Skeleton className="h-80" /> : null}
      {productsQuery.error ? (
        <ErrorBox message={productsQuery.error.message} onRetry={() => productsQuery.refetch()} />
      ) : null}
      {productsQuery.data?.length === 0 ? (
        <EmptyState title="Nenhum produto" message="Cadastre o primeiro item do cardápio." />
      ) : null}
      {productsQuery.data && productsQuery.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Produto</th>
                <th className="px-4 py-3">Preço</th>
                <th className="px-4 py-3">Estoque</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {productsQuery.data.map((product) => (
                <tr key={product.id} className="border-t border-slate-100">
                  <td className="px-4 py-3">
                    <p className="font-medium">{product.name}</p>
                    <p className="text-xs text-slate-500">{product.description}</p>
                  </td>
                  <td className="px-4 py-3">{formatCurrency(product.promotionalPrice ?? product.price)}</td>
                  <td className="px-4 py-3">
                    {product.stockMode === "Controlled"
                      ? `${product.stockQuantity ?? 0} un.`
                      : "Não controlado"}
                  </td>
                  <td className="px-4 py-3">
                    <Badge
                      value={product.isAvailable ? "Ready" : "Cancelled"}
                      label={product.isAvailable ? "Disponível" : "Indisponível"}
                    />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="ghost" onClick={() => startEdit(product)}>
                      Editar
                    </Button>
                    <Button variant="ghost" onClick={() => toggle.mutate(product)}>
                      {product.isAvailable ? "Desativar" : "Ativar"}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {open ? (
        <Modal title={editing ? "Editar produto" : "Novo produto"} onClose={() => setOpen(false)}>
          <div className="max-h-[70vh] space-y-3 overflow-y-auto pr-1">
            <Field label="Nome">
              <Input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </Field>
            <Field label="Descrição">
              <Textarea
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </Field>
            <Field label="Categoria">
              <Select
                value={form.menuCategoryId}
                onChange={(event) => setForm({ ...form, menuCategoryId: event.target.value })}
              >
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </Select>
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Preço">
                <Input
                  type="number"
                  step="0.01"
                  value={form.price}
                  onChange={(event) => setForm({ ...form, price: event.target.value })}
                />
              </Field>
              <Field label="Preço promocional">
                <Input
                  type="number"
                  step="0.01"
                  value={form.promotionalPrice}
                  onChange={(event) => setForm({ ...form, promotionalPrice: event.target.value })}
                />
              </Field>
            </div>
            {!editing ? (
              <div className="grid grid-cols-2 gap-3">
                <Field label="Modo de estoque">
                  <Select
                    value={form.stockMode}
                    onChange={(event) => setForm({ ...form, stockMode: event.target.value })}
                  >
                    <option value="Uncontrolled">Não controlado</option>
                    <option value="Controlled">Controlado</option>
                  </Select>
                </Field>
                {form.stockMode === "Controlled" ? (
                  <Field label="Quantidade inicial">
                    <Input
                      type="number"
                      value={form.stockQuantity}
                      onChange={(event) => setForm({ ...form, stockQuantity: event.target.value })}
                    />
                  </Field>
                ) : null}
              </div>
            ) : (
              <p className="text-xs text-slate-500">
                O modo de estoque não muda no PUT de produto. Use a tela de Estoque para movimentar.
              </p>
            )}
            <Field label="Imagem">
              <Input type="file" accept="image/*" onChange={(event) => onUpload(event.target.files?.[0])} />
              {form.imageUrl ? <p className="text-xs text-slate-500">{form.imageUrl}</p> : null}
            </Field>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setOpen(false)}>
                Cancelar
              </Button>
              <Button loading={save.isPending || uploading} onClick={() => save.mutate()}>
                Salvar
              </Button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}
