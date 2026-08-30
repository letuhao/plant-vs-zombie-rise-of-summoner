import type { ActorView } from "@/contract/types";
import { useActorDerived } from "@/lib/bus/aura";
import { Button } from "@/ui";
import { ChannelContributions } from "./ChannelContributions";
import { PendingNote } from "./shared";
import { StatSummaryGrid } from "./StatSummaryGrid";

/**
 * actor-sheet program, derived-stats-tab — `channelSummary` (the richer, `unitClass`/`cap`/
 * `composeSentence`-bearing contract) is unconditionally pending today (no server endpoint,
 * confirmed via adaptActor), so that half still renders the honest reason, never a fabricated grid.
 *
 * <para><b>aura-skill T18c/GG-49:</b> a SEPARATE, live section below it now answers "why did my stat
 * move" for real, fed by `AuraDerivedEndpoints.cs` (T18b) — only channels a real subsystem actually
 * touched are shown (an untouched registry default carries nothing worth reading). This is
 * deliberately not routed through `channelSummary`/`StatSummaryGrid`'s existing contract: that
 * contract's `unitClass`/`cap`/`composeSentence` fields have no server producer yet either, and
 * fabricating them to force-fit this endpoint's simpler shape would be exactly the "fabricated grid"
 * this program's own discipline forbids.</para>
 */
export function DerivedStatsTab({ data }: { data: ActorView }) {
  const derived = useActorDerived(data.instanceId);
  const touchedChannels = (derived.data?.channels ?? []).filter((c) => c.contributions.length > 0);

  return (
    <div className="mt-4" data-testid="derived-stats-tab">
      {data.channelSummary.state === "known" ? (
        <StatSummaryGrid channels={data.channelSummary.value} />
      ) : (
        <PendingNote pending={data.channelSummary} testId="derived-stats-pending" />
      )}

      <div className="mt-4" data-testid="derived-stats-live">
        {derived.isLoading ? (
          <p className="text-2xs italic text-muted" data-testid="derived-stats-live-loading">
            Loading live channels…
          </p>
        ) : derived.isError ? (
          <p className="text-2xs italic text-muted" data-testid="derived-stats-live-error">
            Live channel data isn't available right now.
          </p>
        ) : touchedChannels.length === 0 ? (
          <p className="text-2xs italic text-muted" data-testid="derived-stats-live-empty">
            No stat changes to show yet.
          </p>
        ) : (
          <ul className="space-y-2" data-testid="derived-stats-live-list">
            {touchedChannels.map((c) => (
              <li
                key={c.channelId}
                className="rounded-sm border border-border-control bg-panel-inset p-2"
                data-testid={`derived-live-channel-${c.channelId}`}
              >
                <div className="flex items-center justify-between">
                  <span className="text-2xs uppercase tracking-wide text-muted">{c.channelId}</span>
                  <span className="font-mono text-sm text-text">{c.value}</span>
                </div>
                <ChannelContributions contributions={c.contributions} />
              </li>
            ))}
          </ul>
        )}
      </div>

      <Button
        className="mt-4"
        disabled
        title="Full stat sheet coming soon"
        data-testid="derived-stats-open-full"
      >
        Open full derived-stat sheet
      </Button>
    </div>
  );
}
