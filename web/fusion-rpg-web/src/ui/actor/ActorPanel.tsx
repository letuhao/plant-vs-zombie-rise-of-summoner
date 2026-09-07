import { useCallback, useEffect, useState } from "react";
import type { CommanderSheetMeta } from "@/contract/types";
import {
  actorSurfaceCatalogNow,
  type ActorSheetTabKind
} from "@/lib/bus/actorSurface";
import { PanelShell } from "@/shell/PanelShell";
import { Badge, Banner, Button } from "@/ui";
import { TabList } from "@/ui";
import type { ActorRungState } from "./actorRungState";
import { RungStateFallback } from "./RungStateFallback";
import { ActorFrame, formatActorPhase, LevelTag, PendingNote, SideBadge, displayInitial } from "./shared";
import { CommanderSheetFooter } from "./CommanderSheetFooter";
import { AptitudesTab, type AptitudeDraftState } from "./AptitudesTab";
import { ConditionTab } from "./ConditionTab";
import { DerivedTab } from "./DerivedTab";
import { ElementsTab, KitTab, ShieldTab, StatusTab } from "./CatalogTabs";
import { PathsTab } from "./PathsTab";
import { LeftoverBar } from "./LeftoverBar";
import { emitActorSheetObs } from "./actorSheetObs";

export type ActorPanelRole = "creature" | "commander";

/**
 * Catalog-era ActorSheet (band 2, GG-9). Near-fullscreen shell; eight structural tab kinds from
 * actor-surface catalog; leftover Confirm only on Aptitudes / dirty draft (GG-61 / GG-63).
 */
