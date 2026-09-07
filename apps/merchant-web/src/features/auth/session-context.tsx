"use client";

import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { getProfile } from "@/features/auth/api";
import { resolveMyEstablishments } from "@/features/establishments/api";
import { canAccessMerchantPanel } from "@/lib/auth/roles";
import type { Establishment, Profile } from "@/lib/types";

type SessionValue = {
  profile?: Profile;
  establishments: Establishment[];
  establishment?: Establishment;
  isLoading: boolean;
  isForbidden: boolean;
  error?: Error;
};

const SessionContext = createContext<SessionValue | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const profileQuery = useQuery({
    queryKey: ["me", "profile"],
    queryFn: getProfile,
    retry: false,
  });

  const allowed = profileQuery.data
    ? canAccessMerchantPanel(profileQuery.data.roles)
    : false;

  const establishmentsQuery = useQuery({
    queryKey: ["me", "establishments"],
    queryFn: resolveMyEstablishments,
    enabled: allowed,
  });

  const value = useMemo<SessionValue>(() => {
    const establishments = establishmentsQuery.data ?? [];
    return {
      profile: profileQuery.data,
      establishments,
      establishment: establishments[0],
      isLoading: profileQuery.isLoading || (allowed && establishmentsQuery.isLoading),
      isForbidden: Boolean(profileQuery.data && !allowed),
      error: (profileQuery.error ?? establishmentsQuery.error) as Error | undefined,
    };
  }, [allowed, establishmentsQuery, profileQuery]);

  return (
    <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
  );
}

export function useSession() {
  const value = useContext(SessionContext);
  if (!value) {
    throw new Error("useSession deve ser usado dentro de SessionProvider");
  }
  return value;
}
