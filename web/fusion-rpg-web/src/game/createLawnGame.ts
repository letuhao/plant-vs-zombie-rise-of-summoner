import Phaser from "phaser";
import { BootScene } from "./scenes/BootScene";
import { LawnWorldScene } from "./scenes/LawnWorldScene";
import { lawnBusEmit } from "./EventBus";

export type CreateLawnGameOptions = {
  parent: HTMLElement;
  generation: number;
  width?: number;
  height?: number;
};

/**
 * Facade: create Phaser.Game and destroy checklist (RT-07).
 * Order: tweens → FxPool/registry (via shutdown) → pick unsub → bus offs → destroy(true)
 */
export function createLawnGame(opts: CreateLawnGameOptions): Phaser.Game {
  const width = opts.width ?? (opts.parent.clientWidth || 640);
  const height = opts.height ?? (opts.parent.clientHeight || 480);

  return new Phaser.Game({
    type: Phaser.AUTO,
    width,
    height,
    parent: opts.parent,
    backgroundColor: "#16120e",
    scene: [BootScene, LawnWorldScene],
    scale: {
      mode: Phaser.Scale.RESIZE,
      autoCenter: Phaser.Scale.NO_CENTER
    },
    callbacks: {
      preBoot: (g) => {
        g.registry.set("generation", opts.generation);
      }
    }
  });
}

export function destroyLawnGame(
  game: Phaser.Game | null,
  generation: number
): void {
  if (!game) return;
  try {
    for (const scene of game.scene.getScenes(true)) {
      scene.tweens?.killAll();
    }
  } catch {
    /* */
  }
  try {
    const world = game.scene.getScene(
      "LawnWorldScene"
    ) as unknown as LawnWorldScene;
    world?.shutdown?.();
  } catch {
    /* scene may already be gone */
  }
  lawnBusEmit("lawn:destroyed", { generation });
  game.destroy(true);
}
