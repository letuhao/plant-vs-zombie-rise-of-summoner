import { useEffect, useRef } from "react";
import {
  allocGameGeneration,
  worldBusEmit,
  worldBusOn,
  type WorldIgnoreRect,
  type WorldModelPayload,
  type WorldSelectPayload
} from "@/game/EventBus";
import { createWorldGame, destroyWorldGame } from "@/game/createWorldGame";
import type { AdaptedWorldState } from "@/contract/adapt";
import { cn } from "@/lib/cn";

export type WorldGameHostProps = {
  model: AdaptedWorldState;
  /** Bumps when overlay inputs (lifelines/supply) change even if graph refs are equal. */
  overlayEpoch?: number;
  selectedSectorId?: string | null;
  ignoreRects?: WorldIgnoreRect[];
  /** Targeting overlay inputs (reachable / pending routes / blocked). */
  targeting?: unknown;
  /** Active map lens id — picker stays React; drawing is Phaser. */
  lens?: string;
  onSelect: (payload: WorldSelectPayload) => void;
  className?: string;
};

function modelFingerprint(model: AdaptedWorldState, overlayEpoch: number): string {
  return `${model.sectors.length}:${model.lanes.length}:${overlayEpoch}:${model.sectors.map((s) => s.sectorId + s.intel).join(",")}`;
}

/**
 * Facade host — mount/destroy idempotent under StrictMode (RT-02/07/11).
 * Buffers world:model / world:interaction / world:lens until world:ready; drops foreign generation.
 * Only this module may import createWorldGame (GG-38).
 */
export function WorldGameHost({
  model,
  overlayEpoch = 0,
  selectedSectorId = null,
  ignoreRects = [],
  targeting = null,
  lens = "ownership",
  onSelect,
  className
}: WorldGameHostProps) {
  const parentRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);
  const generationRef = useRef(0);
  const readyRef = useRef(false);
  const modelSeqRef = useRef(0);
  const lastFingerprintRef = useRef<string>("");
  const bufferedModelRef = useRef<AdaptedWorldState | null>(null);
  const bufferedEpochRef = useRef(0);
  const bufferedLensRef = useRef<string | null>(null);
  const bufferedInteractionRef = useRef<{
    selectedId: string | null;
    ignoreRects: WorldIgnoreRect[];
    targeting: unknown;
  } | null>(null);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) return;

    const generation = allocGameGeneration();
    generationRef.current = generation;
    readyRef.current = false;
    bufferedModelRef.current = null;
    bufferedLensRef.current = null;
    bufferedInteractionRef.current = null;
    lastFingerprintRef.current = "";
    modelSeqRef.current = 0;

    console.info("[world-host]", { event: "create", generation });
    const game = createWorldGame({ parent, generation });
    gameRef.current = game;

    const resizeToParent = () => {
      const w = Math.floor(parent.clientWidth);
      const h = Math.floor(parent.clientHeight);
      if (w < 2 || h < 2) return;
      game.scale.resize(w, h);
      worldBusEmit("world:resized", { generation, width: w, height: h });
    };

    const ro = new ResizeObserver(() => resizeToParent());
    ro.observe(parent);
    resizeToParent();

    const emitModel = (m: AdaptedWorldState, epoch: number) => {
      const fp = modelFingerprint(m, epoch);
      if (fp === lastFingerprintRef.current) return;
      lastFingerprintRef.current = fp;
      modelSeqRef.current += 1;
      const seq = modelSeqRef.current;
      console.info("[world-host]", { event: "model", generation, modelSeq: seq, sectors: m.sectors.length });
      worldBusEmit("world:model", {
        generation,
        modelSeq: seq,
        model: m
      } satisfies WorldModelPayload);
    };

    const flushBuffers = () => {
      if (bufferedModelRef.current) {
        const m = bufferedModelRef.current;
        const epoch = bufferedEpochRef.current;
        bufferedModelRef.current = null;
        emitModel(m, epoch);
      }
      if (bufferedLensRef.current != null) {
        const l = bufferedLensRef.current;
        bufferedLensRef.current = null;
        console.info("[world-host]", { event: "lens", generation, lens: l });
        worldBusEmit("world:lens", { generation, lens: l });
      }
      if (bufferedInteractionRef.current) {
        const i = bufferedInteractionRef.current;
        bufferedInteractionRef.current = null;
        worldBusEmit("world:interaction", {
          generation,
          selectedId: i.selectedId,
          selectedKind: i.selectedId ? "sector" : null,
          ignoreRects: i.ignoreRects,
          targeting: i.targeting
        });
      }
    };

    const offReady = worldBusOn("world:ready", (raw) => {
      const p = raw as { generation?: number };
      if (p.generation !== generation) return;
      readyRef.current = true;
      console.info("[world-host]", { event: "ready", generation });
      flushBuffers();
      worldBusEmit("world:camera", { generation, op: "fit", padLeft: 100, padRight: 40, padTop: 56, padBottom: 80 });
    });

    const offSelect = worldBusOn("world:select", (raw) => {
      const p = raw as WorldSelectPayload;
      if (p.generation !== generation) return;
      console.info("[world-host]", { event: "select", generation, kind: p.kind, id: p.id });
      onSelectRef.current(p);
    });

    return () => {
      ro.disconnect();
      offReady();
      offSelect();
      console.info("[world-host]", { event: "destroy", generation });
      destroyWorldGame(gameRef.current, generation);
      gameRef.current = null;
      readyRef.current = false;
    };
  }, []);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (!readyRef.current) {
      bufferedModelRef.current = model;
      bufferedEpochRef.current = overlayEpoch;
      return;
    }
    const fp = modelFingerprint(model, overlayEpoch);
    if (fp === lastFingerprintRef.current) return;
    lastFingerprintRef.current = fp;
    modelSeqRef.current += 1;
    const seq = modelSeqRef.current;
    console.info("[world-host]", { event: "model", generation, modelSeq: seq, sectors: model.sectors.length });
    worldBusEmit("world:model", {
      generation,
      modelSeq: seq,
      model
    } satisfies WorldModelPayload);
  }, [model, overlayEpoch]);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (!readyRef.current) {
      bufferedLensRef.current = lens;
      return;
    }
    console.info("[world-host]", { event: "lens", generation, lens });
    worldBusEmit("world:lens", { generation, lens });
  }, [lens]);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (!readyRef.current) {
      bufferedInteractionRef.current = {
        selectedId: selectedSectorId,
        ignoreRects,
        targeting
      };
      return;
    }
    worldBusEmit("world:interaction", {
      generation,
      selectedId: selectedSectorId,
      selectedKind: selectedSectorId ? "sector" : null,
      ignoreRects,
      targeting
    });
  }, [selectedSectorId, ignoreRects, targeting]);

  return (
    <div
      ref={parentRef}
      data-testid="world-game-host"
      data-test-id="world-game-host"
      data-selected-sector={selectedSectorId ?? ""}
      className={cn("h-full w-full min-h-0 bg-soil", className)}
      role="img"
      aria-label="World map"
      onContextMenu={(e) => {
        e.preventDefault();
        const generation = generationRef.current;
        if (!generation) return;
        onSelectRef.current({ generation, kind: "empty" });
      }}
    >
      <div data-testid="world-game-canvas" data-test-id="world-game-canvas" className="contents" />
    </div>
  );
}
