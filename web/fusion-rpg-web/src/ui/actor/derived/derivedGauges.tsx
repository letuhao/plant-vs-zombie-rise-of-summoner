import { contributionFictionLabel } from "@/lib/bus/aura";
import {
  BUCKET_COLORS,
  donutPaths,
  type DerivedRowModel
} from "./derivedCook";

/** Share donut + stack rows + GG-49 sources list (HTML SSOT gauges). */
export function ContributionGaugesView({
  row,
  buckets
}: {
  row: DerivedRowModel;
  buckets: { key: string; label: string; value: number }[];
}) {
  const { family, live } = row;
  const totalPos = buckets.filter((b) => b.key !== "neg").reduce((a, b) => a + b.value, 0) || 1;
  const max = Math.max(1, ...buckets.map((b) => b.value));
  const paths = donutPaths(buckets);
  const showDonut = paths.length > 0;

  return (
    <div className="contrib" data-testid="derived-contribution-chart">
      <h4>Why this number</h4>
      {showDonut ? (
        <div className="share-donut" aria-hidden="true" data-testid="derived-share-donut">
          <svg width="80" height="80" viewBox="0 0 80 80">
            <circle
              cx="40"
              cy="40"
              r="34"
              fill="var(--panel-inset)"
              stroke="var(--border)"
              strokeWidth="1"
            />
            {paths.map((p) => (
              <path key={p.key} d={p.d} fill={p.fill} />
            ))}
            <circle cx="40" cy="40" r="18" fill="var(--soil-raised)" />
          </svg>
          <ul className="share-legend">
            {buckets
              .filter((b) => b.key !== "neg")
              .map((b) => (
                <li key={b.key}>
                  <i style={{ background: BUCKET_COLORS[b.key] ?? BUCKET_COLORS.other }} />
                  {b.label} · {Math.round((b.value / totalPos) * 100)}%
                </li>
              ))}
          </ul>
        </div>
      ) : null}
      <div className="stack" data-testid="derived-stack">
        {buckets.length === 0 ? (
          <p className="rd">No contributions.</p>
        ) : (
          buckets.map((b) => {
            const barPct = Math.max(4, Math.round((b.value / max) * 100));
            const sharePct = Math.round((b.value / totalPos) * 100);
            return (
              <div
                key={b.key}
                className="stack-row"
                data-src={b.key}
                data-testid={`derived-contrib-gauge-${b.key}`}
              >
                <span>{b.label}</span>
                <span className="bar">
                  <span className="fill" style={{ width: `${barPct}%` }} />
                </span>
                <span className="share">{sharePct}%</span>
                <span className="amt">
                  {family.unitClass === "UnitInterval"
                    ? b.value.toFixed(2)
                    : `+${Math.round(b.value).toLocaleString()}`}
                </span>
              </div>
            );
          })
        )}
      </div>
      <h4>Sources (GG-49)</h4>
      <ul className="sources" data-testid="derived-sources">
        {(live?.contributions ?? []).length === 0 ? (
          <li>
            <span className="lab">Empty</span>
          </li>
        ) : (
          (live?.contributions ?? []).map((c, i) => (
            <li key={`${c.sourceId}-${i}`}>
              <span className="lab">{c.label || contributionFictionLabel(c.sourceId)}</span>
              <span className="v">
                {c.value >= 0 ? "+" : ""}
                {family.unitClass === "UnitInterval"
                  ? c.value.toFixed(2)
                  : c.value.toLocaleString()}
              </span>
            </li>
          ))
        )}
      </ul>
    </div>
  );
}
