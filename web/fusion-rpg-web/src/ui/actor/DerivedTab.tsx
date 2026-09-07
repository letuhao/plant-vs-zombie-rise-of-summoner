import { useMemo, useState } from "react";
import { Sparkline } from "react-tiny-sparkline";
import type {
  ActorSurfaceCatalog,
  DerivedFamilyCatalogRow,
  ElementCatalogRow
} from "@/lib/bus/actorSurface";
import { useActorDerived, type DerivedChannelDto } from "@/lib/bus/aura";
import type { ActorView } from "@/contract/types";
import { ChannelContributions } from "./ChannelContributions";

export type ExpandedDerivedChannel = {
  channelId: string;
  element: ElementCatalogRow | null;
};

const STATUS_CATEGORY_VARIANTS = ["omni", "dot", "cc", "contagion"] as const;
const ACTION_CATEGORY_VARIANTS = ["attack", "defense", "support", "movement", "status"] as const;

export function expandDerivedFamily(
  family: DerivedFamilyCatalogRow,
  elements: ElementCatalogRow[],
  resources: { id: string }[] = []
): ExpandedDerivedChannel[] {
  switch (family.expand) {
    case "element":
      return [...elements]
        .sort((a, b) => a.ordinal - b.ordinal)
        .map((element) => ({ channelId: `${family.family}.${element.id}`, element }));
    case "status-category":
      return STATUS_CATEGORY_VARIANTS.map((id) => ({
        channelId: `${family.family}.${id}`,
        element: null
      }));
    case "resource":
      return resources.map((r) => ({ channelId: `${family.family}.${r.id}`, element: null }));
    case "action-category":
      return ACTION_CATEGORY_VARIANTS.map((id) => ({
        channelId: `${family.family}.${id}`,
        element: null
      }));
    case "none":
    default:
      return [{ channelId: family.family, element: null }];
  }
}

export function DerivedTab({
  data,
  surface
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
}) {
  const derived = useActorDerived(data.instanceId);
  const groups = [...new Set(surface.families.map((family) => family.sheetGroup))];
  const [openGroup, setOpenGroup] = useState(groups[0] ?? "");
  const byId = useMemo(
    () => new Map((derived.data?.channels ?? []).map((channel) => [channel.channelId, channel])),
    [derived.data]
  );

  return (
    <div className="mt-4" data-testid="derived-tab">
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Derived stat groups">
        {groups.map((group) => (
          <button
            key={group}
            type="button"
            role="tab"
            aria-selected={openGroup === group}
            onClick={() => setOpenGroup(group)}
            className={
              openGroup === group
                ? "rounded-sm bg-lawn px-3 py-1.5 text-sm text-text"
                : "rounded-sm bg-panel-inset px-3 py-1.5 text-sm text-muted"
            }
          >
            {group}
          </button>
        ))}
      </div>

      {derived.isLoading ? <p className="mt-4 text-xs text-muted">Loading derived stats…</p> : null}
      {derived.isError ? <p className="mt-4 text-xs text-muted">Derived stats are unavailable right now.</p> : null}

      <div className="mt-3 space-y-3">
        {surface.families
          .filter((family) => family.sheetGroup === openGroup)
          .map((family) => (
            <DerivedFamilyRow
              key={family.family}
              family={family}
              expanded={expandDerivedFamily(family, surface.elements, surface.resources)}
              byId={byId}
            />
          ))}
      </div>
    </div>
  );
}

function DerivedFamilyRow({
  family,
  expanded,
  byId
}: {
  family: DerivedFamilyCatalogRow;
  expanded: ExpandedDerivedChannel[];
  byId: Map<string, DerivedChannelDto>;
}) {
  const [open, setOpen] = useState(false);
  const values = expanded.map((entry) => byId.get(entry.channelId)?.value ?? 0);

  return (
    <article className="rounded-sm border border-border-control bg-panel-inset" data-testid={`derived-family-${family.family}`}>
      <button
        type="button"
        className="flex w-full items-center gap-3 p-3 text-left"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="min-w-0 flex-1">
          <span className="block font-ui text-sm text-text">{family.displayName}</span>
          <span className="block truncate text-xs text-muted">{family.reading}</span>
        </span>
        <Sparkline
          data={values.length > 1 ? values : [0, values[0] ?? 0]}
          width={88}
          height={28}
          animate={false}
          aria-label={`${family.displayName} values`}
          className="text-lawn-hot"
        />
        <span aria-hidden="true" className="text-muted">
          {open ? "−" : "+"}
        </span>
      </button>

      {open ? (
        <ul className="border-t border-border px-3 py-2" data-testid={`derived-family-expanded-${family.family}`}>
          {expanded.map((entry) => {
            const channel = byId.get(entry.channelId);
            return (
              <li
                key={entry.channelId}
                className="border-b border-border/60 py-2 last:border-0"
                data-testid={`derived-channel-${entry.channelId}`}
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm text-text">{entry.element?.displayName ?? family.displayName}</span>
                  <span className="font-mono text-sm text-text">
                    {channel ? channel.value.toLocaleString() : "No producer"}
                  </span>
                </div>
                {channel && channel.contributions.length > 0 ? (
                  <ChannelContributions contributions={channel.contributions} />
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}
    </article>
  );
}
