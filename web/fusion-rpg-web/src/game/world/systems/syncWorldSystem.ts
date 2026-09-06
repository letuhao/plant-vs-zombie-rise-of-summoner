import type Phaser from "phaser";
import type { AdaptedWorldState } from "@/contract/adapt";
import type { LaneView, SectorView } from "@/contract/types";
import { channelsFor } from "@/stages/world/render/sectorChannels";
import { fogTreatmentFor } from "@/stages/world/render/fogTreatments";
import { ownershipOf, healthOf } from "@/stages/world/render/sectorHealthAndOwnership";
import type { LaneKind } from "@/stages/world/render/laneChannels";
import type { WorldTheme } from "../snapshotTheme";
import { sectorCenter } from "../layout";
import type { ZoomTier } from "../zoomTier";
import type { WorldRegistry } from "../entities/WorldRegistry";
import { createSectorPin } from "../objects/sectorPin";
import { createLaneStroke } from "../objects/laneStroke";
import { createForceMarker } from "../objects/forceMarker";

export type SyncWorldModel = AdaptedWorldState & {
  playerFactionId?: string | null;
};

export type SyncWorldSystemInput = {
  scene: Phaser.Scene;
  theme: WorldTheme;
  registry: WorldRegistry;
  lastApplied: number;
  modelSeq: number;
  model: unknown;
  tier: ZoomTier;
  forceLodRefresh?: boolean;
};

function asModel(model: unknown): SyncWorldModel | null {
  if (model == null || typeof model !== "object") return null;
  const m = model as Partial<SyncWorldModel>;
  if (!Array.isArray(m.sectors) || !Array.isArray(m.lanes)) return null;
  return m as SyncWorldModel;
}

function laneState(lane: LaneView) {
  return {
    severed: lane.state === "Severed",
    wardLevel: lane.wardLevel.value > 0 ? lane.wardLevel.value : null,
    hazardMilli: lane.hazard.value
  };
}

function destroyAbsent(
  registry: WorldRegistry,
  currentIds: string[],
  nextIds: Set<string>,
  kind: "sector" | "lane" | "force"
): void {
  for (const id of currentIds) {
    if (nextIds.has(id)) continue;
    if (kind === "sector") {
      registry.getSector(id)?.destroy();
      registry.deleteSector(id);
    } else if (kind === "lane") {
      registry.getLane(id)?.destroy();
      registry.deleteLane(id);
    } else {
      registry.getForce(id)?.destroy();
      registry.deleteForce(id);
    }
  }
}

function upsertSectorPin(
  scene: Phaser.Scene,
  theme: WorldTheme,
  registry: WorldRegistry,
  sector: SectorView,
  tier: ZoomTier,
  playerFactionId: string | null
): void {
  const ownership = ownershipOf(sector, playerFactionId);
  const health = healthOf(sector, ownership);
  const channels = channelsFor({
    intel: sector.intel,
    ownership,
    health,
    stabilityMilli: sector.stability.value
  });
  const fog = fogTreatmentFor(sector.intel, sector.intelAge);
  const { x, y } = sectorCenter(sector.layoutX, sector.layoutY);

  registry.getSector(sector.sectorId)?.destroy();

  const pin = createSectorPin(scene, theme, {
    id: sector.sectorId,
    channels,
    fog,
    zoom: tier,
    x,
    y
  });
  pin.setDepth(0);
  registry.setSector(sector.sectorId, pin);
}

function upsertLane(
  scene: Phaser.Scene,
  theme: WorldTheme,
  registry: WorldRegistry,
  lane: LaneView,
  sectorsById: Map<string, SectorView>
): void {
  const from = sectorsById.get(lane.fromSectorId);
  const to = sectorsById.get(lane.toSectorId);
  if (!from || !to) return;

  const a = sectorCenter(from.layoutX, from.layoutY);
  const b = sectorCenter(to.layoutX, to.layoutY);

  registry.getLane(lane.laneId)?.destroy();

  const stroke = createLaneStroke(scene, theme, {
    id: lane.laneId,
    kind: lane.typeId as LaneKind,
    state: laneState(lane),
    widthMilli: lane.width.value,
    x0: a.x,
    y0: a.y,
    x1: b.x,
    y1: b.y
  });
  registry.setLane(lane.laneId, stroke);
}

function upsertForces(
  scene: Phaser.Scene,
  theme: WorldTheme,
  registry: WorldRegistry,
  model: SyncWorldModel,
  sectorsById: Map<string, SectorView>
): void {
  const playerFactionId = model.playerFactionId ?? null;
  const nextForceIds = new Set<string>();

  for (const [sectorId, forces] of Object.entries(model.forcesBySectorId ?? {})) {
    const sector = sectorsById.get(sectorId);
    if (!sector) continue;
    const { x: cx, y: cy } = sectorCenter(sector.layoutX, sector.layoutY);

    forces.forEach((force, index) => {
      const id = force.entityId;
      nextForceIds.add(id);
      registry.getForce(id)?.destroy();

      const offset = (index - (forces.length - 1) / 2) * 16;
      const marker = createForceMarker(scene, theme, {
        id,
        force,
        ownership: force.ownerFactionId === playerFactionId ? "yours" : "enemy",
        x: cx + offset,
        y: cy - 20
      });
      marker.setDepth(5);
      registry.setForce(id, marker);
    });
  }

  destroyAbsent(registry, registry.forceIds(), nextForceIds, "force");
}

/**
 * Apply `world:model` when `modelSeq > lastApplied` (or `forceLodRefresh`).
 * Returns the new `lastApplied` seq, or `null` when skipped.
 */
export function syncWorldSystem(input: SyncWorldSystemInput): number | null {
  if (!input.forceLodRefresh && input.modelSeq <= input.lastApplied) return null;

  const model = asModel(input.model);
  if (!model) return input.forceLodRefresh ? input.lastApplied : null;

  const sectorsById = new Map(model.sectors.map((s) => [s.sectorId, s]));
  const playerFactionId = model.playerFactionId ?? null;

  const nextSectorIds = new Set(model.sectors.map((s) => s.sectorId));
  destroyAbsent(input.registry, input.registry.sectorIds(), nextSectorIds, "sector");
  for (const sector of model.sectors) {
    upsertSectorPin(input.scene, input.theme, input.registry, sector, input.tier, playerFactionId);
  }

  const nextLaneIds = new Set(model.lanes.map((l) => l.laneId));
  destroyAbsent(input.registry, input.registry.laneIds(), nextLaneIds, "lane");
  for (const lane of model.lanes) {
    upsertLane(input.scene, input.theme, input.registry, lane, sectorsById);
  }

  upsertForces(input.scene, input.theme, input.registry, model, sectorsById);

  return input.modelSeq;
}
