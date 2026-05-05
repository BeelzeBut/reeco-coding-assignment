import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  ArrowLeft,
  Building2,
  CircleDollarSign,
  Clock,
  Gauge,
  Inbox,
  Mail,
  MapPin,
  Package,
  Star,
  TrendingUp,
  XCircle,
} from "lucide-react";
import { ApiError } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  getSupplier,
  getSupplierPerformance,
  type SupplierDetail,
  type SupplierPerformance,
} from "@/features/suppliers/api";
import { defaultFilters, listOrders } from "@/features/orders/api";
import { formatCurrency, formatDateTime, statusVariant } from "@/features/orders/format";
import { OrderDetailSheet } from "@/features/orders/OrderDetailSheet";
import { cn } from "@/lib/utils";

const RECENT_ORDERS_LIMIT = 10;

export function SupplierDetailPage() {
  const { id = "" } = useParams<{ id: string }>();

  const detail = useQuery({
    queryKey: ["supplier", id],
    queryFn: ({ signal }) => getSupplier(id, signal),
    enabled: id.length > 0,
    retry: (failureCount, error) =>
      error instanceof ApiError && error.status === 404 ? false : failureCount < 1,
  });

  if (detail.isError && detail.error instanceof ApiError && detail.error.status === 404) {
    return <NotFound id={id} />;
  }

  return (
    <div className="space-y-4">
      <div>
        <Link to="/suppliers" className={buttonVariants({ variant: "ghost", size: "sm" })}>
          <ArrowLeft />
          All suppliers
        </Link>
      </div>

      {detail.isError && !(detail.error instanceof ApiError && detail.error.status === 404) && (
        <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
          Failed to load supplier
          {detail.error instanceof ApiError ? ` (${detail.error.code})` : ""}
        </div>
      )}

      {detail.isPending ? (
        <Skeleton className="h-40 rounded-xl" />
      ) : detail.data ? (
        <SupplierHeader supplier={detail.data} />
      ) : null}

      <PerformanceSection id={id} />

      <RecentOrders supplierId={id} />
    </div>
  );
}

function SupplierHeader({ supplier }: { supplier: SupplierDetail }) {
  return (
    <section className="rounded-xl border bg-card p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-xs uppercase tracking-wider text-muted-foreground">
            <Building2 className="h-3 w-3" />
            <span>Supplier</span>
          </div>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{supplier.name}</h1>
          <div className="mt-1 font-mono text-xs text-muted-foreground">{supplier.id}</div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge variant={supplier.active ? "default" : "muted"}>
              {supplier.active ? "active" : "inactive"}
            </Badge>
            {typeof supplier.rating === "number" && (
              <span className="inline-flex items-center gap-1 text-sm tabular-nums">
                <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" />
                {supplier.rating.toFixed(1)}
              </span>
            )}
          </div>
        </div>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm">
          <Field icon={<MapPin className="h-3.5 w-3.5" />} label="Country" value={supplier.country ?? "—"} />
          <Field icon={<Mail className="h-3.5 w-3.5" />} label="Email" value={supplier.email ?? "—"} />
          <Field
            icon={<Package className="h-3.5 w-3.5" />}
            label="Total orders"
            value={supplier.order_count.toLocaleString()}
          />
          <Field
            icon={<CircleDollarSign className="h-3.5 w-3.5" />}
            label="Total revenue"
            value={formatCurrency(supplier.total_revenue)}
            accent
          />
        </dl>
      </div>
    </section>
  );
}

function Field({
  icon,
  label,
  value,
  accent,
}: {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
  accent?: boolean;
}) {
  return (
    <div>
      <dt className="flex items-center gap-1.5 text-xs uppercase tracking-wide text-muted-foreground">
        {icon}
        {label}
      </dt>
      <dd className={cn("mt-1 font-medium tabular-nums", accent && "text-primary")}>{value}</dd>
    </div>
  );
}

