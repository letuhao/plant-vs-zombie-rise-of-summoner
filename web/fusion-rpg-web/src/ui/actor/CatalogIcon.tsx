import type { LucideIcon } from "lucide-react";
import {
  Activity,
  Ban,
  CircleDot,
  Crosshair,
  Flame,
  Heart,
  Hexagon,
  Moon,
  Mountain,
  Shield,
  Snowflake,
  Sparkles,
  Sun,
  Sword,
  Target,
  Virus,
  Wind,
  Zap
} from "lucide-react";
import { cn } from "@/lib/cn";

const LUCIDE_BY_KEY: Record<string, LucideIcon> = {
  heart: Heart,
  zap: Zap,
  sun: Sun,
  sparkles: Sparkles,
  shield: Shield,
  power: Sword,
  defense: Shield,
  crit: Crosshair,
  "crit-resist": Target,
  fire: Flame,
  flame: Flame,
  activity: Activity,
  hexagon: Hexagon,
  snowflake: Snowflake,
  ice: Snowflake,
  wind: Wind,
  air: Wind,
  mountain: Mountain,
  earth: Mountain,
  moon: Moon,
  dark: Moon,
  light: Sun,
  ban: Ban,
  virus: Virus,
  "yin-yang": CircleDot,
  qi: CircleDot
};

/**
 * Catalog `icon` / `hudToken` → lucide-react, with GG-58 generated fallback
 * (side/element tint + authored token text — never idWords).
 */
export function CatalogIcon({
  icon,
  fallbackToken,
  color,
  className,
  testId
}: {
  icon?: string | null;
  fallbackToken?: string | null;
  color?: string | null;
  className?: string;
  testId?: string;
}) {
  const key = (icon ?? "").trim().toLowerCase();
  const Lucide = key ? LUCIDE_BY_KEY[key] : undefined;
  if (Lucide) {
    return (
      <Lucide
        data-testid={testId}
        className={cn("h-4 w-4 shrink-0", className)}
        style={color ? { color } : undefined}
        aria-hidden
      />
    );
  }

  const glyph = (fallbackToken ?? icon ?? "·").slice(0, 2);
  return (
    <span
      data-testid={testId}
      aria-hidden
      className={cn(
        "inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-sm border border-border-control bg-panel-inset font-display text-2xs uppercase text-text",
        className
      )}
      style={color ? { color, borderColor: color } : undefined}
    >
      {glyph}
    </span>
  );
}
