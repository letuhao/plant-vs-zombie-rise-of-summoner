import { CatalogIcon } from "../CatalogIcon";
import {
  formatChannelValue,
  rowKind,
  type DerivedRowModel
} from "./derivedCook";

export function DerivedChannelRow({
  row,
  selected,
  onSelect
}: {
  row: DerivedRowModel;
  selected: boolean;
  onSelect: () => void;
}) {
  const { family, entry, live, state, displayName } = row;
  const valueLabel =
    state === "no-producer" ? "—" : live ? formatChannelValue(live.value, family.unitClass) : "—";
  const dim = state === "default" ? " is-dim" : "";
  const on = selected ? " is-on" : "";

  return (
    <button
      type="button"
      className={`row${dim}${on}`}
      data-kind={rowKind(state)}
      data-id={entry.channelId}
      data-testid={`derived-channel-${entry.channelId}`}
      data-state={state}
      onClick={onSelect}
    >
      <span className="glyph" aria-hidden="true">
        <CatalogIcon
          icon={family.icon}
          color={entry.element?.color}
          fallbackToken={family.displayName.slice(0, 1)}
        />
      </span>
      <span>
        <span className="nm">{displayName}</span>
        <span className="rd">{live?.reading || family.reading}</span>
        {entry.variantLabel && family.expand !== "none" ? (
          <span
            className="rd"
            style={{ textTransform: "uppercase", letterSpacing: "0.06em", fontSize: 9 }}
          >
            {entry.variantLabel}
          </span>
        ) : null}
      </span>
      <span className="val" data-testid={`derived-value-${entry.channelId}`}>
        {valueLabel}
      </span>
      {state === "capped" ? (
        <span className="badge">CAP</span>
      ) : (
        <span className="state-tag">{state}</span>
      )}
    </button>
  );
}
