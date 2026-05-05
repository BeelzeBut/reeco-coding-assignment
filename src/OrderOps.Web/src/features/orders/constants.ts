import type { SortField } from "@/features/orders/api";

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
