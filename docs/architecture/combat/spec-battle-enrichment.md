# Spec: battle-enrichment

Module id `battle-enrichment` in the [combat unification map](../combat-unification-map.md). Depends on `battle-adoption` (everything here rides the unified pipeline). ~~Three waves, owner-ordered; **build held**~~ — **two waves; nothing held.** Each wave gets its own task breakdown at plan time.

> ### ⛔ Rebased 2026-09-04 — read before building any wave
>
> [battle-timeline-map.md](../battle-timeline-map.md) said this spec *"is **partly superseded and
> should be rebased after T5**"*. T5 closed 2026-08-28; **the rebase never happened**, and the text
> below still describes a round loop that no longer exists. Corrected here rather than silently:
>
> - **Wave S is removed from this spec.** It is now its own module,
>   [spec-species-skills.md](spec-species-skills.md). Its `SkillDef` (id, cooldown in **rounds**,
>   action kind, targeting policy) and code-first `SkillCatalog` each re-invent something that has
>   since shipped — `ActionRow`, `ActionEnvelope` (absolute **ticks**, `CooldownLedger.cs:15`),
>   `ActionKind`, `ActionTargetSpec`, and `ActionCatalog` (wired into battle by T19, 2026-08-30).
>   Building it as written would create a fifth content system.
> - **Wave R stands, with one clause spent.** Its concern about sub-round `periodMs` under-delivery
>   was fixed by **T9** (`subsystems-on-timeline`, closed 2026-08-28) — status pulses now fire at
>   true ms. The rider design itself, the dedicated `riders` RNG stream, and the typed-DoT
>   `StatusInstance` element are all unaffected and still correct.
> - **Wave H stands unchanged.** The timeline map's own read: *"genuinely independent
>   (resolver-side, not timeline-side). **Can ship any time**."*
> - **The build hold is lifted** — see the combat-unification map's status box. `RulesetVersion` is
>   **4**, not 2; neither remaining wave bumps it unless it moves a golden.

## Objective

Make battles richer on top of the now-central combat system: attacks that apply statuses, per-species cooldown skills, and hybrid element payloads — all expressed through existing Core machinery (`StatusRuntime`, `GrantShield`, resolver components), never as new engine-side math.

## Wave R — on-hit status riders

- `BattleActorSetup` (or trait defs) gain rider specs: `(statusId, magnitude, durationMs, periodMs, chanceMilli)` applied on landed hits through the existing `StatusApplyInput` path with attacker context — resist/immunity evaluation included.
- **Rider apply rolls draw from a new dedicated `riders` stream** (audit fix: the `status` stream is already the contagion-spread stream — sharing it would make every rider content change a full-battle butterfly, against the engine's own one-system-one-stream rule and the `essence` precedent).
- **Typed DoTs land here:** `StatusInstance` gains an optional element (a `Status/` change — in this wave's structure, coordinated with the status SSOT) so battle *and overlay* DoT pulses can carry the status element as a full-weight component to the shield gate. Until then both modes are element-neutral on DoTs by parity (battle-adoption).
- Rider grammar matches `BattleStatusSpec`; the "trait/attack riders later" seam in `BattleEngine` closes. Trait-sourced riders come from `TraitBattleCatalog` rows, not engine branches.
- **Cross-wave invariant (audit fix):** a zero-rider battle is byte-identical across v2→v3 — testable, and it catches stream-contention accidents.

## Wave S — species skills

- `SkillDef`: id, cooldown (rounds), action kind + params, targeting policy. Action kinds reuse the effect vocabulary semantics: damage (through the resolver + pipeline), heal (pipeline, positive), `GrantShield` (ShieldRuntime — **durations in ms**, host-converted), `ApplyStatus` (StatusRuntime). No bespoke skill math. Crit-damage-flavored skill content is costed against the resolver's **(1.0, 2.0)** crit-multiplier bound — the retired ×3.0 ceiling is unreachable.
- Selection: deterministic policy per actor (skill off cooldown → use, else basic attack); `skills` RNG stream for any chance-based policy. Initiative unchanged.
- `SkillCatalog` code-first like the trait catalog; species → skill mapping via demon catalog metadata (coordinated with the demon stream's species SSOT).
- Report: skill-use events (`skill.used`) in the battle vocabulary.

## Wave H — hybrid payloads

- `BattleActorSetup.ElementSecondary` joins the attack payload as weighted components (policy constant, e.g. 0.7/0.3 — locked at plan time, ask-first to change). The resolver already does per-component weighting; battle just builds richer requests. Matchup vs dual-type defenders is already handled.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
```

## Structure

```
src/FusionRpg.Core/Battle/   → rider wiring (R), SkillCatalog.cs + selection (S), payload build (H)
tests/FusionRpg.Core.Tests/Battle/   → per-wave goldens + determinism replays
```

## Testing strategy

Per wave: deterministic goldens (rider proc sequences under fixed seeds, skill rotation timelines, hybrid matchup tables), replay byte-identity, report-event coverage, and the standing rule that every HP delta still flows through the pipeline (ban test stays green).

## Boundaries

- **Always:** existing vocabularies (status catalog ids, effect action kinds, event kinds); deterministic streams per system; RulesetVersion bump per behavior wave.
- **Ask first:** new status ids; skill resource costs (mana-like) — that's a new resource, its own spec; hybrid weight changes.
- **Never:** skill/rider math inside the engine loop that bypasses resolver/pipeline/StatusRuntime; nondeterministic selection.

## Success criteria

Per wave: goldens + replay green, report events observable, ban test green, expeditions still resolve. Program-complete when all three waves ship on `RulesetVersion` history with decodable stamps.
