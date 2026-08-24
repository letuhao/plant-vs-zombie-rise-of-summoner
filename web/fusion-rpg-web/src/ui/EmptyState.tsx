import { cn } from "@/lib/cn";

export function EmptyState({
  title,
  hint,
  className,
  testId
}: {
  title: string;
  hint?: string;
  className?: string;
  testId?: string;
}) {
  return (
    <div className={cn("rounded-md border border-dashed border-border px-4 py-6 text-center", className)} data-testid={testId}>
      <p className="font-semibold text-text">{title}</p>
      {hint ? <p className="mt-1 text-sm text-muted">{hint}</p> : null}
    </div>
  );
}
