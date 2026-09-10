import type { PieceFactory } from "@/features/gui-lego/types";
import { CatalogIcon } from "@/ui/actor/CatalogIcon";
import { LeftoverBar } from "@/ui/actor/LeftoverBar";
import { themeStyle, vfxClass } from "@/ui/gui-lego/RecipeMount";
import { Cell, Pie, PieChart, ResponsiveContainer } from "recharts";

export const aptitudeScopeChipFactory: PieceFactory = ({ payload }) => {
  const style = themeStyle(payload);
  return (
    <p
      className="text-xs text-muted"
      data-testid="aptitudes-scope-chip"
      data-scope={String(payload.scopeKey ?? "")}
      style={style}
    >
      Scope: {String(payload.title ?? "")}
      {payload.subtitle != null && String(payload.subtitle).length > 0
        ? ` · ${String(payload.subtitle)}`
        : null}
      {payload.commanderAddOn != null && String(payload.commanderAddOn).length > 0 ? (
        <span className="mt-0.5 block" data-testid="aptitudes-scope-commander-addon">
          {String(payload.commanderAddOn)}
        </span>
      ) : null}
    </p>
  );
};

export const leftoverGaugeFactory: PieceFactory = ({ payload }) => {
  const budget = Number(payload.budget) || 0;
  const spent = Number(payload.spent) || 0;
  const overspend = Boolean(payload.overspend) || spent > budget;
  return (
    <div
      className={overspend ? "animate-pulse" : undefined}
      data-testid="leftover-gauge"
      data-revision={payload.revision != null ? String(payload.revision) : undefined}
    >
      <LeftoverBar budget={budget} spent={spent} testId="leftover-gauge-bar" />
    </div>
  );
};

export const allocateDecisionStripFactory: PieceFactory = ({ payload, bus }) => {
  const dirty = Boolean(payload.dirty);
  const withinBudget = Boolean(payload.withinBudget);
  const saving = Boolean(payload.saving);
  const priceAmount = typeof payload.priceAmount === "number" ? payload.priceAmount : null;
  const priceResource =
    payload.priceResource != null && String(payload.priceResource).length > 0
      ? String(payload.priceResource)
      : null;
  return (
    <div
      className="flex w-full flex-wrap items-center justify-between gap-2"
      data-testid="allocate-decision-strip"
    >
      {priceAmount != null && priceResource ? (
        <span className="text-xs text-muted" data-testid="allocate-decision-price">
          Respec cost: {priceAmount} {priceResource}
        </span>
      ) : (
        <span className="text-xs text-muted" data-testid="allocate-decision-hint">
          Confirm commits this draft · Cancel discards
        </span>
      )}
      <div className="flex gap-2">
        <button
          type="button"
          className="rounded-sm border border-border-control px-2 py-1 text-sm text-text"
          data-testid="allocate-decision-cancel"
          disabled={!dirty || saving}
          onClick={() => bus.emit("aptitude.reset", {})}
        >
          Cancel
        </button>
        <button
          type="button"
          className="rounded-sm border border-lawn-hot bg-lawn/20 px-2 py-1 text-sm text-text"
          data-testid={
            payload.confirmTestId != null && String(payload.confirmTestId).length > 0
              ? String(payload.confirmTestId)
              : "allocate-decision-confirm"
          }
          disabled={!dirty || !withinBudget || saving}
          onClick={() => bus.emit("aptitude.confirm", {})}
        >
          {saving ? "Saving…" : "Confirm"}
        </button>
      </div>
    </div>
  );
};

export const presetEntryFactory: PieceFactory = ({ payload, bus }) => {
  const active =
    payload.activePresetName != null && String(payload.activePresetName).length > 0
      ? String(payload.activePresetName)
      : null;
  return (
    <button
      type="button"
      className="rounded-sm border border-border-control px-2 py-1 text-xs text-text"
      data-testid="preset-entry"
      onClick={() => bus.emit("preset.open", {})}
    >
      {String(payload.label ?? "Build presets…")}
      {active ? <span className="ml-2 text-muted">· {active}</span> : null}
    </button>
  );
};

