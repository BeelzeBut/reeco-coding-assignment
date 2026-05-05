import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Building2, ChevronLeft, ChevronRight, Inbox, Star } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ApiError } from "@/api/types";
import { listSuppliers, type SupplierListItem } from "@/features/suppliers/api";
import { useDocumentTitle } from "@/hooks/use-document-title";

const PAGE_SIZE = 25;
const COLUMN_COUNT = 6;

export function SuppliersPage() {
  useDocumentTitle("Suppliers");
  const navigate = useNavigate();
  const [offset, setOffset] = useState(0);

  const query = useQuery({
    queryKey: ["suppliers", { limit: PAGE_SIZE, offset }],
    queryFn: ({ signal }) => listSuppliers({ limit: PAGE_SIZE, offset, signal }),
    placeholderData: keepPreviousData,
  });

  const total = query.data?.total ?? 0;
  const start = total === 0 ? 0 : offset + 1;
  const end = Math.min(offset + PAGE_SIZE, total);
  const canPrev = offset > 0;
  const canNext = offset + PAGE_SIZE < total;

  return (
    <div className="space-y-4">
      <header className="flex items-end justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Suppliers</h1>
          <p className="text-sm text-muted-foreground">
            Every supplier in the catalog. Click a row for performance metrics and recent orders.
          </p>
        </div>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Building2 className="h-4 w-4" />
          <span className="tabular-nums">
            {query.data ? total.toLocaleString() : "—"} total
          </span>
        </div>
      </header>

      <Card className="overflow-hidden shadow-sm">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 border-b bg-muted/30 py-3">
          <CardTitle className="text-base font-medium">
            <Building2 className="mr-2 inline h-4 w-4 text-primary" />
            Catalog
          </CardTitle>
          <CardDescription>
            {query.data
              ? `${start.toLocaleString()}–${end.toLocaleString()} of ${total.toLocaleString()}`
              : "Loading…"}
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {query.isError && (
            <div className="border-b bg-destructive/5 px-4 py-3 text-sm text-destructive">
              Failed to load suppliers
              {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
            </div>
          )}

          <Table>
            <TableHeader className="sticky top-0 z-10 bg-muted/50 backdrop-blur">
              <TableRow className="hover:bg-transparent">
                <TableHead>Supplier</TableHead>
                <TableHead>Country</TableHead>
                <TableHead>Email</TableHead>
                <TableHead className="text-right">Rating</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Joined</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {query.isPending &&
                Array.from({ length: 8 }).map((_, i) => (
                  <TableRow key={`skel-${i}`}>
                    {Array.from({ length: COLUMN_COUNT }).map((__, j) => (
                      <TableCell key={j}>
                        <Skeleton className="h-4 w-full" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              {query.data?.data.map((supplier) => (
                <SupplierRow
                  key={supplier.id}
                  supplier={supplier}
                  onOpen={() => navigate(`/suppliers/${supplier.id}`)}
                />
              ))}
              {query.data && query.data.data.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMN_COUNT}>
                    <div className="flex flex-col items-center justify-center gap-2 py-12 text-muted-foreground">
                      <Inbox className="h-8 w-8 opacity-50" />
                      <p className="text-sm">No suppliers found.</p>
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="flex items-center justify-between">
        <span className="text-xs text-muted-foreground">
          Page {Math.floor(offset / PAGE_SIZE) + 1} of {Math.max(1, Math.ceil(total / PAGE_SIZE))}
        </span>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!canPrev || query.isFetching}
            onClick={() => setOffset((o) => Math.max(0, o - PAGE_SIZE))}
          >
            <ChevronLeft />
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!canNext || query.isFetching}
            onClick={() => setOffset((o) => o + PAGE_SIZE)}
          >
            Next
            <ChevronRight />
          </Button>
        </div>
      </div>
    </div>
  );
}

function SupplierRow({
  supplier,
  onOpen,
}: {
  supplier: SupplierListItem;
  onOpen: () => void;
}) {
  return (
    <TableRow className="cursor-pointer transition-colors" onClick={onOpen}>
      <TableCell>
        <div className="font-medium">{supplier.name}</div>
        <div className="font-mono text-xs text-muted-foreground">{supplier.id}</div>
      </TableCell>
      <TableCell className="text-sm">{supplier.country ?? "—"}</TableCell>
      <TableCell className="max-w-[260px] truncate text-sm text-muted-foreground">
        {supplier.email ?? "—"}
      </TableCell>
      <TableCell className="text-right tabular-nums">
        {typeof supplier.rating === "number" ? (
          <span className="inline-flex items-center gap-1">
            <Star className="h-3 w-3 fill-amber-400 text-amber-400" />
            {supplier.rating.toFixed(1)}
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>
      <TableCell>
        <Badge variant={supplier.active ? "default" : "muted"}>
          {supplier.active ? "active" : "inactive"}
        </Badge>
      </TableCell>
      <TableCell className="text-muted-foreground text-sm">
        {new Date(supplier.created_at).toLocaleDateString()}
      </TableCell>
    </TableRow>
  );
}
