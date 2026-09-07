"use client";

import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";

export function ConfirmDialog({
  title,
  message,
  confirmLabel = "Confirmar",
  danger,
  loading,
  onConfirm,
  onClose,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Modal title={title} onClose={onClose}>
      <p className="text-sm text-slate-600">{message}</p>
      <div className="mt-5 flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose} type="button">
          Cancelar
        </Button>
        <Button
          variant={danger ? "danger" : "primary"}
          loading={loading}
          onClick={onConfirm}
          type="button"
        >
          {confirmLabel}
        </Button>
      </div>
    </Modal>
  );
}
