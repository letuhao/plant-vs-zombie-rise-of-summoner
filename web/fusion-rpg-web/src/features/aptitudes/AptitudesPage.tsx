import { useEffect, useState } from "react";
import { useAptitudes, usePlayers, useSaveAptitudes } from "@/lib/bus";
import { Page } from "@/layouts/Page";
import { Banner, Button, EmptyState, Field, NumberInput, Panel, StatBar } from "@/ui";

/**
 * spec-aptitude-allocation-surface.md — the first player-reachable way to spend aptitude points.
 * Commander scope only (applies to every demon fielded); the twelve ids come straight off the
 * server's own response, never a separately-hardcoded catalog mirror. Free respec today (POST a
 * different body any time) — pricing it is a named follow-up, not built here.
 */
export function AptitudesPage() {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const aptitudes = useAptitudes(playerId);
  const save = useSaveAptitudes();

  const [draft, setDraft] = useState<Record<string, number> | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Re-seed the draft whenever the server state changes (initial load, or after a successful save
  // elsewhere) -- never while the player is actively editing an unsaved draft.
  useEffect(() => {
    if (aptitudes.data && draft === null) setDraft(aptitudes.data.shares);
  }, [aptitudes.data, draft]);

  if (aptitudes.isLoading || !aptitudes.data || draft === null) {
    return (
      <Page title="Primary stats" testId="aptitudes-page">
        <EmptyState title="Loading aptitudes…" testId="aptitudes-loading" />
      </Page>
    );
  }

  const spent = Object.values(draft).reduce((sum, v) => sum + (Number.isFinite(v) ? v : 0), 0);
  const budget = aptitudes.data.budget;
  const withinBudget = spent <= budget;
  const dirty = JSON.stringify(draft) !== JSON.stringify(aptitudes.data.shares);

  async function onSave() {
    setError(null);
    try {
      await save.mutateAsync({ playerId, shares: draft! });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    }
  }

  return (
    <Page
      title="Primary stats"
      description="Spend commander points across the twelve aptitudes. Applies to every demon you field."
      testId="aptitudes-page"
    >
      {error && <Banner tone="error">{error}</Banner>}

      <Panel title="Budget" testId="aptitudes-budget">
        <StatBar label={`${spent} / ${budget} spent (Θ=${aptitudes.data.theta})`} value={spent} max={Math.max(budget, 1)} />
      </Panel>

      <Panel title="Aptitudes" testId="aptitudes-grid">
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
      </Panel>

      <Button
        onClick={onSave}
        disabled={!dirty || !withinBudget || save.isPending}
        title={!withinBudget ? `Over budget by ${spent - budget}` : !dirty ? "No changes to save" : undefined}
        data-testid="aptitudes-save"
      >
        {save.isPending ? "Saving…" : "Save allocation"}
      </Button>
    </Page>
  );
}
