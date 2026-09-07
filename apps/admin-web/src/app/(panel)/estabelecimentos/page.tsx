"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Input, Select } from "@/components/ui/input";
import { listCities } from "@/features/cities/api";
import { listAllEstablishments, listEstablishmentCategories } from "@/features/establishments/api";
import { COMMERCIAL_STATUS_LABEL, OPERATIONAL_STATUS_LABEL, labelOf } from "@/features/orders/labels";
import { formatDateTime } from "@/lib/formatters";

export default function EstablishmentsPage() {
  const [search, setSearch] = useState("");
  const [cityId, setCityId] = useState("");
  const [commercialStatus, setCommercialStatus] = useState("");

  const citiesQuery = useQuery({ queryKey: ["admin", "cities"], queryFn: listCities });
  const categoriesQuery = useQuery({
    queryKey: ["admin", "categories"],
    queryFn: listEstablishmentCategories,
  });
  const query = useQuery({
    queryKey: ["admin", "establishments", "all"],
    queryFn: listAllEstablishments,
  });

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (query.data ?? []).filter((item) => {
      if (cityId && item.cityId !== cityId) return false;
      if (commercialStatus && item.commercialStatus !== commercialStatus) return false;
      if (!term) return true;
      return [item.name, item.slug, item.email, item.document].some((value) =>
        value.toLowerCase().includes(term),
      );
    });
  }, [cityId, commercialStatus, query.data, search]);

  const cityNames = new Map((citiesQuery.data ?? []).map((city) => [city.id, `${city.name}/${city.state}`]));
  const categoryNames = new Map((categoriesQuery.data ?? []).map((item) => [item.id, item.name]));

  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Estabelecimentos</h1>
          <p className="text-sm text-slate-500">
            A listagem admin não tem busca/filtro. A pesquisa abaixo é no cliente sobre as páginas carregadas.
          </p>
        </div>
        <Link href="/estabelecimentos/novo">
          <Button>Novo estabelecimento</Button>
        </Link>
      </div>

      <div className="grid gap-3 md:grid-cols-3">
        <Input
          placeholder="Buscar por nome, slug, e-mail ou documento"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Select value={cityId} onChange={(event) => setCityId(event.target.value)}>
          <option value="">Todas as cidades</option>
          {(citiesQuery.data ?? []).map((city) => (
            <option key={city.id} value={city.id}>
              {city.name}/{city.state}
            </option>
          ))}
        </Select>
        <Select value={commercialStatus} onChange={(event) => setCommercialStatus(event.target.value)}>
          <option value="">Todos os status comerciais</option>
          {Object.entries(COMMERCIAL_STATUS_LABEL).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </Select>
      </div>

      {filtered.length === 0 ? (
        <EmptyState title="Nenhum estabelecimento" message="Ajuste os filtros ou cadastre uma nova loja." />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Nome</th>
                <th className="px-4 py-3">Cidade</th>
                <th className="px-4 py-3">Categoria</th>
                <th className="px-4 py-3">Operação</th>
                <th className="px-4 py-3">Comercial</th>
                <th className="px-4 py-3">Cadastro</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((item) => (
                <tr key={item.id} className="border-t border-slate-100">
                  <td className="px-4 py-3">
                    <Link className="font-medium text-brand-700 hover:underline" href={`/estabelecimentos/${item.id}`}>
                      {item.name}
                    </Link>
                    <p className="text-xs text-slate-500">{item.slug}</p>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{cityNames.get(item.cityId) ?? "—"}</td>
                  <td className="px-4 py-3 text-slate-600">{categoryNames.get(item.categoryId) ?? "—"}</td>
                  <td className="px-4 py-3">
                    <Badge
                      value={item.operationalStatus}
                      label={labelOf(OPERATIONAL_STATUS_LABEL, item.operationalStatus)}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <Badge
                      value={item.commercialStatus}
                      label={labelOf(COMMERCIAL_STATUS_LABEL, item.commercialStatus)}
                    />
                  </td>
                  <td className="px-4 py-3 text-slate-600">{formatDateTime(item.createdAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
