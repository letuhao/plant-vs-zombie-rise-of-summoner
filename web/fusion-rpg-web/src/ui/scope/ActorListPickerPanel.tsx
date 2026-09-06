import { ActorCollection, type ActorCollectionItem, type ActorRungState } from "@/ui/actor";
import type { ScopePickerValue } from "./ActorMenuScopePicker";

type ListKind = "target" | "uniqueDemon";

function idOf(value: ScopePickerValue | null, kind: ListKind): string | null {
  if (!value || value.kind !== kind) return null;
  return value.kind === "target" ? value.targetPtr : value.instanceId;
}

/**
 * Target / UniqueDemon list body for ActorMenuScopePicker.
 * Uses shared ActorCollection (lawn-interactive T11) — never a private ActorRow fork.
 *
 * WhoSelector shapes stay distinct: target → targetPtr, uniqueDemon → instanceId.
 * Never pass instanceId as targetPtr.
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
  const selectedId = idOf(value, kind);

  const items: ActorCollectionItem[] = candidates.map((state, index) => {
    if (state.kind === "ready") {
      return {
        key: state.data.instanceId,
        rungState: state,
        label:
          state.data.displayName.state === "known"
            ? state.data.displayName.value
            : state.data.instanceId,
        sideLabel: state.data.side
      };
    }
    return {
      key: `pending-${kind}-${index}`,
      rungState: state,
      lockedReason: "Not ready"
    };
  });

  return (
    <div data-testid={`scope-${kind}-list`}>
      <ActorCollection
        testId={`scope-${kind}-collection`}
        items={items}
        density="list"
        selectionKey={selectedId}
        onSelect={(key) => {
          // Ready rows only — pending keys are not WhoSelector payloads.
          const ready = candidates.find((c) => c.kind === "ready" && c.data.instanceId === key);
          if (!ready || ready.kind !== "ready") return;
          const id = ready.data.instanceId;
          if (kind === "target") {
            // targetPtr is a board/world pointer identity — for menu demos the candidate id is the
            // stand-in; production consumers must supply ptr-shaped candidates, never confuse with
            // uniqueDemon instanceId when wiring Intent.
            onChange({ kind: "target", targetPtr: id });
          } else {
            onChange({ kind: "uniqueDemon", instanceId: id });
          }
        }}
        empty={
          <p className="text-sm italic text-muted" data-testid={`scope-${kind}-empty`}>
            No candidates available.
          </p>
        }
      />
    </div>
  );
}
