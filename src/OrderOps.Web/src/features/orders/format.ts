const currency = new Intl.NumberFormat(undefined, {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 2,
});

const dateTime = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function formatCurrency(value: number) {
  return currency.format(value);
}

export function formatDateTime(iso: string) {
  return dateTime.format(new Date(iso));
}

export function statusVariant(status: string): "default" | "secondary" | "destructive" | "muted" {
  switch (status) {
    case "delivered":
    case "shipped":
    case "approved":
      return "default";
    case "cancelled":
    case "rejected":
      return "destructive";
    case "pending":
      return "secondary";
    default:
      return "muted";
  }
}
