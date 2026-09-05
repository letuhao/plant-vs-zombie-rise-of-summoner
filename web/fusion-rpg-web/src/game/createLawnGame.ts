import Phaser from "phaser";
import { createGame } from "./createGame";
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
 *
 * base-defense `board-render`: a thin wrapper over the generic `createGame` factory, supplying the
 * lawn's own scene list — byte-identical to this function's own pre-extraction body (same width/
 * height defaults, same "#16120e" background, same scene array, same preBoot generation write).
 */
export function createLawnGame(opts: CreateLawnGameOptions): Phaser.Game {
  return createGame({ ...opts, scenes: [BootScene, LawnWorldScene] });
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
