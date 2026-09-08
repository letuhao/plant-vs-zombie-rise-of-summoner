import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import aptitudeCatalogJson from "../../../../../data/tuning/aptitude-catalog.v1.json";
import derivedCatalogJson from "../../../../../data/tuning/derived-stat-catalog.v2.json";
import elementCatalogJson from "../../../../../data/tuning/element-catalog.v1.json";
import resourceCatalogJson from "../../../../../data/tuning/resource-catalog.v1.json";
import statusCatalogJson from "../../../../../data/tuning/status-catalog.v1.json";
import actorSheetJson from "../../../../../data/tuning/actor-sheet.v1.json";
import { tryGetJson } from "./rest";

export type ActorSheetTabKind =
  | "condition"
  | "aptitudes"
  | "derived"
  | "shield"
  | "status"
  | "elements"
  | "kit"
  | "paths";

export type DerivedExpandKind =
  | "none"
  | "element"
  | "status-category"
  | "status-id"
  | "resource"
  | "action-category";

export type ActorSurfaceTab = {
  kind: ActorSheetTabKind;
  label: string;
  order: number;
  hidden: boolean;
};

export type AptitudeCatalogRow = {
  id: string;
  posture: string;
  ordinal: number;
  displayName: string;
  role: string;
  reading: string;
};

export type DerivedFamilyCatalogRow = {
  family: string;
  expand: DerivedExpandKind;
  compose: string;
  unitClass: string;
  displayName: string;
  reading: string;
  icon: string;
  gauge: string;
  sheetGroup: string;
  capRef: string | null;
};

export type ResourceCatalogRow = {
  id: string;
  class: string;
  exhaustion: boolean;
  actionCost: boolean;
  labels: { plant: string; zombie: string };
  icon: string;
  color: string;
  meterKind: string;
};

export type ElementCatalogRow = {
  id: string;
  displayName: string;
  ordinal: number;
  color: string;
  presentationOnly: boolean;
};

export type StatusCatalogRow = {
  id: string;
  displayName: string;
  reading: string;
  hudToken: string;
  color: string;
};

export type KitRoleCatalogRow = {
  roleId: string;
  labels: { humanoid: string; plant: string };
};

export type ActorSurfaceCatalog = {
  tabs: ActorSurfaceTab[];
  defaultOpen: ActorSheetTabKind;
  aptitudes: AptitudeCatalogRow[];
  families: DerivedFamilyCatalogRow[];
  resources: ResourceCatalogRow[];
  elements: ElementCatalogRow[];
  statuses: StatusCatalogRow[];
  kitRoles: KitRoleCatalogRow[];
  versionStamp: string;
};

type LocaleMap = Record<string, string>;

function localeEn(value: string | LocaleMap): string {
  if (typeof value === "string") return value;
  return value.en ?? Object.values(value)[0] ?? "";
}

const derivedFamilies: DerivedFamilyCatalogRow[] = derivedCatalogJson.entries.map((entry) => ({
  family: entry.family,
  expand: entry.expand as DerivedExpandKind,
  compose: entry.compose,
  unitClass: entry.unitClass,
  displayName: localeEn(entry.displayName as string | LocaleMap),
  reading: localeEn(entry.reading as string | LocaleMap),
  icon: entry.icon,
  gauge: entry.gauge,
  sheetGroup: entry.sheetGroup,
  capRef: entry.capRef ?? null
}));

const fixtureCatalog: ActorSurfaceCatalog = {
  tabs: actorSheetJson.tabs as ActorSurfaceTab[],
  defaultOpen: actorSheetJson.defaultOpen as ActorSheetTabKind,
  aptitudes: aptitudeCatalogJson.entries,
  families: derivedFamilies,
  resources: resourceCatalogJson.entries,
  elements: elementCatalogJson.entries,
  statuses: statusCatalogJson.entries,
  kitRoles: actorSheetJson.kitRoles,
  versionStamp: [
    actorSheetJson.version,
    aptitudeCatalogJson.version,
    derivedCatalogJson.version,
    resourceCatalogJson.version,
    elementCatalogJson.version,
    statusCatalogJson.version
  ].join(".")
};

declare global {
  interface Window {
    /** ActorSheet and Band B share this catalog identity lookup. */
    __fusionRpgActorSurface?: ActorSurfaceCatalog;
  }
}

if (typeof window !== "undefined" && !window.__fusionRpgActorSurface) {
  window.__fusionRpgActorSurface = fixtureCatalog;
}

export function actorSurfaceFixture(): ActorSurfaceCatalog {
  return fixtureCatalog;
}

export async function fetchActorSurfaceCatalog(): Promise<ActorSurfaceCatalog> {
  try {
    return (await tryGetJson<ActorSurfaceCatalog>("/api/catalogs/actor-surface")) ?? fixtureCatalog;
  } catch {
    // FE-only catalog-era bridge. Remove this fallback once every shipped Server exposes the endpoint.
    return fixtureCatalog;
  }
}

export type DerivedSurfaceVariant = {
  id: string;
  displayName: string;
  ordinal: number;
  presentationOnly: boolean;
};

export type DerivedSurfaceFamily = {
  family: string;
  displayName: string;
  reading: string;
  compose: string;
  unitClass: string;
  icon: string;
  gauge: string;
  capRef: string | null;
  expand: DerivedExpandKind;
  channelPattern: string;
};

export type DerivedSurfaceCategory = {
  id: string;
  displayName: string;
  order: number;
  families: DerivedSurfaceFamily[];
};

