import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, Search, SlidersHorizontal, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { cn } from "@/lib/utils";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import type { OrdersFilters, SortField } from "@/features/orders/api";
import {
  ORDER_PRIORITIES,
  ORDER_STATUSES,
  ORDER_WAREHOUSES,
  SORT_OPTIONS,
} from "@/features/orders/constants";
import { listSuppliers } from "@/features/suppliers/api";

interface OrdersFiltersProps {
  value: OrdersFilters;
  onChange: (next: OrdersFilters) => void;
  onReset: () => void;
}

export function OrdersFiltersBar({ value, onChange, onReset }: OrdersFiltersProps) {
  const [searchInput, setSearchInput] = useState(value.search ?? "");
  const debouncedSearch = useDebouncedValue(searchInput, 300);
  const [advancedOpen, setAdvancedOpen] = useState(false);

  const suppliersQuery = useQuery({
    queryKey: ["suppliers", "all"],
    queryFn: ({ signal }) => listSuppliers({ limit: 500, offset: 0, signal }),
    staleTime: 5 * 60_000,
  });

  const supplierOptions = useMemo(() => {
    const rows = suppliersQuery.data?.data ?? [];
    return [...rows].sort((a, b) => a.name.localeCompare(b.name));
  }, [suppliersQuery.data]);

  useEffect(() => {
    const next = debouncedSearch.trim() || null;
    if (next !== value.search) onChange({ ...value, search: next });
  }, [debouncedSearch]); // eslint-disable-line react-hooks/exhaustive-deps

  const toggleStatus = (status: string) => {
    const has = value.statuses.includes(status);
    const next = has ? value.statuses.filter((s) => s !== status) : [...value.statuses, status];
    onChange({ ...value, statuses: next });
  };

  const setPriority = (priority: string | null) => onChange({ ...value, priority });

  const filterCount = countActiveFilters(value);
  const hasAdvanced = !!(
    value.warehouse ||
    value.supplierId ||
    value.dateFrom ||
    value.dateTo ||
    value.minTotal !== null
  );

  return (
    <div className="space-y-3 rounded-xl border bg-card p-4 shadow-sm">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[240px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by product name…"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9"
          />
        </div>
        <Select
          value={value.sort}
          onChange={(e) => onChange({ ...value, sort: e.target.value as SortField })}
          className="w-[160px]"
          aria-label="Sort by"
        >
          {SORT_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              Sort: {o.label}
            </option>
          ))}
        </Select>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onChange({ ...value, order: value.order === "asc" ? "desc" : "asc" })}
          className="h-9"
        >
          {value.order === "asc" ? "Asc ↑" : "Desc ↓"}
        </Button>
        <Button
          variant={advancedOpen || hasAdvanced ? "default" : "outline"}
          size="sm"
          onClick={() => setAdvancedOpen((v) => !v)}
          className="h-9"
        >
          <SlidersHorizontal />
          More filters
          {hasAdvanced && (
            <span className="ml-1 rounded-full bg-primary-foreground/20 px-1.5 text-xs">
              {countAdvanced(value)}
            </span>
          )}
          <ChevronDown
            className={cn("transition-transform duration-200", advancedOpen && "rotate-180")}
          />
        </Button>
        {filterCount > 0 && (
          <Button variant="ghost" size="sm" className="h-9 text-muted-foreground" onClick={onReset}>
            <X />
            Clear all
          </Button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Status
        </span>
        {ORDER_STATUSES.map((s) => {
          const active = value.statuses.includes(s);
          return (
            <Chip key={s} active={active} onClick={() => toggleStatus(s)}>
              {s}
            </Chip>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Priority
        </span>
        {ORDER_PRIORITIES.map((p) => {
          const active = value.priority === p;
          return (
            <Chip
              key={p}
              active={active}
              onClick={() => setPriority(active ? null : p)}
            >
              {p}
            </Chip>
          );
        })}
      </div>

      {advancedOpen && (
        <div className="grid grid-cols-1 gap-3 border-t pt-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Warehouse">
            <Select
              value={value.warehouse ?? ""}
              onChange={(e) =>
                onChange({ ...value, warehouse: e.target.value || null })
              }
            >
              <option value="">Any</option>
              {ORDER_WAREHOUSES.map((w) => (
                <option key={w} value={w}>
                  {w.replace("warehouse_", "")}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Supplier">
            <Select
              value={value.supplierId ?? ""}
              onChange={(e) =>
                onChange({ ...value, supplierId: e.target.value || null })
              }
              disabled={suppliersQuery.isPending}
            >
              <option value="">
                {suppliersQuery.isPending ? "Loading…" : "Any"}
              </option>
              {supplierOptions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.id})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Min total">
            <Input
              type="number"
              min="0"
              step="0.01"
              placeholder="0"
              value={value.minTotal ?? ""}
              onChange={(e) => {
                const v = e.target.value;
                onChange({ ...value, minTotal: v === "" ? null : Number(v) });
              }}
            />
          </Field>
          <div className="grid grid-cols-2 gap-2">
            <Field label="From">
              <Input
                type="date"
                value={value.dateFrom ?? ""}
                onChange={(e) =>
                  onChange({ ...value, dateFrom: e.target.value || null })
                }
              />
            </Field>
            <Field label="To">
              <Input
                type="date"
                value={value.dateTo ?? ""}
                onChange={(e) => onChange({ ...value, dateTo: e.target.value || null })}
              />
            </Field>
          </div>
        </div>
      )}
    </div>
  );
}

function Chip({
  active,
  children,
  onClick,
}: {
  active: boolean;
  children: React.ReactNode;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium capitalize transition-all",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1",
        active
          ? "border-primary bg-primary text-primary-foreground shadow-sm shadow-primary/20"
          : "border-input bg-background text-foreground hover:bg-muted"
      )}
    >
      {children}
    </button>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </span>
      {children}
    </label>
  );
}

function countActiveFilters(f: OrdersFilters): number {
  let n = 0;
  if (f.statuses.length > 0) n++;
  if (f.priority) n++;
  if (f.search) n++;
  return n + countAdvanced(f);
}

function countAdvanced(f: OrdersFilters): number {
  let n = 0;
  if (f.warehouse) n++;
  if (f.supplierId) n++;
  if (f.dateFrom) n++;
  if (f.dateTo) n++;
  if (f.minTotal !== null) n++;
  return n;
}
