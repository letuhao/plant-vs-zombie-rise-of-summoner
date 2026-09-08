import { CatalogIcon } from "../CatalogIcon";
import {
  COMPOSE_SENTENCE,
  UNIT_SENTENCE,
  bucketContributions,
  formatChannelValue,
  registryCapFor,
  type DerivedRowModel
} from "./derivedCook";
import { ContributionGaugesView } from "./derivedGauges";

export function DerivedInspector({ row }: { row: DerivedRowModel }) {
  const { family, entry, live, state, displayName } = row;
  const compose = live?.composeKind || family.compose;
  const cap = registryCapFor(entry.channelId, family.capRef);
  const buckets = bucketContributions(live?.contributions ?? []);
  const title =
    family.expand !== "none" && entry.variantLabel
      ? `${displayName} · ${entry.variantLabel}`
      : displayName;

  if (state === "no-producer") {
    return (
      <div data-testid="derived-inspector" data-state={state}>
        <div className="hd">
          <span className="glyph" aria-hidden="true">
            <CatalogIcon icon={family.icon} fallbackToken={family.displayName.slice(0, 1)} />
          </span>
          <span className="nm">{displayName}</span>
        </div>
        <div className="big">—</div>
        <p className="reading">Nothing grants this yet.</p>
        <p className="sentence">
          <code>{entry.channelId}</code> is registered and readable, but no producer writes it (
          <b>no-producer</b>).
        </p>
      </div>
    );
  }

  const valueLabel = live ? formatChannelValue(live.value, family.unitClass) : "—";

  return (
    <div data-testid="derived-inspector" data-state={state}>
      <div className="hd">
        <span className="glyph" aria-hidden="true">
          <CatalogIcon icon={family.icon} fallbackToken={family.displayName.slice(0, 1)} />
        </span>
        <span className="nm">{title}</span>
      </div>
      <div className="big">{valueLabel}</div>
      <p className="reading">{live?.reading || family.reading}</p>
      {state === "stub" ? (
        <p className="reading">Placeholder — the real curve is not built.</p>
      ) : null}
      <p className="sentence">
        <b>Compose:</b> {COMPOSE_SENTENCE[compose] ?? compose}
        <br />
        <b>Unit:</b> {UNIT_SENTENCE[family.unitClass] ?? family.unitClass}
        <br />
        <b>Join:</b> <code>{entry.channelId}</code>
      </p>
      {cap != null ? (
        <p className={`cap-note${state === "capped" ? "" : " ok"}`}>
          {state === "capped"
            ? `At soft cap ${cap} — the next point does nothing.`
            : `Cap ${cap} — more still counts.`}
        </p>
      ) : (
        <p className="cap-note ok">No registry cap on this channel.</p>
      )}
      <ContributionGaugesView row={row} buckets={buckets} />
    </div>
  );
}
