import { useEffect, useRef, useState } from "react";
import { Button } from "@/ui";
import { DialogShell } from "@/shell/DialogShell";
import { useAcknowledgeOnboardingStory } from "@/lib/bus";
import { OnboardingStoryIds } from "./storyContract";
import { RIFT_ASSETS } from "./riftAssets";
import "./rift.css";

type Beat = {
  speaker: string;
  line: string;
  teaching: string;
  cue: string;
  seal?: boolean;
};

export type RiftPrologueCueId = (typeof BEATS)[number]["cue"];

const BEATS: readonly Beat[] = [
  {
    speaker: "Dave",
    line: "Uh-oh. That lawn is doing the wrong kind of wobbly.",
    teaching: "Start from something familiar—and worth protecting.",
    cue: "rift.portal.open"
  },
  {
    speaker: "Penny",
    line: "Temporal signal unstable.",
    teaching: "The fracture is new, and something inside it is changing.",
    cue: "rift.portal.surge"
  },
  {
    speaker: "Gnome signal",
    line: "UNSTABLE SECTOR. QUARANTINE PENDING.",
    teaching: "Containment can save a world—or cut it away.",
    cue: "rift.quarantine.seal",
    seal: true
  },
  {
    speaker: "Dave",
    line: "Then we fix it before they close the gate. Plants first. Questions later.",
    teaching: "Anchor the lawn now. The wider Rift waits beyond your first victory.",
    cue: "rift.quarantine.fade"
  }
];

export function RiftPrologueDialog({
  open,
  playerId,
  onClose,
  onContinueToLawn,
  onCue
}: {
  open: boolean;
  playerId: number;
  onClose: () => void;
  /** Existing lawn destination; used by successful and degraded completion alike. */
  onContinueToLawn?: () => void;
  /** Presentation seam: the shared VFX host may consume this semantic cue; absent is safe. */
  onCue?: (cueId: RiftPrologueCueId) => void;
}) {
  const [beatIndex, setBeatIndex] = useState(0);
  const busyRef = useRef(false);
  const finishedRef = useRef(false);
  const advanceGuardRef = useRef(false);
  const [ackError, setAckError] = useState(false);
  const [assetMissing, setAssetMissing] = useState(false);
  const acknowledge = useAcknowledgeOnboardingStory(playerId);
  const beat = BEATS[beatIndex]!;
  const emittedBeatRef = useRef<number | null>(null);

  useEffect(() => {
    if (open) {
      setBeatIndex(0);
      setAckError(false);
      setAssetMissing(false);
      busyRef.current = false;
      finishedRef.current = false;
      advanceGuardRef.current = false;
      emittedBeatRef.current = null;
    }
  }, [open]);

  useEffect(() => {
    // Release the navigation guard only after React has committed the new beat. This makes a
    // double-click on Next one transition, while still allowing the next rendered button to work.
    advanceGuardRef.current = false;
  }, [beatIndex]);

  useEffect(() => {
    if (!open || emittedBeatRef.current === beatIndex) return;
    emittedBeatRef.current = beatIndex;
    onCue?.(beat.cue);
  }, [beat.cue, beatIndex, onCue, open]);

  function continueToLawn() {
    onClose();
    onContinueToLawn?.();
  }

  async function finish(outcome: "completed" | "skipped") {
    if (busyRef.current || finishedRef.current) return;
    busyRef.current = true;
    setAckError(false);
    try {
      await acknowledge.mutateAsync({
        storyId: OnboardingStoryIds.RiftPrologue,
        version: OnboardingStoryIds.RiftPrologueVersion,
        outcome
      });
      finishedRef.current = true;
      continueToLawn();
    } catch {
      // Keep the dialog usable: the player can retry the durable acknowledgement or bypass it
      // to the existing lawn destination. The server remains the authority either way.
      setAckError(true);
    } finally {
      busyRef.current = false;
    }
  }

  function advance() {
    if (busyRef.current || finishedRef.current || advanceGuardRef.current) return;
    advanceGuardRef.current = true;
    if (beatIndex < BEATS.length - 1) setBeatIndex((value) => value + 1);
    else void finish("completed");
  }

  return (
    <DialogShell
      open={open}
      onOpenChange={(next) => { if (!next) void finish("skipped"); }}
      onEscapeKeyDown={() => void finish("skipped")}
      title="The Rift is opening"
      subtitle="A short warning before your first lawn"
      testId="rift-prologue-dialog"
      footer={(
        <div className="flex w-full items-center justify-between gap-2">
          {ackError ? (
            <>
              <Button variant="ghost" size="sm" onClick={continueToLawn}>Continue to lawn</Button>
              <Button size="sm" onClick={() => void finish(beatIndex === BEATS.length - 1 ? "completed" : "skipped")}>
                Retry
              </Button>
            </>
          ) : (
            <>
              <Button variant="ghost" size="sm" onClick={() => void finish("skipped")} disabled={acknowledge.isPending}>
                Skip intro
              </Button>
              <Button size="sm" onClick={advance} disabled={acknowledge.isPending}>
                {acknowledge.isPending ? "Saving…" : beatIndex === BEATS.length - 1 ? "Anchor the lawn" : "Next"}
              </Button>
            </>
          )}
        </div>
      )}
    >
      <div className="space-y-3" onKeyDown={(event) => {
        if ((event.key === "Enter" || event.key === " ") && event.target === event.currentTarget) {
          event.preventDefault();
          advance();
        }
      }}>
        <div className="flex items-center justify-between text-xs font-bold uppercase tracking-wide text-muted">
          <span>Rift prologue</span>
          <span aria-label={`Beat ${beatIndex + 1} of ${BEATS.length}`}>{beatIndex + 1} of {BEATS.length}</span>
        </div>
        <div className="rift-prologue-scene" data-cue={beat.cue} data-sealed={beat.seal ? "true" : undefined}>
          {assetMissing ? (
            <div className="rift-prologue-placeholder" role="img" aria-label={RIFT_ASSETS.storySprite.fallbackLabel}>
              <span aria-hidden="true">◈</span>
              <span>{RIFT_ASSETS.storySprite.fallbackLabel}</span>
            </div>
          ) : (
            <img
              className="rift-prologue-art"
              src={RIFT_ASSETS.storySprite.src}
              alt={RIFT_ASSETS.storySprite.alt}
              onError={() => setAssetMissing(true)}
            />
          )}
        </div>
        <div aria-live="polite" className="rounded-sm border border-border bg-soil-raised p-3">
          <p className="text-xs font-bold uppercase tracking-wide text-ok">{beat.speaker}</p>
          <p className="mt-1 font-display text-lg text-text">“{beat.line}”</p>
          <p className="mt-2 text-sm text-muted">{beat.teaching}</p>
        </div>
        {ackError ? <p className="text-xs text-bad" role="status">We’ll try again next time. You can continue to the lawn.</p> : null}
      </div>
    </DialogShell>
  );
}
