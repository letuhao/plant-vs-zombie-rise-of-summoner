import { useEffect, useMemo, useReducer, useState } from "react";
import { StageHost, useStageMountGuard } from "@/shell/stageHost";
import { claimStageEscape, handleEscape } from "@/shell/keymap";
import {
  initialWorldUi,
  orderId,
  reachableFromLegion,
  routeForLegion,
  worldUiReducer,
  type PendingOrder
} from "@/stages/world/worldSelection";
import { toGraph, summarizeLoam } from "@/stages/world/worldViewModel";
import { sectorLabel } from "@/stages/world/labels";
import { usePlayers } from "@/lib/bus";
import { useWorldHeader, useWorldState } from "@/lib/bus/world";
import { adaptWorldState, adaptWorldLegion } from "@/contract/adapt";
import { pendingWithReason } from "@/contract/pending";
import firstLight from "@/stages/world/fixtures/first-light.json";
import { WorldGameHost } from "@/stages/world/host/WorldGameHost";
import { useWorldVerbs, type WorldVerb } from "@/stages/world/turn/worldVerbs";
import { SectorInspector } from "./inspector/SectorInspector";
import { CEDE_ORDER_AVAILABLE } from "./inspector/cedeCapability";
import { QueuedOrders } from "./targeting/QueuedOrders";
import { WorldHud } from "./hud/WorldHud";
import { TopStrip } from "./hud/TopStrip";
import { TurnCluster } from "./turn/TurnCluster";
import { UnresolvedCount } from "./turn/UnresolvedCount";
import { PlaybackPanel } from "./playback/PlaybackPanel";
import { worldBusEmit, type WorldIgnoreRect, type WorldSelectPayload } from "@/game/EventBus";
import { LensPicker } from "./lenses/LensPicker";
import { initialLensState, lensReducer } from "./lenses/lensState";
import { useLensData } from "./lenses/useLensData";

/**
 * World stage — Phaser map plane + React HUD/inspector (world-map-runtime).
 * Falls back to first-light fixture when no live world.
 * Does not import SVG WorldScene / camera / cameraGestures (R15).
 */
