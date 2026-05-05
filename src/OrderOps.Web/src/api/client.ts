import { ApiError, type ApiErrorBody } from "@/api/types";

type Json = Record<string, unknown> | unknown[] | string | number | boolean | null;

interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: Json;
  signal?: AbortSignal;
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = "GET", body, signal } = options;

  const res = await fetch(path, {
    method,
    headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });

  const text = await res.text();
  const parsed = text.length > 0 ? (JSON.parse(text) as unknown) : null;

  if (!res.ok) {
    throw new ApiError(res.status, parsed as ApiErrorBody);
  }

  return parsed as T;
}
