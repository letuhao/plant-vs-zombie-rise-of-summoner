/**
 * GUI Lego shared types (payload-types + MountPlan).
 * Design SSOT: docs/architecture/gui-lego/payload-types.md
 */
import type { ReactNode } from "react";

export type Phase = "ready" | "loading" | "empty" | "error" | "pending";

export type DerivedRenderState =
  | "active"
  | "default"
  | "capped"
  | "stub"
  | "no-producer"
  | "unregistered";

export type ThemeKind =
  | "element"
  | "status-category"
  | "resource"
  | "action-category"
  | "rarity"
  | "side"
  | "cook-tab"
  | "neutral";

export type ThemeRef = {
  kind: ThemeKind;
  id: string;
};

export type ThemeResolved = {
  themeId: string;
  css: Record<string, string>;
  paint: {
    accent: string;
    accentMuted: string;
    onAccent: string;
  };
  vfx: { select: string | null; idle: string | null };
};

export type ThemePack = {
  themeId: string;
  kind: string;
  id: string;
  css: Record<string, string>;
  paint: ThemeResolved["paint"];
  vfx: ThemeResolved["vfx"];
  glyphDefault?: string | null;
};

export type GlyphRef = {
  catalogIcon?: string;
  hudToken?: string;
  fallbackText?: string;
};

export type MagnitudeDisplay = {
  valueRaw: number | string;
  valueText: string;
  unitLabel?: string | null;
  formatterId: string;
};

export type PieceEnvelope = {
  piece: string;
  instanceId: string;
  phase: Phase;
  themeRef?: ThemeRef;
  themeResolved?: ThemeResolved;
};

export type PiecePayload = PieceEnvelope & Record<string, unknown>;

/** Locked MountPlan node — bindSurface output. */
export type MountNode = {
  instanceId: string;
  pieceId: string;
  payload: PiecePayload;
  slots: Record<string, MountNode | MountNode[]>;
};

export type MountPlan = {
  root: MountNode | null;
  overlay: MountNode | null;
  revision: number;
};

/** Recipe piece ref (design JSON). */
export type RecipePieceRef = {
  piece: string;
  instanceId?: string;
  bind?: string;
  slots?: Record<string, RecipeSlotFill>;
};

export type RecipeBindArray = {
  $bindArray: string;
  piece: string;
  instanceIdTemplate: string;
  slots?: Record<string, RecipeSlotFill>;
};

export type RecipeSlotFill =
  | RecipePieceRef
  | RecipePieceRef[]
  | RecipeBindArray;

export type RecipeDocument = {
  surfaceId: string;
  host?: string;
  version?: number;
  notes?: string;
  root: RecipePieceRef;
  lifecycleOverlays?: Partial<
    Record<"loading" | "empty" | "error" | "pendingField", RecipePieceRef>
  >;
};

export type PieceFactory = (args: {
  payload: PiecePayload;
  slots: Record<string, ReactNode>;
  bus: SurfaceBusLike;
}) => ReactNode;

export type PieceRegistration = {
  pieceId: string;
  slots: readonly string[];
  factory?: PieceFactory;
};

/** Minimal bus surface for pieces (generic createSurfaceBus). */
export type SurfaceBusLike = {
  emit: (event: string, payload?: unknown) => void;
  on: (event: string, handler: (payload: unknown) => void) => () => void;
};
