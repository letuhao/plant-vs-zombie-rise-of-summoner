/**
 * foldDerivedSurfaceVm — Model → DerivedSurfaceVm (pure, no React).
 * Spec: docs/architecture/gui-lego/spec-derived-surface-vm.md
 */
import type { DerivedSurfaceTab, ElementCatalogRow } from "@/lib/bus/actorSurface";
import type { ActorContributionDto } from "@/lib/bus/aura";
import { resolveTheme } from "./themeRegistry";
import type {
  DerivedRenderState as LegoDerivedRenderState,
  GlyphRef,
  MagnitudeDisplay,
  Phase,
  PiecePayload,
  ThemeRef
} from "./types";
import {
  BUCKET_LABELS,
  COOK_PRIMARY_TAB_IDS,
  COMPOSE_SENTENCE,
  UNIT_SENTENCE,
  bucketContributions,
  formatDerivedMagnitude,
  isUnchangedState,
  joinDerivedChannelId,
  registryCapFor,
  resolveDerivedRenderState,
  rowKind,
  toLiveMap,
  type LiveChannelView
} from "./cook";

export type DerivedUiState = {
  tabId: string;
  variantId: string | null;
  query: string;
  showUnchanged: boolean;
  selectedChannelId: string | null;
};

export type DerivedSurfaceVmInput = {
  identity: {
    displayName: string;
    level: number;
    side: string;
  };
  cookTabs: DerivedSurfaceTab[];
  sheetChannels?: {
    channelId: string;
    value: number;
    displayName?: string;
    reading?: string;
    composeKind?: string;
    contributions: ActorContributionDto[];
  }[];
  leanChannels?: { channelId: string; value: number; contributions: { sourceId: string; op: string; value: number }[] }[];
  elements: ElementCatalogRow[];
  locale?: string;
  ui: DerivedUiState;
  availability: "ready" | "loading" | "error";
  revision?: number;
};

export type DerivedSurfaceVm = {
  phase: Phase;
  revision: number;
  selectedChannelId: string | null;
  selectedInstanceId: string | null;
  search: PiecePayload;
  showUnchanged: PiecePayload;
  primaryRail: PiecePayload & { chips: PiecePayload[] };
  variantRail: PiecePayload & { chips: PiecePayload[] };
  familyList: PiecePayload;
  families: PiecePayload[];
  dockScroll: PiecePayload;
  inspectScroll: PiecePayload;
  inspect: PiecePayload & {
    hero: PiecePayload;
    meta: PiecePayload;
    cap: PiecePayload;
    donut: PiecePayload;
    stack: PiecePayload;
    sources: PiecePayload;
  };
  foot: PiecePayload;
  phasePayload: PiecePayload;
  themeRef?: ThemeRef;
  themeResolved?: import("./types").ThemeResolved;
};

const PAINT_BUCKET: Record<string, string> = {
  base: "#6b6358",
  aptitude: "#6fc4d9",
  equip: "#e0b44b",
  tree: "#7a9e5a",
  status: "#e0703c",
  grant: "#d8c078",
  other: "#8a8070",
  neg: "#c05050"
};

function sideTheme(side: string): ThemeRef {
  const id = side.toLowerCase() === "zombie" ? "zombie" : "plant";
  return { kind: "side", id };
}

function variantTheme(
  tabId: string,
  variantId: string | null
): ThemeRef | undefined {
  if (!variantId) return undefined;
  if (tabId === "elements") return { kind: "element", id: variantId };
  if (tabId === "status") {
    // Omni + L2b category chips reuse status-category packs; per-status ids tint via category pack.
    if (variantId === "omni" || variantId === "dot" || variantId === "cc" || variantId === "contagion") {
      return { kind: "status-category", id: variantId };
    }
    return { kind: "status-category", id: statusIdToL2b(variantId) };
  }
  if (tabId === "resources") return { kind: "resource", id: variantId };
  return undefined;
}

/** L2b category for status-catalog ids — mirrors StatusCategoryRegistry. */
function statusIdToL2b(statusId: string): string {
  const map: Record<string, string> = {
    wither: "dot",
    poison: "dot",
    leech: "dot",
    bond: "dot",
    rally: "dot",
    expose: "dot",
    command: "dot",
    shatter: "dot",
    "nerve.unsettled": "dot",
    "nerve.shaken": "dot",
    "nerve.afflicted": "dot",
    butter: "cc",
    freeze: "cc",
    cold: "cc",
    hypno: "cc",
    ember: "cc",
    jala: "cc",
    kelp: "cc",
    charm_pulse: "cc",
    blight: "contagion",
    rot: "contagion",
    spark: "contagion",
    pact_mark: "contagion",
    spore: "contagion"
  };
  return map[statusId] ?? "dot";
}

