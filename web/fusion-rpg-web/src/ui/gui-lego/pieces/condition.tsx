import type { PieceFactory } from "@/features/gui-lego/types";
import { CatalogIcon } from "@/ui/actor/CatalogIcon";
import { themeStyle } from "@/ui/gui-lego/RecipeMount";
import "../conditionConsole.css";

type Axis = {
  id: string;
  label: string;
  value: number;
  paint: string;
};

const RADAR_POINTS = [
  [75, 18],
  [132, 62],
  [110, 128],
  [40, 128],
  [18, 62]
] as const;

function axisList(payload: Record<string, unknown>): Axis[] {
  const raw = payload.axes;
  if (!Array.isArray(raw)) return [];
  return raw.map((row) => ({
    id: String((row as Record<string, unknown>).id ?? ""),
    label: String((row as Record<string, unknown>).label ?? ""),
    value: Number((row as Record<string, unknown>).value ?? 0),
    paint: String((row as Record<string, unknown>).paint ?? "#8a8070")
  }));
}

function radarPolygon(axes: Axis[]) {
  return axes
    .map((axis, index) => {
      const [x, y] = RADAR_POINTS[index] ?? [75, 75];
      const pct = Math.max(0, Math.min(100, axis.value)) / 100;
      const dx = x - 75;
      const dy = y - 75;
      return `${75 + dx * pct},${75 + dy * pct}`;
    })
    .join(" ");
}

export const condHeroFactory: PieceFactory = ({ payload, slots }) => (
  <section className="cond-hero" data-testid="condition-hero">
    {slots.radial}
    <div className="pool-col">{slots.meters}</div>
  </section>
);

export const poolRadialFactory: PieceFactory = ({ payload, bus }) => {
  const hpPct = typeof payload.hpPct === "number" ? payload.hpPct : 0;
  const shieldPct = typeof payload.shieldPct === "number" ? payload.shieldPct : 0;
  const style = themeStyle(payload);
  const shieldStyle = { ["--ring-pct" as string]: `${shieldPct}%` };
  return (
    <button
      type="button"
      className="radial-wrap"
      data-testid="condition-hp-radial"
      onClick={() => bus.emit("condition.pool.select", { poolId: "hp" })}
      title={String(payload.valueText ?? "Current / max pending")}
    >
      <span className="radial ring-shield" style={shieldStyle} />
      <span className="radial ring-hp" style={{ ...style, ["--ring-pct" as string]: `${hpPct}%` }} />
      <span className="n">HP</span>
      <span className="v">{String(payload.valueText ?? "Current / max pending")}</span>
    </button>
  );
};

export const poolMeterFactory: PieceFactory = ({ payload, bus }) => {
  const label = String(payload.label ?? "");
  const fillPct = typeof payload.fillPct === "number" ? payload.fillPct : 0;
  const selected = Boolean(payload.selected);
  const poolId = String(payload.poolId ?? "");
  const style = themeStyle(payload);
  const icon = payload.icon != null ? String(payload.icon) : undefined;
  return (
    <button
      type="button"
      className={`meter${selected ? " is-on" : ""}`}
      data-pool={poolId}
      data-testid={`condition-resource-${poolId}`}
      onClick={() => bus.emit("condition.pool.select", { poolId })}
    >
      <span className="cap">
        <span className="cap-title">
          <CatalogIcon icon={icon} fallbackToken={label.slice(0, 1)} />
          {label}
        </span>
        <b>{String(payload.valueText ?? "Current / max pending")}</b>
      </span>
      <span className="track">
        <span className="fill" style={{ ...style, width: `${fillPct}%` }} />
      </span>
    </button>
  );
};

export const progressionGaugeFactory: PieceFactory = ({ payload }) => {
  const fillPct = typeof payload.fillPct === "number" ? payload.fillPct : 0;
  return (
    <section className="progression-gauge" data-testid="condition-progression">
      <div className="hd">
        <span className="eyebrow">Current progression</span>
        <span className="level">Lv {String(payload.level ?? "—")}</span>
      </div>
      <div className="xp-line">
        <b data-testid="condition-xp-count">{String(payload.valueText ?? "—")}</b>
      </div>
      <div className="track" data-testid="condition-xp-track">
        <div className="fill" style={{ width: `${fillPct}%` }} />
      </div>
      {payload.message ? (
        <p className="pending-copy" data-testid="condition-xp-pending">
          {String(payload.message)}
        </p>
      ) : null}
    </section>
  );
};

