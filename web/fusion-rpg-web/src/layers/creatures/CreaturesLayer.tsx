import { useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useUniqueActors } from "@/lib/bus";
import { adaptActor } from "@/contract/adapt";

// Same pattern RelicsLayer.tsx already uses for this exact situation: a layer may never import a
// raw REST DTO type directly (contractGuard.ts, T4's sealed-contract rule) — derive it from the
// adapter function's own parameter instead.
type UniqueActorDto = Parameters<typeof adaptActor>[0];
import { cn } from "@/lib/cn";
import { PanelShell } from "@/shell/PanelShell";
import { Banner, Button, Select, TextInput } from "@/ui";
import { ActorCard, ActorRow, type ActorRungState } from "@/ui/actor";
import { EmptyState } from "@/ui/EmptyState";

// GG-50/GG-51 (plate 02 §D): every collection declares its behaviour at three magnitudes — ≤24
// renders everything, 25–240 windows the render, and above 240 the grid starts empty ("search-first")
// until a search or filter narrows it, so the layer never maps an unbounded array into the DOM.
// T27: the old single `VIRTUALIZE_ABOVE = 50` cutoff only had two of those three tiers.
const RENDER_ALL_MAX = 24;
const SEARCH_FIRST_ABOVE = 240;
const ESTIMATED_ROW_HEIGHT = 56;
const LIST_HEIGHT_PX = 320;

type SideFilter = "all" | "plant" | "zombie";
type SortOrder = "level-desc" | "level-asc";

const SIDE_FILTERS: { id: SideFilter; label: string }[] = [
  { id: "all", label: "All" },
  { id: "plant", label: "Plant" },
  { id: "zombie", label: "Zombie" }
];

/**
 * No creature has a resolved display name yet — `adaptActor`'s `displayName` is `Pending`
 * ("Names resolve from the almanac catalog, not wired to this reader yet"). Search matches the
 * same real fields the row itself renders (side, level, phase) instead of faking a name index.
 */
function searchableText(actor: UniqueActorDto): string {
  return `${actor.side} lvl ${actor.level} ${actor.phase}`.toLowerCase();
}

function CreatureRow({
  actor,
  selectedId,
  onSelect
}: {
  actor: UniqueActorDto;
  selectedId: string | null;
  onSelect: (instanceId: string | null) => void;
}) {
  return (
    <button
      type="button"
      data-testid={`creatures-row-${actor.instanceId}`}
      data-selected={actor.instanceId === selectedId}
      onClick={() => onSelect(actor.instanceId === selectedId ? null : actor.instanceId)}
      className="block w-full text-left focus-visible:bg-panel-raised aria-current:bg-panel-raised"
      aria-current={actor.instanceId === selectedId}
    >
      <ActorRow state={{ kind: "ready", data: adaptActor(actor) }} />
    </button>
  );
}

