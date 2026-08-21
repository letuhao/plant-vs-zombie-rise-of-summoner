# Spec: combat-resolver-core

Module id `combat-resolver-core` in the [combat unification map](../combat-unification-map.md). Foundation for everything else; **build held**. Audited 2026-08-21 (two-lens); audit corrections are marked inline.

## Objective

Make the shipped overlay resolution path — `OverlayCombatCalculator` + `CombatProbability` (sigmoid `1/(1+e^(−delta/scale))`, all scales 100) + `ElementHub` matchup + `CombatDerivedReader` — the **declared and enforced SSOT** for all RPG attack resolution, and close the gaps that stop other hosts from consuming it: deterministic RNG, componentless attacks, and the shared min-chip floor (owner decision). Overlay behavior stays byte-identical.

## Design (locked on approval)

### Deterministic RNG adapter
`SeededRngCombatAdapter : ICombatRng` wrapping an owned `SeededRng` stream (`Next(max) => rng.NextInt(max)`). Replayable hosts use it; `SeededCombatRng` (System.Random) is confined to overlay/debug and never backs goldens. FYI locked in a doc note: `RollSuccess` draws `Next(1_000_000)` — a different rejection-sampling threshold than battle's old `NextPerMille`, covered by the version bump.

### Omni fallback for componentless requests — a contract change, stated honestly
**Audit correction:** today an empty `Components` list does **not** "always miss" — `Compute` **throws** (`ElementPayload.Validate`, double-checked again inside `ElementHub.ResolvePayloadBonus`). The fallback therefore *replaces a hard throw*: an empty list resolves as one pseudo-component over the **omni halves only** (power/defense/accuracy/dodge/crit families at `.omni`; matchup bonus 0; crit families included). Consequences, locked:

- `ElementPayload.Validate` / `ElementHub` gain an explicit empty-is-legal path — both files are in scope (they were missing from the draft's Structure).
- The overlay dispatcher never reaches this path (`OverlayCombatMath.Finalize` returns pass-through on empty payload — verified) so overlay behavior is unchanged; a regression golden proves the dispatcher path byte-identical, and the invalid-weight-sum throw test stays green (only *empty* is legalized, malformed still fails loudly).
- **Neutral-snapshot golden (locked):** componentless attack on all-zero channels → `P(hit) = 0.5`, `P(crit) = 0.5`, `critMult = 1.5`. These are the numbers battle's re-tune (battle-adoption) moves off via baselines.

### Min-chip floor — shared resolver policy (owner decision, 2026-08-21)
`max(0, …)` plus retired variance makes deterministic 0-damage stalemates reachable. New profile-scoped policy: **a landed hit deals at least `ceil(MinChipShareK × BaseOverlayDamage)`, min 1**, with `MinChipShareK` per profile: **battle/sim profile = 0.05; overlay profile = 0 (byte-identity preserved; enabling it in overlay is ask-first)**. Same idiom as the shield chip floor. This is the SSOT-compliant replacement for battle's old floor-1 — never a host-local `Math.Max`.

### RNG draw-consumption contract (locked; audit finding)
- Per swing: 1 draw for hit; +1 draw for crit only when the hit landed. **Saturated probabilities consume no draw** (`p ≤ 0` / `p ≥ 1` short-circuit; sigmoid saturates to exactly 1.0 near delta ≈ +3,650 at scale 100) — a stat change can shift stream consumption. Golden'd so it is a known property, not a surprise.
- `ForceHit`/`ForceCrit` **skip draws entirely** — forced and natural runs are on different stream positions from the first swing. Production battle requests never set them (enforced by the ban test); whole-battle goldens are natural-roll only.

### Crit multiplier bounds (locked property)
`critMult = 1 + Sigmoid(critDmgDelta)` is bounded to **(1.0, 2.0)** per full-weight payload; delta 0 = exactly ×1.5 (the anchor that keeps crit lethality unchanged at adoption). The old battle ceiling ×3.0 is unreachable — enrichment content must be costed against (1, 2). Property test locks the bound; near-×1.0 "phantom crits" are a known reporting semantic (noted for VFX/report copy).

### Retirement contract (executed by `battle-adoption`)
`BattleEngine.ShareMilli`, `BattleRuleset.{Hit*,Crit*}`, `RollDamage` variance/matchup are declared duplicates. This module ships the **ban test** (retired symbols absent post-adoption; no `ForceHit` in battle production paths) and the **parity harness** (host swing ≡ direct resolver call).

### Base-damage semantics (for hosts)
`BaseOverlayDamage` is host content (overlay: packet amount; battle: attacker `Atk`). Power channels carry *adjustments only* — hosts must not double-map their base attack stat into `combat.power.*`. Defense-side stats **do** map into `combat.defense.*` (that is their only consumer). Exact per-host mapping tables live in the adoption specs.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Combat"
.\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs   (omni fallback, min-chip policy hook)
src/FusionRpg.Core/Combat/Element/ElementPayload.cs    (empty-is-legal path)
src/FusionRpg.Core/Combat/Element/ElementHub.cs        (empty payload → zero bonus)
src/FusionRpg.Core/Combat/CombatProfiles.cs            (new — per-profile MinChipShareK)
src/FusionRpg.Core/Battle/SeededRngCombatAdapter.cs
tests/FusionRpg.Core.Tests/Combat/                     (omni goldens, chip floor, draw contract,
                                                        crit bounds, adapter determinism, ban/parity)
docs/architecture/decisions.md                         ("Combat resolution SSOT" row at build start)
```

## Testing strategy

Byte-identical regression for every overlay golden (incl. the invalid-payload throw suite); neutral 0.5/0.5/1.5 omni goldens; min-chip boundary goldens per profile (0-damage → chip in battle profile, unchanged in overlay profile); draw-consumption goldens (saturation, forced-skip); crit-bound property test; adapter determinism vs `SeededRng` goldens; ban + parity harness armed at adoption.

## Boundaries

- **Always:** overlay byte-identical (overlay profile chip = 0); additive API; owned-PRNG adapter for replayable hosts.
- **Ask first:** any `CombatProbabilityPolicy` change; enabling min-chip in overlay; per-element scales; variance as shared policy.
- **Never:** a second resolution implementation; `System.Random` behind goldens; host-local floors or curves; `ForceHit` in production paths.

## Success criteria

1. Overlay goldens byte-identical. 2. Omni fallback locked (throw replaced knowingly; neutral goldens; malformed still throws). 3. Min-chip policy in with per-profile goldens. 4. Draw-consumption + crit-bound properties locked. 5. Adapter determinism tests green. 6. Ban + parity harness ready. 7. decisions.md row drafted.
