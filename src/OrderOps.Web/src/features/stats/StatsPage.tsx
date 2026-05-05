import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  Activity,
  Building2,
  CircleDollarSign,
  Package,
  TrendingUp,
  Warehouse as WarehouseIcon,
} from "lucide-react";
import { ApiError } from "@/api/types";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/features/orders/format";
import { cn } from "@/lib/utils";
import { getOrdersStats, type OrdersStats } from "@/features/stats/api";
import { useDocumentTitle } from "@/hooks/use-document-title";

const STATUS_COLOR: Record<string, string> = {
  delivered: "hsl(142, 70%, 45%)",
  shipped: "hsl(199, 89%, 48%)",
  approved: "hsl(217, 91%, 60%)",
  pending: "hsl(48, 96%, 53%)",
  rejected: "hsl(0, 84%, 60%)",
  cancelled: "hsl(0, 0%, 55%)",
};

export function StatsPage() {
  useDocumentTitle("Analytics");
  const query = useQuery({
    queryKey: ["orders", "stats"],
    queryFn: ({ signal }) => getOrdersStats(signal),
    staleTime: 30_000,
  });

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Analytics</h1>
        <p className="text-sm text-muted-foreground">
          Live snapshot of every purchase order, sliced by status, month, supplier, and warehouse.
        </p>
      </header>

      {query.isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
          Failed to load stats
          {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
        </div>
      )}

      {query.isPending ? <StatsLoading /> : query.data ? <StatsBody data={query.data} /> : null}
    </div>
  );
}

