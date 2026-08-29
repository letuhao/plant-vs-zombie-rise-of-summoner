import { LockedGridSlot } from "./LockedGridSlot";

// Illustrative only — passive skills are the owner's own explicitly deferred sub-feature
// (class-system-map.md: "will be added later," no module id, no spec).
const PLACEHOLDER_PASSIVES = [
  { id: "root-deep", label: "Root Deep" },
  { id: "sunlit", label: "Sunlit" },
  { id: "bloom-everlasting", label: "Bloom Everlasting" },
  { id: "wind-sown", label: "Wind-Sown" }
];

/** actor-sheet program, locked-preview-tabs — a flat locked list, not a node-graph tree (this game doesn't have PoE's content scale to justify one). */
export function PassivesTab() {
  return (
    <div className="mt-4 grid grid-cols-4 gap-2" data-testid="passives-tab">
      {PLACEHOLDER_PASSIVES.map(({ id, label }) => (
        <LockedGridSlot key={id} id={id} label={label} reason="Passive skills are a reserved sub-feature, no target date yet" />
      ))}
    </div>
  );
}
