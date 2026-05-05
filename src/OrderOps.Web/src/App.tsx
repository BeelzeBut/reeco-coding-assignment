import { Link, NavLink, Outlet } from "react-router-dom";
import { cn } from "@/lib/utils";
import { ThemeToggle } from "@/components/theme-toggle";
import { Logo } from "@/components/logo";
import { ScrollToTop } from "@/components/scroll-to-top";

const navItems = [
  { to: "/orders", label: "Orders" },
  { to: "/stats", label: "Analytics" },
  { to: "/suppliers", label: "Suppliers" },
];

export function App() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <ScrollToTop />
      <header className="sticky top-0 z-40 border-b border-border/80 bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="container flex h-14 items-center gap-6">
          <Link
            to="/orders"
            className="flex items-center gap-2 rounded-md outline-none ring-offset-background transition-opacity duration-150 hover:opacity-90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            aria-label="OrderOps — go to orders"
          >
            <Logo withWordmark />
          </Link>
          <span aria-hidden className="hidden h-6 w-px bg-border sm:block" />
          <nav className="flex items-center gap-1 text-sm">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    "relative inline-flex h-14 items-center px-3 font-medium outline-none transition-colors duration-150",
                    "after:pointer-events-none after:absolute after:inset-x-3 after:-bottom-px after:h-0.5 after:rounded-full after:bg-primary after:transition-opacity after:duration-150",
                    "focus-visible:text-foreground",
                    isActive
                      ? "text-foreground after:opacity-100"
                      : "text-muted-foreground hover:text-foreground after:opacity-0"
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
