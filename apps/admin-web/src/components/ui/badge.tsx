import { cn } from "@/lib/cn";

const tones: Record<string, string> = {
  Pending: "bg-amber-50 text-amber-800",
  Accepted: "bg-sky-50 text-sky-800",
  Preparing: "bg-indigo-50 text-indigo-800",
  Ready: "bg-emerald-50 text-emerald-800",
  WaitingDriver: "bg-orange-50 text-orange-800",
  DriverAssigned: "bg-orange-50 text-orange-800",
  PickedUp: "bg-orange-50 text-orange-800",
  OutForDelivery: "bg-orange-50 text-orange-800",
  Delivered: "bg-emerald-100 text-emerald-900",
  PickupCompleted: "bg-emerald-100 text-emerald-900",
  Cancelled: "bg-red-50 text-red-700",
  Delivery: "bg-slate-100 text-slate-700",
  Pickup: "bg-brand-50 text-brand-800",
  Active: "bg-emerald-50 text-emerald-800",
  OverdueGrace: "bg-amber-50 text-amber-800",
  SuspendedPayment: "bg-red-50 text-red-700",
  SuspendedAdmin: "bg-red-50 text-red-700",
  Inactive: "bg-slate-100 text-slate-600",
  Open: "bg-emerald-50 text-emerald-800",
  ClosedManual: "bg-slate-100 text-slate-600",
  ClosedSchedule: "bg-slate-100 text-slate-600",
  Paused: "bg-amber-50 text-amber-800",
  PendingDocuments: "bg-amber-50 text-amber-800",
  UnderReview: "bg-indigo-50 text-indigo-800",
  Approved: "bg-emerald-50 text-emerald-800",
  Rejected: "bg-red-50 text-red-700",
  Eligible: "bg-emerald-50 text-emerald-800",
  DocumentExpired: "bg-red-50 text-red-700",
  Offline: "bg-slate-100 text-slate-600",
  Available: "bg-emerald-50 text-emerald-800",
  Assigned: "bg-sky-50 text-sky-800",
  Busy: "bg-orange-50 text-orange-800",
  Blocked: "bg-red-50 text-red-700",
  Completed: "bg-emerald-100 text-emerald-900",
};

export function Badge({
  value,
  label,
}: {
  value: string;
  label?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex rounded-full px-2.5 py-1 text-xs font-semibold",
        tones[value] ?? "bg-slate-100 text-slate-700",
      )}
    >
      {label ?? value}
    </span>
  );
}
