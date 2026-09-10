import type { Phase, PiecePayload, ThemeRef } from "./types";
import type { ActorSurfaceCatalog, AptitudeCatalogRow } from "@/lib/bus/actorSurface";
import { resolveTheme } from "./themeRegistry";

export type AptitudesMode = "unique" | "species" | "commander";

export type AptitudesSurfaceVmInput = {
  mode: AptitudesMode;
  surface: ActorSurfaceCatalog;
  draftShares: Record<string, number>;
  budget: number;
  spent: number;
  leftover: number;
  dirty: boolean;
  withinBudget: boolean;
  saving: boolean;
  selectedAptitudeId: string | null;
  theta?: number;
  /** Mode A chip subtitle — commander contribution fiction (A5b). */
  commanderAddOn?: string | null;
  activePresetName?: string | null;
  /** family displayNames keyed by aptitude id (G7 / A3). */
  fedFamiliesByAptitudeId?: Record<string, { displayName: string }[]>;
  speciesChrome?: {
    hasOverride: boolean;
    priceAmount: number;
    priceResource: string;
    everRespecced: boolean;
  } | null;
  availability: "ready" | "loading" | "error";
  revision?: number;
};

export type AptitudesSurfaceVm = {
  phase: Phase;
  revision: number;
  rootClass: string;
  testId: string;
  mode: AptitudesMode;
  layout: PiecePayload;
  scopeChip: PiecePayload;
  leftover: PiecePayload;
  presetEntry: PiecePayload;
  speciesChrome?: PiecePayload;
  bands: PiecePayload[];
  inspect: PiecePayload;
  decision: PiecePayload;
  phasePayload: PiecePayload;
};

const POSTURE_TITLE: Record<string, string> = {
  force: "Force",
  finesse: "Finesse",
  bastion: "Bastion"
};

function toPhase(availability: AptitudesSurfaceVmInput["availability"]): Phase {
  if (availability === "loading") return "loading";
  if (availability === "error") return "error";
  return "ready";
}

function stampRevision(payload: PiecePayload, revision: number): PiecePayload {
  payload.revision = revision;
  for (const [key, value] of Object.entries(payload)) {
    if (key === "revision") continue;
    if (Array.isArray(value)) {
      for (const item of value) {
        if (item && typeof item === "object" && "piece" in (item as object)) {
          stampRevision(item as PiecePayload, revision);
        }
      }
    } else if (value && typeof value === "object" && "piece" in (value as object)) {
      stampRevision(value as PiecePayload, revision);
    }
  }
  return payload;
}

function scopeFiction(mode: AptitudesMode, theta: number | undefined): { title: string; subtitle: string } {
  const power = theta != null ? `power ${theta}` : "power —";
  if (mode === "unique") return { title: "Unique specimen", subtitle: power };
  if (mode === "species") return { title: "Species build", subtitle: power };
  return { title: "Commander", subtitle: power };
}

function postureTheme(postureId: string): ThemeRef {
  return { kind: "posture", id: postureId };
}

function buildTile(
  row: AptitudeCatalogRow,
  value: number,
  selected: boolean,
  revision: number,
  allowDirectEdit: boolean
): PiecePayload {
  const themeRef = postureTheme(row.posture);
  const themeResolved = resolveTheme(themeRef);
  return stampRevision(
    {
      piece: "aptitude-tile",
      instanceId: `aptitude:tile:${row.id}`,
      phase: "ready",
      aptitudeId: row.id,
      displayName: row.displayName,
      icon: row.icon ?? null,
      value,
      selected,
      allowDirectEdit,
      themeRef,
      themeResolved
    },
    revision
  );
}

