import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import aptitudeCatalogJson from "../../../../../data/tuning/aptitude-catalog.v1.json";
import derivedCatalogJson from "../../../../../data/tuning/derived-stat-catalog.v1.json";
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
  axis: string;
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

const fixtureCatalog: ActorSurfaceCatalog = {
  tabs: actorSheetJson.tabs as ActorSurfaceTab[],
  defaultOpen: actorSheetJson.defaultOpen as ActorSheetTabKind,
  aptitudes: aptitudeCatalogJson.entries,
  families: derivedCatalogJson.entries,
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
