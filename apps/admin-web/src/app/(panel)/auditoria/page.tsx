"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { Pagination } from "@/components/ui/pagination";
import { listAuditLogs } from "@/features/audit/api";
import { formatDateTime, shortId } from "@/lib/formatters";

export default function AuditPage() {
  const [entityType, setEntityType] = useState("");
  const [actorUserId, setActorUserId] = useState("");
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin", "audit", entityType, actorUserId, page],
    queryFn: () =>
      listAuditLogs({
        entityType: entityType || undefined,
        actorUserId: actorUserId || undefined,
        page,
        pageSize: 20,
      }),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Auditoria</h1>
        <p className="text-sm text-slate-500">
          GET /api/admin/audit-logs?entityType=&actorUserId=. Não há filtro de ação ou intervalo de datas.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <Input
          placeholder="Tipo de entidade (ex.: Establishment)"
          value={entityType}
          onChange={(event) => {
            setPage(1);
            setEntityType(event.target.value);
          }}
        />
        <Input
          placeholder="ID do usuário ator (GUID)"
          value={actorUserId}
          onChange={(event) => {
            setPage(1);
            setActorUserId(event.target.value);
          }}
        />
      </div>

      {query.isLoading ? (
        <Skeleton className="h-80" />
      ) : query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <EmptyState title="Sem eventos" message="Quando houver logs de auditoria, eles aparecem aqui." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Quando</th>
                  <th className="px-4 py-3">Ação</th>
                  <th className="px-4 py-3">Entidade</th>
                  <th className="px-4 py-3">Ator</th>
                  <th className="px-4 py-3">Origem</th>
                </tr>
              </thead>
              <tbody>
                {query.data!.items.map((item) => (
                  <tr key={item.id} className="border-t border-slate-100">
                    <td className="px-4 py-3 whitespace-nowrap">{formatDateTime(item.createdAtUtc)}</td>
                    <td className="px-4 py-3 font-medium">{item.action}</td>
                    <td className="px-4 py-3">
                      {item.entityType}
                      {item.entityId ? ` · ${shortId(item.entityId)}` : ""}
                    </td>
                    <td className="px-4 py-3">{item.actorUserId ? shortId(item.actorUserId) : "—"}</td>
                    <td className="px-4 py-3 text-slate-500">
                      {[item.ip, item.userAgent].filter(Boolean).join(" · ") || "—"}
                    </td>
                  </tr>
                ))}
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
    </div>
  );
}
