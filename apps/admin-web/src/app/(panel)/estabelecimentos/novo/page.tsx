"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/input";
import { useToast } from "@/components/ui/toast";
import { listCities } from "@/features/cities/api";
import { createEstablishment, listEstablishmentCategories } from "@/features/establishments/api";
import { ApiError } from "@/lib/api/errors";

const empty = {
  cityId: "",
  name: "",
  document: "",
  phone: "",
  email: "",
  categoryId: "",
  street: "",
  number: "",
  district: "",
  complement: "",
  zipCode: "",
  latitude: "",
  longitude: "",
  adminName: "",
  adminEmail: "",
  adminPhone: "",
  adminPassword: "",
};

export default function NewEstablishmentPage() {
  const router = useRouter();
  const toast = useToast();
  const [form, setForm] = useState(empty);
  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const categoriesQuery = useQuery({
    queryKey: ["admin", "categories"],
    queryFn: listEstablishmentCategories,
  });

  const mutation = useMutation({
    mutationFn: () =>
      createEstablishment({
        ...form,
        complement: form.complement || null,
        zipCode: form.zipCode || null,
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
      }),
    onSuccess: (created) => {
      toast.push("Estabelecimento criado.", "success");
      router.replace(`/estabelecimentos/${created.id}`);
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  function set(key: keyof typeof empty, value: string) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    mutation.mutate();
  }

  return (
    <form className="space-y-5" onSubmit={onSubmit}>
      <div>
        <h1 className="text-2xl font-semibold">Novo estabelecimento</h1>
        <p className="text-sm text-slate-500">
          POST /api/admin/establishments cria a loja, o endereço e o usuário administrador.
        </p>
      </div>

      <Card className="grid gap-4 md:grid-cols-2">
        <Field label="Nome">
          <Input value={form.name} onChange={(event) => set("name", event.target.value)} required />
        </Field>
        <Field label="Documento">
          <Input value={form.document} onChange={(event) => set("document", event.target.value)} required />
        </Field>
        <Field label="Telefone">
          <Input value={form.phone} onChange={(event) => set("phone", event.target.value)} required />
        </Field>
        <Field label="E-mail">
          <Input type="email" value={form.email} onChange={(event) => set("email", event.target.value)} required />
        </Field>
        <Field label="Cidade">
          <Select value={form.cityId} onChange={(event) => set("cityId", event.target.value)} required>
            <option value="">Selecione</option>
            {(citiesQuery.data ?? []).map((city) => (
              <option key={city.id} value={city.id}>
                {city.name}/{city.state}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Categoria">
          <Select value={form.categoryId} onChange={(event) => set("categoryId", event.target.value)} required>
            <option value="">Selecione</option>
            {(categoriesQuery.data ?? []).map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </Select>
        </Field>
      </Card>

      <Card className="grid gap-4 md:grid-cols-2">
        <h2 className="md:col-span-2 font-semibold">Endereço</h2>
        <Field label="Rua">
          <Input value={form.street} onChange={(event) => set("street", event.target.value)} required />
        </Field>
        <Field label="Número">
          <Input value={form.number} onChange={(event) => set("number", event.target.value)} required />
        </Field>
        <Field label="Bairro">
          <Input value={form.district} onChange={(event) => set("district", event.target.value)} required />
        </Field>
        <Field label="Complemento">
          <Input value={form.complement} onChange={(event) => set("complement", event.target.value)} />
        </Field>
        <Field label="CEP">
          <Input value={form.zipCode} onChange={(event) => set("zipCode", event.target.value)} />
        </Field>
        <Field label="Latitude">
          <Input value={form.latitude} onChange={(event) => set("latitude", event.target.value)} required />
        </Field>
        <Field label="Longitude">
          <Input value={form.longitude} onChange={(event) => set("longitude", event.target.value)} required />
        </Field>
      </Card>

      <Card className="grid gap-4 md:grid-cols-2">
        <h2 className="md:col-span-2 font-semibold">Administrador da loja</h2>
        <Field label="Nome">
          <Input value={form.adminName} onChange={(event) => set("adminName", event.target.value)} required />
        </Field>
        <Field label="E-mail">
          <Input type="email" value={form.adminEmail} onChange={(event) => set("adminEmail", event.target.value)} required />
        </Field>
        <Field label="Telefone">
          <Input value={form.adminPhone} onChange={(event) => set("adminPhone", event.target.value)} required />
        </Field>
        <Field label="Senha">
          <Input
            type="password"
            value={form.adminPassword}
            onChange={(event) => set("adminPassword", event.target.value)}
            required
          />
        </Field>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="secondary" type="button" onClick={() => router.back()}>
          Cancelar
        </Button>
        <Button loading={mutation.isPending} type="submit">
          Criar
        </Button>
      </div>
    </form>
  );
}
