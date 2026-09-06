import Phaser from "phaser";
import {
  worldBusEmit,
  worldBusOn,
  type WorldCameraPayload,
  type WorldInteractionPayload,
  type WorldModelPayload,
  type WorldResizedPayload
} from "../../EventBus";
import { snapshotTheme, type WorldTheme } from "../snapshotTheme";
import { WorldRegistry } from "../entities/WorldRegistry";
import { syncWorldSystem } from "../systems/syncWorldSystem";
import { worldCameraSystem } from "../systems/worldCameraSystem";
import { worldPickSystem } from "../systems/worldPickSystem";
import { worldOverlaySystem } from "../systems/worldOverlaySystem";
import { zoomTier, type ZoomTier } from "../zoomTier";

/**
 * Persistent world map while #/world is mounted.
 * Systems allow-list: Sync → Camera/LOD → Pick → Overlay (RT-08 analogue).
 * No WorldBootScene in v1 — framed placeholders live here (D10).
 */
export class WorldMapScene extends Phaser.Scene {
  private generation = 0;
  private theme: WorldTheme = snapshotTheme();
  private worldRegistry = new WorldRegistry();
  private lastApplied = 0;
  private model: unknown = null;
  private tier: ZoomTier = "map";
  private offs: Array<() => void> = [];
  private interaction: WorldInteractionPayload | null = null;
  private lens = "ownership";

  constructor() {
    super("WorldMapScene");
  }

  create(): void {
    this.generation = (this.game.registry.get("generation") as number) ?? 0;
    const bootTheme = this.game.registry.get("worldTheme") as WorldTheme | undefined;
    if (bootTheme) this.theme = bootTheme;

    this.cameras.main.setBackgroundColor(this.theme.soil);
    this.data.set("worldRegistry", this.worldRegistry);
    this.drawPlaceholderGrid();

    this.offs.push(
      worldBusOn("world:model", (raw) => {
        const p = raw as WorldModelPayload;
        if (p.generation !== this.generation) return;
        const applied = syncWorldSystem({
          scene: this,
          theme: this.theme,
          registry: this.worldRegistry,
          lastApplied: this.lastApplied,
          modelSeq: p.modelSeq,
          model: p.model,
          tier: this.tier
        });
        if (applied != null) {
          this.lastApplied = applied;
          this.model = p.model;
          worldOverlaySystem({
            scene: this,
            theme: this.theme,
            registry: this.worldRegistry,
            model: this.model,
            interaction: this.interaction,
            lens: this.lens,
            tier: this.tier
          });
        }
      })
    );

    this.offs.push(
      worldBusOn("world:camera", (raw) => {
        const p = raw as WorldCameraPayload;
        if (p.generation !== this.generation) return;
        worldCameraSystem.applyCommand(this, p);
        this.refreshLod();
      })
    );

    this.offs.push(
      worldBusOn("world:resized", (raw) => {
        const p = raw as WorldResizedPayload;
        if (p.generation !== this.generation) return;
        this.scale.resize(p.width, p.height);
      })
    );

    this.offs.push(
      worldBusOn("world:interaction", (raw) => {
        const p = raw as WorldInteractionPayload;
        if (p.generation !== this.generation) return;
        this.interaction = p;
        worldOverlaySystem({
          scene: this,
          theme: this.theme,
          registry: this.worldRegistry,
          model: this.model,
          interaction: this.interaction,
          lens: this.lens,
          tier: this.tier
        });
      })
    );

    this.offs.push(
      worldBusOn("world:lens", (raw) => {
        const p = raw as { generation?: number; lens?: string };
        if (p.generation !== this.generation) return;
        this.lens = p.lens ?? "ownership";
        console.info("[world-scene]", { event: "lens", generation: this.generation, lens: this.lens });
        worldOverlaySystem({
          scene: this,
          theme: this.theme,
          registry: this.worldRegistry,
          model: this.model,
          interaction: this.interaction,
          lens: this.lens,
          tier: this.tier
        });
      })
    );

    worldPickSystem.wire(this, this.generation, () => this.interaction?.ignoreRects ?? []);
    worldCameraSystem.wirePointer(this, () => this.refreshLod());

    worldBusEmit("world:ready", { generation: this.generation });

    // Observability — host and e2e can probe mount generation + pin screen coords.
    if (typeof window !== "undefined") {
      const w = window as unknown as {
        __fusionRpgWorldGen?: number;
        __fusionRpgWorldProbe?: {
          generation: number;
          pinScreen: (sectorId: string) => { x: number; y: number } | null;
          pinCount: () => number;
          emitSelect?: (kind: string, id: string) => void;
          pickAt?: (cssX: number, cssY: number) => string | null;
        };
      };
      w.__fusionRpgWorldGen = this.generation;
      w.__fusionRpgWorldProbe = {
        generation: this.generation,
        pinCount: () => this.worldRegistry.sectorIds().length,
        pinScreen: (sectorId: string) => {
          const go = this.worldRegistry.getSector(sectorId) as Phaser.GameObjects.Container | undefined;
          if (!go) return null;
          const bounds = go.getBounds?.();
          if (!bounds) return null;
          return { x: bounds.centerX, y: bounds.centerY };
        },
        emitSelect: (kind: string, id: string) => {
          worldBusEmit("world:select", {
            generation: this.generation,
            kind: kind as "sector" | "force" | "lane" | "empty",
            id
          });
        },
        /** Hit-test at canvas CSS coords — same math as DOM pointerup pick. */
        pickAt: (cssX: number, cssY: number) => {
          const world = this.cameras.main.getWorldPoint(cssX, cssY);
          for (const id of this.worldRegistry.sectorIds()) {
            const go = this.worldRegistry.getSector(id) as Phaser.GameObjects.Container | undefined;
            if (!go) continue;
            const dx = go.x - world.x;
            const dy = go.y - world.y;
            if (dx * dx + dy * dy <= 28 * 28) {
              worldBusEmit("world:select", { generation: this.generation, kind: "sector", id });
              return id;
            }
          }
          worldBusEmit("world:select", { generation: this.generation, kind: "empty" });
          return null;
        }
      };
    }
  }

  update(_time: number, delta: number): void {
    worldCameraSystem.tickEdgeScroll(this, delta);
  }

  shutdown(): void {
    for (const off of this.offs) off();
    this.offs = [];
    worldPickSystem.unwire(this);
    worldCameraSystem.unwire(this);
    this.worldRegistry.clear(this);
    worldOverlaySystem.clear(this);
  }

  private refreshLod(): void {
    const next = zoomTier(this.cameras.main.zoom);
    if (next === this.tier) return;
    this.tier = next;
    if (this.model != null) {
      syncWorldSystem({
        scene: this,
        theme: this.theme,
        registry: this.worldRegistry,
        lastApplied: this.lastApplied - 1,
        modelSeq: this.lastApplied,
        model: this.model,
        tier: this.tier,
        forceLodRefresh: true
      });
    }
  }

  private drawPlaceholderGrid(): void {
    const g = this.add.graphics();
    g.lineStyle(1, Phaser.Display.Color.HexStringToColor(this.theme.panel).color, 0.35);
    const step = 220;
    for (let x = -2000; x <= 4000; x += step) g.lineBetween(x, -2000, x, 4000);
    for (let y = -2000; y <= 4000; y += step) g.lineBetween(-2000, y, 4000, y);
    g.setDepth(-100);
    g.setName("world-backdrop-grid");
  }
}
