import type Phaser from "phaser";

/** Cosmetic FX object pool — reset checklist on release (RT-07). */
export class FxPool {
  private readonly free: Phaser.GameObjects.Arc[] = [];

  constructor(private readonly scene: Phaser.Scene) {}

  acquireRing(): Phaser.GameObjects.Arc {
    const ring = this.free.pop();
    if (ring) {
      ring.setActive(true).setVisible(true).setAlpha(1);
      return ring;
    }
    return this.scene.add.circle(0, 0, 22, 0xe0b44b, 0).setStrokeStyle(2, 0xe0b44b, 0.9);
  }

  release(ring: Phaser.GameObjects.Arc): void {
    this.scene.tweens.killTweensOf(ring);
    ring.setActive(false).setVisible(false).setAlpha(1);
    this.free.push(ring);
  }

  drain(): void {
    for (const ring of this.free) {
      this.scene.tweens.killTweensOf(ring);
      ring.destroy();
    }
    this.free.length = 0;
  }
}
