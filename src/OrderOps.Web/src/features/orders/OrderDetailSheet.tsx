import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Building2,
  Calendar,
  CircleDollarSign,
  Clock,
  FileText,
  Flag,
  Hash,
  Package,
  Tag,
  Truck,
  Warehouse,
} from "lucide-react";
import { Sheet, SheetBody, SheetHeader } from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/api/types";
import { getOrder, patchOrder, type OrderDetail } from "@/features/orders/api";
import { formatCurrency, formatDateTime, statusVariant } from "@/features/orders/format";
import { ORDER_STATUSES } from "@/features/orders/constants";
import { cn } from "@/lib/utils";

interface OrderDetailSheetProps {
  orderId: string | null;
  onClose: () => void;
}

export function OrderDetailSheet({ orderId, onClose }: OrderDetailSheetProps) {
  const open = orderId !== null;
  const query = useQuery({
    queryKey: ["order", orderId],
    queryFn: ({ signal }) => getOrder(orderId!, signal),
    enabled: open,
  });

  return (
    <Sheet open={open} onClose={onClose} className="max-w-lg">
      <SheetHeader className="border-b-0 bg-gradient-to-br from-primary/10 via-background to-background p-6 pb-4">
        <div className="flex items-center gap-2 text-xs uppercase tracking-wider text-muted-foreground">
          <Hash className="h-3 w-3" />
          <span>Order</span>
        </div>
        <h2 className="font-mono text-xl font-semibold tracking-tight">{orderId ?? ""}</h2>
        {query.data && (
          <div className="flex items-center gap-2 pt-1">
            <Badge variant={statusVariant(query.data.status)}>{query.data.status}</Badge>
            <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
              <span
                className={cn(
                  "h-1.5 w-1.5 rounded-full",
                  priorityDotClass(query.data.priority)
                )}
              />
              <span className="capitalize">{query.data.priority} priority</span>
            </span>
          </div>
        )}
      </SheetHeader>

      <SheetBody className="space-y-4">
        {query.isPending && (
          <div className="space-y-3">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        )}
        {query.isError && (
          <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
            Failed to load order
            {query.error instanceof ApiError ? ` (${query.error.code})` : ""}
          </div>
        )}
        {query.data && (
          <>
            <StatusPatcher order={query.data} />
            <DetailContent order={query.data} onClose={onClose} />
          </>
        )}
      </SheetBody>
    </Sheet>
  );
}

function DetailContent({ order, onClose }: { order: OrderDetail; onClose: () => void }) {
  return (
    <>
      {order.flagged_at && (
        <section className="rounded-lg border border-amber-500/30 bg-amber-50 p-4 shadow-sm dark:bg-amber-950/30">
          <div className="mb-1.5 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-amber-700 dark:text-amber-400">
            <Flag className="h-4 w-4 fill-amber-400/40 text-amber-500" />
            Flagged for review
          </div>
          <div className="text-xs text-muted-foreground">
            {formatDateTime(order.flagged_at)}
          </div>
          {order.flag_reason && (
            <p className="mt-2 whitespace-pre-wrap break-words text-sm text-foreground/90">
              {order.flag_reason}
            </p>
          )}
        </section>
      )}

      <Section icon={<Building2 className="h-4 w-4" />} title="Supplier">
        <div className="font-medium">{order.supplier_name}</div>
        <Link
          to={`/suppliers/${order.supplier_id}`}
          onClick={onClose}
          className="font-mono text-xs text-muted-foreground transition-colors hover:text-foreground hover:underline underline-offset-2"
        >
          {order.supplier_id}
        </Link>
      </Section>

      <Section icon={<Package className="h-4 w-4" />} title="Product">
        <div className="font-medium">{order.product_name}</div>
        <div className="font-mono text-xs text-muted-foreground">{order.product_id}</div>
      </Section>

      <Section icon={<CircleDollarSign className="h-4 w-4" />} title="Pricing">
        <dl className="grid grid-cols-3 gap-3 text-sm">
          <Stat label="Quantity" value={order.quantity.toLocaleString()} />
          <Stat label="Unit price" value={formatCurrency(order.unit_price)} />
          <Stat label="Total" value={formatCurrency(order.total_price)} accent />
        </dl>
      </Section>

      <Section icon={<Truck className="h-4 w-4" />} title="Logistics">
        <dl className="grid grid-cols-2 gap-3 text-sm">
          <Stat
            label="Warehouse"
            value={
              order.warehouse ? (
                <span className="inline-flex items-center gap-1.5">
                  <Warehouse className="h-3.5 w-3.5 text-muted-foreground" />
                  {order.warehouse.replace("warehouse_", "")}
                </span>
              ) : (
                "—"
              )
            }
          />
          <Stat
            label="Priority"
            value={
              <span className="inline-flex items-center gap-1.5 capitalize">
                <Tag className="h-3.5 w-3.5 text-muted-foreground" />
                {order.priority}
              </span>
            }
          />
        </dl>
      </Section>

      <Section icon={<Clock className="h-4 w-4" />} title="Timeline">
        <dl className="space-y-2 text-sm">
          <TimelineRow
            icon={<Calendar className="h-3.5 w-3.5" />}
            label="Created"
            value={formatDateTime(order.created_at)}
          />
          <TimelineRow
            icon={<Clock className="h-3.5 w-3.5" />}
            label="Updated"
            value={formatDateTime(order.updated_at)}
          />
        </dl>
      </Section>

      {order.notes && (
        <Section icon={<FileText className="h-4 w-4" />} title="Notes">
          <p className="whitespace-pre-wrap text-sm leading-relaxed text-foreground/90">
            {order.notes}
          </p>
        </Section>
      )}
    </>
  );
}

