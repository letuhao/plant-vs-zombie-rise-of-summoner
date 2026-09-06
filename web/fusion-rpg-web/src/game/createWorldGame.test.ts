import { describe, expect, it, vi } from "vitest";

vi.mock("phaser", () => ({
  default: {
    AUTO: "AUTO",
    Scale: { RESIZE: "RESIZE", NO_CENTER: "NO_CENTER" },
    Display: { Color: { HexStringToColor: () => ({ color: 0x2a241c }) } }
  }
}));

vi.mock("./world/scenes/WorldMapScene", () => ({
  WorldMapScene: class {}
}));

vi.mock("./world/snapshotTheme", () => ({
  snapshotTheme: () => ({
    soil: "#16120e",
    panel: "#2a241c",
    sun: "#e8c547",
    ink: "#f2ebe0",
    sidePlant: "#6fbf73",
    sideZombie: "#c45c5c",
    fontFamily: "serif"
  })
}));

import { buildGameConfig } from "./createGame";
import { WorldMapScene } from "./world/scenes/WorldMapScene";

describe("createWorldGame config shape", () => {
  it("would pass WorldMapScene alone (no BootScene)", () => {
    const parent = { clientWidth: 1280, clientHeight: 720 } as unknown as HTMLElement;
    const config = buildGameConfig({
      parent,
      scenes: [WorldMapScene as unknown as Phaser.Types.Scenes.SceneType],
      generation: 1,
      backgroundColor: "#16120e"
    });
    expect(config.scene).toHaveLength(1);
    expect(config.backgroundColor).toBe("#16120e");
  });
});
