# Spec: `basic-attack-seed`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** — (leaf, parallelisable)

---

## Objective

`act.attack` — the engine's "always legal" fallback basic attack — exists only as a hardcoded C# row
(`BattleRunState.cs:64-81`) whose `Costs` is a literal `Array.Empty<CompiledActionCost>()` (`:81`).
That puts a balance number in code and makes the action **uncostable by any data path**, because
`costsByActionId` is built only from *held* actions (`:558-565`) and a basic attack is never held
(D2: basics cost no loadout capacity).

Owner decision, 2026-09-13: **ship the shared fallback, but load it from seed data.** Not per-species
generation — element already varies per actor without it, because element rides
`attacker.AttackComponents`, not the action row (`action-ideal.md:158-160`).

Success: `act.attack` is an authored seed row whose cost, rung band and atom family are reviewable
config, and the engine's fallback resolves to it.

## Tech stack

`data/seed/actions/` (authored content) + `FusionRpg.Core/Actions/Corpus` (parser, composer) +
`FusionRpg.Server/Program.cs` (the startup import).

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCorpus"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionCorpusImporter"
```

## Project structure

| Path | Duty |
|---|---|
| `data/seed/actions/authored-basics.json` | **Already authored.** `act.attack`, `kindHint: "basic"`, atom family `atom.fx-overlay-damage`, `scope: general`, `rungBand [1,1]` |
| `src/FusionRpg.Core/Actions/Corpus/ActionCorpusBriefJson.cs` | Must begin consuming `kindHint` |
| `src/FusionRpg.Core/Actions/Corpus/ActionCorpusComposer.cs` | Must honour it instead of hardcoding `Kind = ActionKind.Skill` (`:144`) |
| `src/FusionRpg.Server/Program.cs` | The hardcoded two-file loader array (`:402`) must include the authored file |
| `data/tuning/action-corpus-cost-templates.v1.json` | Gains Kind-aware cost resolution (below) |

## The authored seed is a new file, never an edit to generated data

Every entry in `committed-round-*.json` carries a `_provenance` block (`model`, `pipeline`,
`promptVersion`) marking it **seedsmith output**, which this repo forbids hand-editing — the sanctioned
path is always "fix the generator and regenerate". `authored-basics.json` deliberately carries **no**
`_provenance`, and its `_meta` says so explicitly, so a regeneration run cannot mistake it for output
and no generator may write to it.

**`kindHint` is real and already shipped in the data** — briefs carry `"kindHint": "innate"`. The
parser explicitly declines to consume it (`ActionCorpusBriefJson.cs:10-20`: *"content-authoring
metadata this module does not consume"*) and the composer discards it. That is the defect; the field
is not new.

## Cost resolution must become Kind-aware

`action-corpus-cost-templates.v1.json` is keyed by **`ActionCategory`**, and every category resolves
to `qi` — `attack → qi 20`. But D5 requires a **Basic** to cost `stamina`, and a signature action
(`ActionKind.Innate`) to cost `qi`. Category alone cannot express that: `act.attack` and a fire-breath
innate are both `Category = attack`.

**Decision: the per-Kind rule lives in the template, not on the brief.** An explicit cost on an
authored brief would fragment cost authoring across two places; keeping it in the template preserves
one authority and one file for a balance pass to edit. Resolution order becomes **Kind first, then
Category**.

## Code style

The authored row's identity must match the engine's own, or the fallback and the seed diverge
silently:

```csharp
// BasicAttack.cs:30 — the id the engine's fallback already uses
ActionId = "act.attack"
```

Timing is **not** authored here — `ActionTimingDerivation.DeriveBasicAttack(BasicAttackEnvelope,
ActionTimingPolicy.Tuning)` already derives it per category from `action-timing.v1.json`. Authoring
timing in the seed would duplicate a shipped curve.

`rungBand: [1,1]` is deliberate: magnitude scales with `Θ` through `anchor(Θ) = sharePermille ×
P(Θ)/1000`, and a flat rung means the basic attack is the floor that never individually improves —
never a second progression curve (`action-ideal.md` §1.3).

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | `ActionCorpusBriefJson` parses `kindHint`; an unknown value is rejected with the brief id named, never coerced |
| Core unit | `ActionCorpusComposer` emits `Kind = Basic` for `kindHint: "basic"`, `Innate` for `"innate"`, and `Skill` when absent (back-compat for all 179 shipped briefs) |
| Core unit | Kind-aware cost: a Basic resolves `stamina`; an Innate `attack`-category action resolves `qi`; existing category behaviour unchanged where Kind is absent |
| Core unit | The authored brief's atom family resolves — the composer rejects a brief whose families resolve no atom (`ActionCorpusComposer.cs:69-75`) |
| Data test | Importing `authored-basics.json` yields exactly one row, id `act.attack`, `Kind = Basic`, with a `stamina` cost row attached |
| Regression | The 179 existing briefs still import unchanged; **no shipped action changes Kind or cost** |

## Boundaries

- **Always:** treat `authored-basics.json` as hand-authored content — reviewed edits only, never
  generator output, never regenerated.
- **Always:** keep the seed's `id` identical to `BasicAttackEnvelope.ActionId`.
- **Ask first:** widening `Program.cs`'s loader to the full generated corpus — 155 of 179 briefs do
  not load today, and turning them on is the action program's own content gate, not this module's.
- **Never:** author timing in the seed (it is derived); author a cost on the brief (it belongs in the
  template); hand-edit any `committed-round-*.json`; add `_provenance` to the authored file.

## Success criteria

- [ ] `kindHint` is parsed, honoured, and round-trips to `ActionKind`.
- [ ] `act.attack` imports from seed as `Kind = Basic` with a `stamina` cost row.
- [ ] Kind-aware cost resolves Basic → `stamina`, Innate → `qi`, with Category as the fallback.
- [ ] All 179 existing briefs import unchanged — no Kind or cost drift.
- [ ] The engine's fallback and the seeded row are the same action id.
- [ ] No `committed-round-*.json` modified.
