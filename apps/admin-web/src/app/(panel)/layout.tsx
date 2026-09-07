import { PanelShell } from "@/components/layout/panel-shell";
import { SessionProvider } from "@/features/auth/session-context";

export default function PanelLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <SessionProvider>
      <PanelShell>{children}</PanelShell>
    </SessionProvider>
  );
}
