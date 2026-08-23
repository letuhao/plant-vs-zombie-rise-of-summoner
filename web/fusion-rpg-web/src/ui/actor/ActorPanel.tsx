import { PanelShell } from "@/shell/PanelShell";
import { Button } from "@/ui/Button";
import type { ActorRungState } from "./actorRungState";
import { RungStateFallback } from "./RungStateFallback";
import { ActorFrame, LevelTag, PendingNote, SideBadge, displayInitial } from "./shared";

/** Rung 5 — band 2, opens over any stage (GG-9: the one canonical actor surface, not a sixth one per screen). */
export function ActorPanel({
  state,
  open,
  onOpenChange
}: {
  state: ActorRungState;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
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
          <Button variant="ghost" size="sm" data-testid="actor-panel-release">
            Release
          </Button>
          <Button size="sm" data-testid="actor-panel-deploy">
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
              {data.phase}
            </span>
          </div>
          <PendingNote pending={data.displayName} testId="actor-name-pending" />
        </div>
      </div>

      <div className="mt-4">
        <p className="text-2xs font-bold uppercase tracking-wide text-faint">Standing</p>
        <PendingNote pending={data.channelSummary} testId="actor-standing-pending" />
      </div>

      <div className="mt-4">
        <p className="text-2xs font-bold uppercase tracking-wide text-faint">Element typing</p>
        <PendingNote pending={data.elementTyping} testId="actor-element-pending" />
      </div>

      <div className="mt-4">
        <p className="text-2xs font-bold uppercase tracking-wide text-faint">Shield</p>
        <PendingNote pending={data.shieldStack} testId="actor-shield-pending" />
      </div>

      <div className="mt-4">
        <p className="text-2xs font-bold uppercase tracking-wide text-faint">Equipment</p>
        <PendingNote pending={data.equipSlots} testId="actor-equip-pending" />
      </div>
    </PanelShell>
  );
}
