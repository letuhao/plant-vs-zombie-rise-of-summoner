import { useEffect, useMemo, useRef, useState } from "react";
import type { ActorView } from "@/contract/types";
import type { ActorSurfaceCatalog, AptitudeCatalogRow } from "@/lib/bus/actorSurface";
import { useAptitudes, usePlayers, useSaveAptitudes } from "@/lib/bus";
import { useAllocationDraft } from "@/hooks/useAllocationDraft";
import { Banner, EmptyState } from "@/ui";
import { AptitudeTile } from "./AptitudeTile";
import { InspectSplit } from "./InspectSplit";

export type AptitudeDraftState = {
  budget: number;
  spent: number;
  leftover: number;
  dirty: boolean;
  withinBudget: boolean;
  saving: boolean;
  revert: () => void;
  save: () => Promise<void>;
};

/**
 * Catalog-era aptitudes tab — tiles from aptitude-catalog, commander-scope allocate.
 * Leftover Confirm lives in the shell footer (GG-63); this tab reports draft state upward.
 */
export function AptitudesTab({
  data,
  surface,
  onDraftState
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
  onDraftState?: (state: AptitudeDraftState | null) => void;
}) {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? data.playerId ?? 0;
  const aptitudes = useAptitudes(playerId);
  const save = useSaveAptitudes();

  useEffect(() => {
    return () => onDraftState?.(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (aptitudes.isLoading || !aptitudes.data) {
    return (
      <div className="mt-4">
        <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
      </div>
    );
  }

  return (
    <AptitudesBody
      surface={surface}
      budget={aptitudes.data.budget}
      theta={aptitudes.data.theta}
      serverShares={aptitudes.data.shares}
      playerId={playerId}
      save={save}
      onDraftState={onDraftState}
    />
  );
}

function AptitudesBody({
  surface,
  budget,
  theta,
  serverShares,
  playerId,
  save,
  onDraftState
}: {
  surface: ActorSurfaceCatalog;
  budget: number;
  theta: number;
  serverShares: Record<string, number>;
  playerId: number;
  save: ReturnType<typeof useSaveAptitudes>;
  onDraftState?: (state: AptitudeDraftState | null) => void;
}) {
  const seeded = useMemo(() => {
    const next: Record<string, number> = {};
    for (const row of surface.aptitudes) {
      next[row.id] = serverShares[row.id] ?? 0;
    }
    return next;
  }, [surface.aptitudes, serverShares]);

  const allocation = useAllocationDraft({
    serverValues: seeded,
    budget,
    isSaving: save.isPending,
    onSave: (draft) => save.mutateAsync({ playerId, shares: draft })
  });

  const allocationRef = useRef(allocation);
  allocationRef.current = allocation;

  const [selectedId, setSelectedId] = useState<string | null>(surface.aptitudes[0]?.id ?? null);
  const selected: AptitudeCatalogRow | undefined = surface.aptitudes.find((row) => row.id === selectedId);

  useEffect(() => {
    if (allocation.draft === null) {
      onDraftState?.(null);
      return;
    }
    onDraftState?.({
      budget,
      spent: allocation.spent,
      leftover: budget - allocation.spent,
      dirty: allocation.dirty,
      withinBudget: allocation.withinBudget,
      saving: save.isPending,
      revert: () => allocationRef.current.revert(),
      save: () => allocationRef.current.save()
    });
  }, [
    allocation.draft,
    allocation.spent,
    allocation.dirty,
    allocation.withinBudget,
    budget,
    save.isPending,
    onDraftState
  ]);

  if (allocation.draft === null) {
    return (
      <div className="mt-4">
        <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
      </div>
    );
  }

  const postures = [...new Set(surface.aptitudes.map((row) => row.posture))];

  return (
    <div className="mt-4" data-testid="aptitudes-tab">
      {allocation.error ? <Banner tone="error">{allocation.error}</Banner> : null}
      <p className="text-xs text-muted" data-testid="aptitudes-scope-chip">
        Scope: Commander · power {theta}
      </p>

      <InspectSplit
        list={
          <div className="space-y-4">
            {postures.map((posture) => (
              <section key={posture} aria-label={posture}>
                <h3 className="mb-2 text-2xs font-bold uppercase tracking-wide text-muted">{posture}</h3>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
                  {surface.aptitudes
                    .filter((row) => row.posture === posture)
                    .sort((a, b) => a.ordinal - b.ordinal)
                    .map((row) => (
                      <AptitudeTile
                        key={row.id}
                        id={row.id}
                        displayName={row.displayName}
                        value={allocation.draft![row.id] ?? 0}
                        selected={selectedId === row.id}
                        onSelect={() => setSelectedId(row.id)}
                        onInc={() => allocation.setValue(row.id, (allocation.draft![row.id] ?? 0) + 1)}
                        onDec={() => allocation.setValue(row.id, (allocation.draft![row.id] ?? 0) - 1)}
                      />
                    ))}
                </div>
              </section>
            ))}
          </div>
        }
        inspector={
          selected ? (
            <>
              <h3 className="font-display text-base text-text">{selected.displayName}</h3>
              <p className="text-sm text-muted">{selected.reading}</p>
              <p className="text-xs text-muted">{selected.role}</p>
            </>
          ) : (
            <p className="text-xs italic text-muted">Select an aptitude.</p>
          )
        }
      />
    </div>
  );
}
