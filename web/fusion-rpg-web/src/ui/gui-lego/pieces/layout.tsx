import { Fragment, type ReactNode } from "react";
import type { PieceFactory } from "@/features/gui-lego/types";
import { themeStyle, vfxClass } from "@/ui/gui-lego/RecipeMount";
import "../derivedConsole.css";

/** Outer Derived console — landmark `.console` with direct header/nav/split/foot children. */
export const surfaceShellFactory: PieceFactory = ({ payload, slots }) => {
  const style = themeStyle(payload);
  const vfx = vfxClass(payload);
  return (
    <div
      className={["derived-combat-console", "console", vfx].filter(Boolean).join(" ")}
      data-testid="derived-combat-console"
      data-derived-root="1"
      data-phase={payload.phase}
      style={style}
    >
      <header className="console-hd" data-testid="derived-tab">
        {slots.identity}
        <div className="hd-tools">{slots.tools}</div>
      </header>
      {slots.railPrimary}
      {slots.railVariant}
      {slots.main}
      {slots.foot}
    </div>
  );
};

/** Dock | inspect split — landmark `.inspect-split`. */
export const splitInspectFactory: PieceFactory = ({ slots }) => (
  <div className="inspect-split" data-testid="derived-inspect">
    {slots.dock}
    {slots.inspect}
  </div>
);

/** Overflow host — dock wraps list-pane; inspect is aside.inspect. */
export const scrollRegionFactory: PieceFactory = ({ payload, slots }) => {
  const region = String(payload.region ?? "dock");
  if (region === "inspect") {
    return (
      <aside className="inspect" aria-live="polite">
        {slots.content}
      </aside>
    );
  }
  if (region === "list-pane") {
    return (
      <div className="list-pane" data-testid="derived-list">
        {slots.content}
      </div>
    );
  }
  return (
    <div className="dock">
      <div className="list-pane" data-testid="derived-list">
        {slots.content}
      </div>
    </div>
  );
};

/** Fragment of family-block children only — list-pane owned by scroll-region. */
export const familyListFactory: PieceFactory = ({ payload, slots }) => {
  const empty =
    payload._arrayLen === 0 ||
    (typeof payload._arrayLen === "number" && payload._arrayLen === 0);
  return (
    <Fragment>
      {slots.blocks}
      {empty ? <p className="rd">No channels in this filter.</p> : null}
    </Fragment>
  );
};

/** Section header (payload) + channel rows. */
export const familyBlockFactory: PieceFactory = ({ payload, slots }) => {
  const title = String(payload.title ?? "");
  const hint = payload.hint != null ? String(payload.hint) : null;
  return (
    <div className="family-block" data-family-id={payload.familyId != null ? String(payload.familyId) : undefined}>
      <div className="family-hd">
        <h3>{title}</h3>
        {hint ? <span className="hint">{hint}</span> : null}
      </div>
      {slots.rows}
    </div>
  );
};

export const LAYOUT_SLOT_MAP: Record<string, readonly string[]> = {
  "surface-shell": ["identity", "tools", "railPrimary", "railVariant", "main", "foot"],
  "split-inspect": ["dock", "inspect"],
  "scroll-region": ["content"],
  "family-list": ["blocks"],
  "family-block": ["rows"]
};

export type LayoutFactories = Record<string, PieceFactory>;

export const layoutFactories: LayoutFactories = {
  "surface-shell": surfaceShellFactory,
  "split-inspect": splitInspectFactory,
  "scroll-region": scrollRegionFactory,
  "family-list": familyListFactory,
  "family-block": familyBlockFactory
};

/** Re-export for typed slot children in tests. */
export type SlotChildren = Record<string, ReactNode>;