export const standRowFactory: PieceFactory = ({ slots }) => (
  <section className="stand-row" data-testid="condition-standing">
    {slots.radar}
    <div className="live-col">{slots.live}</div>
  </section>
);

export const standingRadarFactory: PieceFactory = ({ payload }) => {
  const axes = axisList(payload as Record<string, unknown>);
  return (
    <div className="radar-box" data-testid="condition-standing-radar">
      <svg width="150" height="150" viewBox="0 0 150 150" role="img" aria-label="Standing five axes">
        <polygon points="75,18 132,62 110,128 40,128 18,62" fill="none" stroke="var(--border-control)" />
        <polygon points={radarPolygon(axes)} fill="rgb(224 180 75 / 0.28)" stroke="var(--sun)" />
        {axes.map((axis, index) => {
          const [cx, cy] = RADAR_POINTS[index] ?? [75, 75];
          return <circle key={axis.id} cx={cx} cy={cy} r="3" fill={axis.paint} />;
        })}
      </svg>
      {payload.message ? <p className="pending-copy">{String(payload.message)}</p> : null}
    </div>
  );
};

export const standingBarsFactory: PieceFactory = ({ payload }) => {
  const axes = axisList(payload as Record<string, unknown>);
  const title = String(payload.title ?? "Standing · five axes");
  return (
    <>
      <p className="spec-label">{title}</p>
      <div className="pw">
        {axes.map((axis) => (
          <div className="bar" key={axis.id} data-c={axis.id}>
            <span>{axis.label}</span>
            <span className="t">
              <span className="f" style={{ width: `${axis.value}%`, background: axis.paint }} />
            </span>
            <b>{axis.value}</b>
          </div>
        ))}
      </div>
      {payload.message ? <p className="pending-copy" data-testid="actor-standing-pending">{String(payload.message)}</p> : null}
    </>
  );
};

export const statusGlyphStripFactory: PieceFactory = ({ payload, bus }) => {
  const items = Array.isArray(payload.items) ? payload.items as Record<string, unknown>[] : [];
  return (
    <>
      <p className="spec-label">{String(payload.title ?? "Live status · tap for Status tab")}</p>
      <div className="glyph-wrap" data-testid="condition-live-effects">
        {items.map((item) => (
          <button
            type="button"
            key={String(item.id ?? "")}
            className="status-glyph"
            style={{ ["--glyph-color" as string]: String(item.color ?? "#6dbb63") }}
            onClick={() => bus.emit("condition.status.open", { statusId: item.id })}
          >
            {String(item.hudToken ?? item.id ?? "?")}
            <span className="ring" />
          </button>
        ))}
        {typeof payload.overflowCount === "number" && payload.overflowCount > 0 ? (
          <span className="tag">+{payload.overflowCount}</span>
        ) : null}
      </div>
      {payload.message ? (
        <p className="pending-copy" data-testid="condition-live-effects-pending">
          {String(payload.message)}
        </p>
      ) : null}
    </>
  );
};

export const CONDITION_SLOT_MAP: Record<string, readonly string[]> = {
  "cond-hero": ["radial", "meters"],
  "pool-radial": [],
  "pool-meter": [],
  "progression-gauge": [],
  "stand-row": ["radar", "live"],
  "standing-radar": [],
  "standing-bars": [],
  "status-glyph-strip": []
};

export const conditionFactories: Record<string, PieceFactory> = {
  "cond-hero": condHeroFactory,
  "pool-radial": poolRadialFactory,
  "pool-meter": poolMeterFactory,
  "progression-gauge": progressionGaugeFactory,
  "stand-row": standRowFactory,
  "standing-radar": standingRadarFactory,
  "standing-bars": standingBarsFactory,
  "status-glyph-strip": statusGlyphStripFactory
};
