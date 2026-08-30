import { useState } from "react";
import { PanelShell } from "@/shell/PanelShell";
import { Button } from "@/ui/Button";
import { TabList, type TabItem } from "@/ui";
import type { ActorRungState } from "./actorRungState";
import { RungStateFallback } from "./RungStateFallback";
import { ActorFrame, formatActorPhase, LevelTag, PendingNote, SideBadge, displayInitial } from "./shared";
import { GearTab } from "./GearTab";
import { ActionsTab } from "./ActionsTab";
import { PassivesTab } from "./PassivesTab";
import { DerivedStatsTab } from "./DerivedStatsTab";
import { ProgressionTab } from "./ProgressionTab";

type ActorSheetTab = "overview" | "progression" | "derived-stats" | "actions" | "passives" | "gear";

const TABS: TabItem[] = [
  { id: "overview", label: "Overview", testId: "actor-sheet-tab-overview" },
  { id: "progression", label: "Progression", testId: "actor-sheet-tab-progression" },
  { id: "derived-stats", label: "Derived Stats", testId: "actor-sheet-tab-derived-stats" },
  { id: "actions", label: "Actions", testId: "actor-sheet-tab-actions" },
  { id: "passives", label: "Passives", testId: "actor-sheet-tab-passives" },
  { id: "gear", label: "Gear", testId: "actor-sheet-tab-gear" }
];

/**
 * Rung 5 — band 2, opens over any stage (GG-9: the one canonical actor surface, not a sixth one per
 * screen). actor-sheet-shell: six tabs, one door instead of five scattered surfaces
 * (actor-sheet-map.md). Standing/Element-typing stay Overview's own content, unchanged from before
 * this tab bar existed — this module relocates, it does not redesign. Progression/Derived Stats/
 * Actions/Passives/Gear are each a later module's own tab body; until each lands, its slot is simply
 * empty (never a duplicate of another tab's content).
 */
export function ActorPanel({
  state,
  open,
  onOpenChange
}: {
  state: ActorRungState;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [tab, setTab] = useState<ActorSheetTab>("overview");

  if (state.kind !== "ready") {
    return (
      <PanelShell open={open} onOpenChange={onOpenChange} title="Actor" testId="actor-panel">
        <RungStateFallback state={state} dimensionClass="h-40 w-full rounded-md" label="panel" />
      </PanelShell>
    );
  }
  const { data } = state;
  const name = data.displayName.state === "known" ? data.displayName.value : `#${data.instanceId.slice(0, 6)}`;
  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title={name}
      subtitle={`${data.side === "plant" ? "Plant" : "Zombie"} · Lv ${data.level}`}
      testId="actor-panel"
      footer={
        <>
          <Button variant="ghost" size="sm" data-testid="actor-panel-release" onClick={() => onOpenChange(false)}>
            Release
          </Button>
          <Button size="sm" data-testid="actor-panel-deploy" onClick={() => onOpenChange(false)}>
            Deploy
          </Button>
        </>
      }
    >
      <div className="flex items-start gap-3">
        <ActorFrame side={data.side} initial={displayInitial(data.displayName, data.side)} size="panel" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <SideBadge side={data.side} />
            <LevelTag level={data.level} />
            <span className="text-muted" data-testid="actor-phase">
              {formatActorPhase(data.phase)}
            </span>
          </div>
          <PendingNote pending={data.displayName} testId="actor-name-pending" />
        </div>
      </div>

      <TabList tabs={TABS} value={tab} onChange={(id) => setTab(id as ActorSheetTab)} testId="actor-sheet-tabs" className="mt-4" />

      <div data-testid="actor-sheet-tab-panel">
        {tab === "overview" ? (
          <>
            <div className="mt-4">
              <p className="text-2xs font-bold uppercase tracking-wide text-muted">Standing</p>
              <PendingNote pending={data.channelSummary} testId="actor-standing-pending" />
            </div>

            <div className="mt-4">
              <p className="text-2xs font-bold uppercase tracking-wide text-muted">Element typing</p>
              <PendingNote pending={data.elementTyping} testId="actor-element-pending" />
            </div>

            <div className="mt-4">
              <p className="text-2xs font-bold uppercase tracking-wide text-muted">Shield</p>
              <PendingNote pending={data.shieldStack} testId="actor-shield-pending" />
            </div>
          </>
        ) : null}
        {tab === "progression" ? <ProgressionTab data={data} /> : null}
        {tab === "derived-stats" ? <DerivedStatsTab data={data} /> : null}
        {tab === "actions" ? <ActionsTab data={data} /> : null}
        {tab === "passives" ? <PassivesTab /> : null}
        {tab === "gear" ? <GearTab data={data} /> : null}
      </div>
    </PanelShell>
  );
}
