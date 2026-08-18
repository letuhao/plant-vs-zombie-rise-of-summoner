import Phaser from "phaser";
import type { LawnViewMode } from "@/features/lawn/lawnViewMode";
import type { LawnViewModel } from "@/features/lawn/lawnViewModel";
import {
  DEFAULT_COLS,
  DEFAULT_ROWS,
  listOccupants,
  normalizePtr
} from "@/features/lawn/lawnViewModel";
import { parseLawnViewMode } from "@/features/lawn/lawnViewMode";
import {
  PHASER_OCCUPANT_BUDGET,
  pickPhaserOccupants
} from "@/features/lawn/pickPhaserOccupants";
import {
  forceSyncLastApplied,
  shouldSyncLawnSprites
} from "@/features/lawn/lawnSyncGate";
import { subscribeIconEpoch } from "@/lib/bus/icon-epoch";
import { PtrEntityRegistry } from "../entities/PtrEntityRegistry";
import { FxPool } from "../fx/FxPool";
import {
  lawnBusEmit,
  lawnBusOn,
  type LawnInteractionPayload,
  type LawnModelPayload,
  type LawnViewModePayload
} from "../EventBus";
import { CELL_H, CELL_W, ORIGIN_X, ORIGIN_Y, lawnCameraZoom } from "../gridMath";
import { layoutGrid } from "../systems/LayoutGridSystem";
import { wirePickSystem } from "../systems/PickSystem";
import { tickStatusFx } from "../systems/StatusFxSystem";
import {
  applySelectionChrome,
  bustLawnIconTextures,
  refreshOccupantIcons,
  syncFromModel,
  wireLawnIconLoadErrors,
  type SyncContext
} from "../systems/SyncFromModelSystem";

export type LawnWorldInit = {
  generation: number;
};

/**
 * Persistent lawn world while #/lawn mounted.
 * Systems allow-list: Sync → Layout → StatusFx → Pick (RT-08).
 * Board-stats refresh is the React host, not this scene.
 */
export class LawnWorldScene extends Phaser.Scene {
  private generation = 0;
  private ptrRegistry = new PtrEntityRegistry();
  private fx!: FxPool;
  private lastApplied = 0;
  private model: LawnViewModel | null = null;
  private selectedPtr?: string;
  private viewMode: LawnViewMode = "split";
  private lastViewMode: LawnViewMode = "split";
  private lastCanvasKey = "";
  private unsubs: Array<() => void> = [];
  private pickUnsub?: () => void;
  private gridGfx?: Phaser.GameObjects.Graphics;
  private gridRows = 0;
  private gridCols = 0;
  private phaseText?: Phaser.GameObjects.Text;
  private ghost?: Phaser.GameObjects.Rectangle;

  constructor() {
    super({ key: "LawnWorldScene" });
  }

  init(data: LawnWorldInit): void {
    this.generation = data?.generation ?? 0;
  }

  create(): void {
    this.fx = new FxPool(this);
    this.cameras.main.setBackgroundColor(0x16120e);
    this.ensureGrid(DEFAULT_ROWS, DEFAULT_COLS);
    this.phaseText = this.add
      .text(12, 8, "Idle", {
        fontSize: "14px",
        color: "#a89880"
      })
      .setDepth(1000);

    this.pickUnsub = wirePickSystem(
      this,
      this.ptrRegistry,
      this.generation,
      () => ({
        rows: this.model?.rows ?? DEFAULT_ROWS,
        cols: this.model?.cols ?? DEFAULT_COLS
      })
    );

    this.unsubs.push(
      lawnBusOn("lawn:model", (raw) => {
        const p = raw as LawnModelPayload;
        if (p.generation !== this.generation) return;
        this.model = p.model as LawnViewModel;
        this.applyModel();
      })
    );

    this.unsubs.push(
      lawnBusOn("lawn:interaction", (raw) => {
        const p = raw as LawnInteractionPayload;
        if (p.generation !== this.generation) return;
        this.selectedPtr = p.ptr;
        if (p.mode === "SpawnTargeting" && p.row != null && p.col != null) {
          this.showGhost(p.row, p.col);
        } else {
          this.clearGhost();
        }
        this.applyModel();
      })
    );

    this.unsubs.push(
      lawnBusOn("lawn:viewMode", (raw) => {
        const p = raw as LawnViewModePayload;
        if (p.generation !== this.generation) return;
        this.viewMode = parseLawnViewMode(p.viewMode);
        this.applyModel();
      })
    );

    this.scale.on("resize", this.onScaleResize, this);
    this.unsubs.push(wireLawnIconLoadErrors(this));
    this.unsubs.push(subscribeIconEpoch(() => {
      bustLawnIconTextures(this);
      this.lastApplied = 0;
      this.applyModel();
    }));

    this.fitCamera();
    lawnBusEmit("lawn:ready", { generation: this.generation });
  }

