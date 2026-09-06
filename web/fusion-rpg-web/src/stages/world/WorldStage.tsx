import { useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { StageHost, useStageMountGuard } from "@/shell/stageHost";
import { claimStageEscape, handleEscape } from "@/shell/keymap";
import { Rail } from "@/shell/Rail";
import { deriveRailEntries, type RailEntry, type RailUnlockInputs } from "@/shell/railState";
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
import { useDemonRoster, usePlayers, useRelics, useRuns } from "@/lib/bus";
import { useContracts } from "@/lib/bus/contracts";
import { useWorldHeader, useWorldState } from "@/lib/bus/world";
import { useExpeditionReturnWatcher } from "@/layers/expeditions/expeditionReturnWatcher";
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
import { NotifyRail } from "./notify/NotifyRail";
import { dismiss, open, type RailItem } from "./notify/notifyRailStore";
import { Outliner } from "./outliner/Outliner";
import { OutlinerFilter } from "./outliner/OutlinerFilter";
import {
  applyOutlinerFilter,
  buildOutlinerGroups,
  type OutlinerFilter as OutlinerFilterId,
  type OutlinerRow
} from "./outliner/outlinerModel";
import { centreTargetForOutlinerRow } from "./outliner/outlinerCentre";
import { worldBusEmit, type WorldIgnoreRect, type WorldSelectPayload } from "@/game/EventBus";
import { LensPicker } from "./lenses/LensPicker";
import { initialLensState, lensReducer } from "./lenses/lensState";
import { useLensData } from "./lenses/useLensData";
import { isWorldMapChromeMuted } from "./mapChromeMute";
import { buildWorldIgnoreRects, fitPadLeft, rectRelativeToCanvas, type MeasuredIgnoreAnchors } from "./worldIgnoreRects";

/**
 * World stage — Phaser map plane + React HUD/inspector (world-map-runtime).
 * Falls back to first-light fixture when no live world.
 * Does not import SVG WorldScene / camera / cameraGestures (R15).
 * Shell Rail mounts like Lawn (gaps D22); chrome order Notify → Outliner → Playback (D24).
 */
export function WorldStage() {
  useStageMountGuard("world");
  const navigate = useNavigate();

  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const header = useWorldHeader(playerId);
  const worldId = header.data?.worldId ?? null;
  const live = useWorldState(worldId);

  // Unlock queries duplicate Sanctum's — react-query dedupes by key (gaps D22 / Lawn pattern).
  const runsQuery = useRuns();
  const contractsQuery = useContracts(playerId);
  const relicsQuery = useRelics();
  const demonRosterQuery = useDemonRoster(playerId);
  const { returnedCount } = useExpeditionReturnWatcher(playerId);
  const railInputs: RailUnlockInputs = {
    currentStageId: "world",
    hasCompletedARun: (runsQuery.data?.length ?? 0) > 0,
    hasAnyDemon: (demonRosterQuery.data?.items.length ?? 0) > 0,
    hasAnyContract: (contractsQuery.data?.contracts.length ?? 0) > 0,
    hasAnyRelic: (relicsQuery.data?.items.length ?? 0) > 0,
    hasAnyBoundDemon: contractsQuery.data?.contracts.some((c) => c.bound) ?? false,
    returnedExpeditionCount: returnedCount,
    unreadResultCount: 0
  };
  const railEntries = deriveRailEntries(railInputs);

  function openLayerOnSanctum(id: Exclude<RailEntry["id"], "sanctum">) {
    navigate(`/sanctum?panel=${id}`);
  }

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

  const [overlayEpoch, setOverlayEpoch] = useState(0);
  useEffect(() => {
    setOverlayEpoch((n) => n + 1);
  }, [lens.active, lensData.displayed, lensData.isLensFourLoading]);

  const [worldGeneration, setWorldGeneration] = useState(0);
  const mapPaneRef = useRef<HTMLDivElement | null>(null);
  const [mapPaneSize, setMapPaneSize] = useState({ w: 1280, h: 720 });
  const [measuredAnchors, setMeasuredAnchors] = useState<MeasuredIgnoreAnchors>({});
  useEffect(() => {
    const el = mapPaneRef.current;
    if (!el) return;
    const sync = () => setMapPaneSize({ w: Math.max(2, el.clientWidth), h: Math.max(2, el.clientHeight) });
    sync();
    const ro = new ResizeObserver(sync);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const [ui, dispatch] = useReducer(worldUiReducer, initialWorldUi);
  const [outlinerFilter, setOutlinerFilter] = useState<OutlinerFilterId>("all");
  const [notifyItems, setNotifyItems] = useState<RailItem[]>([]);

  useEffect(
    () => claimStageEscape("world-stage", () => dispatch({ type: "select-sector", sectorId: null })),
    []
  );

  /** Arrow pan when map owns input. W is not pan (world-stage arbitration). GG-18 mutes under panel. */
  const panVerbs = useMemo((): WorldVerb[] => {
    const step = 48;
    const pan = (dx: number, dy: number) => {
      if (isWorldMapChromeMuted()) return;
      if (!worldGeneration) return;
      worldBusEmit("world:camera", { generation: worldGeneration, op: "pan", dx, dy });
    };
    return [
      { key: "ArrowLeft", id: "world-pan-left", handler: () => pan(-step, 0) },
      { key: "ArrowRight", id: "world-pan-right", handler: () => pan(step, 0) },
      { key: "ArrowUp", id: "world-pan-up", handler: () => pan(0, -step) },
      { key: "ArrowDown", id: "world-pan-down", handler: () => pan(0, step) }
    ];
  }, [worldGeneration]);
  useWorldVerbs(panVerbs);

  const selectedSector = world.sectors.find((s) => s.sectorId === ui.selectedSectorId) ?? null;
  const prospectedSectorIds: string[] = dto.prospectedSectorIds ?? [];

  useEffect(() => {
    const el = mapPaneRef.current;
    if (!el) return;
    const syncMeasured = () => {
      const canvas = el.querySelector("canvas");
      if (!canvas) {
        setMeasuredAnchors({});
        return;
      }
      const canvasRect = canvas.getBoundingClientRect();
      const measure = (testid: string) => {
        const node = document.querySelector(`[data-testid="${testid}"]`);
        if (!node) return undefined;
        return rectRelativeToCanvas(canvasRect, node.getBoundingClientRect()) ?? undefined;
      };
      setMeasuredAnchors({
        top: measure("world-hud-anchor-top-strip"),
        bottomLeft: measure("world-hud-anchor-bottom-left"),
        right: measure("world-hud-right-column"),
        dock: measure("sector-inspector")
      });
    };
    syncMeasured();
    const ro = new ResizeObserver(syncMeasured);
    ro.observe(el);
    return () => ro.disconnect();
  }, [ui.selectedSectorId, mapPaneSize.w, mapPaneSize.h]);

  const graph = useMemo(() => toGraph(dto), [dto]);
  const loamSummary = useMemo(() => summarizeLoam(graph.nodes.map((n) => n.data)), [graph]);
  /** All entities for map markers (gaps D26) — turn cluster still filters to mine. */
  const allLegions = useMemo(() => dto.entities.map(adaptWorldLegion), [dto]);
  const myLegions = useMemo(
    () => allLegions.filter((e) => e.kind === "Legion" && e.ownerFactionId === playerFactionId),
    [allLegions, playerFactionId]
  );
  const mySectors = useMemo(
    () => world.sectors.filter((s) => s.ownerFactionId === playerFactionId),
    [world.sectors, playerFactionId]
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

  const outlinerGroups = useMemo(
    () => applyOutlinerFilter(buildOutlinerGroups(myLegions, mySectors, ui.pending), outlinerFilter),
    [myLegions, mySectors, ui.pending, outlinerFilter]
  );

  const outlinerSelectedId = ui.selectedEntityId ?? ui.selectedSectorId;

  const [blockedTarget, setBlockedTarget] = useState<{ sectorId: string; reason: string } | null>(null);
  useEffect(() => setBlockedTarget(null), [ui.selectedEntityId]);

  const ignoreRects = useMemo((): WorldIgnoreRect[] => {
    // Canvas sits beside the shell rail (flex frame) — do not double-subtract 92px (gaps D22).
    return buildWorldIgnoreRects({
      width: mapPaneSize.w,
      height: mapPaneSize.h,
      dockOpen: selectedSector != null,
      canvasBesideRail: true,
      measured: measuredAnchors
    });
  }, [selectedSector, mapPaneSize, measuredAnchors]);

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

  function handleOutlinerSelect(id: string, kind: OutlinerRow["kind"]) {
    if (kind === "legion") {
      dispatch({ type: "select-entity", entityId: id });
      if (ui.selectedSectorId != null) dispatch({ type: "select-sector", sectorId: null });
      return;
    }
    dispatch({ type: "select-sector", sectorId: id });
    if (ui.selectedEntityId != null) dispatch({ type: "select-entity", entityId: null });
  }

  function handleOutlinerCentre(row: OutlinerRow) {
    if (!worldGeneration) return;
    const point = centreTargetForOutlinerRow(row, world.sectors);
    if (!point) return;
    worldBusEmit("world:camera", {
      generation: worldGeneration,
      op: "centre",
      x: point.x,
      y: point.y
    });
  }

  return (
    <StageHost>
      <div className="flex h-full min-h-0 items-stretch" data-testid="world-frame">
        <Rail
          entries={railEntries}
          onSelect={(id) => (id === "sanctum" ? navigate("/sanctum") : openLayerOnSanctum(id))}
        />
        <div className="min-w-0 flex-1" ref={mapPaneRef}>
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
              <div
                className="pointer-events-auto flex flex-col items-end gap-2 p-2"
                data-testid="world-hud-turn-wrap"
              >
                <UnresolvedCount
                  legions={myLegions}
                  pending={ui.pending}
                  displayNames={myLegionDisplayNames}
                  onFocus={(entityId) => dispatch({ type: "select-entity", entityId })}
                />
                {worldId ? (
                  <TurnCluster
                    worldId={worldId}
                    currentTurn={dto.currentTurn}
                    commanderId={playerFactionId ?? ""}
                    legions={myLegions}
                    pending={ui.pending}
                    onOrdersFiled={() => dispatch({ type: "clear-queue" })}
                  />
                ) : null}
              </div>
            }
            bottomLeft={
              <div className="pointer-events-auto flex flex-col items-start gap-2">
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    data-testid="world-map-fit"
                    className="rounded border border-border bg-panel px-2 py-1 text-sm text-ink"
                    onClick={() => {
                      if (!worldGeneration) return;
                      worldBusEmit("world:camera", {
                        generation: worldGeneration,
                        op: "fit",
                        padLeft: fitPadLeft(selectedSector != null, measuredAnchors.dock?.width),
                        padRight: 40,
                        padTop: measuredAnchors.top?.height ?? 56,
                        padBottom: 80
                      });
                    }}
                  >
                    Fit
                  </button>
                  <button
                    type="button"
                    data-testid="world-map-zoom-in"
                    className="rounded border border-border bg-panel px-2 py-1 text-sm text-ink"
                    aria-label="Zoom in"
                    onClick={() => {
                      if (!worldGeneration) return;
                      worldBusEmit("world:camera", {
                        generation: worldGeneration,
                        op: "zoom",
                        factor: 1.15
                      });
                    }}
                  >
                    +
                  </button>
                  <button
                    type="button"
                    data-testid="world-map-zoom-out"
                    className="rounded border border-border bg-panel px-2 py-1 text-sm text-ink"
                    aria-label="Zoom out"
                    onClick={() => {
                      if (!worldGeneration) return;
                      worldBusEmit("world:camera", {
                        generation: worldGeneration,
                        op: "zoom",
                        factor: 1 / 1.15
                      });
                    }}
                  >
                    −
                  </button>
                </div>
                <LensPicker
                  active={lens.active}
                  onSelect={(id) => dispatchLens({ type: "select", id })}
                  isLensFourLoading={lensData.isLensFourLoading}
                />
                <QueuedOrders orders={ui.pending} onTakeBack={(commandId) => dispatch({ type: "unqueue", commandId })} />
              </div>
            }
            rightEdge={
              <div
                className="pointer-events-auto flex w-[280px] flex-col gap-2 p-2"
                data-testid="world-hud-right-column"
              >
                <NotifyRail
                  items={notifyItems}
                  onOpen={(id) => setNotifyItems((items) => open(items, id))}
                  onDismiss={(id) => setNotifyItems((items) => dismiss(items, id))}
                  onUndoDismiss={(id) =>
                    setNotifyItems((items) =>
                      items.map((item) =>
                        item.id === id && item.state === "dismissed" ? { ...item, state: "opened" } : item
                      )
                    )
                  }
                />
                <OutlinerFilter filter={outlinerFilter} onChange={setOutlinerFilter} />
                <Outliner
                  groups={outlinerGroups}
                  selectedId={outlinerSelectedId}
                  onSelect={handleOutlinerSelect}
                  onCentreRequest={handleOutlinerCentre}
                />
                {worldId ? <PlaybackPanel worldId={worldId} turn={dto.currentTurn - 1} /> : null}
              </div>
            }
          >
            <div className="h-full w-full min-h-0">
              <WorldGameHost
                model={world}
                legions={allLegions}
                playerFactionId={playerFactionId}
                overlayEpoch={overlayEpoch}
                selectedSectorId={ui.selectedSectorId}
                selectedEntityId={ui.selectedEntityId}
                ignoreRects={ignoreRects}
                targeting={targeting}
                lens={lens.active}
                onGeneration={setWorldGeneration}
                onSelect={handleWorldSelect}
              />
            </div>
          </WorldHud>
        </div>
      </div>

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
