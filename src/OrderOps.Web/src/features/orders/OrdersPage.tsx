import { useMemo, useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowUp,
  ChevronLeft,
  ChevronRight,
  Inbox,
  Package,
  TrendingUp,
} from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ApiError } from "@/api/types";
import { defaultFilters, listOrders, type OrdersFilters, type SortField } from "@/features/orders/api";
import { formatCurrency, formatDateTime, statusVariant } from "@/features/orders/format";
import { OrderDetailSheet } from "@/features/orders/OrderDetailSheet";
import { OrdersFiltersBar } from "@/features/orders/OrdersFilters";
import { BulkActionBar } from "@/features/orders/BulkActionBar";
import { BulkJobToast } from "@/features/orders/BulkJobToast";
import { useBulkSelection } from "@/features/orders/useBulkSelection";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 25;

export function OrdersPage() {
  const [filters, setFiltersInternal] = useState<OrdersFilters>(defaultFilters);
  const [offset, setOffset] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selection = useBulkSelection();
  const [activeJobIds, setActiveJobIds] = useState<string[]>([]);

  const setFilters = (next: OrdersFilters) => {
    setFiltersInternal(next);
    setOffset(0);
  };

  const query = useQuery({
    queryKey: ["orders", { filters, limit: PAGE_SIZE, offset }],
    queryFn: ({ signal }) => listOrders({ filters, limit: PAGE_SIZE, offset, signal }),
    placeholderData: keepPreviousData,
  });

  const total = query.data?.total ?? 0;
  const start = total === 0 ? 0 : offset + 1;
  const end = Math.min(offset + PAGE_SIZE, total);
  const canPrev = offset > 0;
  const canNext = offset + PAGE_SIZE < total;

  const pageRows = query.data?.data ?? [];
  const pageIds = useMemo(() => pageRows.map((r) => r.id), [pageRows]);
  const pageSelectedCount = useMemo(
    () => pageIds.reduce((n, id) => (selection.isSelected(id) ? n + 1 : n), 0),
    [pageIds, selection]
  );
  const allOnPageSelected = pageIds.length > 0 && pageSelectedCount === pageIds.length;
  const someOnPageSelected = pageSelectedCount > 0 && !allOnPageSelected;

  const toggleSort = (field: SortField) => {
    if (filters.sort === field) {
      setFilters({ ...filters, order: filters.order === "asc" ? "desc" : "asc" });
    } else {
      setFilters({ ...filters, sort: field, order: "asc" });
    }
  };

  return (
    <>
      <div className="space-y-4">
        <header className="flex items-end justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Orders</h1>
            <p className="text-sm text-muted-foreground">
              Browse, filter, and inspect every purchase order in the system.
            </p>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Package className="h-4 w-4" />
            <span className="tabular-nums">
              {query.data ? total.toLocaleString() : "—"} total
            </span>
          </div>
        </header>

        <OrdersFiltersBar
          value={filters}
          onChange={setFilters}
          onReset={() => setFilters(defaultFilters)}
        />

        <BulkActionBar
          selectedIds={selection.ids}
          onClear={selection.clear}
          onJobSubmitted={(jobId) => setActiveJobIds((prev) => [...prev, jobId])}
        />

        <Card className="overflow-hidden shadow-sm">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 border-b bg-muted/30 py-3">
            <CardTitle className="text-base font-medium">
              <TrendingUp className="mr-2 inline h-4 w-4 text-primary" />
              Results
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
                Failed to load orders
                {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
              </div>
            )}

            <Table>
              <TableHeader className="sticky top-0 z-10 bg-muted/50 backdrop-blur">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="w-10">
                    <Checkbox
                      aria-label={
                        allOnPageSelected ? "Deselect all on page" : "Select all on page"
                      }
                      checked={allOnPageSelected}
                      indeterminate={someOnPageSelected}
                      disabled={pageIds.length === 0}
                      onChange={(e) =>
                        selection.setMany(pageIds, (e.target as HTMLInputElement).checked)
                      }
                    />
                  </TableHead>
                  <SortableHead
                    field="id"
                    filters={filters}
                    onToggle={toggleSort}
                  >
                    Order
                  </SortableHead>
                  <SortableHead
                    field="status"
                    filters={filters}
                    onToggle={toggleSort}
                  >
                    Status
                  </SortableHead>
                  <SortableHead
                    field="priority"
                    filters={filters}
                    onToggle={toggleSort}
                  >
                    Priority
                  </SortableHead>
                  <TableHead>Product</TableHead>
                  <SortableHead
                    field="supplier_id"
                    filters={filters}
                    onToggle={toggleSort}
                  >
                    Supplier
                  </SortableHead>
                  <SortableHead
                    field="quantity"
                    filters={filters}
                    onToggle={toggleSort}
                    align="right"
                  >
                    Qty
                  </SortableHead>
                  <SortableHead
                    field="total_price"
                    filters={filters}
                    onToggle={toggleSort}
                    align="right"
                  >
                    Total
                  </SortableHead>
                  <SortableHead
                    field="created_at"
                    filters={filters}
                    onToggle={toggleSort}
                  >
                    Created
                  </SortableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {query.isPending &&
                  Array.from({ length: 8 }).map((_, i) => (
                    <TableRow key={`skel-${i}`}>
                      {Array.from({ length: 9 }).map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-full" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                {query.data?.data.map((order) => {
                  const checked = selection.isSelected(order.id);
                  return (
                  <TableRow
                    key={order.id}
                    className={cn(
                      "group cursor-pointer transition-colors",
                      selectedId === order.id && "bg-primary/5",
                      checked && "bg-primary/[0.04]"
                    )}
                    onClick={() => setSelectedId(order.id)}
                  >
                    <TableCell
                      className="w-10"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <Checkbox
                        aria-label={`Select ${order.id}`}
                        checked={checked}
                        onChange={() => selection.toggle(order.id)}
                      />
                    </TableCell>
                    <TableCell className="font-mono text-xs">{order.id}</TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(order.status)}>{order.status}</Badge>
                    </TableCell>
                    <TableCell>
                      <PriorityDot priority={order.priority} />
                    </TableCell>
                    <TableCell className="max-w-[260px] truncate">
                      <div className="font-medium">{order.product_name}</div>
                      <div className="font-mono text-xs text-muted-foreground">
                        {order.product_id}
                      </div>
                    </TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">
                      {order.supplier_id}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{order.quantity}</TableCell>
                    <TableCell className="text-right font-medium tabular-nums">
                      {formatCurrency(order.total_price)}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {formatDateTime(order.created_at)}
                    </TableCell>
                  </TableRow>
                  );
                })}
                {query.data && query.data.data.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={9}>
                      <div className="flex flex-col items-center justify-center gap-2 py-12 text-muted-foreground">
                        <Inbox className="h-8 w-8 opacity-50" />
                        <p className="text-sm">No orders match these filters.</p>
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

      <OrderDetailSheet orderId={selectedId} onClose={() => setSelectedId(null)} />

      {activeJobIds.length > 0 && (
        <div className="pointer-events-none fixed bottom-4 right-4 z-40 flex flex-col gap-2">
          {activeJobIds.map((jobId) => (
            <BulkJobToast
              key={jobId}
              jobId={jobId}
              onDismiss={() =>
                setActiveJobIds((prev) => prev.filter((id) => id !== jobId))
              }
            />
          ))}
        </div>
      )}
    </>
  );
}