function PerformanceSection({ id }: { id: string }) {
  const query = useQuery({
    queryKey: ["supplier", id, "performance"],
    queryFn: ({ signal }) => getSupplierPerformance(id, signal),
    enabled: id.length > 0,
    retry: (failureCount, error) =>
      error instanceof ApiError && error.status === 404 ? false : failureCount < 1,
  });

  if (query.isError && query.error instanceof ApiError && query.error.status === 404) {
    return null;
  }

  return (
    <Card className="shadow-sm">
      <CardHeader className="border-b py-3">
        <CardTitle className="text-base font-medium">
          <Gauge className="mr-2 inline h-4 w-4 text-primary" />
          Performance
        </CardTitle>
        <CardDescription>
          Delivery, quality, and pricing signals across this supplier&apos;s history.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5 pt-5">
        {query.isError && (
          <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
            Failed to load performance
            {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
          </div>
        )}

        {query.isPending ? (
          <Skeleton className="h-24" />
        ) : query.data ? (
          <PerformanceStats data={query.data} />
        ) : null}

        {query.isPending ? (
          <Skeleton className="h-64" />
        ) : query.data ? (
          <MonthlyTrendChart data={query.data} />
        ) : null}
      </CardContent>
    </Card>
  );
}

function PerformanceStats({ data }: { data: SupplierPerformance }) {
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
      <Stat
        icon={<Package className="h-4 w-4" />}
        label="Total orders"
        value={data.total_orders.toLocaleString()}
      />
      <Stat
        icon={<Clock className="h-4 w-4" />}
        label="Avg delivery"
        value={`${data.avg_delivery_days.toFixed(1)} d`}
      />
      <Stat
        icon={<XCircle className="h-4 w-4" />}
        label="Rejection rate"
        value={`${(data.rejection_rate * 100).toFixed(1)}%`}
      />
      <Stat
        icon={<CircleDollarSign className="h-4 w-4" />}
        label="Avg order value"
        value={formatCurrency(data.avg_order_value)}
        accent
      />
      <Stat
        icon={<TrendingUp className="h-4 w-4" />}
        label="Price consistency"
        value={data.price_consistency.toFixed(2)}
      />
    </div>
  );
}

function Stat({
  icon,
  label,
  value,
  accent,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  accent?: boolean;
}) {
  return (
    <div className="rounded-lg border bg-background p-3">
      <div className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        <span className="text-primary">{icon}</span>
        {label}
      </div>
      <div className={cn("mt-1.5 text-lg font-semibold tabular-nums", accent && "text-primary")}>
        {value}
      </div>
    </div>
  );
}

