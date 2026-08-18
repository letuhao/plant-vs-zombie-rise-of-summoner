import Phaser from "phaser";

/** Preload placeholder; hand off to LawnWorld with generation. */
export class BootScene extends Phaser.Scene {
  constructor() {
    super({ key: "BootScene" });
  }

  preload(): void {
    const g = this.make.graphics({ x: 0, y: 0 }, false);
    g.fillStyle(0x5a8f62, 1);
    g.fillRoundedRect(0, 0, 40, 40, 4);
    g.generateTexture("lawn-placeholder", 40, 40);
    g.destroy();
  }

  create(): void {
    const generation = (this.game.registry.get("generation") as number) ?? 0;
    this.scene.start("LawnWorldScene", { generation });
  }
}
