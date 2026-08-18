import { cn } from "@/lib/cn";

export function EmptyState({
  title,
  hint,
  className
}: {
  title: string;
  hint?: string;
  className?: string;
}) {
  return (
    <div className={cn("rounded-md border border-dashed border-border px-4 py-6 text-center", className)}>
      <p className="font-semibold text-text">{title}</p>
      {hint ? <p className="mt-1 text-sm text-muted">{hint}</p> : null}
    </div>
  );
}