function VirtualCreatureList({
  actors,
  selectedId,
  onSelect
}: {
  actors: UniqueActorDto[];
  selectedId: string | null;
  onSelect: (instanceId: string | null) => void;
}) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const virtualizer = useVirtualizer({
    count: actors.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT,
    overscan: 8
  });

  return (
    <div
      ref={scrollRef}
      className="overflow-y-auto rounded-md border border-border"
      style={{ height: LIST_HEIGHT_PX }}
      data-testid="creatures-list"
      data-virtualized="true"
    >
      <div style={{ height: virtualizer.getTotalSize(), position: "relative", width: "100%" }}>
        {virtualizer.getVirtualItems().map((row) => (
          <div
            key={row.key}
            data-index={row.index}
            ref={virtualizer.measureElement}
            style={{ position: "absolute", top: 0, left: 0, width: "100%", transform: `translateY(${row.start}px)` }}
          >
            <CreatureRow actor={actors[row.index]!} selectedId={selectedId} onSelect={onSelect} />
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * The bound roster (information-architecture.md §3: `C`, replaces
 * `/roster`). Ladder rungs: row for the list, card for the selected
 * creature's detail — the same `ActorView` contract T8 built, never a
 * second rendering of the same data (GG-9). No `typeId` anywhere: every
 * label here comes from the adapted view, not the raw DTO.
 */
export function CreaturesLayer({
  open,
  onOpenChange,
  playerId,
  selectedId,
  onSelect
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  playerId: number;
  selectedId: string | null;
  onSelect: (instanceId: string | null) => void;
}) {
  const navigate = useNavigate();
  const query = useUniqueActors(playerId);
  const actors = query.data?.items ?? [];
  const selected = selectedId ? actors.find((a) => a.instanceId === selectedId) : undefined;
  const selectedState: ActorRungState | null = selected ? { kind: "ready", data: adaptActor(selected) } : null;

  // T27/GG-51: search text, filter and sort belong to the layer, not the DOM node — plain `useState`
  // here already satisfies "survives a close/reopen within the session", since `CreaturesLayer`'s own
  // component instance never unmounts once opened (`SanctumStage.tsx`'s `mountedLayers` gate keeps it
  // mounted, and `PanelShell`'s Radix `Dialog` only toggles visibility of what THIS component passes
  // it as children) — the same reason `SystemLayer`'s toggles don't need special persistence either.
  const [searchText, setSearchText] = useState("");
  const [sideFilter, setSideFilter] = useState<SideFilter>("all");
  const [sortOrder, setSortOrder] = useState<SortOrder>("level-desc");

  const totalCount = actors.length;
  const volumeTier: "all" | "windowed" | "search-first" =
    totalCount <= RENDER_ALL_MAX ? "all" : totalCount <= SEARCH_FIRST_ABOVE ? "windowed" : "search-first";
  const hasActiveQuery = searchText.trim().length > 0 || sideFilter !== "all";

  const filtered = useMemo(() => {
    const q = searchText.trim().toLowerCase();
    const matched = actors.filter(
      (a) => (sideFilter === "all" || a.side === sideFilter) && (!q || searchableText(a).includes(q))
    );
    return [...matched].sort((a, b) => (sortOrder === "level-desc" ? b.level - a.level : a.level - b.level));
  }, [actors, searchText, sideFilter, sortOrder]);

  const showSearchFirstPrompt = volumeTier === "search-first" && !hasActiveQuery;

  return (
    <PanelShell
      open={open}
      onOpenChange={onOpenChange}
      title="Creatures"
      subtitle={`${actors.length} bound`}
      testId="creatures-layer"
    >
      {query.isLoading ? (
        <p className="text-sm text-muted" data-testid="creatures-loading" aria-busy="true">
          Loading creatures…
        </p>
      ) : query.isError ? (
        <Banner tone="error" data-testid="creatures-error">
          Couldn't load your creatures.
          <Button size="sm" variant="ghost" className="ml-2" onClick={() => void query.refetch()}>
            Retry
          </Button>
        </Banner>
      ) : actors.length === 0 ? (
        <EmptyState
          title="No creatures bound yet"
          hint="Bind one to see it here — the roster fills in as you play."
        />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2" data-testid="creatures-controls">
            <TextInput
              data-testid="creatures-search"
              placeholder="Search by side, level, phase…"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              className="max-w-[220px]"
            />
            <div className="flex gap-1" data-testid="creatures-filter-side">
              {SIDE_FILTERS.map((f) => (
                <button
                  key={f.id}
                  type="button"
                  data-testid={`creatures-filter-${f.id}`}
                  aria-current={sideFilter === f.id}
                  onClick={() => setSideFilter(f.id)}
                  className={cn(
                    "rounded-sm border px-2 py-1 text-xs",
                    sideFilter === f.id ? "border-lawn-hot bg-lawn text-text" : "border-border text-muted hover:bg-panel"
                  )}
                >
                  {f.label}
                </button>
              ))}
            </div>
            <Select
              data-testid="creatures-sort"
              aria-label="Sort creatures"
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value as SortOrder)}
            >
              <option value="level-desc">Level (high to low)</option>
              <option value="level-asc">Level (low to high)</option>
            </Select>
          </div>

          <p className="text-xs text-muted" data-testid="creatures-rung-note">
            Rows, not the plate's card grid — creature names aren't resolved yet (only side, level and
            phase are real), and rows keep the virtualized list's row-height math simple at volume.
          </p>

          {showSearchFirstPrompt ? (
            <EmptyState
              testId="creatures-search-first-prompt"
              title={`${totalCount.toLocaleString()} creatures`}
              hint="Search or filter to see them — this roster is too large to render at once."
            />
          ) : filtered.length === 0 ? (
            <EmptyState testId="creatures-no-match" title="No creatures match" hint="Try a different search or filter." />
          ) : filtered.length > RENDER_ALL_MAX ? (
            <VirtualCreatureList actors={filtered} selectedId={selectedId} onSelect={onSelect} />
          ) : (
            <div className="rounded-md border border-border" data-testid="creatures-list">
              {filtered.map((actor) => (
                <CreatureRow key={actor.instanceId} actor={actor} selectedId={selectedId} onSelect={onSelect} />
              ))}
            </div>
          )}

          {selectedState ? (
            <div data-testid="creatures-detail">
              <p className="mb-2 text-xs font-bold uppercase tracking-wide text-muted">Selected</p>
              <ActorCard state={selectedState} />
              {selected?.phase === "Roster" ? (
                <Button
                  size="sm"
                  className="mt-2"
                  data-testid="creatures-deploy"
                  onClick={() => navigate(`/lawn?deploy=${encodeURIComponent(selected.instanceId)}`)}
                >
                  Deploy to the lawn
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>
      )}
    </PanelShell>
  );
}
