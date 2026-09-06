import Phaser from "phaser";
import { createGame } from "./createGame";
import { worldBusEmit } from "./EventBus";
import { snapshotTheme } from "./world/snapshotTheme";
import { WorldMapScene } from "./world/scenes/WorldMapScene";

export type CreateWorldGameOptions = {
  parent: HTMLElement;
  generation: number;
  width?: number;
  height?: number;
};

/**
 * Facade: create Phaser.Game for the world map (RT-07).
 * Scene list is WorldMapScene only — no BootScene in v1.
 * Destroy order: tweens → scene shutdown → world:destroyed → game.destroy(true).
 */
export function createWorldGame(opts: CreateWorldGameOptions): Phaser.Game {
  const theme = snapshotTheme();
  const game = createGame({
    ...opts,
    scenes: [WorldMapScene],
    backgroundColor: theme.soil
  });
  game.registry.set("worldTheme", theme);
  return game;
}

export function destroyWorldGame(game: Phaser.Game | null, generation: number): void {
  if (!game) return;
  try {
    for (const scene of game.scene.getScenes(true)) {
      scene.tweens?.killAll();
    }
  } catch {
    /* */
  }
  try {
    const world = game.scene.getScene("WorldMapScene") as unknown as WorldMapScene;
    world?.shutdown?.();
  } catch {
    /* scene may already be gone */
  }
  worldBusEmit("world:destroyed", { generation });
  game.destroy(true);
}
