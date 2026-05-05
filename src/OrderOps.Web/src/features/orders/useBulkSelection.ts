import { useCallback, useMemo, useState } from "react";

export interface BulkSelection {
  count: number;
  ids: string[];
  isSelected: (id: string) => boolean;
  toggle: (id: string) => void;
  setMany: (ids: string[], on: boolean) => void;
  clear: () => void;
}

export function useBulkSelection(): BulkSelection {
  const [set, setSet] = useState<Set<string>>(() => new Set());

  const toggle = useCallback((id: string) => {
    setSet((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const setMany = useCallback((ids: string[], on: boolean) => {
    if (ids.length === 0) return;
    setSet((prev) => {
      const next = new Set(prev);
      if (on) ids.forEach((id) => next.add(id));
      else ids.forEach((id) => next.delete(id));
      return next;
    });
  }, []);

  const clear = useCallback(() => setSet(new Set()), []);

  const isSelected = useCallback((id: string) => set.has(id), [set]);

  const ids = useMemo(() => Array.from(set), [set]);

  return {
    count: set.size,
    ids,
    isSelected,
    toggle,
    setMany,
    clear,
  };
}
