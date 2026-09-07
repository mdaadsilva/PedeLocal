"use client";

import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { orderStatusLabel, paymentLabel } from "@/features/orders/labels";
import { formatCurrency, formatDateTime, shortId } from "@/lib/formatters";
import type { Order } from "@/lib/types";

export function OrderTable({ orders }: { orders: Order[] }) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
      <table className="min-w-full text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-4 py-3">Pedido</th>
            <th className="px-4 py-3">Quando</th>
            <th className="px-4 py-3">Tipo</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Pagamento</th>
            <th className="px-4 py-3 text-right">Total</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr key={order.id} className="border-t border-slate-100">
              <td className="px-4 py-3">
                <Link className="font-semibold text-brand-700 hover:underline" href={`/pedidos/${order.id}`}>
                  #{shortId(order.id)}
                </Link>
              </td>
              <td className="px-4 py-3 text-slate-600">{formatDateTime(order.createdAtUtc)}</td>
              <td className="px-4 py-3">
                <Badge value={order.type} label={order.type === "Pickup" ? "Retirada" : "Entrega"} />
              </td>
              <td className="px-4 py-3">
                <Badge value={order.status} label={orderStatusLabel(order.status)} />
              </td>
              <td className="px-4 py-3 text-slate-600">{paymentLabel(order.paymentMethod)}</td>
              <td className="px-4 py-3 text-right font-medium">{formatCurrency(order.total)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
