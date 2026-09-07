"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { createContext, useContext, useEffect, useMemo, type ReactNode } from "react";
import { getProfile, logout } from "@/features/auth/api";
import { ApiError } from "@/lib/api/errors";
import { canAccessAdminPanel } from "@/lib/auth/roles";
import type { Profile } from "@/lib/types";

type SessionValue = {
  profile?: Profile;
  isLoading: boolean;
  isForbidden: boolean;
  error?: Error;
};

const SessionContext = createContext<SessionValue | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const profileQuery = useQuery({
    queryKey: ["me", "profile"],
    queryFn: getProfile,
    retry: false,
  });

  const allowed = profileQuery.data
    ? canAccessAdminPanel(profileQuery.data.roles)
    : false;

  useEffect(() => {
    const error = profileQuery.error;
    if (error instanceof ApiError && error.status === 401) {
      void logout().finally(() => {
        router.replace("/login");
        router.refresh();
      });
    }
  }, [profileQuery.error, router]);

  const value = useMemo<SessionValue>(
    () => ({
      profile: profileQuery.data,
      isLoading: profileQuery.isLoading,
      isForbidden: Boolean(profileQuery.data && !allowed),
      error: profileQuery.error as Error | undefined,
    }),
    [allowed, profileQuery.data, profileQuery.error, profileQuery.isLoading],
  );

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
