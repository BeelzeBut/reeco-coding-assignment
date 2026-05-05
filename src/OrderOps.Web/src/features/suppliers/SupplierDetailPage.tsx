import { useParams } from "react-router-dom";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function SupplierDetailPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <Card>
      <CardHeader>
        <CardTitle>Supplier {id}</CardTitle>
        <CardDescription>
          Performance metrics and order history land here. Backed by <code>/api/suppliers/:id/performance</code>.
        </CardDescription>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">Coming soon.</CardContent>
    </Card>
  );
}
