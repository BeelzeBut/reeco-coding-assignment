export interface Paginated<T> {
  data: T[];
  total: number;
  limit: number;
  offset: number;
}

export interface ApiErrorBody {
  error: string;
  code: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, body: ApiErrorBody) {
    super(body.error);
    this.name = "ApiError";
    this.status = status;
    this.code = body.code;
  }
}
