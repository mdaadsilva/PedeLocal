"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { listCustomers } from "@/features/users/api";
import { formatDateTime } from "@/lib/formatters";

export default function UsersPage() {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["admin", "customers", page],
    queryFn: () => listCustomers({ page, pageSize: 20 }),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">Usuários</h1>
        <p className="text-sm text-slate-500">
          O backend só expõe GET /api/admin/customers (papel CUSTOMER). Não há listagem de
          ESTABLISHMENT_ADMIN, SUPER_ADMIN ou filtro por cidade/role.
        </p>
      </div>

      <Card className="text-sm text-slate-600">
        Entregadores ficam em{" "}
        <Link className="font-medium text-brand-700" href="/entregadores">
          Entregadores
        </Link>
        .
        Administradores de loja e Super Admins não têm endpoint de listagem.
      </Card>

      {query.isLoading ? (
        <Skeleton className="h-72" />
      ) : query.error ? (
        <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <EmptyState title="Nenhum cliente" message="Quando houver cadastros CUSTOMER, eles aparecem aqui." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Nome</th>
                  <th className="px-4 py-3">E-mail</th>
                  <th className="px-4 py-3">Telefone</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Cadastro</th>
                </tr>
              </thead>
              <tbody>
                {query.data!.items.map((user) => (
                  <tr key={user.id} className="border-t border-slate-100">
                    <td className="px-4 py-3 font-medium">{user.name}</td>
                    <td className="px-4 py-3">{user.email}</td>
                    <td className="px-4 py-3">{user.phoneNumber}</td>
                    <td className="px-4 py-3">
                      <Badge
                        value={user.isActive ? "Active" : "Inactive"}
                        label={user.isActive ? "Ativo" : "Inativo"}
                      />
                    </td>
                    <td className="px-4 py-3 text-slate-600">{formatDateTime(user.createdAtUtc)}</td>
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