function Section({
  icon,
  title,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-lg border bg-card p-4 shadow-sm">
      <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        <span className="text-primary">{icon}</span>
        {title}
      </div>
      {children}
    </section>
  );
}

function Stat({
  label,
  value,
  accent,
}: {
  label: string;
  value: React.ReactNode;
  accent?: boolean;
}) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd
        className={cn(
          "mt-0.5 font-medium tabular-nums",
          accent && "text-primary text-base"
        )}
      >
        {value}
      </dd>
    </div>
  );
}

function TimelineRow({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="inline-flex items-center gap-2 text-muted-foreground">
        {icon}
        {label}
      </span>
      <span className="font-medium">{value}</span>
    </div>
  );
}

function StatusPatcher({ order }: { order: OrderDetail }) {
  const qc = useQueryClient();
  const [next, setNext] = useState("");

  useEffect(() => {
    setNext("");
  }, [order.id]);

  const mutation = useMutation({
    mutationFn: (status: string) => patchOrder(order.id, { status }),
    onSuccess: (updated) => {
      qc.setQueryData(["order", order.id], updated);
      qc.invalidateQueries({ queryKey: ["orders"] });
      setNext("");
    },
  });

  const disabled = !next || next === order.status || mutation.isPending;

  return (
    <section className="rounded-lg border bg-card p-4 shadow-sm">
      <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        <span className="text-primary">
          <Tag className="h-4 w-4" />
        </span>
        Update status
      </div>
      <div className="flex items-center gap-2">
        <Select
          value={next}
          onChange={(e) => setNext(e.target.value)}
          disabled={mutation.isPending}
          aria-label="New status"
          className="flex-1"
        >
          <option value="">Change status…</option>
          {ORDER_STATUSES.filter((s) => s !== order.status).map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </Select>
        <Button
          size="sm"
          disabled={disabled}
          onClick={() => mutation.mutate(next)}
        >
          {mutation.isPending ? "Applying…" : "Apply"}
        </Button>
      </div>
      {mutation.isError && (
        <div className="mt-2 text-xs text-destructive">
          {mutation.error instanceof ApiError
            ? `${mutation.error.message} (${mutation.error.code})`
            : "Failed to update status."}
        </div>
      )}
    </section>
  );
}

function priorityDotClass(priority: string): string {
  switch (priority) {
    case "low":
      return "bg-slate-300";
    case "medium":
      return "bg-blue-400";
    case "high":
      return "bg-amber-400";
    case "critical":
      return "bg-red-500";
    default:
      return "bg-muted-foreground/40";
  }
}