  private onScaleResize(gameSize: { width: number; height: number }): void {
    this.fitCamera(gameSize.width, gameSize.height);
  }

  private fitCamera(viewW?: number, viewH?: number): void {
    const w = viewW ?? this.scale.gameSize.width;
    const h = viewH ?? this.scale.gameSize.height;
    const z = lawnCameraZoom(
      w,
      h,
      this.gridRows || DEFAULT_ROWS,
      this.gridCols || DEFAULT_COLS
    );
    this.cameras.main.setZoom(Math.max(0.2, z));
    const cx = ORIGIN_X + (this.gridCols * CELL_W) / 2;
    const cy = ORIGIN_Y + (this.gridRows * CELL_H) / 2;
    this.cameras.main.centerOn(cx, cy);
  }

  private syncCtx(canvasPtrs?: Set<string>): SyncContext {
    return {
      scene: this,
      registry: this.ptrRegistry,
      lastApplied: this.lastApplied,
      selectedPtr: this.selectedPtr,
      canvasPtrs,
      onIconsReady: () => {
        if (!this.model) return;
        refreshOccupantIcons(this.syncCtx(canvasPtrs), this.model);
      }
    };
  }

  private ensureGrid(rows: number, cols: number): void {
    if (this.gridGfx && this.gridRows === rows && this.gridCols === cols) return;
    this.gridGfx?.destroy();
    const g = this.add.graphics();
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const x = ORIGIN_X + c * CELL_W;
        const y = ORIGIN_Y + r * CELL_H;
        g.fillStyle((r + c) % 2 === 0 ? 0x221c16 : 0x2a231b, 0.9);
        g.fillRect(x, y, CELL_W - 2, CELL_H - 2);
        g.lineStyle(1, 0x3d6b45, 0.45);
        g.strokeRect(x, y, CELL_W - 2, CELL_H - 2);
      }
    }
    this.gridGfx = g;
    this.gridRows = rows;
    this.gridCols = cols;
  }

  private applyModel(): void {
    if (!this.model) return;
    this.ensureGrid(
      Math.max(DEFAULT_ROWS, this.model.rows),
      Math.max(DEFAULT_COLS, this.model.cols)
    );
    this.phaseText?.setText(`${this.model.phase} · rev ${this.model.revision}`);

    const pick = pickPhaserOccupants(
      listOccupants(this.model),
      PHASER_OCCUPANT_BUDGET,
      this.selectedPtr
    );
    const canvasPtrs = new Set(pick.onCanvas.map((o) => normalizePtr(o.ptr)));
    const canvasKey = [...canvasPtrs].sort().join(",");
    const canvasChanged = canvasKey !== this.lastCanvasKey;
    const modeChanged = this.viewMode !== this.lastViewMode;
    const sync = shouldSyncLawnSprites({
      revision: this.model.revision,
      lastApplied: this.lastApplied,
      canvasKey,
      lastCanvasKey: this.lastCanvasKey
    });

    if (sync) {
      const ctx = this.syncCtx(canvasPtrs);
      ctx.lastApplied = forceSyncLastApplied(
        this.model.revision,
        ctx.lastApplied,
        canvasChanged
      );
      this.lastApplied = syncFromModel(ctx, this.model);
    }

    applySelectionChrome(this.syncCtx(canvasPtrs));
    if (sync || modeChanged) {
      layoutGrid(this.ptrRegistry, this.model, this.viewMode);
    }
    this.lastCanvasKey = canvasKey;
    this.lastViewMode = this.viewMode;
    if (sync || modeChanged) this.fitCamera();
  }

  private showGhost(row: number, col: number): void {
    this.clearGhost();
    const x = ORIGIN_X + col * CELL_W + CELL_W / 2;
    const y = ORIGIN_Y + row * CELL_H + CELL_H / 2;
    this.ghost = this.add
      .rectangle(x, y, 48, 56, 0xe0b44b, 0.25)
      .setStrokeStyle(2, 0xe0b44b, 0.8)
      .setDepth(500);
  }

  private clearGhost(): void {
    this.ghost?.destroy();
    this.ghost = undefined;
  }

  update(_time: number, delta: number): void {
    tickStatusFx(this.ptrRegistry, this.fx, delta);
  }

  shutdown(): void {
    this.scale.off("resize", this.onScaleResize, this);
    this.pickUnsub?.();
    this.pickUnsub = undefined;
    for (const u of this.unsubs) u();
    this.unsubs = [];
    try {
      this.tweens.killAll();
    } catch {
      /* */
    }
    this.fx?.drain();
    this.ptrRegistry.clear();
    this.clearGhost();
  }
}
