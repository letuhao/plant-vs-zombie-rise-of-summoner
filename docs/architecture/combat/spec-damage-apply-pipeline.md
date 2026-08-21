# Spec: damage-apply-pipeline

Module id `damage-apply-pipeline` in the [combat unification map](../combat-unification-map.md). Depends on `combat-resolver-core`. **Build held.** Audited 2026-08-21; the draft's API had three fork-prone seams (key spaces, sink typing, gate signature) — all pinned below.

## Objective

Extract the apply tail from `CombatDamageDispatcher.DispatchInstant` — finalized delta → **shield gate** → HP sink — into one host-mountable `DamageApplyPipeline`, so every mode's damage passes the same shield stage and sink discipline. This is the module that structurally fixes "battle has no shields."

## Design (locked on approval)

### API — two key spaces, one owner (audit fix)

The draft's single `targetKey` conflated the **ptr space** (host actor id: overlay `"1a2b3c"`, battle `"squad:0"`) with the **owner-key space** (`"entity:{ptr}"`). Locked contract:

- The pipeline takes the **normalized host ptr** only. It owns all prefixing: `EffectOwnerKeys.Entity(ptr)` for both the shield lookup and the funnel enqueue. Hosts never pass a pre-prefixed key (a prefixed input would silently key shields under `entity:entity:…`).
- **One-key discipline per host:** every HP delta for an actor goes through the pipeline, or none do. Mixed raw/prefixed enqueues split an actor's FA10 mutation slot silently (the funnel merges by exact key; the sink strips the prefix afterward, so nothing visibly breaks — verified "works by accident"). Invariant test: one actor, one flush window, several deltas → exactly one mutation slot.

```text
DamageApplyPipeline.Apply(
    ptr, finalizedSignedAmount, hitCount,
    components, attackerSnapshot?, ownerSnapshot,
    shieldGate?, sink, meta {pluginId, effectId, grantId},
    noteOverlayDamage: bool)
  → (appliedAmount, absorbedAmount, enqueued)
```

### Sink abstraction (audit fix — sim has no funnel at all)

`sink` is a minimal `IHpDeltaSink` with exactly two implementations in this program: `EffectFunnel` (overlay + battle — via a thin adapter over `EnqueueMutation`) and sim's direct-apply sink. `EffectFunnel` cannot be constructed without a full `EffectBag`, so a funnel-less mode is a hard requirement, not a convenience. The zero-guard (fully absorbed → nothing reaches the sink) lives in the **pipeline**, not the sink, so both branches behave identically.

### `NoteOverlayDamage` is part of the tail — now an explicit, host-optional stage (audit fix)

The dispatcher tail also calls `funnel.NoteOverlayDamage(...)` (overlay proc chains). The pipeline exposes it as the `noteOverlayDamage` flag: **overlay = on** (unchanged), **battle/sim = off** in this program (battle's bag carries the seeded catalog; overlay-damage procs in battle are a future program, ask-first). Never an accident of which funnel got passed.

### Gate signature (audit fix — no double source of truth)

`ShieldGate.AbsorbFinalized` currently takes a `DamagePacket` and **re-resolves snapshots through its own delegate**. The gate gains a packet-free overload — `AbsorbFinalized(amount, ptr, components, hitCount, attackerSnap?, ownerSnap)` — and the pipeline's snapshots are the **only** ones used on this path (one snapshot source for resolver and gate; divergence impossible). The packet-based path remains solely as a thin wrapper for the legacy dispatcher call until the dispatcher itself delegates.

### Overlay adoption
`DispatchInstant` delegates its tail to the pipeline (funnel sink, `noteOverlayDamage: true`) — byte-identical, proven by every existing dispatcher/gate golden running unchanged.

### Guard discipline (audit-verified tripwires)
`guard-funnel-delta` greps all Core text — comments included — for `EntityStatWriter`, `AddPlantHp`, `AddZombieHp`, **`targetPtrs`** (unanchored substring). The pipeline stays single-target (`ptr`); any plural is named `resolvedPtrList`-style, never `targetPtrs`; doc comments say "the host's FA10 sink owns the write", never the writer's class name.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Shield|FullyQualifiedName~Combat"
.\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Combat/DamageApplyPipeline.cs      (new — pipeline + IHpDeltaSink + funnel adapter)
src/FusionRpg.Core/Combat/Shield/ShieldGate.cs        (packet-free overload; legacy wrapper kept)
src/FusionRpg.Core/Combat/CombatDamageDispatcher.cs   (delegates; behavior frozen)
tests/FusionRpg.Core.Tests/Combat/                    (pipeline units, one-slot invariant, sink parity,
                                                       dispatcher byte-identity regression)
```

## Testing strategy

Existing dispatcher + gate goldens unchanged (refactor proof). New units: partial/full absorb, heal bypass, zero-to-sink on full absorption, null-gate pass-through, hitCount forwarding, funnel-vs-direct sink parity (same inputs → same applied numbers), one-mutation-slot invariant, noteOverlayDamage on/off.

## Boundaries

- **Always:** byte-identical overlay; pipeline owns key prefixing; zero-guard in the pipeline; single-target API; gate consumes pipeline snapshots only.
- **Ask first:** new pipeline stages; enabling `noteOverlayDamage` for battle/sim.
- **Never:** host funnel enqueues for combat damage that skip the pipeline (post-adoption; ban test); a second gate implementation; pre-prefixed keys at the API; the token tripwires above.

## Success criteria

1. Dispatcher delegates; all existing goldens byte-identical. 2. Sink abstraction proves funnel and direct modes in parity tests. 3. One-slot invariant green. 4. Packet-free gate overload is the only path the pipeline uses. 5. Guards green. 6. Battle/sim specs consume the API as published here.
