import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function SuppliersPage() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Suppliers</CardTitle>
        <CardDescription>
          Supplier list with filters lands in a later slice. Click-through navigates to supplier details.
        </CardDescription>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">
        Coming soon: <code>/api/suppliers</code> table and per-supplier performance views.
      </CardContent>
    </Card>
  );
}
