"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ErrorBox, Skeleton } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import {
  FEATURE_KEY_LABEL,
  KNOWN_FEATURE_KEYS,
  listFeatures,
  setFeature,
} from "@/features/features/api";
import { ApiError } from "@/lib/api/errors";

export function FeaturesPanel({ establishmentId }: { establishmentId: string }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ["admin", "features", establishmentId],
    queryFn: () => listFeatures(establishmentId),
  });

  const mutation = useMutation({
    mutationFn: ({ featureKey, enabled }: { featureKey: string; enabled: boolean }) =>
      setFeature(establishmentId, featureKey, { enabled }),
    onSuccess: () => {
      toast.push("Funcionalidade atualizada.", "success");
      queryClient.invalidateQueries({ queryKey: ["admin", "features", establishmentId] });
    },
    onError: (error: Error) => {
      toast.push(error instanceof ApiError ? error.message : error.message, "error");
    },
  });

  if (query.isLoading) return <Skeleton className="h-48" />;
  if (query.error) return <ErrorBox message={query.error.message} onRetry={() => query.refetch()} />;

  const current = new Map((query.data ?? []).map((item) => [item.featureKey, item]));
  const extra = (query.data ?? []).filter(
    (item) => !KNOWN_FEATURE_KEYS.includes(item.featureKey as (typeof KNOWN_FEATURE_KEYS)[number]),
  );
  const keys = [...KNOWN_FEATURE_KEYS, ...extra.map((item) => item.featureKey)];

  return (
    <Card className="space-y-4">
      <p className="text-sm text-slate-500">
        Chaves conhecidas vêm de FeatureKeys no backend. PUT cria ou atualiza a flag do
        estabelecimento — não há catálogo separado.
      </p>
      <div className="space-y-3">
        {keys.map((key) => {
          const feature = current.get(key);
          const enabled = feature?.enabled ?? false;
          return (
            <div key={key} className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-3 last:border-0">
              <div>
                <p className="font-medium">{FEATURE_KEY_LABEL[key] ?? key}</p>
                <p className="text-xs text-slate-500">{key}</p>
              </div>
              <div className="flex items-center gap-2">
                <Badge value={enabled ? "Active" : "Inactive"} label={enabled ? "Ligada" : "Desligada"} />
                <Button
                  variant="secondary"
                  loading={mutation.isPending}
                  onClick={() => mutation.mutate({ featureKey: key, enabled: !enabled })}
                >
                  {enabled ? "Desabilitar" : "Habilitar"}
                </Button>
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
}
