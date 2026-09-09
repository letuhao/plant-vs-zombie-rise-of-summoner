import type { CSSProperties } from "react";
import type { PieceFactory } from "@/features/gui-lego/types";
import { TypeIcon } from "@/ui";
import "../conditionConsole.css";

type Axis = {
  id: string;
  label: string;
  value: number;
  fillPct: number;
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
  return raw.map((row) => {
    const r = row as Record<string, unknown>;
    const value = Number(r.value ?? 0);
    const fillPct = typeof r.fillPct === "number" ? Number(r.fillPct) : value;
    return {
      id: String(r.id ?? ""),
      label: String(r.label ?? ""),
      value,
      fillPct,
      paint: String(r.paint ?? "#8a8070")
    };
  });
}

function radarPolygon(axes: Axis[]) {
  return axes
    .map((axis, index) => {
      const [x, y] = RADAR_POINTS[index] ?? [75, 75];
      const pct = Math.max(0, Math.min(100, axis.fillPct)) / 100;
      const dx = x - 75;
      const dy = y - 75;
      return `${75 + dx * pct},${75 + dy * pct}`;
    })
    .join(" ");
}

export const condHeroFactory: PieceFactory = ({ slots }) => (
  <section className="cond-hero" data-testid="condition-hero" data-grid-area="hero">
    {slots.radial}
    <div className="pool-col">{slots.meters}</div>
  </section>
);

export const poolRadialFactory: PieceFactory = ({ payload, bus }) => {
  const hpPending = payload.hpPct == null;
  const shieldKnown = typeof payload.shieldPct === "number";
  const hpPct = typeof payload.hpPct === "number" ? payload.hpPct : 0;
  const shieldPct = typeof payload.shieldPct === "number" ? payload.shieldPct : 0;
  const shieldColor =
    payload.shieldColor != null ? String(payload.shieldColor) : "var(--el-fire, #e7733f)";
  return (
    <button
      type="button"
      className={`radial-wrap${hpPending ? " is-pending" : ""}`}
      data-testid="condition-hp-radial"
      data-pending={hpPending || undefined}
      onClick={() => bus.emit("condition.pool.select", { poolId: "hp" })}
      title={String(payload.valueText ?? "HP")}
    >
      {shieldKnown ? (
        <div
          className="radial radial-shield"
          style={
            {
              width: 96,
              height: 96,
              ["--p" as string]: shieldPct,
              ["--c" as string]: shieldColor
            } as CSSProperties
          }
        />
      ) : null}
      <div
        className="radial radial-hp"
        style={
          {
            width: 72,
            height: 72,
            ["--p" as string]: hpPct,
            ["--c" as string]: "var(--bad, #d98787)"
          } as CSSProperties
        }
      />
      <span className="n">HP</span>
    </button>
  );
};

export const poolMeterFactory: PieceFactory = ({ payload, bus }) => {
  const label = String(payload.label ?? "");
  const fillPending = payload.fillPct == null;
  const fillPct = typeof payload.fillPct === "number" ? payload.fillPct : null;
  const selected = Boolean(payload.selected);
  const poolId = String(payload.poolId ?? "");
  return (
    <button
      type="button"
      className={`meter${selected ? " is-on" : ""}${fillPending ? " is-pending" : ""}`}
      data-pool={poolId}
      data-testid={`condition-resource-${poolId}`}
      data-pending={fillPending || undefined}
      onClick={() => bus.emit("condition.pool.select", { poolId })}
    >
      <span className="cap">
        <span>{label}</span>
        <b>{String(payload.valueText ?? "Current / max pending")}</b>
      </span>
      <span className="track">
        {fillPct != null ? <span className="fill" style={{ width: `${fillPct}%` }} /> : null}
      </span>
    </button>
  );
};

