/**
 * Derived combat console shell — owns HTML landmark tree.
 * Visual SSOT = docs/design/derived-combat-console.html.
 * .inspect-split MUST be a direct child of .console (CSS > combinators).
 */
import type { DerivedSurfaceTab } from "@/lib/bus/actorSurface";
import { SHOW_UNCHANGED_KEY, type DerivedRowModel } from "./derivedCook";
import { DerivedChannelRow } from "./DerivedChannelRow";
import { DerivedInspector } from "./DerivedInspector";
import "../DerivedCombatConsole.css";

export type DerivedVariantChoice = {
  id: string;
  displayName: string;
  presentationOnly?: boolean;
};

export type DerivedCombatConsoleProps = {
  displayName: string;
  level: number;
  side: string;
  loading: boolean;
  errored: boolean;
  tabs: DerivedSurfaceTab[];
  activeTab: DerivedSurfaceTab | null;
  tabId: string;
  onTabId: (id: string) => void;
  variantChoices: DerivedVariantChoice[];
  variantId: string | null;
  onVariantId: (id: string) => void;
  query: string;
  onQuery: (q: string) => void;
  showUnchanged: boolean;
  onShowUnchanged: (next: boolean) => void;
  groupedVisible: [string, { label: string; rows: DerivedRowModel[] }][];
  visibleCount: number;
  hiddenCount: number;
  selectedId: string | null;
  selected: DerivedRowModel | null;
  onSelect: (channelId: string) => void;
};

export function DerivedCombatConsole(props: DerivedCombatConsoleProps) {
  const {
    displayName,
    level,
    side,
    loading,
    errored,
    tabs,
    activeTab,
    tabId,
    onTabId,
    variantChoices,
    variantId,
    onVariantId,
    query,
    onQuery,
    showUnchanged,
    onShowUnchanged,
    groupedVisible,
    visibleCount,
    hiddenCount,
    selectedId,
    selected,
    onSelect
  } = props;

  const variantAria =
    activeTab?.id === "elements"
      ? "Element variants"
      : activeTab?.id === "status"
        ? "Status category variants"
        : activeTab?.id === "resources"
          ? "Resource variants"
          : "Action category variants";

  return (
    <div
      className="derived-combat-console console"
      data-testid="derived-combat-console"
      data-derived-root="1"
    >
      <header className="console-hd" data-testid="derived-tab">
        <div className="identity">
          <div className="who">{displayName}</div>
          <div className="meta">
            Lv <b>{level}</b> · {side} · cook <b>{activeTab?.id ?? "—"}</b>
            {loading ? " · loading…" : null}
            {errored ? " · unavailable" : null}
          </div>
        </div>
        <div className="hd-tools">
          <input
            className="search"
            type="search"
            value={query}
            onChange={(e) => onQuery(e.target.value)}
            placeholder="Search channels…"
            aria-label="Search derived channels"
            data-testid="derived-search"
          />
          <label
            className={`toggle${showUnchanged ? " is-on" : ""}`}
            title="Persist in localStorage"
          >
            <input
              type="checkbox"
              checked={showUnchanged}
              data-testid="derived-show-unchanged"
              aria-label="Show unchanged"
              style={{ position: "absolute", opacity: 0, width: 1, height: 1 }}
              onChange={(e) => {
                const next = e.target.checked;
                onShowUnchanged(next);
                try {
                  localStorage.setItem(SHOW_UNCHANGED_KEY, next ? "1" : "0");
                } catch {
                  /* ignore */
                }
              }}
            />
            <span className="track" aria-hidden="true">
              <span className="knob" />
            </span>
            Show unchanged
          </label>
        </div>
      </header>

      <nav
        className="cat-bar"
        role="tablist"
        aria-label="Derived surface tabs"
        data-testid="derived-primary-tablist"
      >
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`chip${tabId === tab.id ? " is-on" : ""}`}
            aria-selected={tabId === tab.id}
            data-tab={tab.id}
            data-testid={`derived-tab-${tab.id}`}
            onClick={() => onTabId(tab.id)}
          >
            {tab.displayName}{" "}
            <span className="n">{tab.categories.reduce((n, c) => n + c.families.length, 0)}</span>
          </button>
        ))}
      </nav>

      {variantChoices.length > 0 ? (
        <nav
          className="cat-bar variant-bar"
          role="tablist"
          aria-label={variantAria}
          data-testid="derived-variant-rail"
        >
          {variantChoices.map((v) => (
            <button
              key={v.id}
              type="button"
              role="tab"
              className={`chip${variantId === v.id ? " is-on" : ""}`}
              aria-selected={variantId === v.id}
              data-v={v.id}
              data-el={activeTab?.id === "elements" ? v.id : undefined}
              data-testid={`derived-variant-${v.id}`}
              onClick={() => onVariantId(v.id)}
            >
              {v.displayName}
            </button>
          ))}
        </nav>
      ) : (
        <nav className="cat-bar variant-bar" hidden aria-hidden="true" />
      )}

      <div className="inspect-split" data-testid="derived-inspect">
        <div className="dock">
          <div className="list-pane" data-testid="derived-list">
            {groupedVisible.map(([catId, group]) => (
              <div key={catId} className="family-block">
                <div className="family-hd">
                  <h3>{group.label}</h3>
                  <span className="hint">
                    {activeTab?.id ?? "—"} · {variantId || "—"}
                  </span>
                </div>
                {group.rows.map((row) => (
                  <DerivedChannelRow
                    key={row.entry.channelId}
                    row={row}
                    selected={row.entry.channelId === selectedId}
                    onSelect={() => onSelect(row.entry.channelId)}
                  />
                ))}
              </div>
            ))}
            {visibleCount === 0 ? <p className="rd">No channels in this filter.</p> : null}
          </div>
        </div>
        <aside className="inspect" aria-live="polite">
          {selected ? (
            <DerivedInspector row={selected} />
          ) : (
            <p className="reading">Select a channel.</p>
          )}
        </aside>
      </div>

      <footer className="foot">
        <span>
          Hidden unchanged: <b data-testid="derived-hidden-count">{hiddenCount}</b> · expand×join ·
          UnitClass closed
        </span>
        <span>Deferred in FE v1: live build compare · full xyflow calc graph</span>
      </footer>
    </div>
  );
}
