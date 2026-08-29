import { ActorRow, type ActorRungState } from "@/ui/actor";
import type { ScopePickerValue } from "./ActorMenuScopePicker";

type ListKind = "target" | "uniqueDemon";

function idOf(value: ScopePickerValue | null, kind: ListKind): string | null {
  if (!value || value.kind !== kind) return null;
  return value.kind === "target" ? value.targetPtr : value.instanceId;
}

/**
 * Shared by both the "Target" and "UniqueDemon" modes (fe-essentials spec-actor-menu-scope-picker.md
 * — "one implementation, not two", matching this repo's own "one entity, one ladder, no forks" rule).
 * Renders every candidate through the real `ActorRow` — its own doc comment already names "deploy
 * pickers" as an intended use, so this is composition, not a new list-row implementation.
 */
export function ActorListPickerPanel({
  kind,
  candidates,
  value,
  onChange
}: {
  kind: ListKind;
  candidates: ActorRungState[];
  value: ScopePickerValue | null;
  onChange: (value: ScopePickerValue) => void;
}) {
  if (candidates.length === 0) {
    return (
      <p className="text-sm italic text-muted" data-testid={`scope-${kind}-empty`}>
        No candidates available.
      </p>
    );
  }

  const selectedId = idOf(value, kind);

  return (
    <div className="max-h-64 overflow-y-auto rounded-md border border-border" data-testid={`scope-${kind}-list`}>
      {candidates.map((state, index) => {
        if (state.kind !== "ready") {
          // loading/empty/error/locked — ActorRow's own RungStateFallback covers this; not selectable.
          return <ActorRow key={index} state={state} />;
        }
        const id = state.data.instanceId;
        const selected = selectedId === id;
        return (
          <button
            key={id}
            type="button"
            data-testid={`scope-${kind}-option-${id}`}
            aria-pressed={selected}
            onClick={() => {
              console.debug("[fe-essentials] actor-menu-scope-picker: list selection", { kind, id });
              onChange(kind === "target" ? { kind: "target", targetPtr: id } : { kind: "uniqueDemon", instanceId: id });
            }}
            className={`block w-full text-left transition-colors ${selected ? "bg-lawn/20" : "hover:bg-panel"}`}
          >
            <ActorRow state={state} />
          </button>
        );
      })}
    </div>
  );
}
