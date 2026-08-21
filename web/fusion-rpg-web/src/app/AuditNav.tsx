import { NavLink } from "react-router-dom";
import { useSimState } from "@/lib/bus";
import { cn } from "@/lib/cn";

const links = [
  { to: "/status", label: "Status" },
  { to: "/stats", label: "Stats" },
  { to: "/pvz-stats", label: "PvzStats" },
  { to: "/pvz-activity", label: "PvzActivity" },
  { to: "/rpg-progression", label: "Progression" },
  { to: "/icon-dump", label: "IconDump" },
  { to: "/almanac-dump", label: "AlmanacText" },
  { to: "/cheats", label: "Cheats" },
  { to: "/types", label: "Types" },
  { to: "/recipes", label: "Recipes" },
  { to: "/log", label: "Log" },
  { to: "/runs", label: "Runs" },
  { to: "/lawn", label: "Lawn" },
  { to: "/roster", label: "Roster" },
  { to: "/demons", label: "Demons" },
  { to: "/expeditions", label: "Expeditions" },
  { to: "/fusion", label: "Fusion" },
  { to: "/storage", label: "Storage" }
] as const;

export function AuditNav() {
  const sim = useSimState();
  const simOn = sim.data != null;

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
              isActive && "z-nav-active border-lawn-hot bg-lawn text-text"
            )
          }
        >
          {link.label}
        </NavLink>
      ))}
      {simOn ? (
        <NavLink
          to="/sim"
          data-testid="nav-sim"
          className={({ isActive }) =>
            cn(
              "rounded-sm border border-transparent px-2.5 py-1.5 text-sm font-semibold text-muted transition-colors",
              isActive && "z-nav-active border-lawn-hot bg-lawn text-text"
            )
          }
        >
          Sim
        </NavLink>
      ) : null}
    </nav>
  );
}