export const progressionGaugeFactory: PieceFactory = ({ payload }) => {
  const fillPending = payload.fillPct == null;
  const fillPct = typeof payload.fillPct === "number" ? payload.fillPct : null;
  return (
    <section
      className={`progression-gauge${fillPending ? " is-pending" : ""}`}
      data-testid="condition-progression"
      data-grid-area="prog"
      data-pending={fillPending || undefined}
    >
      <div className="hd">
        <span className="eyebrow">Current progression</span>
        <span className="level">Lv {String(payload.level ?? "—")}</span>
      </div>
      <div className="xp-line">
        <b data-testid="condition-xp-count">{String(payload.valueText ?? "—")}</b>
      </div>
      <div className="track" data-testid="condition-xp-track">
        {fillPct != null ? <div className="fill" style={{ width: `${fillPct}%` }} /> : null}
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
  <section className="stand-row" data-testid="condition-standing" data-grid-area="stand">
    {slots.radar}
    <div className="live-col">{slots.live}</div>
  </section>
);

export const standingRadarFactory: PieceFactory = ({ payload }) => {
  if (payload.phase === "pending") {
    return (
      <div className="radar-box" data-testid="condition-standing-radar">
        <p className="pending-copy" data-testid="actor-standing-pending">
          {String(payload.message ?? "Standing vector isn't ready yet.")}
        </p>
      </div>
    );
  }
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
    </div>
  );
};

export const standingBarsFactory: PieceFactory = ({ payload }) => {
  const title = String(payload.title ?? "Standing · five axes");
  if (payload.phase === "pending") {
    return (
      <>
        <p className="spec-label">{title}</p>
        <p className="pending-copy">{String(payload.message ?? "Standing vector isn't ready yet.")}</p>
      </>
    );
  }
  const axes = axisList(payload as Record<string, unknown>);
  return (
    <>
      <p className="spec-label">{title}</p>
      <div className="pw">
        {axes.map((axis) => (
          <div className="bar" key={axis.id} data-c={axis.id}>
            <span>{axis.label}</span>
            <span className="t">
              <span className="f" style={{ width: `${axis.fillPct}%`, background: axis.paint }} />
            </span>
            <b>{axis.value}</b>
          </div>
        ))}
      </div>
    </>
  );
};

export const statusGlyphStripFactory: PieceFactory = ({ payload, bus }) => {
  const items = Array.isArray(payload.items) ? (payload.items as Record<string, unknown>[]) : [];
  const isPending = payload.phase === "pending";
  const isEmpty = payload.phase === "empty";
  return (
    <div className="status-strip" data-testid="condition-status-strip">
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
        <p
          className="pending-copy"
          data-testid={
            isPending
              ? "condition-live-effects-pending"
              : isEmpty
                ? "condition-live-effects-empty"
                : undefined
          }
        >
          {String(payload.message)}
        </p>
      ) : null}
    </div>
  );
};

export const actorIdentityFactory: PieceFactory = ({ payload }) => {
  const species = payload.speciesName != null ? String(payload.speciesName) : null;
  const phaseLabel = payload.phaseLabel != null ? String(payload.phaseLabel) : null;
  const elements = Array.isArray(payload.elements) ? payload.elements.map(String) : [];
  const pending = payload.phase === "pending";
  const side = String(payload.side ?? "plant") as "plant" | "zombie";
  const typeId = typeof payload.typeId === "number" ? payload.typeId : 0;
  return (
    <section
      className={`actor-identity${pending ? " is-pending" : ""}`}
      data-testid="condition-actor-identity"
      data-grid-area="identity"
      data-pending={pending || undefined}
    >
      <TypeIcon
        side={side}
        typeId={typeId}
        size={48}
        className="border border-border-control"
        testId="condition-identity-portrait"
      />
      <div className="identity-body">
        <p className="eyebrow">Species</p>
        <p className="species-name" data-testid="condition-species-name">
          {species ?? "—"}
        </p>
        <div className="chips">
          {phaseLabel ? <span className="chip">{phaseLabel}</span> : null}
          {elements.map((el) => (
            <span key={el} className="chip" data-testid={`condition-identity-element-${el}`}>
              {el}
            </span>
          ))}
        </div>
        {payload.message ? <p className="pending-copy">{String(payload.message)}</p> : null}
      </div>
    </section>
  );
};

export const CONDITION_SLOT_MAP: Record<string, readonly string[]> = {
  "cond-hero": ["radial", "meters"],
  "pool-radial": [],
  "pool-meter": [],
  "progression-gauge": [],
  "actor-identity": [],
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
  "actor-identity": actorIdentityFactory,
  "stand-row": standRowFactory,
  "standing-radar": standingRadarFactory,
  "standing-bars": standingBarsFactory,
  "status-glyph-strip": statusGlyphStripFactory
};