export type DerivedSurfaceTab = {
  id: string;
  displayName: string;
  order: number;
  expand: DerivedExpandKind;
  variants: DerivedSurfaceVariant[];
  actionCategoryVariants?: DerivedSurfaceVariant[] | null;
  categories: DerivedSurfaceCategory[];
};

export type DerivedSurfaceDto = {
  lang: string;
  side: string;
  schemaVersion: number;
  versionStamp: string;
  tabs: DerivedSurfaceTab[];
};

/** Offline / missing-endpoint: cook-shaped surface mirroring DerivedSurfaceCook tabs. */
export function derivedSurfaceFromFixture(
  surface: ActorSurfaceCatalog = fixtureCatalog,
  side = "plant"
): DerivedSurfaceDto {
  const raw = derivedCatalogJson as {
    tabs: { id: string; displayName: string | LocaleMap; order: number; expand: string }[];
    sheetGroups: { id: string; tab: string; displayName: string | LocaleMap; order: number }[];
    statusCategoryVariants: {
      id: string;
      displayName: string | LocaleMap;
      ordinal: number;
      presentationOnly?: boolean;
    }[];
    actionCategoryVariants: { id: string; displayName: string | LocaleMap; ordinal: number }[];
  };

  const byGroup = new Map<string, DerivedFamilyCatalogRow[]>();
  for (const family of surface.families) {
    const list = byGroup.get(family.sheetGroup) ?? [];
    list.push(family);
    byGroup.set(family.sheetGroup, list);
  }

  const toFamily = (f: DerivedFamilyCatalogRow): DerivedSurfaceFamily => ({
    family: f.family,
    displayName: f.displayName,
    reading: f.reading,
    compose: f.compose,
    unitClass: f.unitClass,
    icon: f.icon,
    gauge: f.gauge,
    capRef: f.capRef,
    expand: f.expand,
    channelPattern: f.expand === "none" ? "{family}" : "{family}.{variant}"
  });

  const tabs: DerivedSurfaceTab[] = [...raw.tabs]
    .sort((a, b) => a.order - b.order)
    .map((tab) => {
      const categories: DerivedSurfaceCategory[] = raw.sheetGroups
        .filter((g) => g.tab === tab.id)
        .sort((a, b) => a.order - b.order)
        .map((g) => ({
          id: g.id,
          displayName: localeEn(g.displayName),
          order: g.order,
          families: (byGroup.get(g.id) ?? []).map(toFamily)
        }));

      let variants: DerivedSurfaceVariant[] = [];
      let actionCategoryVariants: DerivedSurfaceVariant[] | null = null;

      if (tab.id === "elements") {
        variants = surface.elements
          .slice()
          .sort((a, b) => a.ordinal - b.ordinal)
          .map((e) => ({
            id: e.id,
            displayName: e.displayName,
            ordinal: e.ordinal,
            presentationOnly: e.presentationOnly
          }));
      } else if (tab.id === "status") {
        variants = [
          {
            id: "omni",
            displayName: "Omni",
            ordinal: 0,
            presentationOnly: true
          },
          ...surface.statuses
            .slice()
            .sort((a, b) => a.id.localeCompare(b.id))
            .map((s, i) => ({
              id: s.id,
              displayName: s.displayName,
              ordinal: i + 1,
              presentationOnly: false
            }))
        ];
      } else if (tab.id === "resources") {
        variants = surface.resources.map((r, i) => ({
          id: r.id,
          displayName: side === "zombie" ? r.labels.zombie : r.labels.plant,
          ordinal: i,
          presentationOnly: false
        }));
      } else if (tab.id === "other") {
        actionCategoryVariants = raw.actionCategoryVariants
          .slice()
          .sort((a, b) => a.ordinal - b.ordinal)
          .map((v) => ({
            id: v.id,
            displayName: localeEn(v.displayName),
            ordinal: v.ordinal,
            presentationOnly: false
          }));
      }

      return {
        id: tab.id,
        displayName: localeEn(tab.displayName),
        order: tab.order,
        expand: tab.expand as DerivedExpandKind,
        variants,
        actionCategoryVariants,
        categories
      };
    });

  return {
    lang: "en",
    side,
    schemaVersion: 2,
    versionStamp: surface.versionStamp,
    tabs
  };
}

export async function fetchDerivedSurface(lang = "en", side = "plant"): Promise<DerivedSurfaceDto> {
  try {
    const cooked = await tryGetJson<DerivedSurfaceDto>(
      `/api/catalogs/derived-surface?lang=${encodeURIComponent(lang)}&side=${encodeURIComponent(side)}`
    );
    if (cooked?.tabs?.length) return cooked;
  } catch {
    /* fall through to fixture cook-shape */
  }
  return derivedSurfaceFromFixture(fixtureCatalog, side);
}

export function useDerivedSurface(lang = "en", side = "plant") {
  return useQuery({
    queryKey: ["derivedSurface", lang, side] as const,
    queryFn: () => fetchDerivedSurface(lang, side),
    staleTime: Infinity
  });
}

export function useActorSurfaceCatalog() {
  const query = useQuery({
    queryKey: ["actorSurfaceCatalog"] as const,
    queryFn: fetchActorSurfaceCatalog,
    staleTime: Infinity
  });

  useEffect(() => {
    if (query.data) window.__fusionRpgActorSurface = query.data;
  }, [query.data]);

  return query;
}

export function actorSurfaceCatalogNow(): ActorSurfaceCatalog {
  return window.__fusionRpgActorSurface ?? fixtureCatalog;
}