/** Pure fold — Modes A/B/C aptitudes-console VM. */
export function foldAptitudesSurfaceVm(input: AptitudesSurfaceVmInput): AptitudesSurfaceVm {
  const revision = input.revision ?? 1;
  const phase = toPhase(input.availability);
  const phasePayload: PiecePayload = stampRevision(
    {
      piece: "phase-loading",
      instanceId: "aptitudes:phase",
      phase,
      message:
        phase === "loading"
          ? "Loading aptitudes…"
          : phase === "error"
            ? "Aptitudes failed to load."
            : ""
    },
    revision
  );

  if (phase !== "ready") {
    const emptyLayout = stampRevision(
      {
        piece: "aptitudes-layout",
        instanceId: "aptitudes:layout",
        phase,
        mode: input.mode,
        testId: "aptitudes-tab"
      },
      revision
    );
    return {
      phase,
      revision,
      rootClass: "aptitudes-console",
      testId: "aptitudes-tab",
      mode: input.mode,
      layout: emptyLayout,
      scopeChip: stampRevision(
        { piece: "aptitude-scope-chip", instanceId: "aptitudes:scope", phase, title: "…", subtitle: "" },
        revision
      ),
      leftover: stampRevision(
        {
          piece: "leftover-gauge",
          instanceId: "aptitudes:leftover",
          phase,
          budget: 0,
          spent: 0,
          leftover: 0,
          overspend: false
        },
        revision
      ),
      presetEntry: stampRevision(
        {
          piece: "preset-entry",
          instanceId: "aptitudes:preset",
          phase,
          label: "Build presets…",
          activePresetName: null
        },
        revision
      ),
      bands: [],
      inspect: stampRevision(
        { piece: "aptitude-inspect", instanceId: "aptitudes:inspect", phase },
        revision
      ),
      decision: stampRevision(
        {
          piece: "allocate-decision-strip",
          instanceId: "aptitudes:decision",
          phase,
          dirty: false,
          withinBudget: true,
          saving: false
        },
        revision
      ),
      phasePayload
    };
  }

  const fiction = scopeFiction(input.mode, input.theta);
  const scopeChip = stampRevision(
    {
      piece: "aptitude-scope-chip",
      instanceId: "aptitudes:scope",
      phase: "ready",
      title: fiction.title,
      subtitle: fiction.subtitle,
      scopeKey: input.mode,
      commanderAddOn: input.mode === "unique" ? input.commanderAddOn ?? null : null
    },
    revision
  );

  const leftover = stampRevision(
    {
      piece: "leftover-gauge",
      instanceId: "aptitudes:leftover",
      phase: "ready",
      budget: input.budget,
      spent: input.spent,
      leftover: input.leftover,
      overspend: input.leftover < 0
    },
    revision
  );

  const presetEntry = stampRevision(
    {
      piece: "preset-entry",
      instanceId: "aptitudes:preset",
      phase: "ready",
      label: "Build presets…",
      activePresetName: input.activePresetName ?? null
    },
    revision
  );

  let speciesChrome: PiecePayload | undefined;
  if (input.mode === "species" && input.speciesChrome) {
    const sc = input.speciesChrome;
    speciesChrome = stampRevision(
      {
        piece: "species-build-chrome",
        instanceId: "aptitudes:species-chrome",
        phase: "ready",
        hasOverride: sc.hasOverride,
        priceAmount: sc.everRespecced ? sc.priceAmount : null,
        priceResource: sc.everRespecced ? sc.priceResource : null,
        everRespecced: sc.everRespecced
      },
      revision
    );
  }

  const postures = [...new Set(input.surface.aptitudes.map((r) => r.posture))];
  const bands: PiecePayload[] = postures.map((postureId) => {
    const themeRef = postureTheme(postureId);
    const themeResolved = resolveTheme(themeRef);
    const rows = input.surface.aptitudes
      .filter((r) => r.posture === postureId)
      .sort((a, b) => a.ordinal - b.ordinal);
    const tiles = rows.map((row) =>
      buildTile(
        row,
        input.draftShares[row.id] ?? 0,
        input.selectedAptitudeId === row.id,
        revision,
        input.mode === "species"
      )
    );
    return stampRevision(
      {
        piece: "posture-band",
        instanceId: `aptitudes:band:${postureId}`,
        phase: "ready",
        postureId,
        displayName: POSTURE_TITLE[postureId] ?? postureId,
        themeRef,
        themeResolved,
        tiles
      },
      revision
    );
  });

  const selected = input.surface.aptitudes.find((r) => r.id === input.selectedAptitudeId);
  const fed =
    selected && input.fedFamiliesByAptitudeId
      ? input.fedFamiliesByAptitudeId[selected.id] ?? []
      : [];
  const inspect = stampRevision(
    {
      piece: "aptitude-inspect",
      instanceId: "aptitudes:inspect",
      phase: "ready",
      displayName: selected?.displayName ?? "",
      reading: selected?.reading ?? "",
      role: selected?.role ?? "",
      fedFamilies: fed
    },
    revision
  );

  const decision = stampRevision(
    {
      piece: "allocate-decision-strip",
      instanceId: "aptitudes:decision",
      phase: "ready",
      dirty: input.dirty,
      withinBudget: input.withinBudget,
      saving: input.saving,
      confirmTestId: input.mode === "species" ? "species-build-save" : "allocate-decision-confirm",
      priceAmount:
        input.mode === "species" && input.speciesChrome?.everRespecced
          ? input.speciesChrome.priceAmount
          : null,
      priceResource:
        input.mode === "species" && input.speciesChrome?.everRespecced
          ? input.speciesChrome.priceResource
          : null
    },
    revision
  );

  const layout = stampRevision(
    {
      piece: "aptitudes-layout",
      instanceId: "aptitudes:layout",
      phase: "ready",
      mode: input.mode,
      testId: "aptitudes-tab"
    },
    revision
  );

  return {
    phase: "ready",
    revision,
    rootClass: "aptitudes-console",
    testId: "aptitudes-tab",
    mode: input.mode,
    layout,
    scopeChip,
    leftover,
    presetEntry,
    speciesChrome,
    bands,
    inspect,
    decision,
    phasePayload
  };
}
