import { cn } from "@/lib/cn";
import { useToastStack } from "./toastStack";

/**
 * Band-4. Corner-positioned and `pointer-events-none` on the container so it
 * never blocks input and never sits over a centered Dialog (GG-5's "blocks
 * input below: no" plus a layout choice, not a z-index fight — Toast (400)
 * outranks Dialog (300) in the band order, so staying out of the dialog's
 * footprint is what keeps this true rather than the stacking order alone).
 */
export function Toasts() {
  const toasts = useToastStack((s) => s.toasts);

  return (
    <div
      className="band-toast pointer-events-none fixed bottom-4 right-4 flex flex-col items-end gap-2"
      data-testid="toast-stack"
      aria-live="polite"
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          data-testid={`toast-${toast.id}`}
          data-tone={toast.tone}
          role="status"
          className={cn(
            "pointer-events-auto min-w-[280px] max-w-[380px] rounded-sm border-l-4 bg-panel-raised px-3 py-2 text-sm shadow-panel",
            toast.tone === "ok" && "border-ok",
            toast.tone === "bad" && "border-bad",
            toast.tone === "warn" && "border-warn"
          )}
        >
          <p className="font-semibold text-text" data-testid="toast-title">
            {toast.title}
          </p>
          {toast.message ? (
            <p className="text-muted" data-testid="toast-message">
              {toast.message}
            </p>
          ) : null}
        </div>
      ))}
    </div>
  );
}
