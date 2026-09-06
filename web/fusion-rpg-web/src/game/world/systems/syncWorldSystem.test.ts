import { describe, expect, it, vi } from "vitest";
import { WorldRegistry } from "../entities/WorldRegistry";
import { syncWorldSystem } from "./syncWorldSystem";
import { WORLD_THEME_FALLBACK } from "../snapshotTheme";

vi.mock("phaser", () => ({
  default: {
    Display: { Color: { HexStringToColor: () => ({ color: 0xffffff }) } }
  }
}));

function fakeScene() {
  const objects: Array<{ destroy: () => void; setDepth?: (d: number) => void; setName?: (n: string) => void; setData?: () => void; setSize?: () => void; setInteractive?: () => void }> = [];
  const mk = () => {
    const o = {
      destroy: vi.fn(),
      setDepth: vi.fn(),
      setName: vi.fn(),
      setData: vi.fn(),
      setSize: vi.fn(),
      setInteractive: vi.fn(),
      setOrigin: vi.fn(),
      add: vi.fn()
    };
    objects.push(o);
    return o;
  };
  return {
    add: {
      container: vi.fn(() => ({ ...mk(), add: vi.fn() })),
      graphics: vi.fn(mk),
      zone: vi.fn(mk),
      text: vi.fn(mk)
    },
    objects
  } as unknown as Phaser.Scene;
}

const emptyModel = {
  sectors: [],
  lanes: [],
  slotsBySectorId: {},
  forcesBySectorId: {}
};

describe("syncWorldSystem — modelSeq monotonic skip (RT-10)", () => {
  it("ignores modelSeq <= lastApplied unless forceLodRefresh", () => {
    const scene = fakeScene();
    const registry = new WorldRegistry();
    const base = {
      scene,
      theme: WORLD_THEME_FALLBACK,
      registry,
      model: emptyModel,
      tier: "map" as const
    };

    expect(syncWorldSystem({ ...base, lastApplied: 5, modelSeq: 5 })).toBeNull();
    expect(syncWorldSystem({ ...base, lastApplied: 5, modelSeq: 3 })).toBeNull();

    expect(syncWorldSystem({ ...base, lastApplied: 5, modelSeq: 6 })).toBe(6);
    expect(syncWorldSystem({ ...base, lastApplied: 6, modelSeq: 6 })).toBeNull();
  });

  it("forceLodRefresh applies at the same modelSeq for LOD tier changes", () => {
    const scene = fakeScene();
    const registry = new WorldRegistry();
    const applied = syncWorldSystem({
      scene,
      theme: WORLD_THEME_FALLBACK,
      registry,
      lastApplied: 4,
      modelSeq: 5,
      model: emptyModel,
      tier: "detail",
      forceLodRefresh: true
    });
    expect(applied).toBe(5);
  });
});
