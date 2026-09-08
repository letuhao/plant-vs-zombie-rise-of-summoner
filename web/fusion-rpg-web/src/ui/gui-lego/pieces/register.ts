import { registerPiece } from "@/features/gui-lego/pieceRegistry";
import type { PieceFactory } from "@/features/gui-lego/types";
import { CHROME_SLOT_MAP, chromeFactories } from "./chrome";
import { DOMAIN_SLOT_MAP, domainFactories } from "./domain";
import { LAYOUT_SLOT_MAP, layoutFactories } from "./layout";
import { LIFECYCLE_SLOT_MAP, lifecycleFactories } from "./lifecycle";

let registered = false;

const ALL: {
  factories: Record<string, PieceFactory>;
  slots: Record<string, readonly string[]>;
}[] = [
  { factories: layoutFactories, slots: LAYOUT_SLOT_MAP },
  { factories: chromeFactories, slots: CHROME_SLOT_MAP },
  { factories: domainFactories, slots: DOMAIN_SLOT_MAP },
  { factories: lifecycleFactories, slots: LIFECYCLE_SLOT_MAP }
];

/** Register all Derived v1 piece factories into `pieceRegistry` (idempotent). */
export function registerDerivedPieces(): void {
  if (registered) return;
  for (const group of ALL) {
    for (const [pieceId, factory] of Object.entries(group.factories)) {
      registerPiece({
        pieceId,
        slots: group.slots[pieceId] ?? [],
        factory
      });
    }
  }
  registered = true;
}

/** Test helper — allows re-register after `clearPieceRegistryForTests`. */
export function resetDerivedPiecesRegistrationFlagForTests(): void {
  registered = false;
}
