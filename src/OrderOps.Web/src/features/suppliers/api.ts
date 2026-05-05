import { apiFetch } from "@/api/client";
import type { Paginated } from "@/api/types";

export interface SupplierListItem {
  id: string;
  name: string;
  email?: string;
  rating?: number;
  country?: string;
  active: boolean;
  created_at: string;
}

export interface SupplierDetail extends SupplierListItem {
  order_count: number;
  total_revenue: number;
}

export interface MonthlyTrendPoint {
  month: string;
  order_count: number;
  revenue: number;
}

export interface SupplierPerformance {
  supplier_id: string;
  total_orders: number;
  avg_delivery_days: number;
  rejection_rate: number;
  avg_order_value: number;
  monthly_trend: MonthlyTrendPoint[];
  price_consistency: number;
}

export interface ListSuppliersParams {
  limit: number;
  offset: number;
  signal?: AbortSignal;
}

export function listSuppliers({ limit, offset, signal }: ListSuppliersParams) {
  const qs = new URLSearchParams();
  qs.set("limit", String(limit));
  qs.set("offset", String(offset));
  return apiFetch<Paginated<SupplierListItem>>(`/api/suppliers?${qs}`, { signal });
}

export function getSupplier(id: string, signal?: AbortSignal) {
  return apiFetch<SupplierDetail>(`/api/suppliers/${encodeURIComponent(id)}`, { signal });
}

export function getSupplierPerformance(id: string, signal?: AbortSignal) {
  return apiFetch<SupplierPerformance>(
    `/api/suppliers/${encodeURIComponent(id)}/performance`,
    { signal }
  );
}
