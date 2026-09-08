import type { PieceFactory } from "@/features/gui-lego/types";
import { SHOW_UNCHANGED_KEY } from "@/features/gui-lego/cook";
import { themeStyle, vfxClass } from "@/ui/gui-lego/RecipeMount";

export const identityHdFactory: PieceFactory = ({ payload }) => {
  const style = themeStyle(payload);
  return (
    <div className="identity" style={style}>
      <div className="who">{String(payload.who ?? "")}</div>
      <div className="meta">{String(payload.meta ?? "")}</div>
    </div>
  );
};

export const toolSearchFactory: PieceFactory = ({ payload, bus }) => (
  <input
    className="search"
    type="search"
    value={String(payload.query ?? "")}
    placeholder={String(payload.placeholder ?? "Search channels…")}
    aria-label="Search derived channels"
    data-testid="derived-search"
    onChange={(e) => bus.emit("derived.search.set", { query: e.target.value })}
  />
);

export const toolToggleFactory: PieceFactory = ({ payload, bus }) => {
  const value = Boolean(payload.value);
  const label = String(payload.label ?? "Show unchanged");
  return (
    <label className={`toggle${value ? " is-on" : ""}`} title="Persist in localStorage">
      <input
        type="checkbox"
        checked={value}
        data-testid="derived-show-unchanged"
        aria-label={label}
        style={{ position: "absolute", opacity: 0, width: 1, height: 1 }}
        onChange={(e) => {
          const next = e.target.checked;
          bus.emit("derived.showUnchanged.set", { value: next });
          try {
            localStorage.setItem(SHOW_UNCHANGED_KEY, next ? "1" : "0");
          } catch {
            /* ignore quota / private mode */
          }
        }}
      />
      <span className="track" aria-hidden="true">
        <span className="knob" />
      </span>
      {label}
    </label>
  );
};

export const railPrimaryFactory: PieceFactory = ({ payload, slots }) => (
  <nav
    className="cat-bar"
    role="tablist"
    aria-label={String(payload.ariaLabel ?? "Derived surface tabs")}
    data-testid="derived-primary-tablist"
  >
    {slots.chips}
  </nav>
);

export const railVariantFactory: PieceFactory = ({ payload, slots }) => {
  const hidden = Boolean(payload.hidden);
  return (
    <nav
      className="cat-bar variant-bar"
      role="tablist"
      aria-label={String(payload.ariaLabel ?? "Variants")}
      data-testid="derived-variant-rail"
      hidden={hidden || undefined}
      aria-hidden={hidden || undefined}
    >
      {slots.chips}
    </nav>
  );
};

export const chipFactory: PieceFactory = ({ payload, bus }) => {
  const id = String(payload.id ?? "");
  const rail = payload.rail === "variant" ? "variant" : "primary";
  const selected = Boolean(payload.selected);
  const label = String(payload.label ?? id);
  const count = typeof payload.count === "number" ? payload.count : null;
  const style = themeStyle(payload);
  const vfx = vfxClass(payload);
  const elementId =
    payload.elementId != null && String(payload.elementId).length > 0
      ? String(payload.elementId)
      : undefined;

  return (
    <button
      type="button"
      role="tab"
      className={["chip", selected ? "is-on" : null, vfx].filter(Boolean).join(" ")}
      aria-selected={selected}
      data-tab={rail === "primary" ? id : undefined}
      data-v={rail === "variant" ? id : undefined}
      data-el={elementId}
      data-testid={rail === "variant" ? `derived-variant-${id}` : `derived-tab-${id}`}
      style={style}
      onClick={() => {
        if (rail === "variant") bus.emit("derived.variant.set", { variantId: id });
        else bus.emit("derived.tab.set", { tabId: id });
      }}
    >
      {label}
      {count != null ? (
        <>
          {" "}
          <span className="n">{count}</span>
        </>
      ) : null}
    </button>
  );
};

export const surfaceFootFactory: PieceFactory = ({ payload }) => {
  const hiddenCount = typeof payload.hiddenCount === "number" ? payload.hiddenCount : 0;
  const note = payload.note != null ? String(payload.note) : "expand×join · UnitClass closed";
  const deferred =
    payload.deferred != null
      ? String(payload.deferred)
      : "Deferred in FE v1: live build compare · full xyflow calc graph";
  return (
    <footer className="foot">
      <span>
        Hidden unchanged: <b data-testid="derived-hidden-count">{hiddenCount}</b>
        {note ? ` · ${note}` : null}
      </span>
      <span>{deferred}</span>
    </footer>
  );
};

export const CHROME_SLOT_MAP: Record<string, readonly string[]> = {
  "identity-hd": [],
  "tool-search": [],
  "tool-toggle": [],
  "rail-primary": ["chips"],
  "rail-variant": ["chips"],
  chip: [],
  "surface-foot": []
};

export const chromeFactories: Record<string, PieceFactory> = {
  "identity-hd": identityHdFactory,
  "tool-search": toolSearchFactory,
  "tool-toggle": toolToggleFactory,
  "rail-primary": railPrimaryFactory,
  "rail-variant": railVariantFactory,
  chip: chipFactory,
  "surface-foot": surfaceFootFactory
};
