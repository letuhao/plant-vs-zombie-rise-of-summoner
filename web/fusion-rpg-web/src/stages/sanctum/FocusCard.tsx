import type { ActorRungState } from "@/ui/actor";
import { ActorCard } from "@/ui/actor";

/**
 * The "what next" card (information-architecture.md §2.1). Never an empty
 * box: with no bound creature it renders the first-run script (GG-43 — one
 * instruction, everything else absent rather than greyed) instead of a
 * blank panel waiting for content that doesn't exist yet.
 */
export function FocusCard({
  actorCount,
  firstActor,
  onOpenCreatures
}: {
  actorCount: number;
  firstActor: ActorRungState | null;
  onOpenCreatures: () => void;
}) {
  if (actorCount === 0 || !firstActor) {
    return (
      <div
        className="max-w-md rounded-md border border-border-control bg-panel p-4"
        data-testid="focus-card-first-run"
      >
        <p className="font-display text-lg text-text">Bind your first creature</p>
        <p className="mt-1 text-sm text-muted">
          Everything else in the Sanctum opens up from there — one instruction at a time.
        </p>
        <button
          type="button"
          data-testid="focus-card-cta"
          onClick={onOpenCreatures}
          className="mt-3 rounded-sm bg-lawn px-3 py-1.5 text-sm font-semibold text-text hover:bg-lawn-hot"
        >
          Open Creatures
        </button>
      </div>
    );
  }

  return (
    <div data-testid="focus-card-actor">
      <p className="mb-2 text-sm text-muted" data-testid="focus-card-count">
        {actorCount} creature{actorCount === 1 ? "" : "s"} bound
      </p>
      <ActorCard state={firstActor} onInspect={onOpenCreatures} />
    </div>
  );
}
