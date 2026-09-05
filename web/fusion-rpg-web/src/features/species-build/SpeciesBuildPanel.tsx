import { useEffect, useRef, useState } from "react";
import { formatMagnitude } from "@/i18n/magnitude";
import { Banner, Button, ConfirmDialog, EmptyState, Field, NumberInput, Panel, StatBar } from "@/ui";
import { useSpeciesBuild } from "./useSpeciesBuild";

function points(value: number): string {
  return formatMagnitude({ unit: "aptitudePoints", value });
}

/**
 * spec-allocation-surface.md — "a Pokédex entry with an edit button," not a configuration screen.
 * Shows the shipped baseline, the override as a deviation from it, and the remaining budget; saves
 * always through `useSpeciesBuild`'s one respec mutation, which decides free-vs-priced for itself.
 *
 * Mirrors `AptitudesPage.tsx`'s own draft-and-save shape (spec's own "Code style" instruction) rather
 * than inventing a third: a local `draft` seeded from the server response, re-seeded only when the
 * server state changes and the player isn't mid-edit.
 */
export function SpeciesBuildPanel({ playerId, speciesId }: { playerId: number; speciesId: string }) {
  const { state, price, respec, save } = useSpeciesBuild(playerId, speciesId);
  const [draft, setDraft] = useState<Record<string, number> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  // Re-seed on initial load (and after switching species, below) -- never while the player is
  // actively editing an unsaved draft. A SUCCESSFUL SAVE does not go through this path at all (see
  // `commit`'s own comment) -- it seeds directly from the mutation's own response, which is the only
  // way to avoid racing the query cache's own invalidation-triggered refetch.
  useEffect(() => {
    if (state.data && draft === null) setDraft(state.data.shares);
  }, [state.data, draft]);

  // Switching species (the panel stays mounted, only `speciesId` changes) must drop the OLD
  // species' draft -- otherwise the new species would render with the previous one's numbers for
  // one frame, and a save would post the wrong species' shares. Guarded to skip the FIRST render
  // (`prevSpeciesId` starts equal to `speciesId`): without this, both effects fire on mount in
  // declaration order, and this one would immediately clobber the seed the effect above just set,
  // leaving `draft` stuck at `null` forever.
  const prevSpeciesId = useRef(speciesId);
  useEffect(() => {
    if (prevSpeciesId.current === speciesId) return;
    prevSpeciesId.current = speciesId;
    setDraft(null);
    setError(null);
    setConfirmOpen(false);
  }, [speciesId]);

  if (state.isLoading || !state.data || draft === null) {
    return <EmptyState title="Loading species build…" testId="species-build-loading" />;
  }

  if (state.isError) {
    return (
      <Banner tone="error" data-testid="species-build-error">
        Couldn&apos;t load this species&apos; build.
        <Button size="sm" variant="ghost" className="ml-2" onClick={() => void state.refetch()}>
          Retry
        </Button>
      </Banner>
    );
  }

  const data = state.data;
  const spent = Object.values(draft).reduce((sum, v) => sum + (Number.isFinite(v) ? v : 0), 0);
  const withinBudget = spent <= data.budget;
  const dirty = JSON.stringify(draft) !== JSON.stringify(data.shares);
  const isRevert = dirty && Object.values(draft).every((v) => v === 0);
  // Predicts what the server will actually decide (spec-species-respec.md's own free/priced rule) —
  // `everRespecced`, never `respecCount === 0` (that decays over time; `everRespecced` never resets).
  const isFree = isRevert || !(price.data?.everRespecced ?? false);

  async function commit() {
    setError(null);
    try {
      const result = await save(draft!);
      // Real bug found and fixed via the E2E round trip: seeding the draft from `state.data` after a
      // save (e.g. by dropping it to `null` and letting the re-seed effect above pick it back up)
      // RACES the query cache's own invalidation-triggered refetch -- the effect can fire on a render
      // where `state.data` hasn't updated yet (a revert then shows stale zeros) OR where it updated
      // TOO early relative to draft becoming null (a change then reverts to the pre-save baseline).
      // The mutation's OWN response already carries the authoritative `shares` the server just
      // computed (whether this was a first override, a revert, or a priced change) -- using it
      // directly needs no race with anything.
      setDraft(result.shares);
      setConfirmOpen(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
      setConfirmOpen(false);
    }
  }

  function onSaveClick() {
    if (isFree) {
      void commit();
    } else {
      setConfirmOpen(true); // priced change: the price is shown here, BEFORE the confirm
    }
  }

  const saveLabel = isRevert ? "Reset to default" : isFree ? "Save build" : "Respec…";
  const saveTitle = !withinBudget
    ? `Over this species' budget by ${points(spent - data.budget)}`
    : !dirty
      ? "No changes to save"
      : isRevert
        ? "Reset to the shipped baseline — free"
        : isFree
          ? "First override — free"
          : price.data
            ? `Costs ${price.data.priceAmount.toLocaleString()} ${price.data.priceResource.toLowerCase()}`
            : "Loading price…";

  return (
    <div className="flex flex-col gap-4" data-testid="species-build-panel">
      {error && <Banner tone="error">{error}</Banner>}

      <Panel title="Shipped build" testId="species-build-baseline">
        <p className="text-sm text-muted" data-testid="species-build-status">
          {data.hasOverride ? "You've overridden the shipped build below." : "You're running the shipped build."}
        </p>
        <StatBar
          label={`${points(spent)} / ${points(data.budget)} spent`}
          value={spent}
          max={Math.max(data.budget, 1)}
        />
      </Panel>

      <Panel title="Aptitudes" testId="species-build-grid">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {Object.keys(data.baseline).map((id) => {
            const baselineValue = data.baseline[id] ?? 0;
            const draftValue = draft[id] ?? 0;
            const deviation = draftValue - baselineValue;
            return (
              <Field key={id} label={id}>
                <NumberInput
                  min={0}
                  value={draftValue}
                  data-testid={`species-build-input-${id}`}
                  onChange={(next) =>
                    setDraft((d) => ({ ...(d ?? {}), [id]: Math.max(0, Math.trunc(next)) }))
                  }
                />
                {deviation !== 0 ? (
                  <span className="text-xs text-muted" data-testid={`species-build-deviation-${id}`}>
                    {deviation > 0 ? "+" : ""}
                    {points(deviation)} vs shipped
                  </span>
                ) : null}
              </Field>
            );
          })}
        </div>
      </Panel>

      <Button
        onClick={onSaveClick}
        disabled={!dirty || !withinBudget || respec.isPending}
        title={saveTitle}
        data-testid="species-build-save"
      >
        {respec.isPending ? "Saving…" : saveLabel}
      </Button>

      <ConfirmDialog
        open={confirmOpen}
        title="Respec this species?"
        message={
          price.data
            ? `This changes an existing build and costs ${price.data.priceAmount.toLocaleString()} ${price.data.priceResource.toLowerCase()}.`
            : "Loading the current price…"
        }
        confirmLabel="Respec"
        busy={respec.isPending}
        onConfirm={() => void commit()}
        onCancel={() => setConfirmOpen(false)}
        testId="species-build-respec-confirm"
      />
    </div>
  );
}
