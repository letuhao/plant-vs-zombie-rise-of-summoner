import { useMemo } from "react";
import { Button } from "@/ui";
import { useClaimOnboarding, useOnboarding } from "@/lib/bus";

const TITLES: Record<string, string> = {
  "first-win-dave": "Crazy Dave joins your side",
  "level-3-general-species": "Your empire is learning",
  "level-4-dave-equipment": "Dave found a first piece of gear"
};

export function OnboardingReveal({ playerId, onOpenCommanders }: { playerId: number; onOpenCommanders: () => void }) {
  const query = useOnboarding(playerId);
  const claim = useClaimOnboarding(playerId);
  const current = useMemo(
    () => query.data?.checkpoints.find((row) => row.state === "earned" && !row.claimedUtc),
    [query.data]
  );

  if (query.isLoading) return <div className="mb-4 rounded-md border border-panel bg-panel p-4" data-testid="onboarding-loading">Loading your first rewards…</div>;
  if (query.isError) return <div className="mb-4 rounded-md border border-bad bg-panel p-4" data-testid="onboarding-error"><p>We couldn’t load your first rewards.</p><Button size="sm" className="mt-2" onClick={() => void query.refetch()}>Retry</Button></div>;
  if (!current) return null;

  const isDave = current.checkpointId === "first-win-dave" || current.checkpointId === "level-4-dave-equipment";
  return (
    <div className="mb-4 rounded-md border border-ok bg-panel p-4 shadow-lg" data-testid="onboarding-reveal">
      <p className="text-xs font-bold uppercase tracking-wide text-ok">New milestone</p>
      <h2 className="mt-1 font-display text-xl text-text">{TITLES[current.checkpointId] ?? "Progress unlocked"}</h2>
      <p className="mt-1 text-sm text-muted">
        {current.checkpointId === "level-3-general-species"
          ? "Every ordinary demon in this lawn run now benefits from your empire’s species progression."
          : "This reward is saved automatically and is ready on your commander sheet."}
      </p>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button
          size="sm"
          onClick={() => void claim.mutateAsync(current.checkpointId)}
          disabled={claim.isPending}
          title={claim.isPending ? "Saving reward…" : "Acknowledge this reward"}
        >
          {claim.isPending ? "Saving…" : "Got it"}
        </Button>
        {isDave ? <Button size="sm" variant="ghost" onClick={onOpenCommanders}>Open Dave’s sheet</Button> : null}
      </div>
    </div>
  );
}