function statusGlyph(statusId: string): string {
  const map: Record<string, string> = {
    omni: "hexagon",
    butter: "ban",
    freeze: "snowflake",
    cold: "snowflake",
    poison: "virus",
    hypno: "sparkles",
    ember: "flame",
    jala: "flame",
    kelp: "wind",
    wither: "activity",
    bond: "hexagon",
    rally: "shield",
    leech: "heart",
    expose: "crosshair",
    command: "sword",
    shatter: "zap",
    charm_pulse: "sparkles",
    blight: "virus",
    rot: "virus",
    spark: "zap",
    pact_mark: "hexagon",
    spore: "virus",
    "nerve.unsettled": "activity",
    "nerve.shaken": "activity",
    "nerve.afflicted": "activity"
  };
  return map[statusId] ?? "hexagon";
}

function glyphFromFamily(icon: string | null | undefined, title: string): GlyphRef {
  return {
    catalogIcon: icon ?? undefined,
    fallbackText: title.slice(0, 1)
  };
}

export function foldDerivedSurfaceVm(input: DerivedSurfaceVmInput): DerivedSurfaceVm {
  const locale = input.locale ?? "en";
  const revision = input.revision ?? 1;
  // Pin primary rail to COOK_PRIMARY_TAB_IDS order (SSOT); drop unknown cook ids.
  const byCookId = new Map(input.cookTabs.map((t) => [t.id, t]));
  const tabs = COOK_PRIMARY_TAB_IDS.map((id) => byCookId.get(id)).filter(
    (t): t is DerivedSurfaceTab => t != null
  );
  const tabId =
    tabs.some((t) => t.id === input.ui.tabId) && input.ui.tabId
      ? input.ui.tabId
      : (tabs[0]?.id ?? "elements");
  const activeTab = tabs.find((t) => t.id === tabId) ?? tabs[0] ?? null;

  const variantChoices = (() => {
    if (!activeTab) return [] as { id: string; displayName: string }[];
    if (activeTab.id === "other") {
      return (activeTab.actionCategoryVariants ?? []).map((v) => ({
        id: v.id,
        displayName: v.displayName
      }));
    }
    return activeTab.variants.map((v) => ({
      id: v.id,
      displayName: v.displayName
    }));
  })();

  let variantId = input.ui.variantId;
  if (variantChoices.length === 0) variantId = null;
  else if (!variantId || !variantChoices.some((v) => v.id === variantId)) {
    variantId =
      activeTab?.id === "elements" && variantChoices.some((v) => v.id === "fire")
        ? "fire"
        : variantChoices[0]!.id;
  }

  const byId = toLiveMap(input.sheetChannels, input.leanChannels as never);
  const themeVar = variantTheme(tabId, variantId);

  type RowBuilt = {
    categoryId: string;
    categoryLabel: string;
    channelId: string;
    familyId: string;
    displayName: string;
    reading: string;
    unitClass: string;
    compose: string;
    expand: string;
    icon: string | null;
    variantLabel: string;
    element: ElementCatalogRow | null;
    live: LiveChannelView | undefined;
    state: ReturnType<typeof resolveDerivedRenderState>;
    capRef: string | null;
  };

  const familyRows: RowBuilt[] = [];
  if (activeTab) {
    for (const cat of activeTab.categories) {
      for (const family of cat.families) {
        let channelVariant = variantId;
        let variantLabel =
          variantChoices.find((v) => v.id === variantId)?.displayName ?? variantId ?? "";
        if (family.expand === "none") {
          channelVariant = null;
          variantLabel = family.displayName;
        } else if (!variantId) {
          continue;
        }
        const channelId = joinDerivedChannelId(family.family, family.expand, channelVariant);
        const element =
          family.expand === "element"
            ? (input.elements.find((e) => e.id === channelVariant) ?? null)
            : null;
        const live = byId.get(channelId);
        const state = resolveDerivedRenderState(channelId, live, family.capRef);
        familyRows.push({
          categoryId: cat.id,
          categoryLabel: cat.displayName,
          channelId,
          familyId: family.family,
          displayName: family.displayName,
          reading: live?.reading || family.reading,
          unitClass: family.unitClass,
          compose: live?.composeKind || family.compose,
          expand: family.expand,
          icon: family.icon ?? null,
          variantLabel,
          element,
          live,
          state,
          capRef: family.capRef
        });
      }
    }
  }

  const q = input.ui.query.trim().toLowerCase();
  let selectedChannelId = input.ui.selectedChannelId;

  const visibleRows = familyRows.filter((row) => {
    if (
      q &&
      !`${row.displayName} ${row.channelId} ${row.reading} ${row.categoryLabel}`
        .toLowerCase()
        .includes(q)
    ) {
      return false;
    }
    if (
      !input.ui.showUnchanged &&
      isUnchangedState(row.state) &&
      row.channelId !== selectedChannelId
    ) {
      return false;
    }
    return true;
  });
  const hiddenCount = familyRows.length - visibleRows.length;

  if (!selectedChannelId || !visibleRows.some((r) => r.channelId === selectedChannelId)) {
    const firstActive = visibleRows.find((r) => r.state === "active") ?? visibleRows[0];
    selectedChannelId = firstActive?.channelId ?? null;
  }

  let phase: Phase = "ready";
  if (input.availability === "loading") phase = "loading";
  else if (input.availability === "error") phase = "error";
  else if (visibleRows.length === 0) phase = "empty";

  const sideRef = sideTheme(input.identity.side);

  const search: PiecePayload = {
    piece: "tool-search",
    instanceId: "tool:search",
    phase: "ready",
    query: input.ui.query,
    placeholder: "Search channels…"
  };

  const showUnchanged: PiecePayload = {
    piece: "tool-toggle",
    instanceId: "tool:showUnchanged",
    phase: "ready",
    value: input.ui.showUnchanged,
    label: "Show unchanged"
  };

  const primaryChips: PiecePayload[] = tabs.map((tab) => ({
    piece: "chip",
    instanceId: `chip:primary:${tab.id}`,
    phase: "ready" as Phase,
    id: tab.id,
    label: tab.displayName,
    count: tab.categories.reduce((n, c) => n + c.families.length, 0),
    selected: tab.id === tabId,
    rail: "primary" as const
  }));

  const primaryRail: PiecePayload & { chips: PiecePayload[] } = {
    piece: "rail-primary",
    instanceId: "rail:primary",
    phase: "ready",
    ariaLabel: "Derived surface tabs",
    chips: primaryChips
  };

  const variantChips: PiecePayload[] = variantChoices.map((v) => {
    const themeRef = variantTheme(tabId, v.id);
    const themeResolved = resolveTheme(themeRef ?? null);
    if (tabId === "status") {
      themeResolved.glyphDefault = statusGlyph(v.id);
    }
    return {
      piece: "chip",
      instanceId: `chip:variant:${v.id}`,
      phase: "ready" as Phase,
      id: v.id,
      label: v.displayName,
      selected: v.id === variantId,
      rail: "variant" as const,
      elementId: tabId === "elements" ? v.id : undefined,
      themeRef,
      themeResolved
    };
  });

  const variantRail: PiecePayload & { chips: PiecePayload[] } = {
    piece: "rail-variant",
    instanceId: "rail:variant",
    phase: "ready",
    ariaLabel:
      tabId === "elements"
        ? "Element variants"
        : tabId === "status"
          ? "Status category variants"
          : tabId === "resources"
            ? "Resource variants"
            : "Action category variants",
    hidden: variantChoices.length === 0,
    chips: variantChips
  };

  const grouped = new Map<string, { label: string; rows: RowBuilt[] }>();
  for (const row of visibleRows) {
    const bucket = grouped.get(row.categoryId) ?? { label: row.categoryLabel, rows: [] };
    bucket.rows.push(row);
    grouped.set(row.categoryId, bucket);
  }

  const families: PiecePayload[] = [...grouped.entries()].map(([catId, group]) => {
    const rows: PiecePayload[] = group.rows.map((row) => {
      const mag: MagnitudeDisplay | null =
        row.state === "no-producer" || !row.live
          ? null
          : formatDerivedMagnitude(row.live.value, row.unitClass, {
              locale,
              role: "total"
            });
      const rowTheme = row.element
        ? ({ kind: "element", id: row.element.id } as ThemeRef)
        : themeVar;
      return {
        piece: "channel-row",
        instanceId: `row:${row.channelId}`,
        phase: "ready" as Phase,
        channelId: row.channelId,
        familyId: row.familyId,
        title: row.displayName,
        reading: row.reading,
        variantLabel: row.expand !== "none" ? row.variantLabel : null,
        state: row.state as LegoDerivedRenderState,
        kind: rowKind(row.state),
        selected: row.channelId === selectedChannelId,
        valueText: mag?.valueText ?? "—",
        valueRaw: mag?.valueRaw ?? null,
        formatterId: mag?.formatterId ?? null,
        glyphRef: glyphFromFamily(row.icon, row.displayName),
        glyphColor: row.element?.color ?? null,
        themeRef: rowTheme,
        themeResolved: resolveTheme(rowTheme ?? null)
      };
    });
    return {
      piece: "family-block",
      instanceId: `family:${catId}`,
      phase: "ready" as Phase,
      familyId: catId,
      title: group.label,
      hint: `${activeTab?.id ?? "—"} · ${variantId || "—"}`,
      rows
    };
  });

  const selected = visibleRows.find((r) => r.channelId === selectedChannelId) ?? null;
  const inspect = buildInspect(selected, locale, themeVar);

  const foot: PiecePayload = {
    piece: "surface-foot",
    instanceId: "foot:derived",
    phase: "ready",
    hiddenCount,
    note: "expand×join · UnitClass closed",
    deferred: "Deferred in FE v1: live build compare · full xyflow calc graph"
  };

  const phasePayload: PiecePayload = {
    piece:
      phase === "loading"
        ? "phase-loading"
        : phase === "error"
          ? "phase-error"
          : phase === "empty"
            ? "phase-empty"
            : "phase-loading",
    instanceId: `overlay:${phase}`,
    phase,
    message:
      phase === "loading"
        ? "Loading derived channels…"
        : phase === "error"
          ? "Derived sheet unavailable"
          : "No channels in this filter.",
    canRetry: phase === "error",
    retryLabel: "Retry"
  };

  return {
    // No top-level `piece` — children bind to `vm` (e.g. split-inspect) and must not
    // inherit surface-shell's piece id. Root piece comes from the recipe.
    phase,
    revision,
    selectedChannelId,
    selectedInstanceId: selectedChannelId ? `row:${selectedChannelId}` : null,
    search,
    showUnchanged,
    primaryRail,
    variantRail,
    /** List metadata for family-list bind (count drives dock empty). */
    familyList: {
      piece: "family-list",
      instanceId: "families:derived",
      phase: phase === "empty" ? ("empty" as Phase) : ("ready" as Phase),
      count: families.length,
      message: "No channels in this filter."
    },
    families,
    dockScroll: {
      piece: "scroll-region",
      instanceId: "scroll:dock",
      phase: "ready",
      region: "dock"
    },
    inspectScroll: {
      piece: "scroll-region",
      instanceId: "scroll:inspect",
      phase: "ready",
      region: "inspect"
    },
    inspect,
    foot,
    phasePayload,
    themeRef: themeVar ?? sideRef,
    themeResolved: resolveTheme(themeVar ?? sideRef)
  };
}

