import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/api/types";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogBody,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { submitBulkAction, type BulkAction } from "@/features/orders/api";
import { BULK_ACTIONS, BULK_MAX_BATCH } from "@/features/orders/constants";

const REASON_MAX = 4096;

interface BulkConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  action: BulkAction | null;
  orderIds: string[];
  onSubmitted: (jobId: string) => void;
}

export function BulkConfirmDialog({
  open,
  onClose,
  action,
  orderIds,
  onSubmitted,
}: BulkConfirmDialogProps) {
  const qc = useQueryClient();
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (!open) setReason("");
  }, [open]);

  const meta = action ? BULK_ACTIONS.find((a) => a.value === action) : null;
  const overCap = orderIds.length > BULK_MAX_BATCH;

  const mutation = useMutation({
    mutationFn: () =>
      submitBulkAction({
        orderIds,
        action: action!,
        reason: reason.trim() ? reason.trim() : undefined,
      }),
    onSuccess: (res) => {
      onSubmitted(res.jobId);
      qc.invalidateQueries({ queryKey: ["orders"] });
      onClose();
    },
  });

  if (!action || !meta) return null;

  const disabled = mutation.isPending || orderIds.length === 0 || overCap;

  return (
    <AlertDialog open={open} onClose={mutation.isPending ? () => undefined : onClose}>
      <AlertDialogHeader>
        <AlertDialogTitle>
          {meta.verb} {orderIds.length.toLocaleString()}{" "}
          {orderIds.length === 1 ? "order" : "orders"}?
        </AlertDialogTitle>
        <AlertDialogDescription>{meta.description}</AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogBody>
        <label className="block space-y-1.5">
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Reason{" "}
            <span className="text-muted-foreground/70 normal-case">(optional)</span>
          </span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={mutation.isPending}
            maxLength={REASON_MAX}
            rows={3}
            placeholder={`Notes for this ${meta.label.toLowerCase()} batch…`}
            className="w-full resize-none rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 disabled:cursor-not-allowed disabled:opacity-50"
          />
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>Recorded with the job for audit.</span>
            <span className="tabular-nums">
              {reason.length.toLocaleString()} / {REASON_MAX.toLocaleString()}
            </span>
          </div>
        </label>

        {overCap && (
          <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs text-destructive">
            Bulk batches are capped at {BULK_MAX_BATCH.toLocaleString()} orders. You have{" "}
            {orderIds.length.toLocaleString()} selected — narrow the selection and retry.
          </div>
        )}

        {mutation.isError && (
          <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs text-destructive">
            {mutation.error instanceof ApiError
              ? `${mutation.error.message} (${mutation.error.code})`
              : "Failed to submit bulk action."}
          </div>
        )}
      </AlertDialogBody>
      <AlertDialogFooter>
        <Button onClick={() => mutation.mutate()} disabled={disabled}>
          {mutation.isPending ? "Submitting…" : `Confirm ${meta.label.toLowerCase()}`}
        </Button>
        <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
      </AlertDialogFooter>
    </AlertDialog>
  );
}
