import { LockedGridSlot } from "./LockedGridSlot";

// Illustrative only — no catalog exists yet for either side's actions (spec-action-layer.md is
// "approved 2026-08-22, not yet built"; `rpg_action` returns zero hits in src/ today).
const PLACEHOLDER_ACTIONS = [
  { id: "strike", label: "Strike" },
  { id: "firebolt", label: "Firebolt" },
  { id: "guard", label: "Guard" },
  { id: "overgrowth", label: "Overgrowth" }
];

/** actor-sheet program, locked-preview-tabs — every slot locked, no exceptions (no action system exists yet to unlock any of them). */
export function ActionsTab() {
  return (
    <div className="mt-4 grid grid-cols-4 gap-2" data-testid="actions-tab">
      {PLACEHOLDER_ACTIONS.map(({ id, label }) => (
        <LockedGridSlot key={id} id={id} label={label} reason="Unlocks once the action system ships (approved, not yet built)" />
      ))}
    </div>
  );
}
