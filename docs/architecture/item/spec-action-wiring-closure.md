# Spec: `action-wiring-closure`

**Module id:** `action-wiring-closure` · **Program:** [item](../item-map.md) · **Build order:** independent
**Depends on:** nothing new — the real dependency (`rpg_action` populated) already exists and is
already wired; this closes item's own stale belief that it does not
**Rulings:** **X3** (`item-map.md` §3, `item-todo.md:7159-7175`), gates **GA3**/**GA4** (module 19)

## Objective

Correct a stale claim across item's own docs and one production error string — *"nothing produces
actions yet (`ActionSeeder.Generate` has zero production callers)"* — and, since the claim is stale
because a real mechanism already populates `rpg_action`, attempt GA3 and GA4 (module 19's own two
gates that were held specifically because "with no `rpg_action` row, both would be a fixture
pretending to be a proof").

⛔ **This is not building another program's feature.** Action-corpus (the program) is not "still
under active construction" the way `item-plan.md`'s D36 recorded it in 2026-09-03/04 — its own
memory-of-record shows all 25 of its own modules (A1–A25) closed as of 2026-09-07, same day as this
session. D36's *reason* for treating X3 as hands-off ("under another owner's active construction")
no longer holds; what remains is item's own docs not knowing the dependency already resolved,
exactly the class of staleness this whole session's audit spent its time hunting elsewhere.

## Design

### The real mechanism, traced end to end — not `ActionSeeder.Generate`

1. **What item's own docs, and one live C# error string, still say**: `ItemGrantValidator.cs:121-125`
   — the literal, shipped rejection message for an unresolvable `ActionId` — reads *"⛔ X3: nothing
   produces actions yet (ActionSeeder.Generate has zero production callers), so gate GA2 ships DDL
   and validator with zero content rows rather than rows pointing at an empty table."* Confirmed
   still true in isolation: `ActionSeeder.Generate` genuinely has zero non-test callers anywhere in
   `src/`.
2. **The real mechanism that makes the claim's CONCLUSION false anyway**: `src/FusionRpg.Server/
   Program.cs:371-386` — gated by `FUSIONRPG_ACTION_CORPUS_IMPORT` (default ON, the same kill-switch
   convention as `FUSIONRPG_PERF`), runs at every server startup, before any connection is accepted:
   reads `data/tuning/action-corpus-cost-templates.v1.json` plus two real brief files
   (`data/seed/actions/committed-round-1.json` — 19 briefs, `committed-round-2.json` — 5 briefs, 24
   total, confirmed non-empty by direct read 2026-09-07) and calls
   `FusionRpg.Data.ActionCorpusImporter.Import(store, actionBriefs, actionCostTemplate,
   RungPolicy.Table)` — a REAL, wired, production caller. `rpg_action` is not empty in any freshly
   booted server.
3. **The composer stamps every row `Grantable = true`**: `ActionCorpusComposer.cs:148` — unconditional,
   confirmed by direct read. `ItemGrantValidator.ValidateAction`'s own four real gates
   (`action is null` / `!action.Enabled` / `action.Kind == Basic` / `!action.Grantable` /
   `DefaultAttackEligible` for a default-attack role) are therefore very likely satisfiable by at
   least one of the 24 real rows today — **verify, don't assume**: confirm at least one real
   imported row has `Kind != Basic` before treating GA3/GA4 as unblocked (acceptance #1).

### The fix

1. **Doc correction** (mechanical, do first): every citation of "`ActionSeeder.Generate` has zero
   production callers" as a live blocker — the `ItemGrantValidator.cs:121-125` error string, and
   every `item-todo.md`/`item-map.md` row citing X3 the same way — is corrected to name the real
   mechanism (`ActionCorpusImporter`, `Program.cs:371-386`) and the real, current state (`rpg_action`
   is populated at boot). The error string specifically: keep an `UnknownAction` rejection for a
   genuinely-missing id (that case is still real — a brief that names an action outside the 24
   imported ones still resolves to nothing), but stop asserting "nothing produces actions yet" as
   the reason, since it is no longer the reason.
2. **GA3** — pick one real, `Kind != Basic`, `Grantable`-true action from the 24 imported rows that
   makes sense as a weapon's granted ability (a `scope: "family"`/`"general"` `attack`-category row
   is the natural fit, e.g. one of `action.family.pea.001`/`action.family.cactus.001`/
   `action.general.*`'s attack-shaped rows — pick by reading the real brief content, not by
   assumption); author ONE real `ItemGrantedActionRow` on an existing weapon base type naming it;
   drive a real battle where the granting item is equipped and the granted action fires — the exact
   "one weapon base type with a real action driven through a battle" GA3 always asked for, now
   provable with a real row instead of a fixture standing in for one.
3. **GA4** — exercise the `granted` role (as opposed to `DefaultAttack`) for the first real time,
   using the same real action row: confirm a granted (non-default-attack) action reaches a real
   battle turn through whatever selection path module 19's own `ItemGrantRole.Granted` arm uses.
4. If NO real imported row turns out to satisfy all of GA3/GA4's real gates (acceptance #1 comes back
   negative) — this spec's fallback is explicit, not silent: report exactly which gate the closest
   candidate row fails, and leave GA3/GA4 open with that PRECISE reason (a missing `Kind` classification,
   say) rather than the old, now-provably-wrong "no rows exist at all" reason. That is still a real,
   evidenced improvement even in the negative case.

### What this does not do, on purpose

- Does not touch `ActionSeeder.Generate` itself, or attempt to give it a production caller — it is
  not the mechanism that matters here, and per D36's still-standing NARROWER point (item does not
  amend or extend action-corpus's own internal machinery), this spec leaves it exactly as action-corpus's
  own program shipped it.
- Does not author new action-corpus content. The 24 real briefs already shipped are the fixture; if
  none of them fit GA3's "weapon action" shape well, that is named as a real, precise gap for
  action-corpus's own next content pass — not something this spec invents new briefs to paper over.
- Does not reopen D36's broader point about action-corpus's map/schedule. This spec's premise is
  narrowly "action-corpus's own program is closed, per its own record" — not a general license to
  treat every future action-corpus ask as item's to resolve unilaterally.

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~ItemGrant
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~ItemGrantValidator
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~GrantedAction
```

## Project structure

- `src/FusionRpg.Core/Items/Grants/ItemGrantValidator.cs` — error-string correction only, no
  behavior change to the four real gate checks.
- `tasks/item-todo.md`, `docs/architecture/item-map.md` — doc corrections (X3 row, GA3/GA4 rows).
- Module 19's real granted-actions test files, extended with the new real-row GA3/GA4 evidence.
- No production code path changes beyond the error string, unless acceptance #1's verification finds
  a genuine, narrow gap (e.g., a missing `Kind` value on an otherwise-fitting row) worth a small,
  separately-named fix.

## Code style

The corrected error string keeps `ItemGrantRules.Fail(ItemGrantRules.UnknownAction, ...)`'s existing
shape — only the prose inside the string changes, matching this file's own established one-string-
per-rule convention.

## Testing strategy

1. **Acceptance #1, first**: a real test loads `rpg_action` from a freshly-imported store and asserts
   at least one row has `Enabled=true`, `Kind != Basic`, `Grantable=true` — proving GA3/GA4's
   prerequisite for real, not by inspection alone.
2. **GA3, red-first**: before authoring the grant row, a battle with the chosen weapon base type
   equipped fires no extra action beyond the actor's three basics (proves the gap is real). After:
   the granted action fires in a real `BattleEngine.Resolve` pass.
3. **GA4**: the same real row, granted via `ItemGrantRole.Granted` (not `DefaultAttack`), is offered
   as a real, selectable option in whatever the granted-role's own selection surface is — proven
   live, not asserted from the DTO shape alone.
4. **Regression**: `ItemGrantValidatorTests` (or equivalent) still refuses a genuinely-unknown action
   id, a disabled action, a basic action, and a non-grantable action — the four real gates are
   unaffected by the string-only correction.
5. **Doc-consistency check**: grep the corrected doc locations for the literal phrase
   "ActionSeeder.Generate has zero production callers" and confirm zero remaining hits framed as a
   current blocker (a historical "was true when written" framing, dated, is fine — matching this
   session's own established convention for correcting stale claims without erasing history).

## Boundaries

**Always:** verify GA3/GA4's real gates against a real, freshly-imported `rpg_action` table before
claiming either closed — a claim without the executed check is exactly the "claims are not proof"
failure this whole audit exists to catch.

**Ask first:** none identified — every touched file is item's own code/docs, or a read-only
verification against action-corpus's already-shipped, already-closed output.

**Never:** re-introduce the old "X3 blocks everything" framing as a default if GA3/GA4's specific
verification comes back negative for a narrow reason — name the narrow reason precisely instead.

## Success criteria

- The error string and every doc citation of the stale X3 claim reflect the real, current mechanism.
- GA3 and GA4 are either genuinely closed with live-battle evidence, or left open with a precise,
  newly-discovered reason distinct from "no rows exist."
- Zero regression in `ItemGrantValidator`'s four real content gates.
