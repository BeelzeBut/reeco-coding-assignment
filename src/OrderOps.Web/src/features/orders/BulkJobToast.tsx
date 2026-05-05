import { useCallback, useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, Info, Loader2, X, XCircle } from "lucide-react";
import { ApiError } from "@/api/types";
import { Button } from "@/components/ui/button";
import { getJob, type JobStatus } from "@/features/orders/api";
import { useBulkCompleted } from "@/lib/useBulkJobEvents";
import { cn } from "@/lib/utils";

const TERMINAL: JobStatus[] = ["completed", "failed"];
const AUTO_DISMISS_MS = 8_000;

interface BulkJobToastProps {
  jobId: string;
  onDismiss: () => void;
}

export function BulkJobToast({ jobId, onDismiss }: BulkJobToastProps) {
  const qc = useQueryClient();
  const [terminal, setTerminal] = useState(false);
  const [interacted, setInteracted] = useState(false);

  const query = useQuery({
    queryKey: ["job", jobId],
    queryFn: ({ signal }) => getJob(jobId, signal),
    refetchInterval: terminal ? false : 1_000,
    refetchIntervalInBackground: true,
    staleTime: 0,
  });

  useEffect(() => {
    if (query.data && TERMINAL.includes(query.data.status)) {
      setTerminal(true);
      qc.invalidateQueries({ queryKey: ["orders"] });
    }
  }, [query.data, qc]);

  const refetchRef = useRef(query.refetch);
  useEffect(() => {
    refetchRef.current = query.refetch;
  });
  const onCompleted = useCallback(
    (incomingJobId: string) => {
      if (incomingJobId !== jobId) return;
      refetchRef.current();
    },
    [jobId]
  );
  useBulkCompleted(onCompleted);

  useEffect(() => {
    if (!terminal || interacted) return;
    const t = setTimeout(onDismiss, AUTO_DISMISS_MS);
    return () => clearTimeout(t);
  }, [terminal, interacted, onDismiss]);

  const data = query.data;
  const status = data?.status ?? "processing";
  const total = data?.progress.total ?? 0;
  const done = (data?.progress.completed ?? 0) + (data?.progress.failed ?? 0);
  const completed = data?.progress.completed ?? 0;
  const failed = data?.progress.failed ?? 0;
  const pct = total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0;

  return (
    <div className="pointer-events-auto relative w-80 rounded-xl border bg-card shadow-2xl ring-1 ring-black/5 dark:ring-white/10">
      <div className="flex items-start gap-3 p-4">
        <span
          className={cn(
            "mt-0.5 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full",
            status === "processing" && "bg-primary/10 text-primary",
            status === "completed" && "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
            status === "failed" && "bg-destructive/10 text-destructive"
          )}
        >
          {status === "processing" && <Loader2 className="h-4 w-4 animate-spin" />}
          {status === "completed" && <CheckCircle2 className="h-4 w-4" />}
          {status === "failed" && <XCircle className="h-4 w-4" />}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-medium">
              {status === "processing" && "Processing bulk job"}
              {status === "completed" && "Bulk job complete"}
              {status === "failed" && "Bulk job failed"}
            </span>
            <button
              type="button"
              onClick={onDismiss}
              aria-label="Dismiss"
              className="rounded-sm text-muted-foreground transition-colors hover:text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <p className="mt-0.5 truncate font-mono text-xs text-muted-foreground">{jobId}</p>

          {query.isError && (
            <p className="mt-2 text-xs text-destructive">
              {query.error instanceof ApiError
                ? `${query.error.message} (${query.error.code})`
                : "Lost contact with the job."}
            </p>
          )}

          {data && (
            <>
              <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-muted">
                <div
                  className={cn(
                    "h-full transition-all duration-300",
                    status === "failed" ? "bg-destructive" : "bg-primary"
                  )}
                  style={{ width: `${pct}%` }}
                />
              </div>
              <div className="mt-2 flex items-center justify-between text-xs">
                <span className="text-muted-foreground tabular-nums">
                  {done.toLocaleString()} / {total.toLocaleString()}
                </span>
                <span className="flex items-center gap-2 tabular-nums">
                  <span className="text-emerald-600 dark:text-emerald-400">
                    {completed.toLocaleString()} ok
                  </span>
                  <span
                    className={cn(
                      failed > 0 ? "text-destructive" : "text-muted-foreground"
                    )}
                  >
                    {failed.toLocaleString()} failed
                  </span>
                </span>
              </div>
              {terminal && failed > 0 && (
                <FailureExplainer onOpen={() => setInteracted(true)} />
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function FailureExplainer({ onOpen }: { onOpen: () => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onClickAway = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onClickAway);
    return () => document.removeEventListener("mousedown", onClickAway);
  }, [open]);

  return (
    <div ref={ref} className="relative mt-2">
      <Button
        variant="ghost"
        size="sm"
        className="h-7 px-2 text-xs"
        onClick={() => {
          setOpen((v) => {
            if (!v) onOpen();
            return !v;
          });
        }}
      >
        <Info className="h-3.5 w-3.5" />
        Why some failed
      </Button>
      {open && (
        <div className="absolute bottom-full left-0 z-50 mb-2 w-72 rounded-md border bg-popover p-3 text-xs text-popover-foreground shadow-lg">
          <p className="font-medium">An order is counted as failed if:</p>
          <ul className="mt-1 list-disc space-y-1 pl-4 text-muted-foreground">
            <li>The order id does not exist.</li>
            <li>The order is already cancelled.</li>
            <li>Another writer changed it during the run (version conflict).</li>
          </ul>
        </div>
      )}
    </div>
  );
}
