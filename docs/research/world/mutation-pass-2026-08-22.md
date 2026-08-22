# Mutation pass — `ai-commander` W25–W31

> Superseded as a *method*: this pass was run by hand from a throwaway script. It is now
> `scripts/mutate.ps1` with the mutants in `scripts/mutants/*.json`, and `scripts/coverage.ps1`
> sits beside it. The findings below stand; re-run them with `.\scripts\mutate.ps1 -Set world-ai`.

**Run:** 2026-08-22, ten hand-written mutants across the six files W25–W30 added or changed.
A mutant that *survives* means every test passed while the code was deliberately wrong.

| Mutant | First run | After | Note |
|---|---|---|---|
| Hops: every reachable sector is one hop away | caught | caught | |
| Hops: unreachable reports 0 instead of null | caught | caught | |
| MarchGraph: built on the supply lens | caught | caught | the fixture `first-light` cannot provide |
| MarchGraph: climate always unknown | **survived** | caught | the ley test went through `LaneCost` directly and never through the wiring |
| BelievedSupply: any owned sector seeds supply, Seat or not | **survived** | caught | attrition would simply never fire |
| BelievedSupply: enemy zones of control do not bar the chain | **survived** | caught | |
| SupplyReach: one-way links carry supply both ways | **survived** | caught | unreachable through shipped content — see below |
| LaneLens: every lane traversable under both lenses | caught | caught | |
| commit: the named turn is not checked | caught | caught | |
| stand-fast: command id forgets which turn it is | **survived** | **survives, by design** | see below |

## What the survivors were worth

**Two were real holes.** The Seat requirement and the zone-of-control check in `BelievedSupply` had
no test at all. Without the first, every faction is permanently and invisibly supplied and attrition
never fires; without the second, supply routes straight through a sector an enemy army is standing
in, which is the entire thing a zone of control is for.

**One was untested wiring.** The ley-discount test proved `LaneCost` honours a climate lookup but
went to `LaneCost` directly — nothing checked that `MarchGraph` hands it the *believed* one. A
lookup returning null for every sector passed the whole suite.

**One is unreachable through shipped content.** No lane type is both one-way and supply-carrying, so
`SupplyReach` can never receive a one-way link from a real world and `SupplyGraph` can never
exercise the direction check. Rather than delete defensive code that a future supply-carrying
current would need, the rule is now tested directly at `SupplyReach.From`, where a one-way link can
simply be handed in.

**One survives on purpose.** The turn in a command id is legibility, not correctness — the store's
key is already `(world, turn, commander, commandId)`. The code now says so, so the next person to
notice the mutation finds a note rather than concluding the tests have a hole.

## Two tests that were passing while proving nothing

Found by hand in the same pass, both in W30, both since replaced:

- *"a chain cut behind a lane you cannot see still looks intact"* severed a lane between two sectors
  Dave did not own, so it would have passed with the divergence code deleted.
- *"a Seat you have only glimpsed is not counted as a source"* asserted an `IntelRecorder` property
  and never called `BelievedSupply` at all.

Chasing the second turned up the finding that reshaped the module's documentation: **holding a
sector grants full sight of it**, and supply only walks between sectors you hold — so two of the
three divergences the spec claimed for believed supply cannot occur, and only remembered *ownership*
is real. See `BelievedSupply`'s summary.

## Method note

Restoring a mutant with `shutil.move` gives the file an older mtime than the compiled output, so
MSBuild keeps the **mutated** assembly and the next full run fails against clean source. Touch the
files after restoring, or the pass ends by looking like a regression it did not cause.

## Follow-up, same day: the harnesses became real

Both checks are now scripts anybody can run, rather than numbers somebody measured once:

```powershell
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.World      # what the tests touched
.\scripts\mutate.ps1   -Set world-ai                        # what they would notice
```

**18 mutants, all caught** — the 14 in `world-ai` and 4 in `world-turn` covering the commit path.

Coverage then found two things mutation could not, because mutation only ever asks about code a
mutant was written for:

- **`AllPairsCost.TotalPairCost()` had no callers at all**, along with the `Graph` property and the
  `Between(int,int)` overload. Worse, its doc comment claimed `ReconnectionCost` used it — that
  method computes its own pairwise sum and never did. Dead code with a confident explanation
  attached is harder to remove later than dead code with none. Deleted.
- **`TurnReport.FromEntries` read 44% in Core and is fully exercised — from `FusionRpg.Data.Tests`.**
  Coverage is per test project, so Core's number cannot see Data's tests driving Core's code. A low
  row is a question, not a verdict.

Two caveats the tools now carry in their own help:

- A **timing assertion cannot survive instrumentation.** `AtomBenchGuardTests` asserts nanoseconds
  per atom and fails under coverlet, which rewrites every sequence point. `coverage.ps1` excludes
  `~Bench` by default rather than letting it look like a regression.
- **Restoring a mutant leaves MSBuild holding the mutated assembly**, because the restored file has
  the older timestamp. `mutate.ps1` touches every file it restores. Discovered the hard way: a full
  sweep came back with four failures against clean source.
