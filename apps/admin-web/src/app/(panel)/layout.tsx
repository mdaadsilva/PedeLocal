import { PanelShell } from "@/components/layout/panel-shell";
import { SessionProvider } from "@/features/auth/session-context";
import { RealtimeBridge } from "@/features/realtime/realtime-bridge";

export default function PanelLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <SessionProvider>
      <RealtimeBridge />
      <PanelShell>{children}</PanelShell>
    </SessionProvider>
  );
}
