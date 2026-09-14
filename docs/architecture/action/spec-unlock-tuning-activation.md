# Spec: unlock-tuning-activation (A26)

Module **A26** in the [action map](../action-map.md) §17. Reads
[action-playability-ideal.md](../action-playability-ideal.md) gap #1. Depends on nothing — every
downstream consumer is already built and tested.

## Objective

`RpgStore.UniqueActors.cs:1902` early-returns (`if (UnlockTuningPolicy.Tuning is not { } tuning)
return;`) before a real specimen's level-up ever rolls the unlock ladder. `UnlockTuningPolicy.Configure`
(`UnlockTuningPolicy.cs:15`) is never called by the real host (`Program.cs`) — only by test fixtures and
`tools/ProveHubCombat`. This module adds the one call. **Nothing else changes** — `ActionEligibility`,
`UnlockState`, `RpgStore.UpsertGrant`, and every reader of a grant are already correct once fed a
non-null tuning.

**Why this is the whole feature's root cause**: every other gap in A27–A32 is downstream of this one. A
specimen can hold a real equipped loadout, a player can see it, an FE can render it — none of it changes
without this line, because nothing ever grants a second action to a real specimen today.

## Design

1. `data/tuning/action-unlock.v1.json` already ships (`p1=500‰, delta=880‰, floor=1‰, cap=10`,
   `discardTaxCoeffMilli=100`) and `UnlockTuningLoader.Parse` already exists (`UnlockTuning.cs:34`).
2. `Program.cs` already has ~18 sibling calls of the shape
   `Foo.Policy.Configure(FooTuningLoader.Parse(File.ReadAllText(path)))` (lines 30–90, e.g.
   `ContractPolicy.Configure`, `LoamPolicy.Configure`, `SoulEarnPolicy.Configure`). Add one more, same
   block, same shape:

```csharp
FusionRpg.Core.Actions.Unlock.UnlockTuningPolicy.Configure(
    FusionRpg.Core.Actions.Unlock.UnlockTuningLoader.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "tuning", "action-unlock.v1.json"))));
```

3. No new file, no new class, no new config surface. `UnlockTuningLoader.Parse` already rejects a
   malformed or missing-field tuning file the same way every sibling loader does (throws) — startup
   failure on a bad tuning file is the existing, correct behavior for every other `*Policy.Configure`
   call, so this needs no special handling.

## Tunables

None new. `data/tuning/action-unlock.v1.json` already ships and is already the balance surface (rung
cap, chance decay, discard tax coefficient) — this module only makes it reachable.

## Commands

```powershell
dotnet build src\FusionRpg.Server
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~UnlockTuning"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionUnlockGrant"
```

## Project Structure

```
src/FusionRpg.Server/Program.cs              (one new Configure call, same block as its ~18 siblings)
tests/FusionRpg.Server.Tests/Actions/UnlockTuningActivationTests.cs   (new)
```

## Code Style

Match the exact call shape of the surrounding block — no wrapper method, no new abstraction. A one-line
diff should look like every other line around it.

## Testing Strategy

**The regression this module exists to catch is "the call is present in a test fixture but absent from
the real host"** — exactly how this gap survived until now. So the test must boot the real host or its
real bootstrap sequence, not call `Configure` directly:

| Case | Expect |
|---|---|
| Build the real `WebApplication` via `RpgApiFactory : WebApplicationFactory<Program>` (`tests/FusionRpg.E2E.Tests/RpgApiFactory.cs:9`, confirmed to boot the real `Program.cs` entry point — not a hand-rolled test host) | `UnlockTuningPolicy.Tuning` is non-null immediately after |
| A real specimen levels up through the real award path (`AwardUniqueActorXpUnlocked`, hosted, with a poisoned/guaranteed-hit RNG) | a real `rpg_action_grant` row appears for that specimen when `ActionEligibility.Candidates` is non-empty |
| `action-unlock.v1.json` deleted/corrupted (simulate via a temp copy) | host startup **throws**, naming the file — never a silent null `Tuning` |
| Existing `ActionUnlockGrantWiringTests.cs` / `Server.Tests` fixtures that already call `Configure` directly | still pass unchanged — this module doesn't touch their setup, only adds the missing real-host path |

## Boundaries

**Always:** run the full `Core.Tests`/`Data.Tests`/`Server.Tests` suites after the change — this touches
`Program.cs`, shared by every endpoint; confirm zero regressions, don't assume.

**Ask first:** nothing — this is the lowest-risk item in the whole reopening (an already-tested chain
gaining its one missing caller).

**Never:** touch `UnlockTuningPolicy`, `UnlockState`, `ActionEligibility`, or the grant-write path
themselves. They are correct. If a test here fails, the bug is almost certainly in this module's own new
call, not in code this module doesn't touch.

## Audit note (2026-09-13, strengthening pass)

**Verified, not assumed**: `RpgApiFactory` (`tests/FusionRpg.E2E.Tests/RpgApiFactory.cs:9`) is
`WebApplicationFactory<Program>` — it genuinely boots the real `Program.cs` top-level bootstrap, so the
integration test proposed above proves the real production path, not a parallel one. This closes the one
risk this module's test design could otherwise have silently missed (a test host that duplicates only
part of the real wiring).

**No ordering dependency found**: nothing else in `Program.cs`'s Configure block reads
`UnlockTuningPolicy.Tuning` during its own configuration — confirmed by the same grep that located the
~18 sibling calls. This call may be placed anywhere in that block.

## Success Criteria

1. `UnlockTuningPolicy.Tuning` is non-null after the real host starts.
2. A real specimen's real level-up produces a real, persisted `rpg_action_grant` row when eligible.
3. Zero regressions across `Core.Tests`, `Data.Tests`, `Server.Tests`.
4. A missing/corrupt tuning file fails host startup loudly, matching every sibling `*Policy.Configure`
   call's existing behavior.
