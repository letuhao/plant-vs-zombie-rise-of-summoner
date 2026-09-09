import type { Phase, PiecePayload, ThemeRef } from "./types";
import type {
  ActorResourcePoolDto,
  ActorSheetDto,
  ActorStatusGlyphDto
} from "@/lib/bus/aura";
import type { ActorSurfaceCatalog, ResourceCatalogRow, StatusCatalogRow } from "@/lib/bus/actorSurface";
import type { ActorView } from "@/contract/types";
import { isKnown } from "@/contract/pending";

type StandingAxis = {
  id: string;
  label: string;
  value: number;
  paint: string;
};

export type ConditionSurfaceVmInput = {
  data: ActorView;
  sheet: ActorSheetDto | null | undefined;
  surface: ActorSurfaceCatalog;
  selectedPoolId: string | null;
  availability: "ready" | "loading" | "error";
  revision?: number;
};

export type ConditionSurfaceVm = {
  phase: Phase;
  revision: number;
  rootClass: string;
  testId: string;
  main: PiecePayload[];
  phasePayload: PiecePayload;
};

const STANDING_AXES: StandingAxis[] = [
  { id: "offense", label: "Offense", value: 0, paint: "#d98787" },
  { id: "survivability", label: "Survivability", value: 0, paint: "#6dbb63" },
  { id: "control", label: "Control", value: 0, paint: "#b48ae6" },
  { id: "utility", label: "Utility", value: 0, paint: "#6aa4f7" },
  { id: "economy", label: "Economy", value: 0, paint: "#dcb04e" }
];

function toSurfacePhase(availability: ConditionSurfaceVmInput["availability"]): Phase {
  if (availability === "loading") return "loading";
  if (availability === "error") return "error";
  return "ready";
}

function currentName(data: ActorView, sheet: ActorSheetDto | null | undefined): string {
  if (sheet?.displayName) return sheet.displayName;
  if (isKnown(data.displayName)) return data.displayName.value;
  return `#${data.instanceId.slice(0, 6)}`;
}

function poolById(sheet: ActorSheetDto | null | undefined) {
  return new Map((sheet?.resourcePools ?? []).map((row) => [row.resourceId, row]));
}

function glyphByStatus(sheet: ActorSheetDto | null | undefined) {
  return new Map((sheet?.liveStatuses ?? []).map((row) => [row.statusId, row]));
}

function progressFillPct(level: number, xp: number, xpToNext: number | null | undefined): number | null {
  if (!Number.isFinite(xp) || !Number.isFinite(level) || !xpToNext || xpToNext <= 0) return null;
  return Math.max(0, Math.min(100, Math.round((xp / xpToNext) * 100)));
}

function buildProgression(data: ActorView, sheet: ActorSheetDto | null | undefined): PiecePayload {
  const xp = sheet?.xp ?? data.xp;
  const xpToNext = sheet?.xpToNext ?? null;
  const level = sheet?.level ?? data.level;
  const fillPct = progressFillPct(level, xp, xpToNext);
  return {
    piece: "progression-gauge",
    instanceId: "condition:progression",
    phase: xpToNext == null ? "pending" : "ready",
    level,
    xp,
    xpToNext,
    fillPct,
    valueText: xpToNext == null ? `${xp.toLocaleString()} XP` : `${xp.toLocaleString()} / ${xpToNext.toLocaleString()} XP`,
    message: xpToNext == null ? "Next level isn't shown yet" : null
  };
}

