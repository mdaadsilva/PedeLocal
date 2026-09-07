"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { logout } from "@/features/auth/api";
import { useSession } from "@/features/auth/session-context";
import { Button } from "@/components/ui/button";
import { EmptyState, ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { cn } from "@/lib/cn";

const NAV = [
  { href: "/", label: "Dashboard" },
  { href: "/cidades", label: "Cidades" },
  { href: "/estabelecimentos", label: "Estabelecimentos" },
  { href: "/assinaturas", label: "Assinaturas" },
  { href: "/funcionalidades", label: "Funcionalidades" },
  { href: "/usuarios", label: "Usuários" },
  { href: "/entregadores", label: "Entregadores" },
  { href: "/pedidos", label: "Pedidos" },
  { href: "/entregas", label: "Entregas" },
  { href: "/auditoria", label: "Auditoria" },
];

export function PanelShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const session = useSession();
  const [open, setOpen] = useState(false);

  async function onLogout() {
    await logout();
    router.replace("/login");
    router.refresh();
  }

  if (session.isLoading) {
    return (
      <div className="mx-auto max-w-5xl space-y-3 p-8">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-32" />
        <Skeleton className="h-64" />
      </div>
    );
  }

  if (session.isForbidden) {
    return (
      <div className="mx-auto max-w-lg p-8">
        <EmptyState
          title="Acesso restrito"
          message="Esta área é exclusiva de contas SUPER_ADMIN. A sessão não será usada neste painel."
        />
        <div className="mt-4 flex justify-center">
          <Button onClick={onLogout}>Sair</Button>
        </div>
      </div>
    );
  }

  if (session.error) {
    return (
      <div className="mx-auto max-w-lg p-8">
        <ErrorBox message={session.error.message} />
        <div className="mt-4">
          <Button onClick={onLogout}>Sair</Button>
        </div>
      </div>
    );
  }

  const nav = (
    <nav className="space-y-1">
      {NAV.map((item) => {
        const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
        return (
          <Link
            key={item.href}
            href={item.href}
            onClick={() => setOpen(false)}
            className={cn(
              "block rounded-lg px-3 py-2 text-sm font-medium",
              active
                ? "bg-brand-50 text-brand-800"
                : "text-slate-600 hover:bg-slate-50 hover:text-slate-900",
            )}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );

  return (
    <div className="min-h-screen bg-slate-50 lg:grid lg:grid-cols-[16rem_1fr]">
      <aside className="hidden border-r border-slate-200 bg-white lg:flex lg:flex-col">
        <div className="px-5 py-6">
          <p className="text-lg font-semibold text-brand-800">PedeLocal</p>
          <p className="text-xs text-slate-500">Painel Super Admin</p>
        </div>
        <div className="flex-1 overflow-y-auto px-3 pb-6">{nav}</div>
      </aside>

      {open ? (
        <div className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden">
          <aside className="h-full w-72 overflow-y-auto bg-white p-4 shadow-xl">
            <div className="mb-4 flex items-center justify-between">
              <p className="font-semibold text-brand-800">PedeLocal</p>
              <button onClick={() => setOpen(false)} type="button">
                Fechar
              </button>
            </div>
            {nav}
          </aside>
        </div>
      ) : null}

      <div className="min-w-0">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-3 border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur">
          <div className="flex items-center gap-3">
            <button
              className="rounded-lg border border-slate-200 px-3 py-1.5 text-sm lg:hidden"
              onClick={() => setOpen(true)}
              type="button"
            >
              Menu
            </button>
            <div>
              <p className="text-sm font-semibold text-slate-900">
                {session.profile?.name ?? "Super Admin"}
              </p>
              <p className="text-xs text-slate-500">{session.profile?.email}</p>
            </div>
          </div>
          <Button variant="secondary" onClick={onLogout}>
            Sair
          </Button>
        </header>
        <main className="mx-auto max-w-6xl space-y-6 px-4 py-6">{children}</main>
      </div>
    </div>
  );
}
