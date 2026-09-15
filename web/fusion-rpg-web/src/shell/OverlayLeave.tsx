import { useState } from "react";
import { Button } from "@/ui";
import { postOverlayLeave } from "@/lib/bus";
import { useOverlayEmbed } from "./useOverlayEmbed";

/**
 * rift-gate overlay-hide: the page's **Leave** control.
 *
 * Rendered **only when the page is embedded** (the host's `?embed=1` marker, decision 16). A plain
 * browser visit gets no control at all rather than a dead button — the host keys (Esc/F10) belong to
 * the native window, not to the page, so in a browser there is nothing for Leave to ask.
 *
 * One action: ask the host to close the overlay. It touches no story state.
 */
export function OverlayLeave() {
  const embedded = useOverlayEmbed();
  const [busy, setBusy] = useState(false);

  if (!embedded) return null;

  async function leave() {
    if (busy) return;
    setBusy(true);
    try {
      await postOverlayLeave();
      // The host hides the window; we do not navigate or unmount anything ourselves, so a failure
      // here is only ever a log — the player can use the host keys instead.
    } catch {
      /* the native host keeps its own Esc/F10; Leave is a convenience, never the only way out */
    } finally {
      setBusy(false);
    }
  }

  return (
    <Button
      variant="ghost"
      size="sm"
      data-testid="overlay-leave"
      disabled={busy}
      onClick={() => void leave()}
    >
      Leave
    </Button>
  );
}
