import { useEffect, useRef } from "react";
import {
  allocGameGeneration,
  lawnBusEmit,
  lawnBusOn,
  type LawnModelPayload,
  type LawnSelectPayload,
  type LawnViewModePayload
} from "@/game/EventBus";
import { createLawnGame, destroyLawnGame } from "@/game/createLawnGame";
import { lawnWorldSize } from "@/game/gridMath";
import { cn } from "@/lib/cn";
import type { LawnViewMode } from "./lawnViewMode";
import { isLargeCanvas } from "./lawnViewMode";
import { DEFAULT_COLS, DEFAULT_ROWS, type LawnViewModel } from "./lawnViewModel";
import type { InteractionState } from "./interactionMode";
import {
  shouldBuffer,
  shouldEmitLawnModel,
  takeBuffered,
  toInteractionPayload
} from "./lawnHostBuffer";

/**
 * Facade host — mount/destroy idempotent under StrictMode (RT-02/07/11).
 * Buffers lawn:model and lawn:interaction until lawn:ready; drops foreign generation.
 */
export function LawnGameHost({
  model,
  interaction,
  viewMode,
  onSelect
}: {
  model: LawnViewModel;
  interaction: InteractionState;
  viewMode: LawnViewMode;
  onSelect: (payload: LawnSelectPayload) => void;
}) {
  const parentRef = useRef<HTMLDivElement | null>(null);
  const gameRef = useRef<Phaser.Game | null>(null);
  const generationRef = useRef(0);
  const readyRef = useRef(false);
  const bufferedModelRef = useRef<LawnViewModel | null>(null);
  const bufferedInteractionRef = useRef<InteractionState | null>(null);
  const bufferedViewRef = useRef<LawnViewMode | null>(null);
  const lastEmittedRev = useRef<number | undefined>(undefined);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) return;

    const generation = allocGameGeneration();
    generationRef.current = generation;
    readyRef.current = false;
    bufferedModelRef.current = null;
    bufferedInteractionRef.current = null;
    bufferedViewRef.current = null;
    lastEmittedRev.current = undefined;

    const game = createLawnGame({ parent, generation });
    gameRef.current = game;

    const resizeToParent = () => {
      const w = Math.floor(parent.clientWidth);
      const h = Math.floor(parent.clientHeight);
      if (w < 2 || h < 2) return;
      game.scale.resize(w, h);
      lawnBusEmit("lawn:resized", { generation, width: w, height: h });
    };

    const ro = new ResizeObserver(() => resizeToParent());
    ro.observe(parent);
    resizeToParent();

    const flushBuffers = () => {
      const bufModel = takeBuffered(bufferedModelRef);
      if (bufModel) {
        lastEmittedRev.current = bufModel.revision;
        lawnBusEmit("lawn:model", {
          generation,
          revision: bufModel.revision,
          model: bufModel
        } satisfies LawnModelPayload);
      }
      const bufIx = takeBuffered(bufferedInteractionRef);
      if (bufIx) {
        lawnBusEmit("lawn:interaction", toInteractionPayload(generation, bufIx));
      }
      const bufView = takeBuffered(bufferedViewRef);
      if (bufView) {
        lawnBusEmit("lawn:viewMode", {
          generation,
          viewMode: bufView
        } satisfies LawnViewModePayload);
      }
    };

    const offReady = lawnBusOn("lawn:ready", (raw) => {
      const p = raw as { generation?: number };
      if (p.generation !== generation) return;
      readyRef.current = true;
      flushBuffers();
    });

    const offSelect = lawnBusOn("lawn:select", (raw) => {
      const p = raw as LawnSelectPayload;
      if (p.generation !== generation) return;
      onSelectRef.current(p);
    });

    return () => {
      ro.disconnect();
      offReady();
      offSelect();
      destroyLawnGame(gameRef.current, generation);
      gameRef.current = null;
      readyRef.current = false;
    };
  }, []);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (shouldBuffer(readyRef.current)) {
      bufferedModelRef.current = model;
      return;
    }
    if (!shouldEmitLawnModel(lastEmittedRev.current, model.revision)) return;
    lastEmittedRev.current = model.revision;
    lawnBusEmit("lawn:model", {
      generation,
      revision: model.revision,
      model
    } satisfies LawnModelPayload);
  }, [model]);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (shouldBuffer(readyRef.current)) {
      bufferedInteractionRef.current = interaction;
      return;
    }
    lawnBusEmit("lawn:interaction", toInteractionPayload(generation, interaction));
  }, [interaction]);

  useEffect(() => {
    const generation = generationRef.current;
    if (!generation) return;
    if (shouldBuffer(readyRef.current)) {
      bufferedViewRef.current = viewMode;
      return;
    }
    lawnBusEmit("lawn:viewMode", {
      generation,
      viewMode
    } satisfies LawnViewModePayload);
  }, [viewMode]);

  const large = isLargeCanvas(viewMode);
  const world = lawnWorldSize(DEFAULT_ROWS, DEFAULT_COLS);

  return (
    <div
      ref={parentRef}
      data-testid="lawn-game-host"
      className={cn(
        "w-full overflow-hidden rounded-sm border border-border bg-soil",
        large
          ? "min-h-[70vh] h-[calc(100vh-12rem)]"
          : "w-full min-h-[280px]"
      )}
      style={
        large
          ? undefined
          : { aspectRatio: `${world.width} / ${world.height}` }
      }
    />
  );
}
