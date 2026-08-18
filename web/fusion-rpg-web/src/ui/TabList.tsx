import { cn } from "@/lib/cn";

export type TabItem = {
  id: string;
  label: string;
  testId?: string;
};

export function TabList({
  tabs,
  value,
  onChange,
  className,
  testId = "tab-list"
}: {
  tabs: TabItem[];
  value: string;
  onChange: (id: string) => void;
  className?: string;
  testId?: string;
}) {
  return (
    <div
      data-testid={testId}
      role="tablist"
      className={cn("mb-4 flex flex-wrap gap-1 border-b border-border pb-2", className)}
    >
      {tabs.map((t) => {
        const active = t.id === value;
        return (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={active}
            data-testid={t.testId ?? `tab-${t.id}`}
            onClick={() => onChange(t.id)}
            className={cn(
              "rounded-sm px-3 py-1.5 font-ui text-sm transition-colors",
              active
                ? "bg-lawn text-almanac shadow-panel"
                : "bg-panel-inset text-muted hover:bg-soil-raised hover:text-text"
            )}
          >
            {t.label}
          </button>
        );
      })}
    </div>
  );
}
