"use client";

import type { ReactNode } from "react";

export function Modal({
  title,
  children,
  onClose,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
}) {
  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
      <button className="absolute inset-0" onClick={onClose} type="button" aria-label="Fechar" />
      <div className="relative z-10 w-full max-w-lg rounded-2xl bg-white p-5 shadow-xl">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
          <button className="text-slate-400 hover:text-slate-700" onClick={onClose} type="button">
            Fechar
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