export function WorldStage() {
  useStageMountGuard("world");

  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const header = useWorldHeader(playerId);
  const worldId = header.data?.worldId ?? null;
  const live = useWorldState(worldId);

  const [lens, dispatchLens] = useReducer(lensReducer, initialLensState);
  const lensData = useLensData(worldId, lens.active);

  const dto = live.data ?? (firstLight as Parameters<typeof adaptWorldState>[0]);
  const fixtureWorld = useMemo(
    () => adaptWorldState(dto, { lifelinesRequested: lens.active === "supply" }),
    [dto, lens.active]
  );
  /** Prefer lens-4 adapted fetch when it has resolved; otherwise fixture/plain adapt. */
  const world = lensData.displayed ?? fixtureWorld;
  const playerFactionId = useMemo(
    () => dto.factions.find((f) => f.kind === "Player")?.factionId ?? null,
    [dto]
  );

  const phaserModel = useMemo(
    () => ({ ...world, playerFactionId }),
    [world, playerFactionId]
  );

  const [overlayEpoch, setOverlayEpoch] = useState(0);
  useEffect(() => {
    setOverlayEpoch((n) => n + 1);
  }, [lens.active, lensData.displayed, lensData.isLensFourLoading]);

  const [ui, dispatch] = useReducer(worldUiReducer, initialWorldUi);

  useEffect(
    () => claimStageEscape("world-stage", () => dispatch({ type: "select-sector", sectorId: null })),
    []
  );

  /** Arrow pan when map owns input. W is not pan (world-stage arbitration). */
  const panVerbs = useMemo((): WorldVerb[] => {
    const step = 48;
    const pan = (dx: number, dy: number) => {
      const gen =
        (typeof window !== "undefined" &&
          (window as unknown as { __fusionRpgWorldGen?: number }).__fusionRpgWorldGen) ||
        0;
      if (!gen) return;
      worldBusEmit("world:camera", { generation: gen, op: "pan", dx, dy });
    };
    return [
      { key: "ArrowLeft", id: "world-pan-left", handler: () => pan(-step, 0) },
      { key: "ArrowRight", id: "world-pan-right", handler: () => pan(step, 0) },
      { key: "ArrowUp", id: "world-pan-up", handler: () => pan(0, -step) },
      { key: "ArrowDown", id: "world-pan-down", handler: () => pan(0, step) }
    ];
  }, []);
  useWorldVerbs(panVerbs);

  const selectedSector = world.sectors.find((s) => s.sectorId === ui.selectedSectorId) ?? null;
  const prospectedSectorIds: string[] = dto.prospectedSectorIds ?? [];

  const graph = useMemo(() => toGraph(dto), [dto]);
  const loamSummary = useMemo(() => summarizeLoam(graph.nodes.map((n) => n.data)), [graph]);
  const myLegions = useMemo(
    () =>
      dto.entities
        .filter((e) => e.kind === "Legion" && e.ownerFactionId === playerFactionId)
        .map(adaptWorldLegion),
    [dto, playerFactionId]
  );
  const myLegionDisplayNames = useMemo(
    () => Object.fromEntries(dto.entities.map((e) => [e.entityId, e.displayName])),
    [dto]
  );
  const selectedLegion = useMemo(
    () => (ui.selectedEntityId ? dto.entities.find((e) => e.entityId === ui.selectedEntityId) ?? null : null),
    [dto, ui.selectedEntityId]
  );
  const reachableSectors = useMemo(() => {
    if (!selectedLegion) return null;
    return Array.from(reachableFromLegion(graph, selectedLegion), ([sectorId, hops]) => ({ sectorId, hops }));
  }, [graph, selectedLegion]);

  const [blockedTarget, setBlockedTarget] = useState<{ sectorId: string; reason: string } | null>(null);
  useEffect(() => setBlockedTarget(null), [ui.selectedEntityId]);

  const ignoreRects = useMemo((): WorldIgnoreRect[] => {
    // Phaser pointer coords are relative to the map canvas (already beside the shell rail).
    // Do not re-subtract the 92px rail — that wrongly ate the left of the map (homeworld).
    const rects: WorldIgnoreRect[] = [];
    if (selectedSector) {
      // DockShell is fixed at left-[92px] w-[380px]; canvas sits under the stage so the dock
      // covers roughly the left 380px of the canvas.
      rects.push({ left: 0, top: 0, width: 380, height: 10000 });
    }
    return rects;
  }, [selectedSector]);

  const targeting = useMemo(
    () =>
      reachableSectors || blockedTarget || ui.pending.length > 0
        ? { reachable: reachableSectors, blocked: blockedTarget, pending: ui.pending }
        : null,
    [reachableSectors, blockedTarget, ui.pending]
  );

  function handleSelectSector(sectorId: string) {
    if (selectedLegion) {
      const path = routeForLegion(graph, selectedLegion, sectorId);
      if (path) {
        const order: PendingOrder = {
          commandId: orderId(dto.currentTurn, "move", selectedLegion.entityId),
          kind: "move",
          entityId: selectedLegion.entityId,
          sectorId,
          lanePath: path,
          label: `March to ${sectorLabel(sectorId)}`
        };
        dispatch({ type: "queue", order });
        setBlockedTarget(null);
      } else {
        setBlockedTarget({ sectorId, reason: "path.empty" });
      }
      return;
    }
    dispatch({ type: "select-sector", sectorId });
  }

  function handleWorldSelect(payload: WorldSelectPayload) {
    if (payload.kind === "empty") {
      handleEscape();
      return;
    }
    if (payload.kind === "sector" && payload.id) {
      handleSelectSector(payload.id);
      return;
    }
    if (payload.kind === "force" && payload.id) {
      dispatch({
        type: "select-entity",
        entityId: ui.selectedEntityId === payload.id ? null : payload.id
      });
      if (ui.selectedSectorId != null) dispatch({ type: "select-sector", sectorId: null });
    }
  }

  return (
    <StageHost>
      <WorldHud
        topStrip={
          <TopStrip
            turn={dto.currentTurn}
            calendar={dto.calendar}
            income={{ unit: "loamUnits", value: loamSummary.production }}
            upkeep={{ unit: "loamUnits", value: loamSummary.upkeep }}
            net={{ unit: "loamUnits", value: loamSummary.net }}
            stock={{ unit: "loamUnits", value: loamSummary.stock }}
            stockCapacity={pendingWithReason("capacity not yet exposed by the server")}
          />
        }
        bottomRight={
          worldId ? (
            <div className="flex flex-col items-end gap-2">
              <UnresolvedCount
                legions={myLegions}
                pending={ui.pending}
                displayNames={myLegionDisplayNames}
                onFocus={(entityId) => dispatch({ type: "select-entity", entityId })}
              />
              <TurnCluster
                worldId={worldId}
                currentTurn={dto.currentTurn}
                commanderId={playerFactionId ?? ""}
                legions={myLegions}
                pending={ui.pending}
                onOrdersFiled={() => dispatch({ type: "clear-queue" })}
              />
            </div>
          ) : null
        }
        bottomLeft={
          <div className="pointer-events-auto flex flex-col items-start gap-2">
            <button
              type="button"
              data-testid="world-map-fit"
              className="rounded border border-border bg-panel px-2 py-1 text-sm text-ink"
              onClick={() => {
                const gen =
                  (typeof window !== "undefined" &&
                    (window as unknown as { __fusionRpgWorldGen?: number }).__fusionRpgWorldGen) ||
                  0;
                if (gen) {
                  worldBusEmit("world:camera", {
                    generation: gen,
                    op: "fit",
                    padLeft: 100,
                    padRight: 40,
                    padTop: 56,
                    padBottom: 80
                  });
                }
              }}
            >
              Fit
            </button>
            <LensPicker
              active={lens.active}
              onSelect={(id) => dispatchLens({ type: "select", id })}
              isLensFourLoading={lensData.isLensFourLoading}
            />
            <QueuedOrders orders={ui.pending} onTakeBack={(commandId) => dispatch({ type: "unqueue", commandId })} />
          </div>
        }
        rightEdge={worldId ? <PlaybackPanel worldId={worldId} turn={dto.currentTurn - 1} /> : null}
      >
        <WorldGameHost
          model={phaserModel}
          overlayEpoch={overlayEpoch}
          selectedSectorId={ui.selectedSectorId}
          ignoreRects={ignoreRects}
          targeting={targeting}
          lens={lens.active}
          onSelect={handleWorldSelect}
        />
      </WorldHud>

      {selectedSector ? (
        <SectorInspector
          open
          onOpenChange={(open) => {
            if (!open) dispatch({ type: "select-sector", sectorId: null });
          }}
          sector={selectedSector}
          slots={world.slotsBySectorId[selectedSector.sectorId] ?? []}
          forces={world.forcesBySectorId[selectedSector.sectorId] ?? []}
          cedeOrderAvailable={CEDE_ORDER_AVAILABLE}
          prospected={prospectedSectorIds.includes(selectedSector.sectorId)}
        />
      ) : null}
    </StageHost>
  );
}
