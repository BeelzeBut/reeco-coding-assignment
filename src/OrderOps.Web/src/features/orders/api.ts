import { apiFetch } from "@/api/client";
import type { Paginated } from "@/api/types";

export interface OrderListItem {
  id: string;
  supplier_id: string;
  product_id: string;
  quantity: number;
  unit_price: number;
  total_price: number;
  status: string;
  priority: string;
  created_at: string;
  updated_at: string;
  warehouse: string | null;
  notes: string | null;
  product_name: string;
}

export interface OrderDetail extends OrderListItem {
  supplier_name: string;
}

export type SortField =
  | "id"
  | "created_at"
  | "updated_at"
  | "total_price"
  | "unit_price"
  | "quantity"
  | "status"
  | "priority"
  | "supplier_id"
  | "warehouse";

export type SortOrder = "asc" | "desc";

export interface OrdersFilters {
  statuses: string[];
  priority: string | null;
  supplierId: string | null;
  warehouse: string | null;
  dateFrom: string | null;
  dateTo: string | null;
  minTotal: number | null;
  search: string | null;
  sort: SortField;
  order: SortOrder;
}

export const defaultFilters: OrdersFilters = {
  statuses: [],
  priority: null,
  supplierId: null,
  warehouse: null,
  dateFrom: null,
  dateTo: null,
  minTotal: null,
  search: null,
  sort: "id",
  order: "asc",
};

export interface ListOrdersParams {
  filters: OrdersFilters;
  limit: number;
  offset: number;
  signal?: AbortSignal;
}

export function listOrders({ filters, limit, offset, signal }: ListOrdersParams) {
  const qs = new URLSearchParams();
  qs.set("limit", String(limit));
  qs.set("offset", String(offset));
  if (filters.statuses.length > 0) qs.set("status", filters.statuses.join(","));
  if (filters.priority) qs.set("priority", filters.priority);
  if (filters.supplierId) qs.set("supplier_id", filters.supplierId);
  if (filters.warehouse) qs.set("warehouse", filters.warehouse);
  if (filters.dateFrom) qs.set("date_from", filters.dateFrom);
  if (filters.dateTo) qs.set("date_to", filters.dateTo);
  if (filters.minTotal !== null) qs.set("min_total", String(filters.minTotal));
  if (filters.search) qs.set("search", filters.search);
  qs.set("sort", filters.sort);
  qs.set("order", filters.order);
  return apiFetch<Paginated<OrderListItem>>(`/api/orders?${qs}`, { signal });
}

export function getOrder(id: string, signal?: AbortSignal) {
  return apiFetch<OrderDetail>(`/api/orders/${encodeURIComponent(id)}`, { signal });
}

export type PatchOrderBody = { status: string };

export function patchOrder(id: string, body: PatchOrderBody, signal?: AbortSignal) {
  return apiFetch<OrderDetail>(`/api/orders/${encodeURIComponent(id)}`, {
    method: "PATCH",
    body,
    signal,
  });
}

export type BulkAction = "approve" | "reject" | "flag";

export interface BulkActionRequest {
  orderIds: string[];
  action: BulkAction;
  reason?: string;
}

export interface BulkActionResponse {
  jobId: string;
}

export function submitBulkAction(body: BulkActionRequest, signal?: AbortSignal) {
  return apiFetch<BulkActionResponse>("/api/orders/bulk-action", {
    method: "POST",
    body: { ...body },
    signal,
  });
}

export type JobStatus = "processing" | "completed" | "failed";

export interface JobProgress {
  total: number;
  completed: number;
  failed: number;
}

export interface JobStatusResponse {
  status: JobStatus;
  progress: JobProgress;
}

export function getJob(jobId: string, signal?: AbortSignal) {
  return apiFetch<JobStatusResponse>(`/api/jobs/${encodeURIComponent(jobId)}`, { signal });
}
