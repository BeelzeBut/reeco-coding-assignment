import { useEffect } from "react";

type Listener = (jobId: string) => void;

let source: EventSource | null = null;
let refCount = 0;
const listeners = new Set<Listener>();

function ensureSource() {
  if (source) return;
  source = new EventSource("/api/events");
  source.onmessage = (e) => {
    if (!e.data) return;
    try {
      const parsed = JSON.parse(e.data) as { type?: string; data?: { jobId?: string } };
      if (parsed.type === "bulk_completed" && parsed.data?.jobId) {
        const id = parsed.data.jobId;
        listeners.forEach((cb) => cb(id));
      }
    } catch {
      // ignore malformed frames; the API should never send them
    }
  };
  source.onerror = () => {
    // EventSource auto-reconnects; nothing to do. Polling is the safety net.
  };
}

function teardownIfIdle() {
  if (refCount === 0 && source) {
    source.close();
    source = null;
  }
}

export function useBulkCompleted(onCompleted: Listener) {
  useEffect(() => {
    refCount++;
    ensureSource();
    listeners.add(onCompleted);
    return () => {
      listeners.delete(onCompleted);
      refCount--;
      teardownIfIdle();
    };
  }, [onCompleted]);
}
