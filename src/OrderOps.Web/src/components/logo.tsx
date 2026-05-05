import { cn } from "@/lib/utils";

type LogoProps = {
  className?: string;
  withWordmark?: boolean;
};

export function Logo({ className, withWordmark = false }: LogoProps) {
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <LogoMark className="size-7" />
      {withWordmark ? (
        <span className="text-base font-semibold tracking-tight">
          OrderOps
        </span>
      ) : null}
    </span>
  );
}

function LogoMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      role="img"
      aria-label="OrderOps logo"
      className={cn("text-primary", className)}
    >
      <rect width="32" height="32" rx="8" fill="currentColor" />
      <g fill="hsl(var(--primary-foreground))">
        <rect x="8" y="9" width="16" height="2.5" rx="1.25" />
        <rect x="8" y="14.75" width="12" height="2.5" rx="1.25" />
        <rect x="8" y="20.5" width="8" height="2.5" rx="1.25" />
      </g>
    </svg>
  );
}
