import { type ReactNode, useEffect, useId, useRef } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import { cn } from "@/lib/cn";
import { useLayerStack } from "./layerStack";

export type PanelShellProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  subtitle?: string;
  footer?: ReactNode;
  children: ReactNode;
  testId?: string;
};

/**
 * Band-2 shell (GG-5): Roster, inventory, almanac, log, shop, progression.
 * Bounded per GG-61 — header/footer are `flex-none`, the body is the only
 * part that scrolls, and the shell itself never grows past
 * `min(720px, 82vh)`. Focus trap and focus-restore-to-opener come from Radix
 * Dialog's own FocusScope; this wrapper does not override either.
 */
export function PanelShell({
  open,
  onOpenChange,
  title,
  subtitle,
  footer,
  children,
  testId = "panel-shell"
}: PanelShellProps) {
  const id = useId();
  const push = useLayerStack((state) => state.push);
  const pop = useLayerStack((state) => state.pop);

  useEffect(() => {
    if (!open) return;
    push({ id, band: "panel" });
    return () => pop(id);
  }, [open, id, push, pop]);

  // Radix restores focus to its own Trigger by default; PanelShell is fully
  // controlled and has no Dialog.Trigger, so that default is a no-op. Capture
  // whatever had focus just before this open (during render, so it runs
  // before any child mounts and steals focus) and restore it ourselves.
  const openerRef = useRef<HTMLElement | null>(null);
  const wasOpenRef = useRef(open);
  if (open && !wasOpenRef.current) {
    openerRef.current = (document.activeElement as HTMLElement) ?? null;
  }
  wasOpenRef.current = open;

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="band-panel fixed inset-0 bg-black/50" data-testid={`${testId}-overlay`} />
        <Dialog.Content
          data-testid={testId}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            openerRef.current?.focus();
          }}
          className={cn(
            "band-panel fixed left-1/2 top-1/2 flex w-[min(640px,92vw)] -translate-x-1/2 -translate-y-1/2",
            "flex-col overflow-hidden rounded-md border border-border bg-panel shadow-panel",
            "max-h-[min(720px,82vh)]"
          )}
        >
          <header className="flex flex-none items-start gap-3 border-b border-border bg-soil-raised px-4 py-3">
            <div className="min-w-0">
              <Dialog.Title className="truncate font-display text-xl text-text">{title}</Dialog.Title>
              <Dialog.Description className={subtitle ? "text-xs text-muted" : "sr-only"}>
                {subtitle ?? title}
              </Dialog.Description>
            </div>
          </header>
          <div
            className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-4 py-4"
            data-testid={`${testId}-body`}
          >
            {children}
          </div>
          {footer ? (
            <footer className="flex flex-none justify-end gap-2 border-t border-border bg-soil-raised px-4 py-3">
              {footer}
            </footer>
          ) : null}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
