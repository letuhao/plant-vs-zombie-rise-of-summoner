/**
 * ActorSheet → Derived. Compose only: hooks → cook model → console shell.
 * Visual SSOT = docs/design/derived-combat-console.html.
 * Structure: tasks/actor-sheet-derived-structure.md
 */
import { useEffect, useMemo, useState } from "react";
import { isKnown } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import {
  derivedSurfaceFromFixture,
  type ActorSurfaceCatalog,
  useDerivedSurface
} from "@/lib/bus/actorSurface";
import { useActorDerived, useActorSheet } from "@/lib/bus/aura";
import { DerivedCombatConsole } from "./derived/DerivedCombatConsole";
import {
  COOK_PRIMARY_TAB_IDS,
  FORBIDDEN_PRIMARY_TAB_IDS,
  SHOW_UNCHANGED_KEY,
  bucketContributions,
  expandDerivedFamily,
  isUnchangedState,
  joinDerivedChannelId,
  resolveDerivedRenderState,
  toLiveMap,
  type DerivedRowModel,
  type DerivedRenderState,
  type ExpandedDerivedChannel,
  type LiveChannelView
} from "./derived/derivedCook";

export {
  COOK_PRIMARY_TAB_IDS,
  FORBIDDEN_PRIMARY_TAB_IDS,
  bucketContributions,
  expandDerivedFamily,
  isUnchangedState,
  joinDerivedChannelId,
  resolveDerivedRenderState,
  type DerivedRenderState,
  type ExpandedDerivedChannel,
  type LiveChannelView
};

export function DerivedTab({
  data,
  surface
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
}) {
  const cook = useDerivedSurface("en", data.side);
  const sheet = useActorSheet(data.instanceId);
  const derived = useActorDerived(data.instanceId);

  const [showUnchanged, setShowUnchanged] = useState(() => {
    try {
      return localStorage.getItem(SHOW_UNCHANGED_KEY) === "1";
    } catch {
      return false;
    }
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [tabId, setTabId] = useState("elements");
  const [variantId, setVariantId] = useState<string | null>(null);

  const tabs = useMemo(() => {
    const cooked = cook.data?.tabs?.length
      ? cook.data.tabs
      : derivedSurfaceFromFixture(surface, data.side).tabs;
    return [...cooked].sort((a, b) => a.order - b.order);
  }, [cook.data, surface, data.side]);

  const activeTab = tabs.find((t) => t.id === tabId) ?? tabs[0] ?? null;

  const variantChoices = useMemo(() => {
    if (!activeTab) return [] as { id: string; displayName: string; presentationOnly?: boolean }[];
    if (activeTab.id === "other") {
      return (activeTab.actionCategoryVariants ?? []).map((v) => ({
        id: v.id,
        displayName: v.displayName,
        presentationOnly: v.presentationOnly
      }));
    }
    return activeTab.variants.map((v) => ({
      id: v.id,
      displayName: v.displayName,
      presentationOnly: v.presentationOnly
    }));
  }, [activeTab]);

  useEffect(() => {
    if (!tabs.some((t) => t.id === tabId) && tabs[0]) setTabId(tabs[0].id);
  }, [tabs, tabId]);

  useEffect(() => {
    if (variantChoices.length === 0) {
      setVariantId(null);
      return;
    }
    if (!variantId || !variantChoices.some((v) => v.id === variantId)) {
      const preferFire =
        activeTab?.id === "elements" && variantChoices.some((v) => v.id === "fire")
          ? "fire"
          : variantChoices[0]!.id;
      setVariantId(preferFire);
    }
  }, [variantChoices, variantId, activeTab?.id]);

  const byId = useMemo(
    () => toLiveMap(sheet.data?.derived, derived.data?.channels),
    [sheet.data, derived.data]
  );

  const familyRows = useMemo(() => {
    if (!activeTab) return [] as DerivedRowModel[];
    const rows: DerivedRowModel[] = [];
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
            ? (surface.elements.find((e) => e.id === channelVariant) ?? null)
            : null;
        const entry: ExpandedDerivedChannel = {
          channelId,
          element,
          variantLabel
        };
        const live = byId.get(channelId);
        const state = resolveDerivedRenderState(channelId, live, family.capRef);
        rows.push({
          family,
          categoryId: cat.id,
          categoryLabel: cat.displayName,
          entry,
          live,
          state,
          displayName: family.displayName
        });
      }
    }
    return rows;
  }, [activeTab, variantId, variantChoices, byId, surface.elements]);

  const q = query.trim().toLowerCase();
  const visibleRows = familyRows.filter((row) => {
    if (
      q &&
      !`${row.displayName} ${row.entry.channelId} ${row.family.reading} ${row.categoryLabel}`
        .toLowerCase()
        .includes(q)
    ) {
      return false;
    }
    if (!showUnchanged && isUnchangedState(row.state) && row.entry.channelId !== selectedId) {
      return false;
    }
    return true;
  });
  const hiddenCount = familyRows.length - visibleRows.length;

  useEffect(() => {
    if (selectedId && visibleRows.some((r) => r.entry.channelId === selectedId)) return;
    const firstActive = visibleRows.find((r) => r.state === "active") ?? visibleRows[0];
    setSelectedId(firstActive?.entry.channelId ?? null);
  }, [visibleRows, selectedId]);

  const selected = visibleRows.find((r) => r.entry.channelId === selectedId) ?? null;
  const loading = sheet.isLoading && derived.isLoading;
  const errored = sheet.isError && derived.isError;

  const groupedVisible = useMemo(() => {
    const map = new Map<string, { label: string; rows: DerivedRowModel[] }>();
    for (const row of visibleRows) {
      const bucket = map.get(row.categoryId) ?? { label: row.categoryLabel, rows: [] };
      bucket.rows.push(row);
      map.set(row.categoryId, bucket);
    }
    return [...map.entries()] as [string, { label: string; rows: DerivedRowModel[] }][];
  }, [visibleRows]);

  const displayName = isKnown(data.displayName) ? data.displayName.value : data.instanceId;

  return (
    <DerivedCombatConsole
      displayName={displayName}
      level={data.level}
      side={data.side}
      loading={loading}
      errored={errored}
      tabs={tabs}
      activeTab={activeTab}
      tabId={activeTab?.id ?? tabId}
      onTabId={setTabId}
      variantChoices={variantChoices}
      variantId={variantId}
      onVariantId={setVariantId}
      query={query}
      onQuery={setQuery}
      showUnchanged={showUnchanged}
      onShowUnchanged={setShowUnchanged}
      groupedVisible={groupedVisible}
      visibleCount={visibleRows.length}
      hiddenCount={hiddenCount}
      selectedId={selectedId}
      selected={selected}
      onSelect={setSelectedId}
    />
  );
}
