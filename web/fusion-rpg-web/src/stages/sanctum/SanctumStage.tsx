import { useEffect, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { usePlayers, useRuns, useSoulBalance, useUniqueActors } from "@/lib/bus";
import { useContracts } from "@/lib/bus/contracts";
import { adaptActor } from "@/contract/adapt";
import { pendingWithReason } from "@/contract/pending";
import { registerGlobalVerb } from "@/shell/keymap";
import { PanelShell } from "@/shell/PanelShell";
import { StageHost, useStageMountGuard } from "@/shell/stageHost";
import { Rail } from "@/shell/Rail";
import { deriveRailEntries, type RailEntry, type RailUnlockInputs } from "@/shell/railState";
import { CreaturesLayer } from "@/layers/creatures/CreaturesLayer";
import type { ActorRungState } from "@/ui/actor";
import { FocusCard } from "./FocusCard";
import { SanctumHud } from "./SanctumHud";

const LAYER_TITLES: Record<Exclude<RailEntry["id"], "sanctum" | "creatures">, string> = {
  relics: "Relics",
  fusion: "Fusion",
  pacts: "Pacts",
  expeditions: "Expeditions",
  almanac: "Almanac",
  chronicle: "Chronicle"
};

const LAYER_KEYS: Record<Exclude<RailEntry["id"], "sanctum">, string> = {
  creatures: "c",
  relics: "r",
  fusion: "f",
  pacts: "p",
  expeditions: "e",
  almanac: "a",
  chronicle: "h"
};

/**
 * The home stage (band 0) — information-architecture.md §2.1. Everything a
 * session with no run in progress lives on, and where every run returns.
 * Open layers live in the URL (`?panel=&sel=`, GG-8) so a deep link restores
 * the stage first, then the layer over it, cold.
 */
export function SanctumStage() {
  useStageMountGuard("sanctum");
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 1;

  const actorsQuery = useUniqueActors(playerId);
  const runsQuery = useRuns();
  const contractsQuery = useContracts(playerId);
  const soulsQuery = useSoulBalance(playerId);

  const [searchParams, setSearchParams] = useSearchParams();
  const openLayer = searchParams.get("panel") as Exclude<RailEntry["id"], "sanctum"> | null;
  const selectedId = searchParams.get("sel");

  function openLayerById(id: Exclude<RailEntry["id"], "sanctum">) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("panel", id);
      return next;
    });
  }
  function closeLayer() {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("panel");
      next.delete("sel");
      return next;
    });
  }
  function selectCreature(instanceId: string | null) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (instanceId) next.set("sel", instanceId);
      else next.delete("sel");
      return next;
    });
  }

  const actors = actorsQuery.data?.items ?? [];
  const hasDuplicateSpecies = useMemo(() => {
    const seen = new Set<number>();
    for (const actor of actors) {
      if (seen.has(actor.typeId)) return true;
      seen.add(actor.typeId);
    }
    return false;
  }, [actors]);

  const railInputs: RailUnlockInputs = {
    currentStageId: "sanctum",
    hasCompletedARun: (runsQuery.data?.length ?? 0) > 0,
    hasDuplicateSpecies,
    hasAnyContract: (contractsQuery.data?.contracts.length ?? 0) > 0,
    hasHeldASector: false, // not derivable without World (T16, excluded this phase)
    unreadResultCount: 0 // no "unread results" concept exists server-side yet
  };
  const railEntries = deriveRailEntries(railInputs);

  // One global verb per unlocked layer (information-architecture.md §5); pressing an open
  // layer's key closes it. Re-registers if the unlock set changes (e.g. a layer just unlocked).
  useEffect(() => {
    const unregisters = railEntries
      .filter((e): e is RailEntry & { id: Exclude<RailEntry["id"], "sanctum"> } => e.id !== "sanctum" && e.state !== "locked")
      .map((entry) =>
        registerGlobalVerb(LAYER_KEYS[entry.id], `sanctum-rail-${entry.id}`, () => {
          if (openLayer === entry.id) closeLayer();
          else openLayerById(entry.id);
        })
      );
    return () => unregisters.forEach((fn) => fn());
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [railEntries.map((e) => e.state).join(","), openLayer]);

  const firstActorState: ActorRungState | null =
    actors.length > 0 ? { kind: "ready", data: adaptActor(actors[0]!) } : null;

  return (
    <StageHost>
      <SanctumHud
        playerName={players.data?.items.find((p) => p.id === playerId)?.name ?? "Summoner"}
        soulsBalance={soulsQuery.data?.balance ?? 0}
        summonerLevel={pendingWithReason(
          "The summoner-led progression loop is the product direction, not what ships today (AGENTS.md)"
        )}
        unreadResultCount={railInputs.unreadResultCount}
      />
      <Rail entries={railEntries} onSelect={(id) => id !== "sanctum" && openLayerById(id)} />

      <div className="p-5" data-testid="sanctum-body">
        <FocusCard
          actorCount={actors.length}
          firstActor={firstActorState}
          onOpenCreatures={() => openLayerById("creatures")}
        />
      </div>

      <CreaturesLayer
        open={openLayer === "creatures"}
        onOpenChange={(open) => !open && closeLayer()}
        playerId={playerId}
        selectedId={selectedId}
        onSelect={selectCreature}
      />

      <PanelShell
        open={openLayer !== null && openLayer !== "creatures"}
        onOpenChange={(open) => !open && closeLayer()}
        title={openLayer && openLayer !== "creatures" ? LAYER_TITLES[openLayer] : ""}
        subtitle="Arrives in a later pass of this refactor"
        testId="sanctum-layer-placeholder"
      >
        <p className="text-sm text-muted">
          {openLayer && openLayer !== "creatures" ? LAYER_TITLES[openLayer] : ""} is unlocked and
          reachable from the rail — its own designed layer lands in a later task. This placeholder
          exists so the rail's reachability is real today rather than a dead button.
        </p>
      </PanelShell>
    </StageHost>
  );
}
