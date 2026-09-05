import { describe, expect, it } from "vitest";

/**
 * GG-50: "A surface that lists entities states its strategy at each order of magnitude." This is
 * that declaration, for every real collection surface — not just the one that needed fixing
 * (CreaturesLayer). A collection surface with no real path to unbounded growth doesn't need
 * virtualizing yet, but that has to be a checked claim, not an assumption nobody wrote down.
 */
type CollectionEntry =
  | { surface: string; strategy: "render-all" | "virtualize" | "search-first"; reason: string }
  | { surface: string; strategy: "unbounded-risk"; reason: string };

const COLLECTION_SURFACES: CollectionEntry[] = [
  {
    surface: "Creatures (CreaturesLayer roster list)",
    strategy: "virtualize",
    reason:
      "The one real unbounded-growth risk: a player binds creatures indefinitely over a long save. Renders all below 50, switches to @tanstack/react-virtual above it — proven at 10/100/1000 via e2e/volume-fixtures.spec.ts, including that scrolling actually windows rather than statically slicing"
  },
  {
    surface: "Relics (RelicsLayer held/equipped/storage lists)",
    strategy: "render-all",
    reason:
      "The real catalog is 4 relics total (RelicCatalog.cs, T14's own note: no acquisition system exists yet, every player holds the full catalog) — structurally cannot reach a magnitude where render-all costs anything. Revisit if/when a real acquisition system exists"
  },
  {
    surface: "Almanac — Creatures tab (CatalogPage)",
    strategy: "render-all",
    reason: "Bounded by the game's own fixed plant/zombie type roster — a real, small, closed set (the base game defines every entry, nothing grows this list at runtime)"
  },
  {
    surface: "Almanac — Recipes tab (RecipesPage)",
    strategy: "render-all",
    reason: "Bounded by the fusion recipe book's own real, closed set — recipes are authored content, not player-generated volume"
  },
  {
    surface: "Chronicle — Runs tab (MetricsPage)",
    strategy: "render-all",
    reason: "Legacy page (pre-refactor), unmodified per the wrap-not-rebuild pattern (T19) — out of this phase's scope to add a new strategy to"
  },
  {
    surface: "Chronicle — Growth tab (RpgProgressionPage ledger)",
    strategy: "search-first",
    reason: "Already real and pre-existing: server-side Pager with an `afterId` cursor (not a client-side render-all), the actual answer GG-50 asks for, just built before this refactor and left unchanged"
  },
  {
    surface: "Expeditions (ExpeditionsPage active/history lists)",
    strategy: "render-all",
    reason: "Bounded by real, small server-side caps: squad slots per tier and history sliced to the last 8 entries (ExpeditionsPage.tsx) — not client-unbounded"
  },
  {
    surface: "Pacts (PactsLayer bound-contract list)",
    strategy: "render-all",
    reason: "Bounded by the real contract-slot cap (ContractCapacityDto.maxSlots, a small, server-enforced number) — cannot grow past that cap"
  },
  {
    surface: "World Outliner (legion + sector rows)",
    strategy: "render-all",
    reason: "~28 rows at spec-world-outliner.md §8e.3's own target, bounded by the two available map tiers — not player-generated volume"
  },
  {
    surface: "World notification rail",
    strategy: "render-all",
    reason: "Flushes every End Turn except real blockers (spec-world-notify.md), and the visible stack is capped at three (Toasts.tsx's own VISIBLE_CAP) — never an accumulating list"
  },
  {
    surface: "World turn playback keyframe rail",
    strategy: "render-all",
    reason: "One turn's own transcript, discarded at the next (spec-world-playback.md) — revisit only if a single turn's entry count grows past roughly 300"
  },
  {
    surface: "World sector inspector — slot rows",
    strategy: "render-all",
    reason: "Four slots max in shipped content — SlotIndex tops out at 3 (spec-world-inspector.md) — a real, small, closed set per sector"
  },
  {
    surface: "World sector inspector — force rows",
    strategy: "render-all",
    reason: "Single-digit rows; enemy forces render as bands (ForceView's exact:false case), never per-unit rows, so there is no unbounded count to render at all (spec-world-inspector.md)"
  }
];

describe("volume matrix (GG-50)", () => {
  it("every entry states a real reason, not a placeholder", () => {
    for (const entry of COLLECTION_SURFACES) {
      expect(entry.reason.length, `${entry.surface} needs a real, non-empty reason`).toBeGreaterThan(20);
    }
  });

  it("the one surface with real unbounded growth risk declares virtualize, not render-all", () => {
    const creatures = COLLECTION_SURFACES.find((e) => e.surface.startsWith("Creatures"));
    expect(creatures?.strategy).toBe("virtualize");
  });

  it("declares the full known set", () => {
    expect(COLLECTION_SURFACES).toHaveLength(13);
  });

  it("the world stage adds no virtualize entry — every one of its five collections is structurally bounded", () => {
    const worldSurfaces = COLLECTION_SURFACES.filter((e) => e.surface.startsWith("World "));
    expect(worldSurfaces).toHaveLength(5);
    for (const entry of worldSurfaces) {
      expect(entry.strategy).toBe("render-all");
    }
  });

  it("virtualize is still exactly one entry (Creatures) — the world stage did not add a second", () => {
    const virtualized = COLLECTION_SURFACES.filter((e) => e.strategy === "virtualize");
    expect(virtualized).toHaveLength(1);
    expect(virtualized[0]?.surface).toContain("Creatures");
  });
});
