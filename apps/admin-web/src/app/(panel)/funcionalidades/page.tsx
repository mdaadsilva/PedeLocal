"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Select } from "@/components/ui/input";
import { listAllEstablishments } from "@/features/establishments/api";
import { FeaturesPanel } from "@/features/features/features-panel";

export default function FeaturesPage() {
  const [establishmentId, setEstablishmentId] = useState("");
  const query = useQuery({
    queryKey: ["admin", "establishments", "all"],
    queryFn: listAllEstablishments,
  });

  if (query.isLoading) return <Skeleton className="h-64" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;

  const establishments = query.data ?? [];

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Funcionalidades</h1>
        <p className="text-sm text-slate-500">
          GET/PUT /api/admin/establishments/{"{id}"}/features. As flags existem por loja; não há
          endpoint de catálogo global.
        </p>
      </div>
      {establishments.length === 0 ? (
        <EmptyState title="Nenhum estabelecimento" message="Cadastre uma loja para gerenciar flags." />
      ) : (
        <>
          <Select
            value={establishmentId}
            onChange={(event) => setEstablishmentId(event.target.value)}
          >
            <option value="">Selecione o estabelecimento</option>
            {establishments.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </Select>
          {establishmentId ? (
            <FeaturesPanel establishmentId={establishmentId} />
          ) : (
            <EmptyState
              title="Escolha um estabelecimento"
              message="As flags são lidas e gravadas só no contexto da loja."
            />
          )}
        </>
      )}
    </div>
  );
}
