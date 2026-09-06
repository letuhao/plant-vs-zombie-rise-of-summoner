import { Button } from "@/ui";
import type { PlanPrice } from "@/contract/passivesPlan";
import type { FocusReading } from "@/contract/passivesYours";
import { PASSIVE_TREE_VOCABULARY } from "@/contract/passiveTreeVocabulary";

/**
 * passive-tree-todo.md I8 — "The Plan object" (spec-tree-surface.md §5.1, §5.2). The draft/dirty/
 * Revert flow already ships per-trait (I7's `TraitDetail`); this is the Plan-level view sitting above
 * it: the three-number price of EVERYTHING currently pending across the whole actor, not one node in
 * isolation, and the one Revert that discards all of it at once.
 *
 * Kept flat under `ui/actor/` rather than a `passives/` subfolder — matching where I2-I7 actually
 * landed `PathBrowse.tsx`/`PathLattice.tsx`/`TraitDetail.tsx`, not `spec-tree-surface.md` §12's
 * originally-proposed (and never actually followed) directory shape.
 */
export function PlanPanel({
  price,
  dirty,
  onRevert,
  focus
}: {
  price: PlanPrice;
  /** Nothing pending -- render nothing (GG-17: an empty plan is not a state worth a panel; the
   * currencies already shown at Level 0 cover "you have spent nothing"). */
  dirty: boolean;
  onRevert: () => void;
  /**
   * Task I9 (spec-tree-surface.md §5.1 item 2, §6) -- "what Focus would become" if this Plan were
   * committed right now. A DRAFT-ONLY preview (`draftFocusPreview`, contract/passivesYours.ts) --
   * NEVER the committed value Level 0's own Focus line reads, which always comes straight off the
   * server's `herfindahlMilli`/`focusMilli`. `undefined` when the wire hasn't shipped the
   * concentration tuning dial yet (a pre-I9 fixture/server): renders no line rather than a fabricated
   * one, the same honest-absence convention `nextUnlockPrice` already uses. `null` when the draft
   * owns nothing yet (H undefined over an empty allocation, §6). Moves live as the draft is edited
   * (test 14: "both halves of the line move together," M8/GG-33) because the caller recomputes it
   * from the merged soul-level map on every render -- no memoized staleness to fight.
   */
  focus?: FocusReading | null;
}) {
  if (!dirty) return null;

  return (
    <section data-testid="passives-plan-panel" className="flex flex-col gap-1 border border-border p-2">
      <p className="text-2xs font-bold uppercase tracking-wide text-muted">Plan</p>

      {/* §5.2: "the Plan renders three numbers, not one" -- newTraits/skillPoints/souls, never a
          single node's price in isolation. */}
      <p className="text-sm text-text" data-testid="plan-trait-count">
        {price.newTraits} new {price.newTraits === 1 ? "trait" : "traits"}
      </p>
      <p className="text-sm text-text" data-testid="plan-skill-points">
        {price.skillPoints} {PASSIVE_TREE_VOCABULARY.currency.skillPoints}
      </p>
      <p className="text-sm text-text" data-testid="plan-souls">
        {price.souls} {PASSIVE_TREE_VOCABULARY.currency.souls}
      </p>

      {/* §6: "One line, on Level 0 and in the plan preview" -- same sentence shape as
          `PassivesTab.tsx`'s own `passives-focus` line, read from a DRAFT preview here rather than
          the committed report. Prose, never a `Magnitude` (test 15: no new `UnitClass`). */}
      {focus ? (
        <p className="text-xs text-muted" data-testid="plan-focus">
          <span className="font-bold text-text">Focus</span> — your commitment sits across about{" "}
          {focus.effectivePaths} {focus.effectivePaths === 1 ? "path" : "paths"}. Path bonuses ×
          {focus.multiplier.toFixed(2)}.
        </p>
      ) : null}

      {/* §5.2's own printed promise. DISCLOSED SIMPLIFICATION: "once" here means once per time the
          panel renders (this module holds no persisted "has the player already seen this" flag) --
          the same disclosed simplification `PathLattice.tsx`'s own D28 fiction sentence makes. */}
      <p className="text-2xs text-muted">A plan costs the same whichever order you follow it in.</p>

      <Button size="sm" variant="ghost" type="button" data-testid="passives-plan-revert" onClick={onRevert}>
        Revert plan
      </Button>
    </section>
  );
}
