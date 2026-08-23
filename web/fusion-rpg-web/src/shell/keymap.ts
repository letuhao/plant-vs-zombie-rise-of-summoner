import type { Band } from "./layerStack";
import { useLayerStack } from "./layerStack";

/**
 * GG-5's dismiss column: Panel/Dialog/System go via Esc, close or
 * click-away; Toast only auto-expires, HUD never dismisses, Shell is
 * explicit-only. Esc must skip a Toast even if one is nominally "on top" of
 * the raw push order — this is what encodes that.
 */
const ESCAPE_DISMISSIBLE_BANDS: ReadonlySet<Band> = new Set(["panel", "dialog", "system"]);

/**
 * Reserved for the injector overlay's own resume hotkey (see
 * OverlaySwitchLayout.cs / the F10 resume note in local dev docs). The app
 * must never bind it — registering it is a programming error, not a runtime
 * condition to handle gracefully.
 */
const FORBIDDEN_KEYS: ReadonlySet<string> = new Set(["F10"]);

type Registration = { id: string; handler: () => void };

const registry = new Map<string, Registration>();
let emptyStackEscapeFallback: { id: string; handler: () => void } | null = null;

/**
 * Every global verb — everything a keydown does regardless of which layer
 * has focus — is registered here, in this one module, so a lint can prove
 * nothing else binds one (T3's guard). Escape is not registered through
 * this; it is a distinct built-in (see `handleEscape`) because GG-6 gives it
 * fixed, non-overridable stack semantics rather than a swappable handler.
 */
export function registerGlobalVerb(key: string, id: string, handler: () => void): () => void {
  if (FORBIDDEN_KEYS.has(key)) {
    throw new Error(
      `registerGlobalVerb: "${key}" is reserved for the injector overlay's own hotkey and must never be handled by the app.`
    );
  }
  const existing = registry.get(key);
  if (existing) {
    throw new Error(
      `registerGlobalVerb: "${key}" is already registered by "${existing.id}" — every global verb has exactly one owner.`
    );
  }
  registry.set(key, { id, handler });
  return () => {
    if (registry.get(key)?.id === id) registry.delete(key);
  };
}

export function dispatchGlobalVerb(key: string): boolean {
  const reg = registry.get(key);
  if (!reg) return false;
  reg.handler();
  return true;
}

/**
 * Claimed once, by whatever owns the System layer (T20). Esc on an empty
 * stack opens it (GG-5's Shell/System row: System is reachable from
 * anywhere, including nowhere).
 */
export function registerEmptyStackEscapeFallback(id: string, handler: () => void): () => void {
  if (emptyStackEscapeFallback) {
    throw new Error(
      `registerEmptyStackEscapeFallback: already claimed by "${emptyStackEscapeFallback.id}" — only the System layer may own this.`
    );
  }
  emptyStackEscapeFallback = { id, handler };
  return () => {
    if (emptyStackEscapeFallback?.id === id) emptyStackEscapeFallback = null;
  };
}

/**
 * GG-6: Esc pops exactly one layer. It finds the topmost Esc-dismissible
 * layer (skipping Toasts) and tells it to close itself via the entry's own
 * `close` — never mutates the stack array directly, so there is exactly one
 * path to "this layer closes" no matter who triggered it (Esc, a close
 * button, click-away). An empty stack falls through to the System layer.
 */
export function handleEscape(): void {
  const layers = useLayerStack.getState().layers;
  for (let i = layers.length - 1; i >= 0; i -= 1) {
    const layer = layers[i]!;
    if (ESCAPE_DISMISSIBLE_BANDS.has(layer.band)) {
      layer.close();
      return;
    }
  }
  emptyStackEscapeFallback?.handler();
}

/** Test-only: both registries are module-level singletons. */
export function resetKeymapForTests(): void {
  registry.clear();
  emptyStackEscapeFallback = null;
}
