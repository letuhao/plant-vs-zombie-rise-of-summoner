import "./scene.css";
import type { AdaptedWorldState } from "@/contract/adapt";
import type { SlotView } from "@/contract/types";
import type { PendingOrder } from "@/features/world/worldSelection";
import { RangeOverlay, type RangeTarget } from "@/stages/world/targeting/RangeOverlay";
import { BlockedTarget } from "@/stages/world/targeting/BlockedTarget";
import { placementFor } from "@/stages/world/targeting/blockedPlacement";
import { channelsFor } from "./sectorChannels";
import { ownershipOf, healthOf } from "./sectorHealthAndOwnership";
import { SectorNode, type SectorSlotView } from "./SectorNode";
import { Fog } from "./Fog";
import { Lane } from "./Lane";
import { ForceMarker } from "./ForceMarker";
import type { LaneKind } from "./laneChannels";
import type { SlotMarker } from "./slotSilhouettes";

/** Ported from `worldViewModel.ts`'s own authored-grid constants — sectors are placed, never
 * auto-laid-out, and this is the one other place that layout unit has to agree. */
export const GRID_X = 220;
export const GRID_Y = 190;

/** The sector card's own fixed footprint inside its `GRID_X`×`GRID_Y` cell — leaves margin for
 * lanes to approach it visibly rather than running edge-to-edge into the next card. */
const CARD_WIDTH = 180;
const CARD_HEIGHT = 140;

function markerFor(slot: SlotView): SlotMarker {
  if (slot.guardState === "Intact") return { kind: "guarded" };
  if (slot.structureId != null) {
    const turns = slot.constructionTurnsRemaining.state === "known" ? slot.constructionTurnsRemaining.value : null;
    if (turns != null && turns > 0) return { kind: "building", turnsRemaining: turns };
    return { kind: "built" };
  }
  return null;
}

export type WorldSceneProps = {
  /** Already-adapted view data — `adaptWorldState` (`contract/adapt.ts`) is the one place allowed
   * to touch the raw wire DTO; `contractGuard.ts` bans every `stages/` file from importing one by
   * name, so this component never could regardless. */
  world: AdaptedWorldState;
  playerFactionId: string | null;
  selectedSectorId: string | null;
  onSelectSector: (sectorId: string) => void;
  /** §4.2's zoom rule: the slot row drops first at map zoom. */
  zoom: "map" | "detail";

  /**
   * The targeting wiring (world-stage W71) — all five optional and all default to "nothing
   * selected/queued", so every existing caller/test that predates this task keeps compiling and
   * rendering unchanged.
   */
  /** Which force, if any, is the current target of a march order. */
  selectedEntityId?: string | null;
  /** Fired only for a selectable (player-owned) force's own marker — `WorldScene` never calls this
   * for a force it drew as unselectable. */
  onSelectEntity?: (entityId: string) => void;
  /** Real hop counts only — no position. `WorldScene` owns turning a sector id into where it
   * actually sits on screen, the same job it already does for sectors and lanes. `null` means no
   * legion is selected right now, and no ring is drawn at all. */
  reachableSectors?: RangeTarget[] | null;
  /** Every currently-queued order — `WorldScene` draws a destination flag and a lit route for each
   * `"move"` one; every other kind is silently ignored here (this scene has nothing to draw for a
   * `stance`/`sustain`/`build` order yet). */
  pendingOrders?: PendingOrder[];
  /** The one sector a click was just refused against, and why — cleared by the caller on the next
   * successful action or selection change. `null` means nothing is currently refused. */
  blockedTarget?: { sectorId: string; reason: string } | null;
};

