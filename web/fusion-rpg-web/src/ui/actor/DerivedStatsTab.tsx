import type { ActorView } from "@/contract/types";
import { Button } from "@/ui";
import { PendingNote } from "./shared";
import { StatSummaryGrid } from "./StatSummaryGrid";

/**
 * actor-sheet program, derived-stats-tab — channelSummary is unconditionally pending today (no
 * server endpoint, confirmed via adaptActor), so this renders the honest reason, never a fabricated
 * grid. The doorway button never links anywhere: spec-derived-stat-sheet.md's own full-sheet
 * component doesn't exist in the tree yet (confirmed by grep), so wiring it would be a dead link.
 */
export function DerivedStatsTab({ data }: { data: ActorView }) {
  return (
    <div className="mt-4" data-testid="derived-stats-tab">
      {data.channelSummary.state === "known" ? (
        <StatSummaryGrid channels={data.channelSummary.value} />
      ) : (
        <PendingNote pending={data.channelSummary} testId="derived-stats-pending" />
      )}
      <Button
        className="mt-4"
        disabled
        title="Full sheet not built yet (spec-derived-stat-sheet.md)"
        data-testid="derived-stats-open-full"
      >
        Open full derived-stat sheet
      </Button>
    </div>
  );
}
