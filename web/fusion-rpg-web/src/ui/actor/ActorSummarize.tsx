import { cn } from "@/lib/cn";
import { displayInitial } from "./shared";
import type { Pending } from "@/contract/pending";
import { isKnown } from "@/contract/pending";
import type { ActorSheetDto } from "@/lib/bus/aura";
import { TypeIcon } from "@/ui";

export type ActorSummarizeProps = {
  displayName: Pending<string>;
  instanceId: string;
  level: number;
  roleLabel: string;
  side: "plant" | "zombie";
  collapsed: boolean;
  sheet?: ActorSheetDto | null;
  className?: string;
};

/**
 * Left-rail identity — name / Lv · role only (+ portrait). Species chips live on Condition.
 */
export function ActorSummarize({
  displayName,
  instanceId,
  level,
  roleLabel,
  side,
  collapsed,
  sheet,
  className
}: ActorSummarizeProps) {
  const name = sheet?.displayName || (isKnown(displayName) ? displayName.value : `#${instanceId.slice(0, 6)}`);
  const effectiveRole = sheet?.roleLabel ?? roleLabel;
  const meta = [`Lv ${sheet?.level ?? level}`, effectiveRole].join(" · ");
  const tip = [name, meta].join(" · ");
  const initial = displayInitial(displayName, side);

  if (collapsed) {
    return (
      <div
        className={cn("flex justify-center", className)}
        data-testid="actor-summarize"
        data-collapsed="true"
        title={tip}
      >
        {sheet ? (
          <TypeIcon
            side={side}
            typeId={sheet.typeId}
            size={32}
            className="rounded-sm border border-border-control"
            testId="actor-summarize-portrait"
          />
        ) : (
          <span
            className="inline-flex h-8 w-8 items-center justify-center rounded-sm border border-border-control bg-panel-inset font-display text-sm text-text"
            data-testid="actor-summarize-glyph"
            aria-label={tip}
          >
            {initial}
          </span>
        )}
      </div>
    );
  }

  return (
    <div className={cn("min-w-0", className)} data-testid="actor-summarize" data-collapsed="false">
      <div className="flex items-start gap-3">
        {sheet ? (
          <TypeIcon side={side} typeId={sheet.typeId} size={48} className="border border-border-control" testId="actor-summarize-portrait" />
        ) : (
          <span
            className="inline-flex h-12 w-12 items-center justify-center rounded-sm border border-border-control bg-panel-inset font-display text-lg text-text"
            data-testid="actor-summarize-glyph"
          >
            {initial}
          </span>
        )}
        <div className="min-w-0 flex-1">
          <p className="truncate font-display text-lg leading-tight text-text" data-testid="actor-summarize-name">
            {name}
          </p>
          <p className="mt-0.5 truncate text-xs text-muted" data-testid="actor-summarize-meta">
            {meta}
          </p>
        </div>
      </div>
    </div>
  );
}
