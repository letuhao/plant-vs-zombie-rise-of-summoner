import { useState } from "react";
import type { ActorView } from "@/contract/types";
import { useAuraCatalog, useAuraRuntime, useDisableAura, useEnableAura } from "@/lib/bus/aura";
import { AuraSlot, type AuraSlotState } from "./AuraSlot";
import { LockedGridSlot } from "./LockedGridSlot";

// Illustrative only — no catalog exists yet for either side's regular actions (spec-action-layer.md
// is "approved 2026-08-22, not yet built"; `rpg_action` returns zero hits in src/ today).
const PLACEHOLDER_ACTIONS = [
  { id: "strike", label: "Strike" },
  { id: "firebolt", label: "Firebolt" },
  { id: "guard", label: "Guard" },
  { id: "overgrowth", label: "Overgrowth" }
];

/** aura-skill T18c: which pool + how much per tick, from real authored cost rows (never
 * fabricated) — undefined (renders nothing) when no cost has been authored for this aura yet, which
 * is every aura's real state today (`grep -rn PerTick data/` finds zero hits). Takes a plain
 * structural shape rather than an imported DTO type — `ui/` binds to values, not `@/lib/bus` types
 * (this repo's own contractGuard). */
function upkeepNoteFor(upkeep: { resourceId: string; amountMin: number; amountMax: number; when: string }[]): string | undefined {
  if (upkeep.length === 0) return undefined;
  return upkeep
    .map((c) => `${c.amountMin === c.amountMax ? c.amountMin : `${c.amountMin}-${c.amountMax}`} ${c.resourceId} ${c.when === "PerTick" ? "per tick" : "on cast"}`)
    .join(", ");
}

const REFUSAL_TEXT: Record<string, string> = {
  NotEquipped: "Not equipped — assign it in your loadout first",
  AlreadyActive: "Already active"
};

/**
 * aura-skill T18c (`spec-aura-surface.md`): real aura slots replace the old locked placeholders.
 * Auras render as a distinct group ABOVE the still-locked regular-action grid — resolves the spec's
 * own open question 1 (Actions is the natural home; the action system itself is still unbuilt).
 */
export function ActionsTab({ data }: { data: ActorView }) {
  const catalog = useAuraCatalog();
  const runtime = useAuraRuntime(data.playerId);
  const enable = useEnableAura(data.playerId);
  const disable = useDisableAura(data.playerId);

  // Per-aura transient UI state: a refusal reason (409) or an eviction note (GG-55 — "enabling X
  // switched off Y" must survive long enough to read, not vanish with the toast).
  const [notes, setNotes] = useState<Record<string, string>>({});

  if (catalog.isLoading || runtime.isLoading) {
    return (
      <div className="mt-4" data-testid="actions-tab-loading">
        Loading auras…
      </div>
    );
  }

  const auras = catalog.data?.items ?? [];
  const activeIds = new Set(runtime.data?.activeAuraIds ?? []);
  const equippedIds = new Set(runtime.data?.equippedAuraIds ?? []);

  function stateFor(auraId: string): AuraSlotState {
    if (activeIds.has(auraId)) return "active";
    if (equippedIds.has(auraId)) return "equipped-inactive";
    return "locked";
  }

  function setNote(auraId: string, note: string | undefined) {
    setNotes((prev) => {
      const next = { ...prev };
      if (note) next[auraId] = note;
      else delete next[auraId];
      return next;
    });
  }

  function handleEnable(auraId: string) {
    setNote(auraId, undefined);
    enable.mutate(auraId, {
      onSuccess: (result) => {
        if (result.evictedAuraId) {
          setNote(result.evictedAuraId, `Switched off — ${auraId} took its slot`);
        }
      },
      onError: (err) => {
        const reason = err instanceof Error ? REFUSAL_TEXT[err.message] ?? err.message : "Could not enable";
        setNote(auraId, reason);
      }
    });
  }

  function handleDisable(auraId: string) {
    setNote(auraId, undefined);
    disable.mutate(auraId);
  }

  return (
    <div className="mt-4" data-testid="actions-tab">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4" data-testid="actions-tab-auras">
        {auras.map(({ auraId, upkeep }) => {
          const state = stateFor(auraId);
          return (
            <AuraSlot
              key={auraId}
              auraId={auraId}
              state={state}
              lockedReason={state === "locked" ? REFUSAL_TEXT.NotEquipped : undefined}
              refusalReason={notes[auraId]}
              upkeepNote={upkeepNoteFor(upkeep)}
              busy={enable.isPending || disable.isPending}
              onEnable={() => handleEnable(auraId)}
              onDisable={() => handleDisable(auraId)}
            />
          );
        })}
      </div>

      <div className="mt-4 grid grid-cols-4 gap-2" data-testid="actions-tab-placeholder">
        {PLACEHOLDER_ACTIONS.map(({ id, label }) => (
          <LockedGridSlot key={id} id={id} label={label} reason="Unlocks once the action system ships (approved, not yet built)" />
        ))}
      </div>
    </div>
  );
}
