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
import type { LegionView } from "@/contract/types";
import { cn } from "@/lib/cn";
import { worldModelProjection } from "./worldModelProjection";

export type WorldGameHostProps = {
  model: AdaptedWorldState;
  /** All adapted entities — not player-only (gaps D15/D26). */
  legions?: readonly LegionView[];
  playerFactionId?: string | null;
  /** Bumps when overlay inputs (lifelines/supply) change even if graph refs are equal. */
  overlayEpoch?: number;
  selectedSectorId?: string | null;
  ignoreRects?: WorldIgnoreRect[];
  /** Targeting overlay inputs (reachable / pending routes / blocked). */
  targeting?: unknown;
  /** Active map lens id — picker stays React; drawing is Phaser. */
  lens?: string;
  /** Host publishes generation to React — not window.__fusionRpgWorldGen (gaps D2). */
  onGeneration?: (generation: number) => void;
  onSelect: (payload: WorldSelectPayload) => void;
  className?: string;
};

function hostLog(payload: Record<string, unknown>): void {
  if (import.meta.env.DEV) {
    console.info("[world-host]", payload);
  }
}

/**
 * Facade host — mount/destroy idempotent under StrictMode (RT-02/07/11).
 * Buffers world:model / world:interaction / world:lens until world:ready; drops foreign generation.
 * Only this module may import createWorldGame (GG-38).
 */
export function WorldGameHost({
  model,
  legions = [],
  playerFactionId = null,
  overlayEpoch = 0,
  selectedSectorId = null,
  ignoreRects = [],
  targeting = null,
  lens = "ownership",
  onGeneration,
  onSelect,
  className
}: WorldGameHostProps) {
  const parentRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);
  const generationRef = useRef(0);
  const readyRef = useRef(false);
  const modelSeqRef = useRef(0);
  const lastFingerprintRef = useRef<string>("");
  const bufferedModelRef = useRef<{
    model: AdaptedWorldState;
    legions: readonly LegionView[];
    playerFactionId: string | null;
    overlayEpoch: number;
  } | null>(null);
  const bufferedLensRef = useRef<string | null>(null);
  const bufferedInteractionRef = useRef<{
    selectedId: string | null;
    ignoreRects: WorldIgnoreRect[];
    targeting: unknown;
  } | null>(null);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;
  const onGenerationRef = useRef(onGeneration);
  onGenerationRef.current = onGeneration;

  const emitModel = (
    generation: number,
    m: AdaptedWorldState,
    legs: readonly LegionView[],
    pf: string | null,
    epoch: number
  ) => {
    const fp = worldModelProjection({
      model: m,
      overlayEpoch: epoch,
      playerFactionId: pf,
      legions: legs
    });
    if (fp === lastFingerprintRef.current) return;
    lastFingerprintRef.current = fp;
    modelSeqRef.current += 1;
    const seq = modelSeqRef.current;
    hostLog({ event: "model", generation, modelSeq: seq, sectors: m.sectors.length });
    worldBusEmit("world:model", {
      generation,
      modelSeq: seq,
      model: { ...m, playerFactionId: pf, legions: legs }
    } satisfies WorldModelPayload);
  };

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) return;

    const generation = allocGameGeneration();
    generationRef.current = generation;
    onGenerationRef.current?.(generation);
    readyRef.current = false;
    bufferedModelRef.current = null;
    bufferedLensRef.current = null;
    bufferedInteractionRef.current = null;
    lastFingerprintRef.current = "";
    modelSeqRef.current = 0;

    hostLog({ event: "create", generation });
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

    const flushBuffers = () => {
      if (bufferedModelRef.current) {
        const buf = bufferedModelRef.current;
        bufferedModelRef.current = null;
        emitModel(generation, buf.model, buf.legions, buf.playerFactionId, buf.overlayEpoch);
      }
      if (bufferedLensRef.current != null) {
        const l = bufferedLensRef.current;
        bufferedLensRef.current = null;
        hostLog({ event: "lens", generation, lens: l });
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
      hostLog({ event: "ready", generation });
      flushBuffers();
      worldBusEmit("world:camera", { generation, op: "fit", padLeft: 100, padRight: 40, padTop: 56, padBottom: 80 });
    });

    const offSelect = worldBusOn("world:select", (raw) => {
      const p = raw as WorldSelectPayload;
      if (p.generation !== generation) return;
      hostLog({ event: "select", generation, kind: p.kind, id: p.id });
      onSelectRef.current(p);
    });

    return () => {
      ro.disconnect();
      offReady();
      offSelect();
      hostLog({ event: "destroy", generation });
      destroyWorldGame(gameRef.current, generation);
      gameRef.current = null;
      readyRef.current = false;
      generationRef.current = 0;
      onGenerationRef.current?.(0);
      if (typeof window !== "undefined") {
        const w = window as unknown as {
          __fusionRpgWorldGen?: number;
          __fusionRpgWorldProbe?: unknown;
        };
        delete w.__fusionRpgWorldGen;
        delete w.__fusionRpgWorldProbe;
      }
    };
    // emitModel closes over refs intentionally for mount lifetime
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (!readyRef.current) {
      bufferedModelRef.current = { model, legions, playerFactionId, overlayEpoch };
      return;
    }
    emitModel(generation, model, legions, playerFactionId, overlayEpoch);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [model, legions, playerFactionId, overlayEpoch]);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (!readyRef.current) {
      bufferedLensRef.current = lens;
      return;
    }
    hostLog({ event: "lens", generation, lens });
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
        // G7 will make Phaser the sole owner; keep preventDefault so the page menu stays closed.
        e.preventDefault();
      }}
    >
      <div data-testid="world-game-canvas" data-test-id="world-game-canvas" className="contents" />
    </div>
  );
}
