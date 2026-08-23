import { useState } from "react";
import { LawnPage } from "@/features/lawn/LawnPage";
import { PanelShell } from "@/shell/PanelShell";
import { StageHost, useStageMountGuard } from "@/shell/stageHost";

/**
 * The lawn hosted as a band-0 stage (GG-1/GG-11): `LawnPage` renders exactly
 * as it always has, and a `PanelShell` can open over it without unmounting
 * it, resetting its view model, or touching the Phaser `Game` instance
 * `LawnGameHost` owns. This is the GG-11 keystone proof (T2) — it wraps the
 * *existing* page rather than a redesigned one, on purpose, so the proof
 * holds before anything else depends on it.
 */
export function LawnStage() {
  useStageMountGuard("lawn");
  const [panelOpen, setPanelOpen] = useState(false);

  return (
    <StageHost>
      <LawnPage />
      <button
        type="button"
        data-testid="lawn-stage-open-panel"
        className="band-hud fixed right-4 top-4 rounded-sm border border-border bg-panel px-3 py-1 text-sm text-text shadow-panel"
        onClick={() => setPanelOpen(true)}
      >
        Board panel
      </button>
      <PanelShell
        open={panelOpen}
        onOpenChange={setPanelOpen}
        title="Board panel"
        subtitle="Proves a panel can open here without disturbing the board"
        testId="lawn-stage-panel"
      >
        <p className="text-sm text-muted">
          Board layers arrive with a later pass — this one exists to prove they can open over the
          live game without resetting it.
        </p>
      </PanelShell>
    </StageHost>
  );
}
