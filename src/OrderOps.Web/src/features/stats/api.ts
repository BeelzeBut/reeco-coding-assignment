import { apiFetch } from "@/api/client";

export interface ByStatusBucket {
  count: number;
  total_value: number;
}

export interface ByMonthBucket {
  month: string;
  order_count: number;
  revenue: number;
}

export interface TopSupplier {
  supplier_id: string;
  supplier_name: string;
  total_revenue: number;
}

export interface ByWarehouseBucket {
  warehouse: string;
  count: number;
  total_value: number;
}

export interface OrdersStats {
  total_orders: number;
  total_revenue: number;
  avg_order_value: number;
  by_status: Record<string, ByStatusBucket>;
  by_month: ByMonthBucket[];
  top_suppliers: TopSupplier[];
  by_warehouse: ByWarehouseBucket[];
}

export function getOrdersStats(signal?: AbortSignal) {
  return apiFetch<OrdersStats>("/api/orders/stats", { signal });
}
