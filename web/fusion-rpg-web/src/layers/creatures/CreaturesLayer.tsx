import { useUniqueActors } from "@/lib/bus";
import { adaptActor } from "@/contract/adapt";
import { PanelShell } from "@/shell/PanelShell";
import { ActorCard, ActorRow, type ActorRungState } from "@/ui/actor";
import { EmptyState } from "@/ui/EmptyState";

/**
 * The bound roster (information-architecture.md §3: `C`, replaces
 * `/roster`). Ladder rungs: row for the list, card for the selected
 * creature's detail — the same `ActorView` contract T8 built, never a
 * second rendering of the same data (GG-9). No `typeId` anywhere: every
 * label here comes from the adapted view, not the raw DTO.
 */
export function CreaturesLayer({
  open,
  onOpenChange,
  playerId,
  selectedId,
  onSelect
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  playerId: number;
  selectedId: string | null;
  onSelect: (instanceId: string | null) => void;
}) {
  const query = useUniqueActors(playerId);
  const actors = query.data?.items ?? [];
  const selected = selectedId ? actors.find((a) => a.instanceId === selectedId) : undefined;
  const selectedState: ActorRungState | null = selected ? { kind: "ready", data: adaptActor(selected) } : null;

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title="Creatures"
      subtitle={`${actors.length} bound`}
      testId="creatures-layer"
    >
      {actors.length === 0 ? (
        <EmptyState
          title="No creatures bound yet"
          hint="Bind one to see it here — the roster fills in as you play."
        />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="rounded-md border border-border" data-testid="creatures-list">
            {actors.map((actor) => (
              <button
                key={actor.instanceId}
                type="button"
                data-testid={`creatures-row-${actor.instanceId}`}
                data-selected={actor.instanceId === selectedId}
                onClick={() => onSelect(actor.instanceId === selectedId ? null : actor.instanceId)}
                className="block w-full text-left focus-visible:bg-panel-raised aria-selected:bg-panel-raised"
                aria-selected={actor.instanceId === selectedId}
              >
                <ActorRow state={{ kind: "ready", data: adaptActor(actor) }} />
              </button>
            ))}
          </div>

          {selectedState ? (
            <div data-testid="creatures-detail">
              <p className="mb-2 text-xs font-bold uppercase tracking-wide text-faint">Selected</p>
              <ActorCard state={selectedState} />
            </div>
          ) : null}
        </div>
      )}
    </PanelShell>
  );
}