function buildHero(
  data: ActorView,
  sheet: ActorSheetDto | null | undefined,
  surface: ActorSurfaceCatalog,
  selectedPoolId: string | null
): PiecePayload {
  const pools = poolById(sheet);
  const hpCatalog = surface.resources.find((row) => row.id === "hp") ?? surface.resources[0]!;
  const hp = pools.get("hp");
  const hpPct =
    hp?.current != null && hp.max != null && hp.max > 0
      ? Math.max(0, Math.min(100, Math.round((hp.current / hp.max) * 100)))
      : null;
  const shield = sheet?.shieldSummary ?? null;
  const shieldPct =
    shield?.current != null && shield.max != null && shield.max > 0
      ? Math.max(0, Math.min(100, Math.round((shield.current / shield.max) * 100)))
      : null;
  const shieldTheme: ThemeRef | undefined =
    shield?.elementId != null && shield.elementId.length > 0
      ? { kind: "element", id: shield.elementId }
      : undefined;

  return {
    piece: "cond-hero",
    instanceId: "condition:hero",
    phase: "ready",
    actorName: currentName(data, sheet),
    selectedPoolId,
    slotsTitle: "Vitality",
    radial: {
      piece: "pool-radial",
      instanceId: "condition:pool:hp",
      phase: hpPct == null ? "pending" : "ready",
      poolId: "hp",
      label: "HP",
      valueText:
        hp?.current != null && hp.max != null
          ? `${hp.current.toLocaleString()} / ${hp.max.toLocaleString()}`
          : "Current / max pending",
      hpPct,
      shieldPct,
      themeRef: { kind: "resource", id: hpCatalog.id },
      shieldThemeRef: shieldTheme,
      message: hpPct == null ? "Current / max HP pending until the live pool adapter lands." : null
    },
    meters: surface.resources.map((resource) => buildMeter(data, resource, pools.get(resource.id), selectedPoolId))
  };
}

function buildMeter(
  data: ActorView,
  resource: ResourceCatalogRow,
  live: ActorResourcePoolDto | undefined,
  selectedPoolId: string | null
): PiecePayload {
  const label = resource.labels[data.side];
  const fillPct =
    live?.current != null && live.max != null && live.max > 0
      ? Math.max(0, Math.min(100, Math.round((live.current / live.max) * 100)))
      : null;
  return {
    piece: "pool-meter",
    instanceId: `condition:resource:${resource.id}`,
    phase: fillPct == null ? "pending" : "ready",
    poolId: resource.id,
    label,
    icon: resource.icon,
    valueText:
      live?.current != null && live.max != null
        ? `${live.current.toLocaleString()} / ${live.max.toLocaleString()}`
        : "Current / max pending",
    fillPct,
    selected: selectedPoolId === resource.id,
    themeRef: { kind: "resource", id: resource.id },
    message: fillPct == null ? "Current / max pending" : null
  };
}

function buildStanding(): PiecePayload {
  return {
    piece: "stand-row",
    instanceId: "condition:standing",
    phase: "ready",
    radar: {
      piece: "standing-radar",
      instanceId: "condition:standing:radar",
      phase: "pending",
      title: "Standing · five axes (definitions.md)",
      axes: STANDING_AXES,
      message: "Standing vector isn't ready yet."
    },
    bars: {
      piece: "standing-bars",
      instanceId: "condition:standing:bars",
      phase: "pending",
      title: "Standing · five axes (definitions.md)",
      axes: STANDING_AXES,
      message: "Standing vector isn't ready yet."
    }
  };
}

function buildStatusStrip(sheet: ActorSheetDto | null | undefined, statuses: StatusCatalogRow[]): PiecePayload {
  const liveById = glyphByStatus(sheet);
  const liveRows = statuses
    .filter((row) => liveById.has(row.id))
    .map((row) => {
      const live = liveById.get(row.id) as ActorStatusGlyphDto;
      return {
        id: row.id,
        label: row.displayName,
        hudToken: row.hudToken,
        color: row.color,
        remainingPermille: live.remainingPermille ?? null
      };
    });

  return {
    piece: "status-glyph-strip",
    instanceId: "condition:status-strip",
    phase: liveRows.length === 0 ? "pending" : "ready",
    title: "Live status · tap for Status tab",
    items: liveRows,
    overflowCount: 0,
    message: liveRows.length === 0 ? "Live effect instances are not available yet." : null
  };
}

export function foldConditionSurfaceVm(input: ConditionSurfaceVmInput): ConditionSurfaceVm {
  const phase = toSurfacePhase(input.availability);
  const revision = input.revision ?? 1;
  const main = [
    buildProgression(input.data, input.sheet),
    buildHero(input.data, input.sheet, input.surface, input.selectedPoolId),
    buildStanding(),
    buildStatusStrip(input.sheet, input.surface.statuses)
  ];

  return {
    phase,
    revision,
    rootClass: "condition-console",
    testId: "condition-console",
    main,
    phasePayload: {
      piece: phase === "loading" ? "phase-loading" : "phase-error",
      instanceId: `condition:phase:${phase}`,
      phase,
      message: phase === "loading" ? "Loading Condition…" : "Condition is unavailable.",
      retryLabel: "Retry",
      retryEvent: "condition.retry"
    }
  };
}
