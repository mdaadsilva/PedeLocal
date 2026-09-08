"use client";

import { useEffect } from "react";
import { useSession } from "@/features/auth/session-context";
import { createHubConnection } from "@/lib/realtime/hub-client";

/**
 * SUPER_ADMIN não entra em grupo de plataforma (`docs/REALTIME_EVENTS.md`).
 * Conectamos o hub com o JWT real, sem assinar eventos inventados e sem
 * atualizar listagens admin a partir de um canal que o servidor não emite.
 */
export function RealtimeBridge() {
  const { profile, isForbidden, isLoading } = useSession();

  useEffect(() => {
    if (isLoading || isForbidden || !profile) return;

    const connection = createHubConnection();
    void connection.start().catch(() => undefined);

    return () => {
      void connection.stop();
    };
  }, [isForbidden, isLoading, profile]);

  return null;
}