function buildInspect(
  selected: {
    channelId: string;
    displayName: string;
    reading: string;
    unitClass: string;
    compose: string;
    expand: string;
    icon: string | null;
    variantLabel: string;
    live: LiveChannelView | undefined;
    state: ReturnType<typeof resolveDerivedRenderState>;
    capRef: string | null;
  } | null,
  locale: string,
  themeVar: ThemeRef | undefined
): DerivedSurfaceVm["inspect"] {
  if (!selected) {
    const empty: PiecePayload = {
      piece: "value-hero",
      instanceId: "inspect:hero",
      phase: "empty",
      title: "Select a channel",
      valueText: "—",
      reading: ""
    };
    return {
      piece: "inspect-pane",
      instanceId: "inspect:derived",
      phase: "empty",
      state: null,
      hero: empty,
      meta: { piece: "meta-sentences", instanceId: "inspect:meta", phase: "empty", sentences: [] },
      cap: { piece: "cap-note", instanceId: "inspect:cap", phase: "empty", capKind: "none", text: "" },
      donut: { piece: "gauge-donut", instanceId: "inspect:donut", phase: "empty", slices: [] },
      stack: { piece: "gauge-stack", instanceId: "inspect:stack", phase: "empty", bars: [] },
      sources: { piece: "source-list", instanceId: "inspect:sources", phase: "empty", items: [] }
    };
  }

  const title =
    selected.expand !== "none" && selected.variantLabel
      ? `${selected.displayName} · ${selected.variantLabel}`
      : selected.displayName;
  const mag =
    selected.state === "no-producer" || !selected.live
      ? null
      : formatDerivedMagnitude(selected.live.value, selected.unitClass, {
          locale,
          role: "total"
        });
  const buckets = bucketContributions(selected.live?.contributions ?? []);
  const totalPos = buckets.filter((b) => b.key !== "neg").reduce((a, b) => a + b.value, 0) || 1;
  const max = Math.max(1, ...buckets.map((b) => b.value));
  const cap = registryCapFor(selected.channelId, selected.capRef);

  const hero: PiecePayload = {
    piece: "value-hero",
    instanceId: "inspect:hero",
    phase: "ready",
    title,
    valueText: mag?.valueText ?? "—",
    valueRaw: mag?.valueRaw ?? null,
    reading:
      selected.state === "no-producer"
        ? "Nothing grants this yet."
        : selected.reading,
    state: selected.state,
    glyphRef: glyphFromFamily(selected.icon, selected.displayName),
    themeRef: themeVar,
    themeResolved: resolveTheme(themeVar ?? null)
  };

  const sentences: string[] = [];
  if (selected.state === "no-producer") {
    sentences.push(
      `${selected.channelId} is registered and readable, but no producer writes it (no-producer).`
    );
  } else {
    sentences.push(`Compose: ${COMPOSE_SENTENCE[selected.compose] ?? selected.compose}`);
    sentences.push(`Unit: ${UNIT_SENTENCE[selected.unitClass] ?? selected.unitClass}`);
    sentences.push(`Join: ${selected.channelId}`);
    if (selected.state === "stub") sentences.push("Placeholder — the real curve is not built.");
  }

  const meta: PiecePayload = {
    piece: "meta-sentences",
    instanceId: "inspect:meta",
    phase: "ready",
    sentences
  };

  const capPayload: PiecePayload = {
    piece: "cap-note",
    instanceId: "inspect:cap",
    phase: "ready",
    capKind: cap == null ? "none" : selected.state === "capped" ? "at" : "under",
    text:
      cap == null
        ? "No registry cap on this channel."
        : selected.state === "capped"
          ? `At soft cap ${cap} — the next point does nothing.`
          : `Cap ${cap} — more still counts.`,
    ok: cap == null || selected.state !== "capped"
  };

  const slices = buckets
    .filter((b) => b.key !== "neg")
    .map((b) => ({
      key: b.key,
      label: b.label,
      value: b.value,
      share: Math.round((b.value / totalPos) * 100),
      paint: PAINT_BUCKET[b.key] ?? PAINT_BUCKET.other!
    }));

  const donut: PiecePayload = {
    piece: "gauge-donut",
    instanceId: "inspect:donut",
    phase: slices.length ? "ready" : "empty",
    title: "Why this number",
    slices
  };

  const bars = buckets.map((b) => ({
    key: b.key,
    label: BUCKET_LABELS[b.key] ?? b.label,
    value: b.value,
    barPct: Math.max(4, Math.round((b.value / max) * 100)),
    sharePct: Math.round((b.value / totalPos) * 100),
    valueText: formatDerivedMagnitude(b.value, selected.unitClass, {
      locale,
      role: "delta"
    }).valueText,
    paint: PAINT_BUCKET[b.key] ?? PAINT_BUCKET.other!
  }));

  const stack: PiecePayload = {
    piece: "gauge-stack",
    instanceId: "inspect:stack",
    phase: "ready",
    bars
  };

  const items = (selected.live?.contributions ?? []).map((c, i) => ({
    id: `${c.sourceId}-${i}`,
    label: c.label,
    valueText: formatDerivedMagnitude(c.value, selected.unitClass, {
      locale,
      role: "delta"
    }).valueText
  }));

  const sources: PiecePayload = {
    piece: "source-list",
    instanceId: "inspect:sources",
    phase: "ready",
    title: "Sources (GG-49)",
    items
  };

  return {
    piece: "inspect-pane",
    instanceId: "inspect:derived",
    phase: "ready",
    state: selected.state,
    channelId: selected.channelId,
    hero,
    meta,
    cap: capPayload,
    donut,
    stack,
    sources,
    themeRef: themeVar,
    themeResolved: resolveTheme(themeVar ?? null)
  };
}
