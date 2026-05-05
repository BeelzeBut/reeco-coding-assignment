import { NavLink, Outlet } from "react-router-dom";
import { cn } from "@/lib/utils";
import { ThemeToggle } from "@/components/theme-toggle";

const navItems = [
  { to: "/orders", label: "Orders" },
  { to: "/stats", label: "Analytics" },
  { to: "/suppliers", label: "Suppliers" },
];

export function App() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b">
        <div className="container flex h-14 items-center gap-6">
          <span className="font-semibold tracking-tight">OrderOps</span>
          <nav className="flex items-center gap-4 text-sm">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    "transition-colors duration-150 hover:text-foreground",
                    isActive ? "text-foreground" : "text-muted-foreground"
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto">
            <ThemeToggle />
          </div>
        </div>
      </header>
      <main className="container py-8">
        <Outlet />
      </main>
    </div>
  );
}
