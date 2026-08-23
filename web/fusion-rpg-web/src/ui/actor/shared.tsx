import type { ActorView } from "@/contract/types";
import type { Pending } from "@/contract/pending";
import { cn } from "@/lib/cn";

/** Art is a registry concern that hasn't shipped yet (web/spec.md §1) — an initial stands in honestly rather than faking an icon. */
export function ActorFrame({
  side,
  initial,
  size = "row",
  testId
}: {
  side: ActorView["side"];
  initial: string;
  size?: "token" | "chip" | "row" | "card" | "panel";
  testId?: string;
}) {
  const sizeClass = {
    token: "h-5 w-5 text-xs",
    chip: "h-6 w-6 text-xs",
    row: "h-8 w-8 text-sm",
    card: "h-14 w-14 text-lg",
    panel: "h-[88px] w-[88px] text-2xl"
  }[size];
  return (
    <span
      data-testid={testId}
      data-side={side}
      className={cn(
        "flex flex-none items-center justify-center rounded-full border-2 font-display uppercase text-text",
        side === "plant" ? "border-side-plant bg-lawn/30" : "border-side-zombie bg-panel-raised",
        sizeClass
      )}
    >
      {initial}
    </span>
  );
}

export function SideBadge({ side }: { side: ActorView["side"] }) {
  return (
    <span
      data-testid="actor-side"
      className={cn(
        "inline-flex items-center gap-1 text-xs",
        side === "plant" ? "text-side-plant" : "text-side-zombie"
      )}
    >
      <i className={cn("h-1.5 w-1.5 rounded-full", side === "plant" ? "bg-side-plant" : "bg-side-zombie")} />
      {side === "plant" ? "Plant" : "Zombie"}
    </span>
  );
}

export function LevelTag({ level }: { level: number }) {
  return (
    <span className="rounded-sm border border-border-control px-1.5 py-0.5 text-xs text-muted" data-testid="actor-level">
      Lv {level}
    </span>
  );
}

/** The sealed-contract UX for a not-yet-servable field (game-gui-map.md's contract section): the reason is rendered, not hidden. */
export function PendingNote({ pending, testId }: { pending: Pending<unknown>; testId?: string }) {
  if (pending.state !== "pending") return null;
  return (
    <p className="text-xs italic text-faint" data-testid={testId}>
      {pending.reason}
    </p>
  );
}

export function displayInitial(name: Pending<string> | undefined, side: ActorView["side"]): string {
  if (name && name.state === "known" && name.value.length > 0) {
    return [...name.value][0]!.toUpperCase();
  }
  return side === "plant" ? "P" : "Z";
}
