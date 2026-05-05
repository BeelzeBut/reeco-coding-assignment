import type { BulkAction, SortField } from "@/features/orders/api";

export const ORDER_STATUSES = [
  "pending",
  "approved",
  "rejected",
  "shipped",
  "delivered",
  "cancelled",
] as const;

export const ORDER_PRIORITIES = ["low", "medium", "high", "critical"] as const;

export const ORDER_WAREHOUSES = [
  "warehouse_north",
  "warehouse_south",
  "warehouse_east",
  "warehouse_west",
  "warehouse_central",
] as const;

export const BULK_MAX_BATCH = 10_000;

export interface BulkActionMeta {
  value: BulkAction;
  label: string;
  verb: string;
  description: string;
}

export const BULK_ACTIONS: BulkActionMeta[] = [
  {
    value: "approve",
    label: "Approve",
    verb: "Approve",
    description: "Set status to approved.",
  },
  {
    value: "reject",
    label: "Reject",
    verb: "Reject",
    description: "Set status to rejected.",
  },
  {
    value: "flag",
    label: "Flag",
    verb: "Flag",
    description: "Tag for review without changing status.",
  },
];

export const SORT_OPTIONS: { value: SortField; label: string }[] = [
  { value: "id", label: "Order ID" },
  { value: "created_at", label: "Created" },
  { value: "updated_at", label: "Updated" },
  { value: "total_price", label: "Total" },
  { value: "unit_price", label: "Unit price" },
  { value: "quantity", label: "Quantity" },
  { value: "status", label: "Status" },
  { value: "priority", label: "Priority" },
];
