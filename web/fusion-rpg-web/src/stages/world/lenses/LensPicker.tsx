import { LENSES, lensLabel, type LensId } from "./lensCatalog";
import { useLensHotkeys } from "./useLensHotkeys";

export type LensPickerProps = {
  active: LensId;
  onSelect: (id: LensId) => void;
  /** world-stage W97 — true only while lens 4's own `?lifelines=true` fetch is in flight
   * (`useLensData`'s `isLensFourLoading`). GG-17: the other five lenses never carry this. */
  isLensFourLoading?: boolean;
};

/**
 * world-stage W96 (spec-world-lenses.md §1) — the chip row plus its readout. No map-controls
 * cluster (zoom/fit) exists yet in `stages/world/` to sit "beside" per the spec's own layout note —
 * that cluster is a separate, unbuilt piece, so this mounts standalone; wiring it into that cluster's
 * layout is a follow-up once the cluster itself is built. Declares no `z-index` of its own — it is
 * plain in-flow chrome, not a layer, so it never competes with the layer stack's own stacking order.
 */
export function LensPicker({ active, onSelect, isLensFourLoading = false }: LensPickerProps) {
  useLensHotkeys(onSelect);

  return (
    <div role="radiogroup" aria-label="Map lens" data-testid="lens-picker">
      <p data-testid="lens-picker-readout">
        {LENSES.findIndex((l) => l.id === active) + 1} / {LENSES.length} · {lensLabel(active)}
      </p>
      {LENSES.map((lens) => {
        const pending = lens.id === "supply" && isLensFourLoading;
        return (
          <button
            key={lens.id}
            type="button"
            role="radio"
            aria-checked={active === lens.id}
            aria-busy={pending}
            data-testid={`lens-picker-${lens.id}`}
            onClick={() => onSelect(lens.id)}
          >
            {lens.label}
            {pending ? <span data-testid="lens-picker-supply-pending">(loading)</span> : null}
          </button>
        );
      })}
    </div>
  );
}