export const postureBandFactory: PieceFactory = ({ payload, slots }) => {
  const style = themeStyle(payload);
  const vfx = vfxClass(payload);
  const postureId = String(payload.postureId ?? "");
  return (
    <section
      className={["rounded-sm border border-border-control p-2", vfx].filter(Boolean).join(" ")}
      aria-label={String(payload.displayName ?? postureId)}
      data-testid={`aptitude-posture-${postureId}`}
      style={style}
    >
      <h3 className="mb-2 text-2xs font-bold uppercase tracking-wide text-muted">
        {String(payload.displayName ?? postureId)}
      </h3>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">{slots.tiles}</div>
    </section>
  );
};

export const aptitudeTileFactory: PieceFactory = ({ payload, bus }) => {
  const style = themeStyle(payload);
  const vfx = vfxClass(payload);
  const id = String(payload.aptitudeId ?? "");
  const displayName = String(payload.displayName ?? id);
  const selected = Boolean(payload.selected);
  const value = Number(payload.value) || 0;
  const icon = payload.icon != null ? String(payload.icon) : null;
  return (
    <div
      data-testid={`aptitude-tile-${id}`}
      data-selected={selected ? "true" : "false"}
      className={[
        "rounded-sm border p-3",
        selected ? "border-lawn-hot bg-lawn/15" : "border-border-control bg-panel-inset",
        vfx
      ]
        .filter(Boolean)
        .join(" ")}
      style={style}
    >
      <button
        type="button"
        className="w-full text-left"
        onClick={() => bus.emit("aptitude.select", { aptitudeId: id })}
      >
        <span className="mb-1 flex items-center gap-2">
          <CatalogIcon
            icon={icon}
            fallbackToken={displayName.slice(0, 2)}
            testId={`aptitude-icon-${id}`}
          />
          <span className="block font-ui text-sm text-text">{displayName}</span>
        </span>
        <span className="block font-mono text-lg text-text" data-testid={`aptitude-value-${id}`}>
          {value}
        </span>
      </button>
      {payload.allowDirectEdit ? (
        <input
          type="number"
          min={0}
          className="mt-1 w-full rounded-sm border border-border-control bg-panel px-2 py-1 font-mono text-sm text-text"
          data-testid={`species-build-input-${id}`}
          value={value}
          onChange={(e) =>
            bus.emit("aptitude.set", { aptitudeId: id, value: Number(e.target.value) || 0 })
          }
        />
      ) : null}
      <div className="mt-2 flex gap-1">
        <button
          type="button"
          className="rounded-sm border border-border-control px-2 py-0.5 text-sm text-text"
          data-testid={`aptitude-dec-${id}`}
          aria-label={`Decrease ${displayName}`}
          onClick={() => bus.emit("aptitude.step", { aptitudeId: id, delta: -1 })}
        >
          −
        </button>
        <button
          type="button"
          className="rounded-sm border border-border-control px-2 py-0.5 text-sm text-text"
          data-testid={`aptitude-inc-${id}`}
          aria-label={`Increase ${displayName}`}
          onClick={() => bus.emit("aptitude.step", { aptitudeId: id, delta: 1 })}
        >
          +
        </button>
      </div>
    </div>
  );
};

