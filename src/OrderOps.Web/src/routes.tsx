import { createBrowserRouter, Navigate } from "react-router-dom";
import { App } from "@/App";
import { OrdersPage } from "@/features/orders/OrdersPage";
import { StatsPage } from "@/features/stats/StatsPage";
import { SuppliersPage } from "@/features/suppliers/SuppliersPage";
import { SupplierDetailPage } from "@/features/suppliers/SupplierDetailPage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <App />,
    children: [
      { index: true, element: <Navigate to="/orders" replace /> },
      { path: "orders", element: <OrdersPage /> },
      { path: "stats", element: <StatsPage /> },
      { path: "suppliers", element: <SuppliersPage /> },
      { path: "suppliers/:id", element: <SupplierDetailPage /> },
    ],
  },
]);
