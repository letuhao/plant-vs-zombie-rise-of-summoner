import { useEffect, useState } from "react";
import { useAptitudes, usePlayers, useSaveAptitudes } from "@/lib/bus";
import type { ActorView } from "@/contract/types";
import { Banner, Button, EmptyState, Field, NumberInput, StatBar } from "@/ui";
import { PendingNote } from "./shared";

/**
 * actor-sheet program, progression-tab — level/XP (typed on ActorView, never rendered anywhere until
 * now) plus primary-stat distribution. The aptitude half is AptitudesPage.tsx's own draft/dirty/
 * budget/save logic, copied verbatim rather than reimplemented with different edge cases — two
 * allocation UIs with subtly different bugs would be worse than one component used from two places.
 * If this drifts, the fix is extracting a shared useAptitudeAllocation() hook, not maintaining two
 * copies by hand.
 */
export function ProgressionTab({ data }: { data: ActorView }) {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const aptitudes = useAptitudes(playerId);
  const save = useSaveAptitudes();

  const [draft, setDraft] = useState<Record<string, number> | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (aptitudes.data && draft === null) setDraft(aptitudes.data.shares);
  }, [aptitudes.data, draft]);

  return (
    <div className="mt-4" data-testid="progression-tab">
      <section data-testid="progression-level">
        <p className="text-2xs font-bold uppercase tracking-wide text-muted">Level &amp; XP</p>
        <p className="font-display text-lg text-text">Level {data.level}</p>
        {data.xpToNext.state === "known" ? (
          <StatBar label={`${data.xp} / ${data.xp + data.xpToNext.value} xp`} value={data.xp} max={data.xp + data.xpToNext.value} />
        ) : (
          <PendingNote pending={data.xpToNext} testId="progression-xp-pending" />
        )}
        <p className="text-xs text-muted" data-testid="progression-xp-raw">
          {data.xp} xp
        </p>
      </section>

      <section className="mt-4" data-testid="progression-aptitudes">
        <p className="text-2xs font-bold uppercase tracking-wide text-muted">Primary stats — commander scope</p>
        {aptitudes.isLoading || !aptitudes.data || draft === null ? (
          <EmptyState title="Loading aptitudes…" testId="progression-aptitudes-loading" />
        ) : (
          <ProgressionAptitudes
            playerId={playerId}
            draft={draft}
            setDraft={setDraft}
            budget={aptitudes.data.budget}
            theta={aptitudes.data.theta}
            serverShares={aptitudes.data.shares}
            save={save}
            error={error}
            setError={setError}
          />
        )}
      </section>
    </div>
  );
}

function ProgressionAptitudes({
  playerId,
  draft,
  setDraft,
  budget,
  theta,
  serverShares,
  save,
  error,
  setError
}: {
  playerId: number;
  draft: Record<string, number>;
  setDraft: (updater: (d: Record<string, number> | null) => Record<string, number>) => void;
  budget: number;
  theta: number;
  serverShares: Record<string, number>;
  save: ReturnType<typeof useSaveAptitudes>;
  error: string | null;
  setError: (e: string | null) => void;
}) {
  const spent = Object.values(draft).reduce((sum, v) => sum + (Number.isFinite(v) ? v : 0), 0);
  const withinBudget = spent <= budget;
  const dirty = JSON.stringify(draft) !== JSON.stringify(serverShares);

  async function onSave() {
    setError(null);
    try {
      await save.mutateAsync({ playerId, shares: draft });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    }
  }

  return (
    <>
      {error && <Banner tone="error">{error}</Banner>}
      <p className="text-xs text-muted">
        {spent} / {budget} spent (Θ={theta})
      </p>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {Object.entries(draft).map(([id, value]) => (
          <Field key={id} label={id}>
            <NumberInput
              min={0}
              value={value}
              data-testid={`aptitude-input-${id}`}
              onChange={(next) => setDraft((d) => ({ ...(d ?? {}), [id]: Math.max(0, Math.trunc(next)) }))}
            />
          </Field>
        ))}
      </div>
      <Button
        className="mt-3"
        onClick={onSave}
        disabled={!dirty || !withinBudget || save.isPending}
        title={!withinBudget ? `Over budget by ${spent - budget}` : !dirty ? "No changes to save" : undefined}
        data-testid="aptitudes-save"
      >
        {save.isPending ? "Saving…" : "Save allocation"}
      </Button>
      <p className="mt-2 text-xs italic text-muted">
        Only the commander scope is wired today — this is the same allocation the standalone Primary
        Stats layer already saves.
      </p>
    </>
  );
}
