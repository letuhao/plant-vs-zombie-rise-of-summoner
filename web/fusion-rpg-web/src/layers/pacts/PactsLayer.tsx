import { useDemonRoster, usePlayers, useSpeciesIndex } from "@/lib/bus";
import { newCorrelationId } from "@/lib/bus/demons";
import { usePatron, useSetPatron } from "@/lib/bus/patron";
import { useBuyContractSlot, useContracts, usePerformRitual, useReleaseContract } from "@/lib/bus/contracts";
import {
  capacityLabel,
  conditionOf,
  fieldingBlockReason,
  loyaltyFraction,
  rankLabel
} from "@/features/demons/contractView";
import { displayName } from "@/features/demons/rosterSplit";
import { auraLabel, auraPreviewMilli } from "@/features/demons/patronView";
import { PanelShell } from "@/shell/PanelShell";
import { EmptyState } from "@/ui/EmptyState";
import { Badge, Button } from "@/ui";

/**
 * T17 — the demon contracts layer (plate 03 §D, spec-demon-contracts.md): loyalty, tribute,
 * Ritual/Release, each carrying its reason inline. The underlying mechanics (`contractView.ts`,
 * the bind/release/ritual/patron mutations) are the same real ones `DemonsPage.tsx` already
 * uses — this is a dedicated, focused view over them, not a new contract system. The plate's
 * "price" line on a content pact's aura doesn't exist server-side (`patronView.ts`'s aura is a
 * pure benefit, no downside term) — shown honestly as a benefit only, not invented.
 */
export function PactsLayer({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const roster = useDemonRoster(playerId);
  const speciesById = useSpeciesIndex();
  const contracts = useContracts(playerId);
  const patron = usePatron(playerId);
  const setPatron = useSetPatron();
  const releaseContract = useReleaseContract();
  const performRitual = usePerformRitual();
  const buySlot = useBuyContractSlot();

  const rows = contracts.data?.contracts.filter((c) => c.bound) ?? [];
  const bySpecimenId = new Map((roster.data?.items ?? []).map((s) => [s.profile.instanceId, s]));

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title="Pacts"
      subtitle={contracts.data ? capacityLabel(contracts.data) : undefined}
      testId="pacts-layer"
      footer={
        contracts.data ? (
          <Button
            size="sm"
            variant="ghost"
            disabled={!contracts.data.capacity.canBuy}
            title={
              contracts.data.capacity.canBuy
                ? `Buy slot ${contracts.data.capacity.total + 1}`
                : "Every slot has been bought"
            }
            onClick={() => void buySlot.mutateAsync({ playerId, correlationId: newCorrelationId() })}
            data-testid="pacts-buy-slot"
          >
            Buy slot
          </Button>
        ) : undefined
      }
    >
      {rows.length === 0 ? (
        <EmptyState title="No pacts yet" hint="Bind a demon's contract from the Demons roster to see it here." />
      ) : (
        <div className="flex flex-col gap-3">
          {rows.map((c) => {
            const specimen = bySpecimenId.get(c.instanceId);
            const species = specimen ? speciesById.get(specimen.profile.speciesId) : undefined;
            const condition = conditionOf(c);
            const isPatron = patron.data?.patron?.instanceId === c.instanceId;
            return (
              <div
                key={c.instanceId}
                className={`rounded-md border p-3 ${condition === "insubordinate" ? "border-bad" : "border-border"}`}
                data-testid={`pact-${c.instanceId}`}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-semibold text-text">
                    {specimen ? displayName(specimen, species?.name) : c.instanceId}
                  </span>
                  <Badge className={condition === "insubordinate" ? "" : undefined}>
                    {condition === "insubordinate" ? "tribute overdue" : "content"}
                  </Badge>
                </div>
                <div className="mt-2 text-xs text-muted">
                  {rankLabel(c.rank)} · {c.personality} · {c.upkeepPerDay} souls/day
                </div>
                <div className="mt-2 h-1.5 w-full rounded-pill bg-panel" data-testid={`pact-loyalty-${c.instanceId}`}>
                  <div
                    className={`h-full rounded-pill ${condition === "insubordinate" ? "bg-bad-solid" : "bg-lawn"}`}
                    style={{ width: `${Math.round(loyaltyFraction(c.loyalty) * 100)}%` }}
                  />
                </div>
                <p className="mt-1 text-xs text-muted">Loyalty {c.loyalty} / 1000</p>

                {isPatron && specimen ? (
                  <p className="mt-2 text-xs text-text" data-testid={`pact-aura-${c.instanceId}`}>
                    Patron —{" "}
                    {auraLabel(
                      specimen.profile.elementPrimary,
                      auraPreviewMilli(specimen.profile.rarity, specimen.profile.star, Number(specimen.actor.level)),
                      Math.floor(
                        auraPreviewMilli(specimen.profile.rarity, specimen.profile.star, Number(specimen.actor.level)) / 2
                      )
                    )}
                  </p>
                ) : null}

                <div className="mt-3 flex items-center gap-2">
                  {condition === "insubordinate" ? (
                    <>
                      <Button size="sm" disabled title="Renegotiate" data-testid={`pact-renegotiate-${c.instanceId}`}>
                        Renegotiate
                      </Button>
                      <span className="text-xs text-muted" data-testid={`pact-renegotiate-reason-${c.instanceId}`}>
                        — {fieldingBlockReason(c)}
                      </span>
                      <span className="flex-1" />
                      <Button
                        size="sm"
                        onClick={() =>
                          void performRitual.mutateAsync({ playerId, instanceId: c.instanceId, correlationId: newCorrelationId() })
                        }
                        data-testid={`pact-ritual-${c.instanceId}`}
                      >
                        Ritual
                      </Button>
                    </>
                  ) : (
                    <>
                      {!isPatron ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          title={patron.data?.patron ? `Switch costs ${patron.data.switchCostSouls} Souls` : "First patron is free"}
                          onClick={() =>
                            void setPatron.mutateAsync({ playerId, instanceId: c.instanceId, correlationId: newCorrelationId() })
                          }
                          data-testid={`pact-make-patron-${c.instanceId}`}
                        >
                          Make patron
                        </Button>
                      ) : null}
                      <span className="flex-1" />
                      <Button
                        size="sm"
                        variant="ghost"
                        title="Frees the slot; the demon keeps its loyalty"
                        onClick={() => void releaseContract.mutateAsync({ playerId, instanceId: c.instanceId })}
                        data-testid={`pact-release-${c.instanceId}`}
                      >
                        Release
                      </Button>
                    </>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </PanelShell>
  );
}
