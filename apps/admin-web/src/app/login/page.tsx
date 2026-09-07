"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { login } from "@/features/auth/api";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { useToast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/errors";

export default function LoginPage() {
  const router = useRouter();
  const toast = useToast();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    try {
      await login(email, password);
      router.replace("/");
      router.refresh();
    } catch (error) {
      toast.push(
        error instanceof ApiError ? error.message : "Não foi possível entrar.",
        "error",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-[linear-gradient(180deg,#f3faf5,white)] px-4">
      <Card className="w-full max-w-md">
        <p className="text-sm font-semibold text-brand-700">PedeLocal</p>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">
          Painel Super Admin
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          Acesso exclusivo para contas SUPER_ADMIN. Tokens ficam em cookies httpOnly.
        </p>
        <form className="mt-6 space-y-4" onSubmit={onSubmit}>
          <Field label="E-mail">
            <Input
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </Field>
          <Field label="Senha">
            <Input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </Field>
          <Button className="w-full" loading={loading} type="submit">
            Entrar
          </Button>
        </form>
      </Card>
    </div>
  );
}
