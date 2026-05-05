import { Monitor, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme, type Theme } from "@/components/theme-provider";

const NEXT: Record<Theme, Theme> = { light: "dark", dark: "system", system: "light" };
const ICON: Record<Theme, typeof Sun> = { light: Sun, dark: Moon, system: Monitor };
const LABEL: Record<Theme, string> = {
  light: "Light theme — click for dark",
  dark: "Dark theme — click for system",
  system: "System theme — click for light",
};

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const Icon = ICON[theme];

  return (
    <Button
      variant="ghost"
      size="icon"
      aria-label={LABEL[theme]}
      title={LABEL[theme]}
      onClick={() => setTheme(NEXT[theme])}
    >
      <Icon />
    </Button>
  );
}
