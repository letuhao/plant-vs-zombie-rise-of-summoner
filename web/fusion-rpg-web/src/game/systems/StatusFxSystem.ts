import type { FxPool } from "../fx/FxPool";
import type { PtrEntityRegistry } from "../entities/PtrEntityRegistry";

/** Cosmetic status pulse / select ring — not EffectBag RNG. */
export function tickStatusFx(
  registry: PtrEntityRegistry,
  fx: FxPool,
  _delta: number
): void {
  for (const rec of registry.entries()) {
    if (rec.chips.length > 0 || rec.selected) {
      // Soft alpha pulse on container
      const t = performance.now() / 400;
      const a = 0.75 + Math.sin(t) * 0.2;
      rec.go.setAlpha(a);
    } else {
      rec.go.setAlpha(1);
    }
  }
  void fx;
}
