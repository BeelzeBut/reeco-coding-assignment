import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { apiFetch } from "@/api/client";
import { ApiError } from "@/api/types";

interface HealthResponse {
  status: string;
}

export function OrdersPage() {
  const health = useQuery({
    queryKey: ["health"],
    queryFn: ({ signal }) => apiFetch<HealthResponse>("/api/healthz", { signal }),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Orders</CardTitle>
        <CardDescription>
          Listing, filters, sort, and bulk actions land here in a later slice.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <p className="text-muted-foreground">API wiring smoke-test:</p>
        {health.isPending && <Skeleton className="h-4 w-40" />}
        {health.isError && (
          <p className="text-destructive">
            API unreachable
            {health.error instanceof ApiError ? ` (${health.error.code})` : ""}
          </p>
        )}
        {health.data && (
          <p>
            <code className="rounded-sm bg-muted px-1 py-0.5 font-mono text-xs">/api/healthz</code>{" "}
            →{" "}
            <span className="font-mono text-xs text-primary">{health.data.status}</span>
          </p>
        )}
      </CardContent>
    </Card>
  );
}
