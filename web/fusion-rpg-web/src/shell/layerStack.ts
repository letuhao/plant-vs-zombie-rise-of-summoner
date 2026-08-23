import { create } from "zustand";

/**
 * The six stacking bands from game-gui-principles.md GG-5. Stage (band 0) is
 * not a stack entry — it is the persistent base every layer opens over — so
 * it has no place here.
 */
export type Band = "shell" | "hud" | "panel" | "dialog" | "toast" | "system";

export type LayerEntry = {
  id: string;
  band: Band;
};

type LayerStackState = {
  layers: LayerEntry[];
  push: (entry: LayerEntry) => void;
  pop: (id?: string) => void;
  popAll: () => void;
};

/**
 * The one owner of layer visibility (GG-6). `push`/`pop` are LIFO on the raw
 * array — band-aware dismissal (Esc skips a Toast, System always wins Esc on
 * an empty stack, etc.) is keymap policy built on top of this, not part of
 * the mechanism itself.
 */
export const useLayerStack = create<LayerStackState>((set) => ({
  layers: [],
  push: (entry) =>
    set((state) => {
      if (state.layers.some((layer) => layer.id === entry.id)) return state;
      return { layers: [...state.layers, entry] };
    }),
  pop: (id) =>
    set((state) => {
      if (state.layers.length === 0) return state;
      if (id === undefined) return { layers: state.layers.slice(0, -1) };
      return { layers: state.layers.filter((layer) => layer.id !== id) };
    }),
  popAll: () => set({ layers: [] })
}));

/** The layer visually and interactionally on top: the last entry pushed. */
export function useTopLayer(): LayerEntry | undefined {
  return useLayerStack((state) => state.layers[state.layers.length - 1]);
}
