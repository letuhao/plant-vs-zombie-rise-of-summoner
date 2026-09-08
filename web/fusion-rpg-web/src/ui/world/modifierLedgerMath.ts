import type { Magnitude, UpkeepBreakdownView } from "@/contract/types";

/**
 * The modifier ledger's arithmetic (world-numbers W41) — GG-49's answer to "why did my net income
 * drop?". **The rows are not a design choice**: they are exactly the four additive operands of
 * `LoamUpkeep.For(garrisonMembers, developmentLevel, dangerBand, intensityMilli, handicapMilli)`
 * (`LoamUpkeep.cs:40`), in that order. Depth is capped at three levels; a fourth would be the
 * tuning file, not this ledger.
 *
 * **Named `modifierLedgerMath.ts`, not `modifierLedger.ts` (the task's own file list).** The two
 * differ only by the first letter's case from `ModifierLedger.tsx` in this same directory —
 * genuinely broken on this Windows machine's case-insensitive filesystem: `ModifierLedger.tsx`'s
 * own `import ... from "./modifierLedger"` resolved back to itself instead of this file, producing
 * a circular self-import where `ModifierLedger` was still `undefined` at render time. Found live by
 * `ModifierLedger.test.tsx` (7/11 cases failing with "Element type is invalid… got: undefined"),
 * not assumed — fixed by the rename, which real environment `require`/`import` resolution needs
 * regardless of platform, not merely a workaround for this one machine.
 */
export type ModifierLedgerRowKey = "base" | "garrison" | "development" | "danger";

export type ModifierLedgerRow = { key: ModifierLedgerRowKey; amount: Magnitude };

/** Exactly the four rows, in the engine's own operand order — nothing else, ever. */
export function ledgerRows(breakdown: UpkeepBreakdownView): ModifierLedgerRow[] {
  return [
    { key: "base", amount: breakdown.base },
    { key: "garrison", amount: breakdown.garrison },
    { key: "development", amount: breakdown.development },
    { key: "danger", amount: breakdown.danger }
  ];
}

/**
 * `sum × intensityMilli × handicapMilli ÷ 1_000_000` — **one** division, truncating (matching
 * `long` integer division on the C# side, never a floating-point round), never two roundings.
 * Reading down the column must reproduce this exactly, or the ledger lies about its own total.
 */
export function reproducedTotal(breakdown: UpkeepBreakdownView): number {
  const sum = breakdown.base.value + breakdown.garrison.value + breakdown.development.value + breakdown.danger.value;
  return Math.trunc((sum * breakdown.intensityMilli.value * breakdown.handicapMilli.value) / 1_000_000);
}
