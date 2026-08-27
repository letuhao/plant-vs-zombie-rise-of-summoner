import { AptitudesPage } from "@/features/aptitudes/AptitudesPage";
import { PanelShell } from "@/shell/PanelShell";

/**
 * spec-aptitude-allocation-surface.md — a layer, never a route (web/spec.md's own hard rule).
 * Matches ExpeditionsLayer's own shape: unchanged page, wrapped in the shell.
 */
export function AptitudesLayer({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  return (
    <PanelShell open={open} onOpenChange={onOpenChange} title="Primary Stats" testId="aptitudes-layer">
      <AptitudesPage />
    </PanelShell>
  );
}
