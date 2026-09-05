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
} from "@/features/world/worldSelection";
import { toGraph, summarizeLoam } from "@/features/world/worldViewModel";
import { sectorLabel } from "@/features/world/labels";
import { usePlayers } from "@/lib/bus";
import { useWorldHeader, useWorldState } from "@/lib/bus/world";
import { adaptWorldState, adaptWorldLegion } from "@/contract/adapt";
import type { SectorView } from "@/contract/types";
import { pendingWithReason } from "@/contract/pending";
import firstLight from "@/features/world/fixtures/first-light.json";
import { fitToExtent, type Extent } from "./camera";
import { WorldScene, GRID_X, GRID_Y } from "./render/WorldScene";
import { SectorInspector } from "./inspector/SectorInspector";
import { CEDE_ORDER_AVAILABLE } from "./inspector/cedeCapability";
import { QueuedOrders } from "./targeting/QueuedOrders";
import { WorldHud } from "./hud/WorldHud";
import { TopStrip } from "./hud/TopStrip";
import { TurnCluster } from "./turn/TurnCluster";
import { UnresolvedCount } from "./turn/UnresolvedCount";
import { PlaybackPanel } from "./playback/PlaybackPanel";

/** Before any real world state has loaded — an empty, centred extent `fitToExtent` still resolves
 * sanely against, so the very first render has a valid `viewBox` rather than a `NaN` one. */
const EMPTY_EXTENT: Extent = { minX: 0, minY: 0, maxX: 0, maxY: 0 };

function extentOf(sectors: readonly SectorView[]): Extent {
  if (sectors.length === 0) return EMPTY_EXTENT;
  const xs = sectors.map((s) => s.layoutX * GRID_X);
  const ys = sectors.map((s) => s.layoutY * GRID_Y);
  return { minX: Math.min(...xs), minY: Math.min(...ys), maxX: Math.max(...xs), maxY: Math.max(...ys) };
}

/**
 * The map stage (world-stage W33/W35). `StageHost` + the GG-11 mount guard + one `<svg>` whose
 * `viewBox` is the camera, now filled by `WorldScene` (the scene-composition wiring closed
 * 2026-09-04 — the gap W50/W57/W65/W71 each named). Falls back to the checked-in `first-light`
 * fixture with no live world, matching the old `#/world` page's own established convention, so the
 * stage is still worth opening against a server with nothing in it yet.
 *
 * **Never imports a `*Dto` type** (`contractGuard.ts` bans it for every `stages/` file) —
 * `adaptWorldState` (`contract/adapt.ts`) is the one place the raw wire shape is touched; this
 * module and `WorldScene` only ever see `SectorView`/`LaneView`/`SlotView`.
 *
 * The stage claims the dismissal gestures the layers above it will depend on (W35): it is a real
 * entry on the escape stack for its whole mounted lifetime, so `Esc` reaches it and dispatches
 * `select-sector: null` whenever nothing else is open — closing the live dead end where a selected
 * sector could never be deselected at all. Right-click on the map pane calls the exact same
 * `handleEscape()` the global `Esc` key already does — one gesture set, no second path, no
 * exceptions (§4.4) — so a band-2 layer open above the stage takes right-click too, never the
 * stage's own selection.
 */
export function WorldStage() {
  useStageMountGuard("world");

  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const header = useWorldHeader(playerId);
  const worldId = header.data?.worldId ?? null;
  const live = useWorldState(worldId);

  const dto = live.data ?? (firstLight as Parameters<typeof adaptWorldState>[0]);
  const world = useMemo(() => adaptWorldState(dto), [dto]);
  const playerFactionId = useMemo(
    () => dto.factions.find((f) => f.kind === "Player")?.factionId ?? null,
    [dto]
  );

  const camera = useMemo(() => fitToExtent(extentOf(world.sectors), 1280, 720), [world.sectors]);
  const [ui, dispatch] = useReducer(worldUiReducer, initialWorldUi);

  useEffect(
    () => claimStageEscape("world-stage", () => dispatch({ type: "select-sector", sectorId: null })),
    []
  );

  const selectedSector = world.sectors.find((s) => s.sectorId === ui.selectedSectorId) ?? null;
  const prospectedSectorIds: string[] = dto.prospectedSectorIds ?? [];

  /**
   * The targeting wiring (world-stage W71). `toGraph`/`dto.entities` touch the raw wire shape
   * directly — the same already-sanctioned exception `dto.factions.find(...)` above already takes,
   * for exactly the same reason: nothing has adapted a legion's route-relevant fields (`atSectorId`/
   * `onLaneId`/`onLaneTowardSectorId`) into the view contract yet, and `routeForLegion` needs them
   * as they actually are on the wire, not summarised. `WorldScene` itself never sees any of this —
   * only the already-adapted `AdaptedWorldState` and the plain sector-id/hop-count pairs below.
   */
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

  /** The one sector a click was just refused against, and why — cleared the moment the targeted
   * legion itself changes (a different legion, or none), so a stale refusal never survives past the
   * selection it was about. */
  const [blockedTarget, setBlockedTarget] = useState<{ sectorId: string; reason: string } | null>(null);
  useEffect(() => setBlockedTarget(null), [ui.selectedEntityId]);

  /**
   * A sector click means two different things depending on whether a legion is under targeting:
   * with nothing selected it is the plain W57/W65 sector-select gesture, unchanged; with a legion
   * selected it is a march decision — queue the order if a route exists, or show why not,
   * **never** both, and never falling through to also opening the inspector (which would cover the
   * very sectors targeting mode needs clickable — the real overlap W65's own notes already found).
   */
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

  /** A force's own marker toggles selection the same way W65 already made sector re-selection work
   * — clicking the same one again clears it, clicking a different one simply switches. Closing
   * whatever sector inspector was open keeps the two selection modes from fighting over the map. */
  function handleSelectEntity(entityId: string) {
    dispatch({ type: "select-entity", entityId: ui.selectedEntityId === entityId ? null : entityId });
    if (ui.selectedSectorId != null) dispatch({ type: "select-sector", sectorId: null });
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
          <QueuedOrders orders={ui.pending} onTakeBack={(commandId) => dispatch({ type: "unqueue", commandId })} />
        }
        rightEdge={worldId ? <PlaybackPanel worldId={worldId} turn={dto.currentTurn - 1} /> : null}
      >
        <svg
          data-testid="world-stage-svg"
          data-selected-sector={ui.selectedSectorId ?? ""}
          viewBox={`${camera.x} ${camera.y} ${camera.w} ${camera.h}`}
          className="h-full w-full"
          role="img"
          aria-label="World map"
          onContextMenu={(event) => {
            event.preventDefault();
            handleEscape();
          }}
        >
          <WorldScene
            world={world}
            playerFactionId={playerFactionId}
            selectedSectorId={ui.selectedSectorId}
            onSelectSector={handleSelectSector}
            zoom="map"
            selectedEntityId={ui.selectedEntityId}
            onSelectEntity={handleSelectEntity}
            reachableSectors={reachableSectors}
            pendingOrders={ui.pending}
            blockedTarget={blockedTarget}
          />
        </svg>
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
