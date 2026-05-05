import { useEffect } from "react";

const SUFFIX = "OrderOps";

export function useDocumentTitle(title: string | null | undefined) {
  useEffect(() => {
    if (!title) return;
    document.title = `${title} · ${SUFFIX}`;
  }, [title]);
}
