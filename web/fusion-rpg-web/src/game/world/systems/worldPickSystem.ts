import Phaser from "phaser";
import { worldBusEmit, type WorldIgnoreRect } from "../../EventBus";
import type { WorldRegistry } from "../entities/WorldRegistry";
import { worldCameraSystem } from "./worldCameraSystem";
import { hitRadiusWorld, nearestSectorId } from "./worldPickHit";
import { cssToGamePoint, gameToCssPoint } from "./worldPickCoords";

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
  scene: Phaser.Scene,
  registry: WorldRegistry,
  worldX: number,
  worldY: number
): string | null {
  const hitR = hitRadiusWorld(scene.cameras.main.zoom);
  const pins = registry.sectorIds().flatMap((id) => {
    const go = registry.getSector(id);
    if (!go || !go.active) return [];
    const container = go as Phaser.GameObjects.Container;
    return [{ id, x: container.x, y: container.y }];
  });
  return nearestSectorId(pins, worldX, worldY, hitR);
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

  /**
   * Phaser pointerup + DOM pointerup both fire for one mouse click. Sector select toggles,
   * so a second emit of the same id clears selection (gaps D21 honest pick). One gesture → one emit.
   */
  let pickConsumed = false;

  const emitSelect = (payload: { kind: string; id?: string }) => {
    if (pickConsumed) return;
    pickConsumed = true;
    worldBusEmit("world:select", { generation, ...payload });
  };

  const emitEmpty = () => emitSelect({ kind: "empty" });

  /** ignoreRects are authored in canvas CSS px (gaps D7). */
  const blockedByChromeOrDragCss = (cssX: number, cssY: number): boolean => {
    if (inIgnoreRect(cssX, cssY, getIgnoreRects())) return true;
    if (worldCameraSystem.isPickSuppressed(scene)) return true;
    return false;
  };

  const pointerToCss = (pointer: Phaser.Input.Pointer) => {
    const canvas = scene.game.canvas as HTMLCanvasElement;
    return gameToCssPoint(pointer.x, pointer.y, scene.scale.width, scene.scale.height, canvas.clientWidth, canvas.clientHeight);
  };

  const onPointerDown = () => {
    pickConsumed = false;
  };

  const onPointerUp = (pointer: Phaser.Input.Pointer) => {
    if (pointer.rightButtonReleased()) return;
    const css = pointerToCss(pointer);
    if (blockedByChromeOrDragCss(css.x, css.y)) return;

    const reg = (scene.data.get("worldRegistry") as WorldRegistry | undefined) ?? registry;
    if (reg) {
      const sectorId = resolveSectorHit(scene, reg, pointer.worldX, pointer.worldY);
      if (sectorId) {
        emitSelect({ kind: "sector", id: sectorId });
        return;
      }
    }

    emitEmpty();
  };

  const onContextMenu = (pointer: Phaser.Input.Pointer) => {
    pointer.event?.preventDefault?.();
    const css = pointerToCss(pointer);
    if (inIgnoreRect(css.x, css.y, getIgnoreRects())) return;
    // Context clear is its own gesture — allow even if a left-pick already consumed.
    pickConsumed = false;
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

  const onGameObjectUp = (pointer: Phaser.Input.Pointer, go: Phaser.GameObjects.GameObject) => {
    const css = pointerToCss(pointer);
    if (blockedByChromeOrDragCss(css.x, css.y)) return;

    const name = namedAncestor(go);
    if (name?.startsWith("pin:")) {
      emitSelect({ kind: "sector", id: name.slice("pin:".length) });
      return;
    }
    if (name?.startsWith("force:")) {
      emitSelect({ kind: "force", id: name.slice("force:".length) });
      return;
    }
    if (name?.startsWith("lane:")) {
      emitSelect({ kind: "lane", id: name.slice("lane:".length) });
    }
  };

  /** DOM path — Playwright / WebView often miss Phaser's synthetic pointer stream. */
  const onDomPointerUp = (ev: PointerEvent) => {
    if (ev.button !== 0) return;
    if (pickConsumed) return;
    const canvas = scene.game.canvas as HTMLCanvasElement | undefined;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const cssX = ev.clientX - rect.left;
    const cssY = ev.clientY - rect.top;
    if (blockedByChromeOrDragCss(cssX, cssY)) return;

    const game = cssToGamePoint(cssX, cssY, scene.scale.width, scene.scale.height, rect.width, rect.height);
    const world = scene.cameras.main.getWorldPoint(game.x, game.y);
    const reg = (scene.data.get("worldRegistry") as WorldRegistry | undefined) ?? registry;
    if (reg) {
      const sectorId = resolveSectorHit(scene, reg, world.x, world.y);
      if (sectorId) {
        emitSelect({ kind: "sector", id: sectorId });
        return;
      }
    }
    emitEmpty();
  };

  scene.input.on("pointerdown", onPointerDown);
  scene.input.on("pointerup", onPointerUp);
  scene.input.on("pointerupoutside", onPointerUp);
  scene.input.on("contextmenu", onContextMenu);
  scene.input.on("gameobjectup", onGameObjectUp);
  const canvasEl = scene.game.canvas as HTMLCanvasElement | undefined;
  canvasEl?.addEventListener("pointerup", onDomPointerUp);

  s.offs.push(
    () => scene.input.off("pointerdown", onPointerDown),
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
