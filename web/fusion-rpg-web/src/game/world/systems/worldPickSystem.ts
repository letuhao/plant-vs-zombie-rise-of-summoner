import Phaser from "phaser";
import { worldBusEmit, type WorldIgnoreRect } from "../../EventBus";
import type { WorldRegistry } from "../entities/WorldRegistry";

const WIRE_KEY = "worldPick";

type PickWireState = {
  generation: number;
  getIgnoreRects: () => WorldIgnoreRect[];
  offs: Array<() => void>;
};

function pickState(scene: Phaser.Scene): PickWireState | undefined {
  return scene.data.get(WIRE_KEY) as PickWireState | undefined;
}

function inIgnoreRect(x: number, y: number, rects: WorldIgnoreRect[]): boolean {
  for (const r of rects) {
    if (x >= r.left && x <= r.left + r.width && y >= r.top && y <= r.top + r.height) return true;
  }
  return false;
}

function resolveSectorHit(
  registry: WorldRegistry,
  worldX: number,
  worldY: number
): string | null {
  // Hit radius in world units — PIN_DISC_PX is screen-ish at zoom 1; use half-disc in world space.
  const hitR = 28;
  let best: string | null = null;
  let bestDist = hitR * hitR;
  for (const id of registry.sectorIds()) {
    const go = registry.getSector(id);
    if (!go || !go.active) continue;
    const container = go as Phaser.GameObjects.Container;
    const dx = container.x - worldX;
    const dy = container.y - worldY;
    const d2 = dx * dx + dy * dy;
    if (d2 <= bestDist) {
      bestDist = d2;
      best = id;
    }
  }
  return best;
}

function wire(
  scene: Phaser.Scene,
  generation: number,
  getIgnoreRects: () => WorldIgnoreRect[]
): void {
  unwire(scene);
  const registry = scene.data.get("worldRegistry") as WorldRegistry | undefined;

  const s: PickWireState = { generation, getIgnoreRects, offs: [] };
  scene.data.set(WIRE_KEY, s);
  let handledPick = false;

  const emitEmpty = () => {
    worldBusEmit("world:select", { generation, kind: "empty" });
  };

  const onPointerUp = (pointer: Phaser.Input.Pointer) => {
    if (handledPick) {
      handledPick = false;
      return;
    }
    if (pointer.rightButtonReleased()) return;
    const sx = pointer.x;
    const sy = pointer.y;
    if (inIgnoreRect(sx, sy, getIgnoreRects())) return;

    const reg = (scene.data.get("worldRegistry") as WorldRegistry | undefined) ?? registry;
    if (reg) {
      const sectorId = resolveSectorHit(reg, pointer.worldX, pointer.worldY);
      if (sectorId) {
        worldBusEmit("world:select", { generation, kind: "sector", id: sectorId });
        return;
      }
    }

    emitEmpty();
  };

  const onContextMenu = (pointer: Phaser.Input.Pointer) => {
    pointer.event?.preventDefault?.();
    if (inIgnoreRect(pointer.x, pointer.y, getIgnoreRects())) return;
    emitEmpty();
  };

  const namedAncestor = (go: Phaser.GameObjects.GameObject): string | undefined => {
    let cur: Phaser.GameObjects.GameObject | null = go;
    while (cur) {
      if (cur.name) return cur.name;
      cur = (cur as Phaser.GameObjects.Container).parentContainer ?? null;
    }
    return undefined;
  };

  const onGameObjectUp = (
    _pointer: Phaser.Input.Pointer,
    go: Phaser.GameObjects.GameObject
  ) => {
    const name = namedAncestor(go);
    if (name?.startsWith("pin:")) {
      handledPick = true;
      const id = name.slice("pin:".length);
      worldBusEmit("world:select", { generation, kind: "sector", id });
      return;
    }
    if (name?.startsWith("force:")) {
      handledPick = true;
      const id = name.slice("force:".length);
      worldBusEmit("world:select", { generation, kind: "force", id });
      return;
    }
    if (name?.startsWith("lane:")) {
      handledPick = true;
      const id = name.slice("lane:".length);
      worldBusEmit("world:select", { generation, kind: "lane", id });
    }
  };

  /** DOM path — Playwright / WebView often miss Phaser's synthetic pointer stream. */
  const onDomPointerUp = (ev: PointerEvent) => {
    if (ev.button !== 0) return;
    const canvas = scene.game.canvas as HTMLCanvasElement | undefined;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const sx = ((ev.clientX - rect.left) / rect.width) * canvas.width;
    const sy = ((ev.clientY - rect.top) / rect.height) * canvas.height;
    // Prefer CSS pixel space used by Phaser Scale Manager (not backing-store pixels).
    const cssX = ev.clientX - rect.left;
    const cssY = ev.clientY - rect.top;
    void sx;
    void sy;
    if (inIgnoreRect(cssX, cssY, getIgnoreRects())) return;
    const world = scene.cameras.main.getWorldPoint(cssX, cssY);
    const reg = (scene.data.get("worldRegistry") as WorldRegistry | undefined) ?? registry;
    if (reg) {
      const sectorId = resolveSectorHit(reg, world.x, world.y);
      if (sectorId) {
        worldBusEmit("world:select", { generation, kind: "sector", id: sectorId });
        return;
      }
    }
    emitEmpty();
  };

  scene.input.on("pointerup", onPointerUp);
  scene.input.on("pointerupoutside", onPointerUp);
  scene.input.on("contextmenu", onContextMenu);
  scene.input.on("gameobjectup", onGameObjectUp);
  const canvasEl = scene.game.canvas as HTMLCanvasElement | undefined;
  canvasEl?.addEventListener("pointerup", onDomPointerUp);

  s.offs.push(
    () => scene.input.off("pointerup", onPointerUp),
    () => scene.input.off("pointerupoutside", onPointerUp),
    () => scene.input.off("contextmenu", onContextMenu),
    () => scene.input.off("gameobjectup", onGameObjectUp),
    () => canvasEl?.removeEventListener("pointerup", onDomPointerUp)
  );
}

function unwire(scene: Phaser.Scene): void {
  const s = pickState(scene);
  if (!s) return;
  for (const off of s.offs) off();
  scene.data.remove(WIRE_KEY);
}

export const worldPickSystem = { wire, unwire };