export function ActorPanel({
  state,
  open,
  onOpenChange,
  role = "creature",
  commanderMeta,
  setDefaultPending = false,
  onSetDefault,
  onDefendLawn,
  onOpenCommandersList,
  matchBanner
}: {
  state: ActorRungState;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  role?: ActorPanelRole;
  commanderMeta?: CommanderSheetMeta;
  setDefaultPending?: boolean;
  onSetDefault?: () => void;
  onDefendLawn?: () => void;
  onOpenCommandersList?: () => void;
  matchBanner?: { displayName: string; auraDisplayName: string | null };
}) {
  const surface = actorSurfaceCatalogNow();
  const [tab, setTab] = useState<ActorSheetTabKind>(surface.defaultOpen);
  const [aptitudeDraft, setAptitudeDraft] = useState<AptitudeDraftState | null>(null);
  const isCommander = role === "commander";
  const showLeftover = tab === "aptitudes" || (aptitudeDraft?.dirty ?? false);

  useEffect(() => {
    if (!open || state.kind !== "ready") return;
    emitActorSheetObs("actor-sheet.open", {
      instanceId: state.data.instanceId,
      tab,
      versionStamp: surface.versionStamp
    });
    // Open once per open cycle — tab changes emit separately.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, state.kind === "ready" ? state.data.instanceId : null]);

  const handleTabChange = useCallback(
    (id: string) => {
      const next = id as ActorSheetTabKind;
      setTab(next);
      emitActorSheetObs("actor-sheet.tab", { tab: next });
    },
    []
  );

  if (state.kind !== "ready") {
    return (
      <PanelShell open={open} onOpenChange={onOpenChange} title="Actor" testId="actor-panel" size="actorSheet">
        <RungStateFallback state={state} dimensionClass="h-40 w-full rounded-md" label="panel" />
      </PanelShell>
    );
  }

  const { data } = state;
  const name = data.displayName.state === "known" ? data.displayName.value : `#${data.instanceId.slice(0, 6)}`;
  const subtitle = isCommander
    ? commanderMeta?.activeAuraName
      ? `Commander · ${commanderMeta.activeAuraName}`
      : "Commander"
    : `${data.side === "plant" ? "Plant" : "Zombie"} · Lv ${data.level}`;

  const tabs = surface.tabs
    .filter((item) => !item.hidden)
    .sort((a, b) => a.order - b.order)
    .map((item) => ({
      id: item.kind,
      label: item.label,
      testId: `actor-sheet-tab-${item.kind}`
    }));

  const leftoverFooter = showLeftover ? (
    <div className="flex w-full flex-wrap items-center justify-between gap-2" data-testid="actor-leftover-footer">
      {aptitudeDraft ? (
        <>
          <LeftoverBar budget={aptitudeDraft.budget} spent={aptitudeDraft.spent} />
          <div className="flex gap-2">
            <Button
              variant="ghost"
              size="sm"
              data-testid="actor-leftover-reset"
              disabled={!aptitudeDraft.dirty || aptitudeDraft.saving}
              onClick={() => aptitudeDraft.revert()}
            >
              Reset
            </Button>
            <Button
              size="sm"
              data-testid="actor-leftover-confirm"
              disabled={!aptitudeDraft.dirty || !aptitudeDraft.withinBudget || aptitudeDraft.saving}
              onClick={() => void aptitudeDraft.save()}
            >
              {aptitudeDraft.saving ? "Saving…" : "Confirm"}
            </Button>
          </div>
        </>
      ) : (
        <p className="text-sm text-muted" data-testid="actor-leftover-pending">
          Loading aptitudes…
        </p>
      )}
    </div>
  ) : null;

  const defaultFooter =
    isCommander && commanderMeta ? (
      <CommanderSheetFooter
        isDefault={commanderMeta.isDefault}
        setDefaultPending={setDefaultPending}
        editsScope={matchBanner ? "nextRun" : undefined}
        onClose={() => onOpenChange(false)}
        onSetDefault={() => onSetDefault?.()}
        onDefendLawn={() => onDefendLawn?.()}
        onOpenCommandersList={() => onOpenCommandersList?.()}
      />
    ) : (
      <>
        <Button variant="ghost" size="sm" data-testid="actor-panel-release" onClick={() => onOpenChange(false)}>
          Release
        </Button>
        <Button size="sm" data-testid="actor-panel-deploy" onClick={() => onOpenChange(false)}>
          Deploy
        </Button>
      </>
    );

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title={name}
      subtitle={subtitle}
      testId="actor-panel"
      size="actorSheet"
      // Leftover strip replaces default footer while Aptitudes is active or draft dirty —
      // never fall back to Release/Deploy on that strip (GG-61 / GG-63).
      footer={showLeftover ? leftoverFooter : defaultFooter}
    >
      <div className="flex items-start gap-3">
        <ActorFrame side={data.side} initial={displayInitial(data.displayName, data.side)} size="panel" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            {isCommander ? (
              <>
                <Badge tone="neutral" data-testid="commander-sheet-role-tag">
                  Commander
                </Badge>
                {commanderMeta?.isDefault ? (
                  <Badge tone="ok" data-testid="commander-sheet-default-tag">
                    default
                  </Badge>
                ) : null}
                {commanderMeta?.activeAuraName ? (
                  <span className="text-muted" data-testid="commander-sheet-aura-tag">
                    {commanderMeta.activeAuraName}
                  </span>
                ) : null}
              </>
            ) : (
              <>
                <SideBadge side={data.side} />
                <LevelTag level={data.level} />
                <span className="text-muted" data-testid="actor-phase">
                  {formatActorPhase(data.phase)}
                </span>
              </>
            )}
          </div>
          <PendingNote pending={data.displayName} testId="actor-name-pending" />
        </div>
      </div>

      {matchBanner ? (
        <Banner tone="info" className="mt-4" data-testid="commander-sheet-match-banner">
          This match: {matchBanner.displayName}
          {matchBanner.auraDisplayName ? ` · ${matchBanner.auraDisplayName}` : ""}
        </Banner>
      ) : null}

      <TabList tabs={tabs} value={tab} onChange={handleTabChange} testId="actor-sheet-tabs" className="mt-4" />

      <div data-testid="actor-sheet-tab-panel">
        {tab === "condition" ? (
          <>
            {isCommander && commanderMeta ? (
              <>
                <div className="mt-4" data-testid="commander-sheet-overview-default">
                  <p className="text-2xs font-bold uppercase tracking-wide text-muted">Default lawn</p>
                  <p className="text-sm text-text">
                    {commanderMeta.isDefault ? "Leads the next run." : "Not your default lawn commander yet."}
                  </p>
                  {onOpenCommandersList ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="mt-1 px-0"
                      data-testid="commander-sheet-overview-change"
                      onClick={onOpenCommandersList}
                    >
                      Change in list
                    </Button>
                  ) : null}
                </div>
                <div className="mt-4" data-testid="commander-sheet-overview-location">
                  <p className="text-2xs font-bold uppercase tracking-wide text-muted">Location</p>
                  <p className="text-sm text-muted">{commanderMeta.locationStub ?? "not shown yet"}</p>
                </div>
                <div className="mt-4" data-testid="commander-sheet-overview-legion">
                  <p className="text-2xs font-bold uppercase tracking-wide text-muted">Legion</p>
                  <p className="text-sm text-muted">{commanderMeta.legionStub ?? "not shown yet"}</p>
                </div>
              </>
            ) : null}
            <ConditionTab data={data} surface={surface} />
          </>
        ) : null}

        {/* Keep aptitudes mounted while draft is dirty so leftover Confirm stays reachable. */}
        {tab === "aptitudes" || aptitudeDraft?.dirty ? (
          <div hidden={tab !== "aptitudes"} data-testid="aptitudes-mount">
            <AptitudesTab data={data} surface={surface} onDraftState={setAptitudeDraft} />
          </div>
        ) : null}

        {tab === "derived" ? <DerivedTab data={data} surface={surface} /> : null}
        {tab === "shield" ? <ShieldTab data={data} /> : null}
        {tab === "status" ? <StatusTab surface={surface} /> : null}
        {tab === "elements" ? <ElementsTab data={data} surface={surface} /> : null}
        {tab === "kit" ? <KitTab data={data} surface={surface} /> : null}
        {tab === "paths" ? <PathsTab elementTyping={data.elementTyping} /> : null}
      </div>
    </PanelShell>
  );
}

export const ActorSheet = ActorPanel;
