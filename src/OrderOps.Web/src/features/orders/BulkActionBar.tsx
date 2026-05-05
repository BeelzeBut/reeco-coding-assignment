import { useState } from "react";
import { CheckCircle2, Flag, X, XOctagon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { BulkConfirmDialog } from "@/features/orders/BulkConfirmDialog";
import type { BulkAction } from "@/features/orders/api";
import { BULK_MAX_BATCH } from "@/features/orders/constants";

interface BulkActionBarProps {
  selectedIds: string[];
  onClear: () => void;
  onJobSubmitted: (jobId: string) => void;
}

export function BulkActionBar({ selectedIds, onClear, onJobSubmitted }: BulkActionBarProps) {
  const [pendingAction, setPendingAction] = useState<BulkAction | null>(null);
  const overCap = selectedIds.length > BULK_MAX_BATCH;

  if (selectedIds.length === 0) return null;

  return (
    <>
      <div className="sticky top-0 z-20 -mx-1 flex flex-wrap items-center gap-2 rounded-xl border border-primary/30 bg-primary/5 p-3 px-4 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-primary/5">
        <Badge variant="default" className="gap-1.5">
          <span className="tabular-nums">{selectedIds.length.toLocaleString()}</span>
          selected
        </Badge>

        {overCap && (
          <span className="text-xs text-destructive">
            Over the {BULK_MAX_BATCH.toLocaleString()} cap — narrow the selection.
          </span>
        )}

        <div className="ml-auto flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => setPendingAction("approve")}
            disabled={overCap}
          >
            <CheckCircle2 />
            Approve
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => setPendingAction("reject")}
            disabled={overCap}
          >
            <XOctagon />
            Reject
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => setPendingAction("flag")}
            disabled={overCap}
          >
            <Flag />
            Flag
          </Button>
          <Button size="sm" variant="ghost" onClick={onClear}>
            <X />
            Clear
          </Button>
        </div>
      </div>

      <BulkConfirmDialog
        open={pendingAction !== null}
        onClose={() => setPendingAction(null)}
        action={pendingAction}
        orderIds={selectedIds}
        onSubmitted={(jobId) => {
          setPendingAction(null);
          onClear();
          onJobSubmitted(jobId);
        }}
      />
    </>
  );
}
