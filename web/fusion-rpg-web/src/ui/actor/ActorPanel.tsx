import { useState } from "react";
import type { CommanderSheetMeta } from "@/contract/types";
import { PanelShell } from "@/shell/PanelShell";
import { Badge, Banner, Button } from "@/ui";
import { TabList, type TabItem } from "@/ui";
import type { ActorRungState } from "./actorRungState";
import { RungStateFallback } from "./RungStateFallback";
import { ActorFrame, formatActorPhase, LevelTag, PendingNote, SideBadge, displayInitial } from "./shared";
import { CommanderSheetFooter } from "./CommanderSheetFooter";
import { GearTab } from "./GearTab";
import { ActionsTab } from "./ActionsTab";
import { PassivesTab } from "./PassivesTab";
import { DerivedStatsTab } from "./DerivedStatsTab";
import { ProgressionTab } from "./ProgressionTab";

type ActorSheetTab = "overview" | "progression" | "derived-stats" | "actions" | "passives" | "gear";
export type ActorPanelRole = "creature" | "commander";

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
  const [tab, setTab] = useState<ActorSheetTab>("overview");
  const isCommander = role === "commander";

  if (state.kind !== "ready") {
    return (
      <PanelShell open={open} onOpenChange={onOpenChange} title="Actor" testId="actor-panel">
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

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title={name}
      subtitle={subtitle}
      testId="actor-panel"
      footer={
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
        )
      }
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

      <TabList tabs={TABS} value={tab} onChange={(id) => setTab(id as ActorSheetTab)} testId="actor-sheet-tabs" className="mt-4" />

      <div data-testid="actor-sheet-tab-panel">
        {tab === "overview" ? (
          isCommander && commanderMeta ? (
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
          ) : (
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
          )
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