/**
 * The scene composition (found and built 2026-09-04, closing the wiring gap this program named
 * four times over — W50, W57, W65, W71): `world-render`'s components (W43-W49) were each built and
 * unit-tested in isolation, but nothing had ever composed them onto the real map with real state.
 * This is that composition — every sector positioned by its own authored `layoutX`/`layoutY`
 * (`GRID_X`/`GRID_Y`, the exact constants `worldViewModel.ts`'s old page already used), wrapped in
 * `Fog` (W47) so intel gates what renders, clickable to select. Lanes draw between their two
 * sectors' real positions.
 *
 * **Deliberately deferred, not silently dropped**: legion markers *on lanes* (a force mid-march,
 * which needs the lane-progress animation `render/LegionMarker.tsx` already owns) and the
 * supply/lifeline overlays (W48) are still not wired in here — this composition's own job, closing
 * W71, was a force **at rest at a sector**, its selection, its reachable range, and its queued
 * order's destination flag/lit route; a force actually on the road mid-turn is a distinct, still-
 * open piece.
 */
export function WorldScene({
  world,
  playerFactionId,
  selectedSectorId,
  onSelectSector,
  zoom,
  selectedEntityId = null,
  onSelectEntity,
  reachableSectors = null,
  pendingOrders = [],
  blockedTarget = null
}: WorldSceneProps) {
  const positionById = new Map(world.sectors.map((s) => [s.sectorId, { x: s.layoutX * GRID_X, y: s.layoutY * GRID_Y }]));
  const laneById = new Map(world.lanes.map((l) => [l.laneId, l]));

  /** The card's own centre in the sector's local translate space — every ring/flag/route below
   * aims at the same point the card itself occupies, not its top-left corner. */
  const CENTER_X = CARD_WIDTH / 2;
  const CENTER_Y = CARD_HEIGHT / 2;

  const moveOrders = pendingOrders.filter(
    (order): order is PendingOrder & { sectorId: string } => order.kind === "move" && order.sectorId != null
  );

  return (
    <>
      {world.lanes.map((lane) => {
        const from = positionById.get(lane.fromSectorId);
        const to = positionById.get(lane.toSectorId);
        if (!from || !to) return null;
        return (
          <Lane
            key={lane.laneId}
            laneId={lane.laneId}
            kind={lane.typeId as LaneKind}
            state={{
              severed: lane.state === "Severed",
              wardLevel: lane.wardLevel.value > 0 ? lane.wardLevel.value : null,
              hazardMilli: lane.hazard.value
            }}
            widthMilli={lane.width.value}
            sourceX={from.x}
            sourceY={from.y}
            targetX={to.x}
            targetY={to.y}
          />
        );
      })}

      {world.sectors.map((sector) => {
        const ownership = ownershipOf(sector, playerFactionId);
        const health = healthOf(sector, ownership);
        const channels = channelsFor({
          intel: sector.intel,
          ownership,
          health,
          stabilityMilli: sector.stability.value
        });

        const slots: SectorSlotView[] = (world.slotsBySectorId[sector.sectorId] ?? []).map((slot) => ({
          slotIndex: slot.slotIndex,
          slotTypeId: slot.slotTypeId,
          marker: markerFor(slot)
        }));

        const netLoam = ownership === "yours" ? sector.loam.net : null;
        const position = positionById.get(sector.sectorId)!;

        const forces = world.forcesBySectorId[sector.sectorId] ?? [];
        const blockedHere = blockedTarget?.sectorId === sector.sectorId ? blockedTarget : null;

        return (
          <g
            key={sector.sectorId}
            data-testid={`world-scene-sector-${sector.sectorId}`}
            transform={`translate(${position.x}, ${position.y})`}
            onClick={() => onSelectSector(sector.sectorId)}
            data-selected={sector.sectorId === selectedSectorId}
            style={{ cursor: "pointer" }}
          >
            {/* SectorNode/Fog are HTML (`<div>`), not SVG elements — a `<div>` dropped directly
                inside an SVG `<g>` simply does not paint, which is exactly the defect that made
                every sector invisible (zero bounding box) the first time this was wired to a real
                browser rather than only jsdom. `foreignObject` is the one bridge. */}
            <foreignObject x={0} y={0} width={CARD_WIDTH} height={CARD_HEIGHT}>
              <Fog intel={sector.intel} intelAge={sector.intelAge}>
                <SectorNode
                  sectorId={sector.sectorId}
                  channels={channels}
                  slots={slots}
                  netLoam={netLoam}
                  zoom={zoom}
                />
              </Fog>
            </foreignObject>

            {/* A force at rest (world-stage W71) — real SVG siblings of the card's own
                `foreignObject`, laid out along its bottom edge in a stable, id-sorted order so two
                forces sharing a sector never swap places from one render to the next. */}
            {forces.map((force, index) => (
              <ForceMarker
                key={force.entityId}
                force={force}
                ownership={force.ownerFactionId === playerFactionId ? "yours" : "enemy"}
                x={20 + index * 26}
                y={CARD_HEIGHT - 14}
                selected={force.entityId === selectedEntityId}
                selectable={force.ownerFactionId === playerFactionId}
                onSelect={onSelectEntity ? () => onSelectEntity(force.entityId) : undefined}
              />
            ))}

            {/* A refusal is drawn where the decision was made — on the sector the player actually
                clicked (world-stage W70's own "placed where the decision is made" rule), just below
                its own card so the refusal never covers the card it is about. */}
            {blockedHere ? (
              <foreignObject x={0} y={CARD_HEIGHT + 4} width={CARD_WIDTH} height={44}>
                <BlockedTarget
                  state={{ kind: "blocked", reason: blockedHere.reason }}
                  placement={placementFor(blockedHere.reason) ?? "sector"}
                />
              </foreignObject>
            ) : null}
          </g>
        );
      })}

      {/* The reachable-range overlay (world-stage W69, wired against a real map by W71) — drawn
          once every reachable sector's real on-screen centre is known, never at the SVG origin. */}
      {reachableSectors && reachableSectors.length > 0 ? (
        <RangeOverlay
          shape="sectors"
          reachable={reachableSectors.flatMap((target) => {
            const position = positionById.get(target.sectorId);
            if (!position) return [];
            return [{ sectorId: target.sectorId, hops: target.hops, x: position.x + CENTER_X, y: position.y + CENTER_Y }];
          })}
        />
      ) : null}

      {/* The queued-order overlay (world-stage W71's own acceptance): a dashed flag at each move
          order's destination, plus its lit route — drawn from the same `positionById`/`laneById`
          maps sectors and lanes already use, never a second layout computation. The force's own
          marker above is never touched by any of this — it stays exactly where `atSectorId` says. */}
      {moveOrders.map((order) => {
        const lit = (order.lanePath ?? []).flatMap((laneId) => {
          const lane = laneById.get(laneId);
          const from = lane ? positionById.get(lane.fromSectorId) : null;
          const to = lane ? positionById.get(lane.toSectorId) : null;
          if (!lane || !from || !to) return [];
          return [
            <path
              key={`${order.commandId}-${laneId}`}
              data-testid={`lane-queued-${laneId}`}
              d={`M ${from.x + CENTER_X},${from.y + CENTER_Y} L ${to.x + CENTER_X},${to.y + CENTER_Y}`}
              fill="none"
              strokeWidth={4}
            />
          ];
        });

        const destination = positionById.get(order.sectorId);

        return (
          <g key={order.commandId}>
            <g data-testid={`lane-route-${order.commandId}`} data-token="lane-route-queued">
              {lit}
            </g>
            {destination ? (
              <g
                data-testid={`destination-flag-${order.commandId}`}
                transform={`translate(${destination.x + CENTER_X}, ${destination.y})`}
              >
                <text textAnchor="middle" dy={16} aria-hidden="true">
                  ⚑
                </text>
              </g>
            ) : null}
          </g>
        );
      })}
    </>
  );
}
