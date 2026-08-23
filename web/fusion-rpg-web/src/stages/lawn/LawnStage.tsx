import { useState } from "react";
import { msg } from "@lingui/macro";
import { useLingui } from "@lingui/react";
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
 *
 * Uses `useLingui()`'s context-bound `_` with `msg` descriptors, not the
 * bare `t` macro. `t` compiles to a call against the global `i18n` singleton
 * directly — correct for non-component code, but a component using only
 * that has nothing subscribing it to `I18nProvider`'s context, so a locale
 * switch elsewhere in the tree never triggers *this* component to
 * re-render (ordinary React children-prop-reference semantics, not a
 * Lingui bug — found by trying to switch locale live and watching nothing
 * update). `_` is a real context consumer, so it does.
 */
export function LawnStage() {
  useStageMountGuard("lawn");
  const { _ } = useLingui();
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
        {_(msg`Board panel`)}
      </button>
      <PanelShell
        open={panelOpen}
        onOpenChange={setPanelOpen}
        title={_(msg`Board panel`)}
        subtitle={_(msg`Proves a panel can open here without disturbing the board`)}
        testId="lawn-stage-panel"
      >
        <p className="text-sm text-muted">
          {_(msg`Board layers arrive with a later pass — this one exists to prove they can open over the live game without resetting it.`)}
        </p>
      </PanelShell>
    </StageHost>
  );
}