export const aptitudeInspectFactory: PieceFactory = ({ payload }) => {
  const families = Array.isArray(payload.fedFamilies)
    ? (payload.fedFamilies as { displayName?: string }[])
    : [];
  const name = String(payload.displayName ?? "");
  if (!name) {
    return (
      <p className="text-xs italic text-muted" data-testid="aptitude-inspect-empty">
        Select an aptitude.
      </p>
    );
  }
  return (
    <div data-testid="aptitude-inspect">
      <h3 className="font-display text-base text-text">{name}</h3>
      <p className="text-sm text-muted">{String(payload.reading ?? "")}</p>
      <p className="text-xs text-muted">{String(payload.role ?? "")}</p>
      {families.length > 0 ? (
        <ul className="mt-2 space-y-0.5" data-testid="aptitude-inspect-fed">
          {families.map((f, i) => (
            <li key={`${f.displayName ?? i}`} className="text-xs text-text">
              {String(f.displayName ?? "")}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
};

export const speciesBuildChromeFactory: PieceFactory = ({ payload }) => {
  const hasOverride = Boolean(payload.hasOverride);
  const priceAmount = typeof payload.priceAmount === "number" ? payload.priceAmount : null;
  const priceResource =
    payload.priceResource != null && String(payload.priceResource).length > 0
      ? String(payload.priceResource)
      : null;
  const statusMessage =
    payload.statusMessage != null && String(payload.statusMessage).length > 0
      ? String(payload.statusMessage)
      : hasOverride
        ? "You've overridden the shipped build below."
        : "You're running the shipped build.";
  return (
    <div
      className="rounded-sm border border-border-control bg-panel-inset p-2 text-xs text-muted"
      data-testid="species-build-chrome"
      data-has-override={hasOverride ? "true" : "false"}
    >
      <p data-testid="species-build-status">{statusMessage}</p>
      {priceAmount != null && priceResource ? (
        <p data-testid="species-build-price">
          Next Confirm / Activate costs {priceAmount} {priceResource}
        </p>
      ) : (
        <p data-testid="species-build-free">First override / revert is free</p>
      )}
    </div>
  );
};

export const aptitudesLayoutFactory: PieceFactory = ({ payload, slots }) => {
  const style = themeStyle(payload);
  return (
    <div
      className="mt-4 flex min-h-0 flex-col gap-3"
      data-testid="aptitudes-tab"
      data-mode={String(payload.mode ?? "")}
      style={style}
    >
      <div className="flex flex-wrap items-center gap-3" data-testid="aptitudes-hero">
        {slots.hero}
      </div>
      <div className="grid min-h-0 flex-1 gap-3 lg:grid-cols-[minmax(0,1fr)_14rem]">
        <div className="space-y-4" data-testid="aptitudes-bands">
          {slots.bands}
        </div>
        <aside className="min-w-0" data-testid="aptitudes-inspect-slot">
          {slots.inspect}
        </aside>
      </div>
      <div data-testid="aptitudes-decision-slot">{slots.decision}</div>
    </div>
  );
};

type DonutSeg = { key: string; name: string; value: number; fill: string };

export const presetDistributionChartFactory: PieceFactory = ({ payload }) => {
  const raw = Array.isArray(payload.segments) ? (payload.segments as Record<string, unknown>[]) : [];
  const segments: DonutSeg[] = raw
    .map((s) => ({
      key: String(s.aptitudeId ?? s.key ?? ""),
      name: String(s.displayName ?? s.aptitudeId ?? ""),
      value: Number(s.permille ?? s.points ?? s.value) || 0,
      fill: String(s.paint ?? "#8a8070")
    }))
    .filter((s) => s.key.length > 0 && s.value > 0);
  if (segments.length === 0) {
    return (
      <div className="text-xs italic text-muted" data-testid="preset-distribution-chart-empty">
        No distribution yet — assign shares or open a preset.
      </div>
    );
  }
  return (
    <div className="h-40 w-full" data-testid="preset-distribution-chart" data-revision={String(payload.revision ?? "")}>
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={segments}
            dataKey="value"
            nameKey="name"
            cx="50%"
            cy="50%"
            innerRadius={28}
            outerRadius={48}
            isAnimationActive={false}
          >
            {segments.map((s) => (
              <Cell key={s.key} fill={s.fill} />
            ))}
          </Pie>
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
};

export const aptitudeFactories: Record<string, PieceFactory> = {
  "aptitude-scope-chip": aptitudeScopeChipFactory,
  "leftover-gauge": leftoverGaugeFactory,
  "allocate-decision-strip": allocateDecisionStripFactory,
  "preset-entry": presetEntryFactory,
  "posture-band": postureBandFactory,
  "aptitude-tile": aptitudeTileFactory,
  "aptitude-inspect": aptitudeInspectFactory,
  "species-build-chrome": speciesBuildChromeFactory,
  "aptitudes-layout": aptitudesLayoutFactory,
  "preset-distribution-chart": presetDistributionChartFactory
};

export const APTITUDE_SLOT_MAP: Record<string, readonly string[]> = {
  "aptitude-scope-chip": [],
  "leftover-gauge": [],
  "allocate-decision-strip": [],
  "preset-entry": [],
  "posture-band": ["tiles"],
  "aptitude-tile": [],
  "aptitude-inspect": [],
  "species-build-chrome": [],
  "aptitudes-layout": ["hero", "bands", "inspect", "decision"],
  "preset-distribution-chart": []
};
