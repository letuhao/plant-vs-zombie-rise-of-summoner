import type { Phase, PiecePayload, ThemeRef } from "./types";
import type {
  ActorResourcePoolDto,
  ActorSheetDto,
  ActorStandingDto,
  ActorStatusGlyphDto
} from "@/lib/bus/aura";
import type { ActorSurfaceCatalog, ResourceCatalogRow, StatusCatalogRow } from "@/lib/bus/actorSurface";
import type { ActorView } from "@/contract/types";

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

const STANDING_PENDING_MESSAGE = "Standing vector isn't ready yet.";

const STANDING_AXES: {
  id: keyof ActorStandingDto;
  label: string;
  paint: string;
}[] = [
  { id: "offense", label: "Offense", paint: "var(--pw-offense, #d98787)" },
  { id: "survivability", label: "Survivability", paint: "var(--pw-survivability, #6dbb63)" },
  { id: "control", label: "Control", paint: "var(--pw-control, #b48ae6)" },
  { id: "utility", label: "Utility", paint: "var(--pw-utility, #7fb4ff)" },
  { id: "economy", label: "Economy", paint: "var(--pw-economy, #e0b44b)" }
];

function toSurfacePhase(availability: ConditionSurfaceVmInput["availability"]): Phase {
  if (availability === "loading") return "loading";
  if (availability === "error") return "error";
  return "ready";
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
    gridArea: "prog",
    level,
    xp,
    xpToNext,
    fillPct,
    valueText:
      xpToNext == null ? `${xp.toLocaleString()} XP` : `${xp.toLocaleString()} / ${xpToNext.toLocaleString()} XP`,
    message: xpToNext == null ? "Next level isn't shown yet" : null
  };
}

function buildIdentity(
  data: ActorView,
  sheet: ActorSheetDto | null | undefined
): PiecePayload {
  const base = {
    piece: "actor-identity",
    instanceId: "condition:identity",
    gridArea: "identity",
    side: data.side,
    typeId: sheet?.typeId ?? data.typeId
  };

  if (sheet == null) {
    return {
      ...base,
      phase: "pending",
      speciesName: null,
      phaseLabel: null,
      elements: [],
      message: "Species isn't loaded yet."
    };
  }

  const elements = sheet.elementTyping
    ? [sheet.elementTyping.primary, ...(sheet.elementTyping.secondary ? [sheet.elementTyping.secondary] : [])]
    : [];

  return {
    ...base,
    phase: sheet.speciesName || sheet.phase || elements.length ? "ready" : "pending",
    speciesName: sheet.speciesName ?? null,
    phaseLabel: sheet.phase ?? null,
    elements,
    message: sheet.speciesName ? null : "Species isn't on file for this actor."
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
    gridArea: "hero",
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
      message: hpPct == null ? "Current / max HP pending until pools land." : null
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

function buildStanding(sheet: ActorSheetDto | null | undefined): PiecePayload {
  const standing = sheet?.standing ?? null;
  if (standing == null) {
    return {
      piece: "stand-row",
      instanceId: "condition:standing",
      phase: "ready",
      gridArea: "stand",
      radar: {
        piece: "standing-radar",
        instanceId: "condition:standing:radar",
        phase: "pending" as const,
        title: "Standing · five axes (definitions.md)",
        message: STANDING_PENDING_MESSAGE
      },
      bars: {
        piece: "standing-bars",
        instanceId: "condition:standing:bars",
        phase: "pending" as const,
        title: "Standing · five axes (definitions.md)",
        message: STANDING_PENDING_MESSAGE
      }
    };
  }

  const raw = STANDING_AXES.map((axis) => ({
    id: axis.id,
    label: axis.label,
    value: standing[axis.id],
    paint: axis.paint
  }));
  const maxAxis = Math.max(1, ...raw.map((a) => a.value));
  const axes = raw.map((a) => ({
    ...a,
    fillPct: Math.max(0, Math.min(100, Math.round((a.value / maxAxis) * 100)))
  }));

  return {
    piece: "stand-row",
    instanceId: "condition:standing",
    phase: "ready",
    gridArea: "stand",
    radar: {
      piece: "standing-radar",
      instanceId: "condition:standing:radar",
      phase: "ready",
      title: "Standing · five axes (definitions.md)",
      axes
    },
    bars: {
      piece: "standing-bars",
      instanceId: "condition:standing:bars",
      phase: "ready",
      title: "Standing · five axes (definitions.md)",
      axes
    }
  };
}

function buildStatusStrip(
  sheet: ActorSheetDto | null | undefined,
  statuses: StatusCatalogRow[]
): PiecePayload {
  const base = {
    piece: "status-glyph-strip",
    instanceId: "condition:status-strip",
    title: "Live status · tap for Status tab",
    overflowCount: 0
  };

  if (sheet == null) {
    return {
      ...base,
      phase: "pending",
      items: [],
      message: "Live effect instances are not available yet."
    };
  }

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

  if (liveRows.length === 0) {
    return {
      ...base,
      phase: "empty",
      items: [],
      message: "No live effects applied."
    };
  }

  return {
    ...base,
    phase: "ready",
    items: liveRows,
    message: null
  };
}

export function foldConditionSurfaceVm(input: ConditionSurfaceVmInput): ConditionSurfaceVm {
  const phase = toSurfacePhase(input.availability);
  const revision = input.revision ?? 1;
  const standing = buildStanding(input.sheet);
  // Nest status under stand-row; also pass catalog statuses into the strip.
  standing.statusStrip = buildStatusStrip(input.sheet, input.surface.statuses);

  const main = [
    buildProgression(input.data, input.sheet),
    buildIdentity(input.data, input.sheet),
    buildHero(input.data, input.sheet, input.surface, input.selectedPoolId),
    standing
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
