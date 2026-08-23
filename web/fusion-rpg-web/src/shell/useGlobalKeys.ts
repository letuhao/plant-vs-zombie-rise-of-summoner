import { useEffect } from "react";
import { dispatchGlobalVerb, handleEscape } from "./keymap";

/**
 * The one `keydown` listener for every global verb (T3). Mounted once, at
 * the app root — see `AppShell`. Escape is handled inline (GG-6's fixed
 * stack semantics); anything else goes through the swappable verb registry.
 */
export function useGlobalKeys(): void {
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        handleEscape();
        return;
      }
      dispatchGlobalVerb(event.key);
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
}
