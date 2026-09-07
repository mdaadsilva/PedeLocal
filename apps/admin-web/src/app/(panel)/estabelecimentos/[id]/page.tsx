"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/confirm";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Field, Input, Select } from "@/components/ui/input";
import { useToast } from "@/components/ui/toast";
import { listCities } from "@/features/cities/api";
import {
  getEstablishment,
  listEstablishmentCategories,
  setEstablishmentStatus,
  updateEstablishment,
} from "@/features/establishments/api";
import { FeaturesPanel } from "@/features/features/features-panel";
import { listAdminOrders } from "@/features/orders/api";
import {
  COMMERCIAL_STATUS_LABEL,
  OPERATIONAL_STATUS_LABEL,
  labelOf,
} from "@/features/orders/labels";
import { OrderTable } from "@/features/orders/order-table";
import { SubscriptionPanel } from "@/features/subscriptions/subscription-panel";
import { ApiError } from "@/lib/api/errors";
import { formatDateTime, shortId } from "@/lib/formatters";

const TABS = [
  { id: "geral", label: "Geral" },
  { id: "assinatura", label: "Assinatura" },
  { id: "flags", label: "Funcionalidades" },
  { id: "pedidos", label: "Pedidos" },
] as const;

export default function EstablishmentDetailPage() {
  const params = useParams<{ id: string }>();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<(typeof TABS)[number]["id"]>("geral");
  const [pendingStatus, setPendingStatus] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["admin", "establishment", params.id],
    queryFn: () => getEstablishment(params.id),
  });
  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const categoriesQuery = useQuery({
    queryKey: ["admin", "categories"],
    queryFn: listEstablishmentCategories,
  });
  const ordersQuery = useQuery({
    queryKey: ["admin", "orders", params.id],
    enabled: tab === "pedidos",
    queryFn: () => listAdminOrders({ establishmentId: params.id, page: 1, pageSize: 20 }),
  });

  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [street, setStreet] = useState("");
  const [number, setNumber] = useState("");
  const [district, setDistrict] = useState("");
  const [complement, setComplement] = useState("");
  const [zipCode, setZipCode] = useState("");
  const [latitude, setLatitude] = useState("");
  const [longitude, setLongitude] = useState("");
  const [hydratedId, setHydratedId] = useState("");

  if (query.data && query.data.id !== hydratedId) {
    setHydratedId(query.data.id);
    setName(query.data.name);
    setPhone(query.data.phone);
    setEmail(query.data.email);
    setCategoryId(query.data.categoryId);
  }

  const saveMutation = useMutation({
    mutationFn: () =>
      updateEstablishment(params.id, {
        name,
        phone,
        email,
        categoryId,
        street,
        number,
        district,
        complement: complement || null,
        zipCode: zipCode || null,
        latitude: Number(latitude),
        longitude: Number(longitude),
      }),
    onSuccess: () => {
      toast.push("Estabelecimento atualizado.", "success");
      queryClient.invalidateQueries({ queryKey: ["admin", "establishment", params.id] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  const statusMutation = useMutation({
    mutationFn: (commercialStatus: string) =>
      setEstablishmentStatus(params.id, { commercialStatus }),
    onSuccess: () => {
      toast.push("Status comercial atualizado.", "success");
      setPendingStatus(null);
      queryClient.invalidateQueries({ queryKey: ["admin", "establishment", params.id] });
      queryClient.invalidateQueries({ queryKey: ["admin", "establishments"] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;
  const establishment = query.data;
  if (!establishment) return null;

  const city = citiesQuery.data?.find((item) => item.id === establishment.cityId);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link className="text-sm text-brand-700" href="/estabelecimentos">
            ← Estabelecimentos
          </Link>
          <h1 className="mt-1 text-2xl font-semibold">{establishment.name}</h1>
          <p className="text-sm text-slate-500">
            {establishment.slug} · {city ? `${city.name}/${city.state}` : "cidade não resolvida"}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge
            value={establishment.operationalStatus}
            label={labelOf(OPERATIONAL_STATUS_LABEL, establishment.operationalStatus)}
          />
          <Badge
            value={establishment.commercialStatus}
            label={labelOf(COMMERCIAL_STATUS_LABEL, establishment.commercialStatus)}
          />
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {TABS.map((item) => (
          <Button
            key={item.id}
            variant={tab === item.id ? "primary" : "secondary"}
            onClick={() => setTab(item.id)}
          >
            {item.label}
          </Button>
        ))}
      </div>

      {tab === "geral" ? (
        <div className="grid gap-4 lg:grid-cols-3">
          <Card className="space-y-3 lg:col-span-2">
            <h2 className="font-semibold">Dados gerais</h2>
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Nome">
                <Input value={name} onChange={(event) => setName(event.target.value)} />
              </Field>
              <Field label="Telefone">
                <Input value={phone} onChange={(event) => setPhone(event.target.value)} />
              </Field>
              <Field label="E-mail">
                <Input value={email} onChange={(event) => setEmail(event.target.value)} />
              </Field>
              <Field label="Categoria">
                <Select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
                  {(categoriesQuery.data ?? []).map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            <p className="text-sm text-slate-500">
              O GET do estabelecimento não devolve o endereço, só addressId ({shortId(establishment.addressId)}).
              O PUT exige o endereço completo — preencha para salvar.
            </p>
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Rua">
                <Input value={street} onChange={(event) => setStreet(event.target.value)} />
              </Field>
              <Field label="Número">
                <Input value={number} onChange={(event) => setNumber(event.target.value)} />
              </Field>
              <Field label="Bairro">
                <Input value={district} onChange={(event) => setDistrict(event.target.value)} />
              </Field>
              <Field label="Complemento">
                <Input value={complement} onChange={(event) => setComplement(event.target.value)} />
              </Field>
              <Field label="CEP">
                <Input value={zipCode} onChange={(event) => setZipCode(event.target.value)} />
              </Field>
              <Field label="Latitude">
                <Input value={latitude} onChange={(event) => setLatitude(event.target.value)} />
              </Field>
              <Field label="Longitude">
                <Input value={longitude} onChange={(event) => setLongitude(event.target.value)} />
              </Field>
            </div>
            <div className="flex justify-end">
              <Button loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
                Salvar
              </Button>
            </div>
          </Card>

          <div className="space-y-4">
            <Card className="space-y-2 text-sm">
              <h2 className="font-semibold">Identificação</h2>
              <p>Documento: {establishment.document}</p>
              <p>E-mail: {establishment.email}</p>
              <p>Cadastro: {formatDateTime(establishment.createdAtUtc)}</p>
              <p className="text-slate-500">
                O contrato não expõe o responsável/admin da loja após o cadastro.
              </p>
            </Card>
            <Card className="space-y-2">
              <h2 className="font-semibold">Bloqueio comercial</h2>
              <p className="text-sm text-slate-500">
                PATCH /status com commercialStatus. Isso não apaga dados da loja.
              </p>
              <div className="flex flex-col gap-2">
                <Button variant="secondary" onClick={() => setPendingStatus("Active")}>
                  Ativar
                </Button>
                <Button variant="secondary" onClick={() => setPendingStatus("SuspendedAdmin")}>
                  Bloquear (admin)
                </Button>
                <Button variant="ghost" onClick={() => setPendingStatus("Inactive")}>
                  Marcar inativo
                </Button>
              </div>
            </Card>
          </div>
        </div>
      ) : null}

      {tab === "assinatura" ? <SubscriptionPanel establishmentId={params.id} /> : null}
      {tab === "flags" ? <FeaturesPanel establishmentId={params.id} /> : null}
      {tab === "pedidos" ? (
        ordersQuery.isLoading ? (
          <Skeleton className="h-64" />
        ) : ordersQuery.error ? (
          <ErrorBox message={ordersQuery.error.message} onRetry={() => ordersQuery.refetch()} />
        ) : (ordersQuery.data?.items.length ?? 0) === 0 ? (
          <EmptyState title="Sem pedidos" message="GET /api/admin/orders?establishmentId= deste estabelecimento." />
        ) : (
          <OrderTable orders={ordersQuery.data!.items} establishments={[establishment]} />
        )
      ) : null}

      {pendingStatus ? (
        <ConfirmDialog
          title="Alterar status comercial"
          message={`Definir commercialStatus como ${labelOf(COMMERCIAL_STATUS_LABEL, pendingStatus)}?`}
          danger={pendingStatus !== "Active"}
          loading={statusMutation.isPending}
          onConfirm={() => statusMutation.mutate(pendingStatus)}
          onClose={() => setPendingStatus(null)}
        />
      ) : null}
    </div>
  );
}