function MonthlyTrendChart({ data }: { data: SupplierPerformance }) {
  if (data.monthly_trend.length === 0) {
    return (
      <div className="flex flex-col items-center gap-1.5 py-8 text-muted-foreground">
        <TrendingUp className="h-6 w-6 opacity-50" />
        <p className="text-xs">No monthly trend data for this supplier.</p>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-2 flex items-center gap-2 text-sm font-medium">
        <TrendingUp className="h-4 w-4 text-primary" />
        Monthly volume &amp; revenue
      </div>
      <ResponsiveContainer width="100%" height={260}>
        <LineChart data={data.monthly_trend} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
          <XAxis dataKey="month" tick={{ fontSize: 11 }} stroke="hsl(var(--muted-foreground))" />
          <YAxis
            yAxisId="revenue"
            tick={{ fontSize: 11 }}
            stroke="hsl(var(--muted-foreground))"
            tickFormatter={(v) =>
              v >= 1_000_000
                ? `$${(v / 1_000_000).toFixed(1)}M`
                : v >= 1_000
                  ? `$${(v / 1_000).toFixed(0)}k`
                  : `$${v}`
            }
          />
          <YAxis
            yAxisId="count"
            orientation="right"
            tick={{ fontSize: 11 }}
            stroke="hsl(var(--muted-foreground))"
            tickFormatter={(v) => v.toLocaleString()}
          />
          <Tooltip
            formatter={(value: number, name: string) =>
              name === "revenue" ? formatCurrency(value) : value.toLocaleString()
            }
            contentStyle={{ borderRadius: 8, border: "1px solid hsl(var(--border))" }}
          />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          <Line
            yAxisId="revenue"
            type="monotone"
            dataKey="revenue"
            stroke="hsl(var(--primary))"
            strokeWidth={2}
            dot={{ r: 2 }}
            activeDot={{ r: 4 }}
          />
          <Line
            yAxisId="count"
            type="monotone"
            dataKey="order_count"
            stroke="hsl(199, 89%, 48%)"
            strokeWidth={2}
            dot={{ r: 2 }}
            activeDot={{ r: 4 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

function RecentOrders({ supplierId }: { supplierId: string }) {
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: [
      "orders",
      { supplierId, limit: RECENT_ORDERS_LIMIT, offset: 0, sort: "created_at", order: "desc" },
    ],
    queryFn: ({ signal }) =>
      listOrders({
        filters: {
          ...defaultFilters,
          supplierId,
          sort: "created_at",
          order: "desc",
        },
        limit: RECENT_ORDERS_LIMIT,
        offset: 0,
        signal,
      }),
    enabled: supplierId.length > 0,
  });

  return (
    <>
      <Card className="shadow-sm">
        <CardHeader className="border-b py-3">
          <CardTitle className="text-base font-medium">
            <Package className="mr-2 inline h-4 w-4 text-primary" />
            Recent orders
          </CardTitle>
          <CardDescription>
            Most recent {RECENT_ORDERS_LIMIT} orders from this supplier.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {query.isError && (
            <div className="border-b bg-destructive/5 px-4 py-3 text-sm text-destructive">
              Failed to load recent orders
              {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
            </div>
          )}
          <Table>
            <TableHeader className="bg-muted/50">
              <TableRow className="hover:bg-transparent">
                <TableHead>Order</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Product</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead>Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {query.isPending &&
                Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={`skel-${i}`}>
                    {Array.from({ length: 5 }).map((__, j) => (
                      <TableCell key={j}>
                        <Skeleton className="h-4 w-full" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              {query.data?.data.map((order) => (
                <TableRow
                  key={order.id}
                  className="cursor-pointer transition-colors"
                  onClick={() => setSelectedId(order.id)}
                >
                  <TableCell className="font-mono text-xs">{order.id}</TableCell>
                  <TableCell>
                    <Badge variant={statusVariant(order.status)}>{order.status}</Badge>
                  </TableCell>
                  <TableCell className="max-w-[260px] truncate">
                    <div className="text-sm font-medium">{order.product_name}</div>
                    <div className="font-mono text-xs text-muted-foreground">{order.product_id}</div>
                  </TableCell>
                  <TableCell className="text-right font-medium tabular-nums">
                    {formatCurrency(order.total_price)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDateTime(order.created_at)}
                  </TableCell>
                </TableRow>
              ))}
              {query.data && query.data.data.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5}>
                    <div className="flex flex-col items-center justify-center gap-2 py-8 text-muted-foreground">
                      <Inbox className="h-6 w-6 opacity-50" />
                      <p className="text-sm">No orders yet for this supplier.</p>
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <OrderDetailSheet orderId={selectedId} onClose={() => setSelectedId(null)} />
    </>
  );
}

function NotFound({ id }: { id: string }) {
  return (
    <div className="space-y-4">
      <div>
        <Link to="/suppliers" className={buttonVariants({ variant: "ghost", size: "sm" })}>
          <ArrowLeft />
          All suppliers
        </Link>
      </div>
      <Card className="shadow-sm">
        <CardHeader>
          <CardTitle>Supplier not found</CardTitle>
          <CardDescription>
            No supplier matches <code className="font-mono">{id}</code>.
          </CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          The id may be wrong or the supplier may have been removed.
        </CardContent>
      </Card>
    </div>
  );
}
