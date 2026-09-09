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
 * Left-rail identity — essential actor recognition only. Collapsed: portrait/glyph + tooltip.
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
  const species = sheet?.speciesName ?? null;
  const phase = sheet?.phase ?? null;
  const meta = [`Lv ${sheet?.level ?? level}`, roleLabel].join(" · ");
  const tip = [name, species, meta].filter(Boolean).join(" · ");
  const initial = displayInitial(displayName, side);
  const elements = sheet?.elementTyping
    ? [sheet.elementTyping.primary, ...(sheet.elementTyping.secondary ? [sheet.elementTyping.secondary] : [])]
    : [];

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
          <div className="mt-2 flex flex-wrap gap-1.5 text-[10px] uppercase tracking-wide text-muted">
            <span className="rounded-full border border-border-control px-2 py-0.5">{side}</span>
            {species ? <span className="rounded-full border border-border-control px-2 py-0.5">{species}</span> : null}
            {phase ? <span className="rounded-full border border-border-control px-2 py-0.5">{phase}</span> : null}
            {elements.map((element) => (
              <span key={element} className="rounded-full border border-border-control px-2 py-0.5" data-testid={`actor-summarize-element-${element}`}>
                {element}
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
