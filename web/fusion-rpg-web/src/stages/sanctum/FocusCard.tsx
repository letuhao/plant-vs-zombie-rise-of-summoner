import type { ActorRungState } from "@/ui/actor";
import { ActorCard } from "@/ui/actor";
import { Button } from "@/ui";
import { FirstRunReveal } from "./FirstRunReveal";

export type OverdueContract = {
  instanceId: string;
  /** Resolved display name — the caller does the roster/species lookup (same one `PactsLayer.tsx`
   * already does), `FocusCard` never reaches into demon data itself. */
  name: string;
};

/**
 * The "what next" card (information-architecture.md §2.1, plate 01 §C). Never an empty box: with
 * no bound creature it renders the first-run script (GG-43 — one instruction, everything else
 * absent rather than greyed) instead of a blank panel waiting for content that doesn't exist yet.
 *
 * T26's priority rule (plate 01 §C's own note): "overdue tribute beats a returned expedition beats
 * a fusable pair beats 'start a run'." Three of those four tiers are real here — overdue tribute
 * (`useContracts` + `conditionOf`, same as `PactsLayer.tsx`) and a returned expedition
 * (`useExpeditionReturnWatcher`, already wired for the rail's own badge) both key off state this
 * app already tracks. The fusable-pair tier does not: star-merge and recipe fusion both have
 * server-computed eligibility (`FusionPage.tsx`'s own preview call, cap and recipe-specific), so
 * "two of the same species" is not a safe client-side stand-in — it would sometimes claim a pair
 * is fusable when it isn't. Rather than ship a heuristic that can lie, this tier is skipped; when
 * nothing pending remains real, the card falls straight to the run prompt.
 */
export function FocusCard({
  actorCount,
  firstActor,
  overdueContract,
  returnedExpeditionCount,
  onOpenCreatures,
  onOpenPacts,
  onOpenExpeditions
}: {
  actorCount: number;
  firstActor: ActorRungState | null;
  overdueContract?: OverdueContract;
  returnedExpeditionCount?: number;
  onOpenCreatures: () => void;
  onOpenPacts?: () => void;
  onOpenExpeditions?: () => void;
}) {
  if (actorCount === 0 || !firstActor) {
    return <FirstRunReveal onBind={onOpenCreatures} />;
  }

  if (overdueContract) {
    return (
      <div className="rounded-md border border-bad bg-panel p-4" data-testid="focus-card-tribute-overdue">
        <p className="text-xs font-bold uppercase tracking-wide text-bad">Waiting on you</p>
        <p className="mt-1 font-display text-lg text-text">{overdueContract.name} wants its tribute</p>
        <p className="mt-1 text-sm text-muted">
          Overdue. Loyalty is falling, and the pact cannot be renegotiated until it is paid.
        </p>
        <Button size="sm" className="mt-3" data-testid="focus-card-pay-tribute" onClick={onOpenPacts}>
          Go to Pacts
        </Button>
      </div>
    );
  }

  if (returnedExpeditionCount && returnedExpeditionCount > 0) {
    return (
      <div className="rounded-md border border-ok bg-panel p-4" data-testid="focus-card-expedition-returned">
        <p className="text-xs font-bold uppercase tracking-wide text-ok">Waiting on you</p>
        <p className="mt-1 font-display text-lg text-text">
          {returnedExpeditionCount} expedition{returnedExpeditionCount === 1 ? "" : "s"} returned
        </p>
        <p className="mt-1 text-sm text-muted">Ready to collect.</p>
        <Button size="sm" className="mt-3" data-testid="focus-card-collect" onClick={onOpenExpeditions}>
          Go to Expeditions
        </Button>
      </div>
    );
  }

  return (
    <div data-testid="focus-card-run-prompt">
      <p className="mb-2 text-sm text-muted" data-testid="focus-card-count">
        {actorCount} creature{actorCount === 1 ? "" : "s"} bound
      </p>
      <ActorCard state={firstActor} onInspect={onOpenCreatures} />
    </div>
  );
}
