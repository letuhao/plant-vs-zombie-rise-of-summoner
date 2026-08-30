import { useEffect, useState } from "react";
import { useRelics, useUniqueActors, useUniqueEquipment, usePutUniqueEquipment } from "@/lib/bus";
import { adaptRelic } from "@/contract/adapt";

type RelicDto = Parameters<typeof adaptRelic>[0];
import { PanelShell } from "@/shell/PanelShell";
import { EmptyState } from "@/ui/EmptyState";
import { Banner, Button, Select } from "@/ui";

type Tab = "held" | "equipped" | "storage";

function RelicRow({
  relic,
  equippedSlotLabel,
  selected,
  onSelect
}: {
  relic: RelicDto;
  equippedSlotLabel?: string;
  selected: boolean;
  onSelect: () => void;
}) {
  const view = adaptRelic(relic);
  return (
    <button
      type="button"
      data-testid={`relics-row-${relic.id}`}
      data-selected={selected}
      aria-current={selected}
      onClick={onSelect}
      className="flex w-full items-center gap-3 border-b border-border px-3 py-2 text-left last:border-b-0 focus-visible:bg-panel-raised aria-current:bg-panel-raised"
    >
      <span
        className="inline-block h-3 w-3 shrink-0 rounded-full"
        style={{ background: view.header.rarity.colour }}
        aria-hidden="true"
      />
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold text-text">{view.header.name}</span>
        <span className="block text-xs text-muted">
          {view.header.baseTypeAndClassNoun} · {view.header.rarity.display}
          {equippedSlotLabel ? <b className="ml-1 text-text">· equipped</b> : null}
        </span>
      </span>
    </button>
  );
}

/**
 * T14 — held/equipped comparison over the real, small, seeded relic catalog
 * (`RelicCatalog.cs`). No acquisition system exists yet, so every player
 * holds the full catalog; equipping persists through the existing per-actor
 * `rpg_unique_equipment` pipeline. "Storage" (plate 02 §B's third tab) has no
 * server concept yet — shown as an honest pending state, not faked.
 */
