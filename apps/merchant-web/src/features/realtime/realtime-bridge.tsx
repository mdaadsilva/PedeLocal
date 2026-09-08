"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useToast } from "@/components/ui/toast";
import { useSession } from "@/features/auth/session-context";
import { HubEvents } from "@/lib/realtime/events";
import { createHubConnection } from "@/lib/realtime/hub-client";
import { asRecord, readString } from "@/lib/realtime/parse";

/**
 * Único ponto de bind do hub no merchant. Telas só leem o cache REST.
 * Grupos vêm do JWT — o cliente não escolhe establishmentId.
 */
export function RealtimeBridge() {
  const { profile, isForbidden, isLoading } = useSession();
  const queryClient = useQueryClient();
  const toast = useToast();
  const seenOrders = useRef(new Set<string>());

  useEffect(() => {
    if (isLoading || isForbidden || !profile) return;

    const connection = createHubConnection();

    const invalidateOrders = () => {
      void queryClient.invalidateQueries({ queryKey: ["orders"] });
    };

    connection.on(HubEvents.OrderCreated, (payload: unknown) => {
      const record = asRecord(payload);
      const orderId = record ? readString(record, "orderId") : null;
      if (orderId && seenOrders.current.has(orderId)) return;
      if (orderId) seenOrders.current.add(orderId);
      toast.push("Novo pedido recebido", "info");
      invalidateOrders();
    });

    connection.on(HubEvents.OrderStatusChanged, invalidateOrders);
    connection.on(HubEvents.DeliveryAccepted, invalidateOrders);
    connection.on(HubEvents.DeliveryCompleted, invalidateOrders);
    connection.on(HubEvents.EstablishmentSuspended, () => {
      toast.push("Estabelecimento suspenso por assinatura.", "error");
      void queryClient.invalidateQueries({ queryKey: ["me", "establishments"] });
    });

    connection.onreconnected(() => {
      invalidateOrders();
    });

    void connection.start().catch(() => undefined);

    return () => {
      connection.off(HubEvents.OrderCreated);
      connection.off(HubEvents.OrderStatusChanged);
      connection.off(HubEvents.DeliveryAccepted);
      connection.off(HubEvents.DeliveryCompleted);
      connection.off(HubEvents.EstablishmentSuspended);
      void connection.stop();
    };
  }, [isForbidden, isLoading, profile, queryClient, toast]);

  return null;
}
