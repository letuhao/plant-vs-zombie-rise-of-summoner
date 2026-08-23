import { NavLink } from "react-router-dom";
import { cn } from "@/lib/cn";

/**
 * T12: the nine developer surfaces (status, stats, pvz-activity, icon-dump,
 * almanac-dump, cheats, sim, log, runs) are gone from here — they're
 * reached through the developer tree (`` ` ``), not player navigation.
 */
const links = [
  { to: "/pvz-stats", label: "PvzStats" },
  { to: "/rpg-progression", label: "Progression" },
  { to: "/types", label: "Types" },
  { to: "/recipes", label: "Recipes" },
  { to: "/lawn", label: "Lawn" },
  { to: "/world", label: "World" },
  { to: "/roster", label: "Roster" },
  { to: "/demons", label: "Demons" },
  { to: "/expeditions", label: "Expeditions" },
  { to: "/fusion", label: "Fusion" },
  { to: "/storage", label: "Storage" }
] as const;

export function AuditNav() {
  return (
    <nav
      className="flex w-44 shrink-0 flex-col gap-1 border-r border-border bg-soil-raised/60 p-3"
      data-testid="audit-nav"
      aria-label="Audit"
    >
      <p className="mb-1 px-2 text-xs font-semibold uppercase tracking-wide text-muted">Audit</p>
      {links.map((link) => (
        <NavLink
          key={link.to}
          to={link.to}
          data-testid={`nav-${link.label.toLowerCase()}`}
          className={({ isActive }) =>
            cn(
              "rounded-sm border border-transparent px-2.5 py-1.5 text-sm font-semibold text-muted transition-colors",
              isActive && "border-lawn-hot bg-lawn text-text"
            )
          }
        >
          {link.label}
        </NavLink>
      ))}
    </nav>
  );
}
