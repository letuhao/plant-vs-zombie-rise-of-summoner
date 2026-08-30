import type { ActorView } from "@/contract/types";
import { EmptyState } from "@/ui";

/**
 * actor-sheet program, gear-tab — equipSlots is unconditionally pending today (no server endpoint),
 * same as channelSummary. Not a lock (nothing gates it — there's just nothing to equip yet), so this
 * gets EmptyState, not the locked-grid treatment locked-preview-tabs uses.
 */
export function GearTab({ data }: { data: ActorView }) {
  if (data.equipSlots.state !== "pending") {
    // Future-proofing only: once equipSlots has a real shape, rendering it is
    // spec-equip-and-paperdoll.md's own job, not designed here. PendingNote would silently render
    // null for a non-pending state, which would look like a bug, not a placeholder — this is
    // deliberately visible instead.
    return (
      <p className="text-xs italic text-muted" data-testid="gear-pending-fallback">
        Gear data received, but this tab doesn't render it yet.
      </p>
    );
  }
  return (
    <EmptyState
      title="No gear slots yet"
      hint="Equipment is coming in a later update."
      testId="gear-tab-empty"
    />
  );
}
