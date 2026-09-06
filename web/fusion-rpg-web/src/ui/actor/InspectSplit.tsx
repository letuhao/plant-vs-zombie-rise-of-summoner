import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/** Plate-13 list | inspector split. Each pane may scroll independently; never pushes band 3. */
export function InspectSplit({
  list,
  inspector,
  className,
  testId = "inspect-split"
}: {
  list: ReactNode;
  inspector: ReactNode;
  className?: string;
  testId?: string;
}) {
  return (
    <div
      data-testid={testId}
      className={cn(
        "mt-4 grid min-h-0 gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(240px,0.42fr)]",
        className
      )}
    >
      <div className="min-h-0 min-w-0" data-testid={`${testId}-list`}>
        {list}
      </div>
      <aside
        className="min-h-0 space-y-3 overflow-y-auto rounded-sm border border-border bg-soil-raised p-3"
        data-testid={`${testId}-inspector`}
      >
        {inspector}
      </aside>
    </div>
  );
}