function StatsBody({ data }: { data: OrdersStats }) {
  return (
    <>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <KpiCard
          icon={<Package className="h-4 w-4" />}
          label="Total orders"
          value={data.total_orders.toLocaleString()}
        />
        <KpiCard
          icon={<CircleDollarSign className="h-4 w-4" />}
          label="Total revenue"
          value={formatCurrency(data.total_revenue)}
          accent
        />
        <KpiCard
          icon={<Activity className="h-4 w-4" />}
          label="Avg order value"
          value={formatCurrency(data.avg_order_value)}
        />
      </div>

      <Panel
        icon={<TrendingUp className="h-4 w-4" />}
        title="Monthly volume & revenue"
        description="Last 24 months of order activity."
      >
        <ResponsiveContainer width="100%" height={260}>
          <LineChart data={data.by_month} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
            <XAxis dataKey="month" tick={{ fontSize: 11 }} stroke="hsl(var(--muted-foreground))" />
            <YAxis
              yAxisId="revenue"
              tick={{ fontSize: 11 }}
              stroke="hsl(var(--muted-foreground))"
              tickFormatter={(v) => `$${(v / 1_000_000).toFixed(0)}M`}
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
      </Panel>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <Panel
          icon={<Activity className="h-4 w-4" />}
          title="Status mix"
          description="How orders are distributed across the lifecycle."
        >
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie
                  data={Object.entries(data.by_status).map(([status, v]) => ({
                    status,
                    value: v.count,
                  }))}
                  dataKey="value"
                  nameKey="status"
                  cx="50%"
                  cy="50%"
                  innerRadius={45}
                  outerRadius={75}
                  paddingAngle={2}
                >
                  {Object.keys(data.by_status).map((status) => (
                    <Cell key={status} fill={STATUS_COLOR[status] ?? "hsl(var(--muted-foreground))"} />
                  ))}
                </Pie>
                <Tooltip
                  formatter={(value: number) => value.toLocaleString()}
                  contentStyle={{ borderRadius: 8, border: "1px solid hsl(var(--border))" }}
                />
              </PieChart>
            </ResponsiveContainer>
            <ul className="space-y-1.5 self-center text-sm">
              {Object.entries(data.by_status)
                .sort((a, b) => b[1].count - a[1].count)
                .map(([status, v]) => (
                  <li key={status} className="flex items-center justify-between gap-3">
                    <span className="inline-flex items-center gap-2 capitalize">
                      <span
                        className="h-2.5 w-2.5 rounded-full"
                        style={{ backgroundColor: STATUS_COLOR[status] ?? "var(--muted-foreground)" }}
                      />
                      {status}
                    </span>
                    <span className="text-muted-foreground tabular-nums">
                      {v.count.toLocaleString()} · {formatCurrency(v.total_value)}
                    </span>
                  </li>
                ))}
            </ul>
          </div>
        </Panel>

        <Panel
          icon={<WarehouseIcon className="h-4 w-4" />}
          title="By warehouse"
          description="Includes the unassigned bucket for orders without a warehouse."
        >
          <ResponsiveContainer width="100%" height={220}>
            <BarChart
              data={[...data.by_warehouse].sort((a, b) => b.count - a.count)}
              layout="vertical"
              margin={{ top: 4, right: 16, left: 8, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
              <XAxis
                type="number"
                tick={{ fontSize: 11 }}
                stroke="hsl(var(--muted-foreground))"
                tickFormatter={(v) => v.toLocaleString()}
              />
              <YAxis
                type="category"
                dataKey="warehouse"
                tick={{ fontSize: 11 }}
                stroke="hsl(var(--muted-foreground))"
                width={110}
                tickFormatter={(v: string) => v.replace("warehouse_", "")}
              />
              <Tooltip
                formatter={(value: number) => value.toLocaleString()}
                contentStyle={{ borderRadius: 8, border: "1px solid hsl(var(--border))" }}
              />
              <Bar dataKey="count" fill="hsl(var(--primary))" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Panel>
      </div>

      <Panel
        icon={<Building2 className="h-4 w-4" />}
        title="Top 10 suppliers by revenue"
      >
        <TopSuppliersList rows={data.top_suppliers} />
      </Panel>
    </>
  );
}

function TopSuppliersList({
  rows,
}: {
  rows: OrdersStats["top_suppliers"];
}) {
  const max = rows[0]?.total_revenue ?? 1;
  return (
    <ol className="space-y-1">
      {rows.map((s, i) => {
        const pct = (s.total_revenue / max) * 100;
        return (
          <li key={s.supplier_id}>
            <Link
              to={`/suppliers/${s.supplier_id}`}
              className="-mx-2 grid grid-cols-[2rem_1fr_auto] items-center gap-3 rounded-md px-2 py-1.5 transition-colors hover:bg-muted/60"
            >
              <span className="text-sm font-mono text-muted-foreground">#{i + 1}</span>
              <div className="min-w-0">
                <div className="truncate text-sm font-medium hover:underline underline-offset-2">
                  {s.supplier_name}
                </div>
                <div className="font-mono text-xs text-muted-foreground">{s.supplier_id}</div>
                <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full bg-primary transition-all duration-500"
                    style={{ width: `${pct}%` }}
                  />
                </div>
              </div>
              <span className="text-sm font-medium tabular-nums">
                {formatCurrency(s.total_revenue)}
              </span>
            </Link>
          </li>
        );
      })}
    </ol>
  );
}

function KpiCard({
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
    <div className="rounded-xl border bg-card p-4 shadow-sm">
      <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        <span className="text-primary">{icon}</span>
        {label}
      </div>
      <div
        className={cn(
          "mt-2 text-2xl font-semibold tabular-nums tracking-tight",
          accent && "text-primary"
        )}
      >
        {value}
      </div>
    </div>
  );
}

function Panel({
  icon,
  title,
  description,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-card p-4 shadow-sm">
      <header className="mb-3">
        <div className="flex items-center gap-2 text-sm font-medium">
          <span className="text-primary">{icon}</span>
          {title}
        </div>
        {description && (
          <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>
        )}
      </header>
      {children}
    </section>
  );
}

function StatsLoading() {
  return (
    <>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-24 rounded-xl" />
      </div>
      <Skeleton className="h-72 rounded-xl" />
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <Skeleton className="h-64 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    </>
  );
}
