import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function StatsPage() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Analytics</CardTitle>
        <CardDescription>
          Charts driven by <code>/api/orders/stats</code> ship in a later slice (Recharts is pre-installed).
        </CardDescription>
      </CardHeader>
      <CardContent className="text-sm text-muted-foreground">
        Coming soon: status distribution, monthly volume, top suppliers.
      </CardContent>
    </Card>
  );
}