export function RelicsLayer({
  open,
  onOpenChange,
  playerId
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  playerId: number;
}) {
  const [tab, setTab] = useState<Tab>("held");
  const [actorId, setActorId] = useState<string>("");
  const [candidateId, setCandidateId] = useState<string | null>(null);

  const actorsQuery = useUniqueActors(playerId);
  const actors = actorsQuery.data?.items ?? [];
  useEffect(() => {
    if (!actorId && actors.length > 0) setActorId(actors[0]!.instanceId);
  }, [actorId, actors]);

  const relicsQuery = useRelics();
  const relics = relicsQuery.data?.items ?? [];
  const equipQuery = useUniqueEquipment(actorId || null);
  const equippedSlots = equipQuery.data?.items ?? [];
  const equipMutation = usePutUniqueEquipment();

  const equippedByRelicId = new Map(equippedSlots.filter((s) => s.itemId).map((s) => [s.itemId, s.slot]));
  const candidate = candidateId ? relics.find((r) => r.id === candidateId) ?? null : null;
  const currentInCandidateSlot = candidate
    ? relics.find((r) => equippedByRelicId.get(r.id) === candidate.slot) ?? null
    : null;

  const selectedActor = actors.find((a) => a.instanceId === actorId);

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title="Relics"
      subtitle={
        selectedActor
          ? `Equipping to #${selectedActor.instanceId.slice(0, 6)} · ${equippedSlots.length} of 3 slots used`
          : "Held"
      }
      testId="relics-layer"
      footer={
        <div className="flex w-full flex-wrap items-center gap-2" data-testid="relics-tabs">
          <button
            type="button"
            data-testid="relics-tab-held"
            aria-current={tab === "held"}
            onClick={() => setTab("held")}
            className={`rounded-sm border px-2 py-1 text-xs ${tab === "held" ? "border-lawn-hot bg-lawn text-text" : "border-border text-muted hover:bg-panel"}`}
          >
            Held
          </button>
          <button
            type="button"
            data-testid="relics-tab-equipped"
            aria-current={tab === "equipped"}
            onClick={() => setTab("equipped")}
            className={`rounded-sm border px-2 py-1 text-xs ${tab === "equipped" ? "border-lawn-hot bg-lawn text-text" : "border-border text-muted hover:bg-panel"}`}
          >
            Equipped
          </button>
          <button
            type="button"
            data-testid="relics-tab-storage"
            aria-current={tab === "storage"}
            onClick={() => setTab("storage")}
            className={`rounded-sm border px-2 py-1 text-xs ${tab === "storage" ? "border-lawn-hot bg-lawn text-text" : "border-border text-muted hover:bg-panel"}`}
          >
            Storage
          </button>
        </div>
      }
    >
      {actorsQuery.isLoading || relicsQuery.isLoading ? (
        <p className="text-sm text-muted" data-testid="relics-loading" aria-busy="true">
          Loading relics…
        </p>
      ) : actorsQuery.isError || relicsQuery.isError ? (
        <Banner tone="error" data-testid="relics-error">
          Couldn't load relics.
          <Button
            size="sm"
            variant="ghost"
            className="ml-2"
            onClick={() => {
              void actorsQuery.refetch();
              void relicsQuery.refetch();
            }}
          >
            Retry
          </Button>
        </Banner>
      ) : actors.length === 0 ? (
        <EmptyState title="No creatures bound yet" hint="Bind a creature to equip relics to it." />
      ) : (
        <div className="flex flex-col gap-4">
          <label className="flex items-center gap-2 text-sm text-muted">
            Equipping to
            <Select
              data-testid="relics-actor-select"
              value={actorId}
              onChange={(e) => {
                setActorId(e.target.value);
                setCandidateId(null);
              }}
            >
              {actors.map((a) => (
                <option key={a.instanceId} value={a.instanceId}>
                  {a.side} · Lv {a.level} · {a.instanceId}
                </option>
              ))}
            </Select>
          </label>

          {tab === "held" ? (
            // Stacked rather than the plate's side-by-side columns (plate 02 §B assumes a
            // 1000px-wide panel; PanelShell caps every layer at 640px) — two columns inside
            // that cap left the comparison too narrow to read comfortably at any width.
            <div className="flex flex-col gap-4">
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-muted">
                  Held — pick one to see what it changes
                </p>
                <div className="rounded-md border border-border" data-testid="relics-list">
                  {relics.map((relic) => (
                    <RelicRow
                      key={relic.id}
                      relic={relic}
                      equippedSlotLabel={equippedByRelicId.get(relic.id)}
                      selected={relic.id === candidateId}
                      onSelect={() => setCandidateId(relic.id === candidateId ? null : relic.id)}
                    />
                  ))}
                </div>
              </div>

              <div>
                {candidate ? (
                  <div data-testid="relics-compare">
                    <p className="mb-2 text-xs font-bold uppercase tracking-wide text-muted">
                      {currentInCandidateSlot?.id === candidate.id
                        ? `${candidate.name} is already equipped`
                        : currentInCandidateSlot
                          ? `Swapping ${currentInCandidateSlot.name} → ${candidate.name}`
                          : `Equipping ${candidate.name} (nothing in that slot yet)`}
                    </p>
                    <div className="flex flex-col gap-2 rounded-md border border-border p-3">
                      <p className="text-sm text-text">{adaptRelic(candidate).flavour}</p>
                      <p className="text-xs text-muted" data-testid="relics-compare-implicit-pending">
                        {(() => {
                          const implicit = adaptRelic(candidate).implicit;
                          return implicit.state === "pending" ? implicit.reason : null;
                        })()}
                      </p>
                    </div>
                    {currentInCandidateSlot?.id !== candidate.id ? (
                      <div className="mt-4 flex gap-2">
                        <Button
                          data-testid="relics-equip-btn"
                          disabled={equipMutation.isPending}
                          title={equipMutation.isPending ? "Equipping…" : undefined}
                          onClick={() =>
                            equipMutation.mutate({ instanceId: actorId, slot: candidate.slot, itemId: candidate.id })
                          }
                        >
                          Equip
                        </Button>
                      </div>
                    ) : null}
                  </div>
                ) : (
                  <EmptyState title="Pick a held relic" hint="Its comparison appears here before you equip it." />
                )}
              </div>
            </div>
          ) : null}

          {tab === "equipped" ? (
            <div className="rounded-md border border-border" data-testid="relics-equipped-list">
              {equippedSlots.filter((s) => s.itemId).length === 0 ? (
                <EmptyState title="Nothing equipped" hint="Equip a held relic from the Held tab." />
              ) : (
                equippedSlots
                  .filter((s) => s.itemId)
                  .map((s) => {
                    const relic = relics.find((r) => r.id === s.itemId);
                    return (
                      <div key={s.slot} className="flex items-center justify-between border-b border-border px-3 py-2 last:border-b-0">
                        <span className="text-sm text-text">{relic ? relic.name : s.itemId}</span>
                        <span className="text-xs text-muted">{s.slot}</span>
                      </div>
                    );
                  })
              )}
            </div>
          ) : null}

          {tab === "storage" ? (
            <EmptyState
              title="Storage isn't tracked yet"
              hint="Held and stored relics aren't split yet — everything you hold shows on the Held tab."
            />
          ) : null}
        </div>
      )}
    </PanelShell>
  );
}
