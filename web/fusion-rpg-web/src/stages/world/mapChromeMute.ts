/**
 * GG-18 mute for world map chrome verbs (gaps D8).
 * When the top Esc-dismissible band is panel/dialog, pan and lens hotkeys no-op.
 */
import type { Band } from "@/shell/layerStack";
import { useLayerStack } from "@/shell/layerStack";

const MUTE_BANDS: ReadonlySet<Band> = new Set(["panel", "dialog"]);

/** Topmost panel/dialog blocks map chrome verbs; stage/hud/toast/system do not (system owns Esc). */
export function isWorldMapChromeMuted(): boolean {
  const layers = useLayerStack.getState().layers;
  for (let i = layers.length - 1; i >= 0; i--) {
    const band = layers[i]!.band;
    if (MUTE_BANDS.has(band)) return true;
    if (band === "system") return false;
  }
  return false;
}