function SortableHead({
  field,
  filters,
  onToggle,
  align,
  children,
}: {
  field: SortField;
  filters: OrdersFilters;
  onToggle: (f: SortField) => void;
  align?: "right";
  children: React.ReactNode;
}) {
  const active = filters.sort === field;
  return (
    <TableHead className={cn(align === "right" && "text-right")}>
      <button
        type="button"
        onClick={() => onToggle(field)}
        className={cn(
          "inline-flex items-center gap-1 transition-colors hover:text-foreground",
          align === "right" && "ml-auto",
          active && "text-foreground"
        )}
      >
        {children}
        {active &&
          (filters.order === "asc" ? (
            <ArrowUp className="h-3 w-3" />
          ) : (
            <ArrowDown className="h-3 w-3" />
          ))}
      </button>
    </TableHead>
  );
}

const PRIORITY_COLOR: Record<string, string> = {
  low: "bg-slate-300",
  medium: "bg-blue-400",
  high: "bg-amber-400",
  critical: "bg-red-500",
};

function PriorityDot({ priority }: { priority: string }) {
  return (
    <span className="inline-flex items-center gap-2 capitalize">
      <span
        className={cn(
          "h-2 w-2 rounded-full ring-2 ring-background",
          PRIORITY_COLOR[priority] ?? "bg-muted-foreground/40"
        )}
      />
      {priority}
    </span>
  );
}
