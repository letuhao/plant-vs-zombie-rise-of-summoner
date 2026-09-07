# Spec: `container-kind-expansion`

**Module id:** `container-kind-expansion` · **Program:** [item](../item-map.md) · **Build order:** independent
**Depends on:** nothing · unblocks modules **12** (`threshold-grants`), **13** (`set-charm-gen`), **16**
(`sockets`), **18** (`consumables`), **21** (`strain-splice-gen`) — all already built and tested, all
shipping today with this one half deferred
**Rulings:** **X7** / **D27** (`item-map.md` §3) · lane: `effect-atom/spec-container-schema.md`

## Objective

Add the four `container_kind` values D27 named and never minted — `gem`, `set`(→`charm`, see naming
note below), `combo`, `consumable` — to `ContainerKind` (`src/FusionRpg.Core/Effects/Atoms/
ContainerRow.cs`), so item modules 12/13/16/18/21's already-built machinery has a real container home
to bind into instead of shipping with the last half of their own acceptance criteria permanently
deferred.

⛔ **This is item claiming ownership of its own four kinds, not amending effect-atom's spec out from
under them.** `ContainerRow.cs`'s own doc comment states the real bar for a new kind: *"adding one is
a reviewed change, because each implies a spec that owns its authoring and its lifecycle."* The
existing precedent, `Enemy` (party-dungeon's D2.6), is *"owned by `encounter-generator`, never
authored outside a delve encounter"* — a DIFFERENT program's addition, reviewed and landed the same
way this spec proposes for these four. **All four new kinds here are owned by item's own modules**
(13, 16, 18, 21) — item is not asking effect-atom to build or own gem/charm/combo/consumable
containers; it is asking to mint the enum value + lifecycle rule for kinds it already built the
authoring and drawing machinery for. The ask was filed into `effect-atom-map.md` §20 on 2026-09-06
and remains unanswered; this spec is the reviewed-change artefact their own doc says the addition
needs, submitted by the party that owns all four resulting lifecycles.

**Users:** module 12 (`threshold-grants`, set-piece binding, currently inert per `item-todo.md:2658-
2703`), module 13 (`set-charm-gen`, 70 real charm rows with no container home), module 16 (`sockets`,
41 real insert rows plus the 102-cell combination grid), module 18 (`consumables`, `consumable`
container kind unminted per `item-todo.md:5004`), module 21 (`strain-splice-gen`, cannot bind either).

## Design

### The real gap, traced against the exact five lifecycle rules D27 needs

`ContainerRow.cs` today (`:9-18`): `Item`, `Trait`, `Skill`, `SpeciesPassive`, `Patron`, `WorldBuff`,
`Enemy` — seven values, confirmed by direct read 2026-09-07, none of D27's five.
`effect-atom/spec-container-schema.md:22` independently confirms the same six (pre-`Enemy`) kinds and
`:153` names adding one as **"Ask first."**

Four new values, each with the lifecycle rule its owning module already implies by how it authors
and consumes the kind:

| Kind | Owner (item module) | Authored by | Never authored outside |
|---|---|---|---|
| `Gem` | 16 (`sockets`) | `sockets-gen` (`gems/*.json`, real: `g1`/`g2`/`g3`, 60 entries) | a socket insert operation |
| `Charm` | 13 (`set-charm-gen`) | `setgen` (`charms/*.json`, real: 70 rows) | the charm-carry equip slot |
| `Combo` | 21 (`strain-splice-gen`) | `combogen` (`combinations/*.json`, real: 2 strains this session) | a socket-combination match |
| `Consumable` | 18 (`consumables`) | `consumablegen` (`consumables/*.json`, real: 63 rows) | a stock-spend action |

⚠ **Naming note, resolved against real code, not D27's original prose**: D27's own wording says
`set`, but `set` already exists as a first-class DB concept (`item_set`) predating containers — the
CONTAINER this ask actually needs is the **charm's** container (a set's bonus is a threshold-grant
row, module 12's own table, not a container). Naming the new value `Charm` (not `Set`) matches what
module 13's real code and real corpus actually need a container home for, and avoids a second,
colliding meaning for "set" in the same enum. Recorded here because acting on D27's literal word
would have minted the wrong kind for a real, already-shipped 70-row corpus.

### The fix

1. `ContainerKind` gains four members: `Gem`, `Charm`, `Combo`, `Consumable` (after `Enemy`, preserving
   every existing ordinal — this enum is stored by name in some paths and by ordinal in others per
   the existing `RpgStore.Containers.cs` read/write pair; verify which before assuming int-safety,
   per acceptance #1).
2. Every real switch/type-check site over `ContainerKind` (16 files identified by direct grep,
   2026-09-07: `ActionCorpusComposer.cs`, `AuraContentCatalog.cs`, `BattleModels.cs`,
   `TraitAtomSource.cs`, `EliteAffix.cs`, `ConsumableValidator.cs`, `ItemGrantedActionRow.cs`,
   `ItemGrantValidator.cs`, `UniqueContainerBuild.cs`, `UniqueValidator.cs`,
   `RpgStore.Containers.cs`, `RpgStore.PlayerSpecies.cs`, `RpgStore.UniqueActors.cs`,
   `ItemPreviewEndpoints.cs`, `ItemWorkbench.cs`, plus `ContainerRow.cs` itself) gets audited: a
   `switch` with no `default` arm must gain one case per new kind (even if that case is "refuse, not
   yet a legal binding target here" for a site the new kind genuinely doesn't touch) — an exhaustive
   switch that silently falls through a new enum value is exactly the class of bug this whole
   session's audit spent its time finding elsewhere in this codebase.
3. `effect-atom/spec-container-schema.md:22` is updated to list all eleven kinds (not proposed here as
   an edit to effect-atom's OWN authored prose without their sign-off — recorded as this spec's own
   "Boundaries" item; see below).
4. Each new kind's authoring path (`sockets-gen`, `setgen`, `combogen`, `consumablegen`) is checked
   against the real `ItemSeedValidator` for whether it currently claims a DIFFERENT kind string for
   its entries (`"kind": "combination"` in the real `combinations/strains.json`, for instance) — if
   so, that string is the seed-file's OWN kind tag (validated against `KindCatalog.cs`'s `SeedKind`
   registry, a *different, already-correct* mechanism from `ContainerRow.ContainerKind`) and is
   **not** renamed to match this enum; the two are related concepts (a seed kind and a runtime
   container kind) that happen to share a name in three of four cases, not the same field.

### What this does not do, on purpose

- Does not touch `Item`/`Trait`/`Skill`/`SpeciesPassive`/`Patron`/`WorldBuff`/`Enemy` — pure addition,
  zero renumbering.
- Does not build the actual bind/draw wiring for all four kinds in one pass if that turns out to be
  four separate, non-trivial change sets once the real switch-site audit (#2 above) is done — this
  spec covers minting the kind and making every existing switch exhaustive over it (refusing, where a
  site isn't ready, rather than silently admitting); each kind's own FULL bind path may be its own
  follow-on task named at VERIFY time, not assumed complete here.
- Does not answer `effect-atom-map.md` §20's own row for this ask — filing this spec is the reviewed
  change their doc says is needed, but their map is theirs to mark accepted/declined, per this
  program's own standing D36 discipline for the *reverse* direction (item doesn't presume to close
  another program's ledger entry on their behalf, even when item does the work).

## Commands

```powershell
dotnet build src\FusionRpg.Core
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~ContainerKind
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Container
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~Container
```

## Project structure

- `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs` — the enum.
- Sixteen consumer files (listed above) — audited for switch exhaustiveness; only sites that
  genuinely need a real case beyond "refuse" get one in this pass.
- `docs/architecture/effect-atom/spec-container-schema.md` — informational update, not an
  authoring change to their spec's decisions (see Boundaries).
- New test file `tests/FusionRpg.Core.Tests/Effects/Atoms/ContainerKindExpansionTests.cs`.

## Code style

New enum members follow the exact `PascalCase` convention already in use (`SpeciesPassive`,
`WorldBuff`); no attribute, no separate int backing beyond the implicit ordinal already relied on.
A refusal added to a switch site uses that file's own existing refusal shape (an `AtomRejection.*`
call, a thrown typed exception, or a `Problems.Add(...)`, whichever that file already does elsewhere
in the same switch) — never a new, local refusal convention.

## Testing strategy

1. **Enum shape**: `ContainerKind` has exactly eleven members after the change, in the exact order
   `Item, Trait, Skill, SpeciesPassive, Patron, WorldBuff, Enemy, Gem, Charm, Combo, Consumable` —
   pins ordinal stability for the existing seven.
2. **Round-trip through storage**: whichever of name-based or ordinal-based persistence
   `RpgStore.Containers.cs` uses, a container row of each new kind writes and reads back identically.
3. **Every switch site, red-first**: for each of the 16 files, a test (or an existing test extended)
   proves a `Gem`/`Charm`/`Combo`/`Consumable` container is handled — either by exercising its real
   new behavior, or by asserting the site's own refusal fires with a real, named reason rather than
   an unhandled-case exception or silent fallthrough.
4. **The four real corpora bind for real** (module-specific, run against real shipped content):
   - a real `gems/g1.json` entry constructs a `Gem`-kind container and a socket insert accepts it.
   - a real `charms/*.json` entry constructs a `Charm`-kind container and the charm-carry slot accepts it.
   - a real `combinations/strains.json` entry (this session's own trial content) constructs a
     `Combo`-kind container and a socket-combination match recognizes it.
   - a real `consumables/*.json` entry constructs a `Consumable`-kind container and a stock-spend
     action accepts it.
5. **Regression**: full `FusionRpg.Core.Tests`/`FusionRpg.Data.Tests` suites, compared against this
   session's own recorded baseline (not against zero), per this program's established convention.

## Boundaries

**Always:** keep every EXISTING `ContainerKind` member's ordinal and name untouched — this is
strictly additive.

**Ask first:** editing `effect-atom/spec-container-schema.md`'s own DECISIONS (not its factual
enum-value listing) stays effect-atom's call; this spec updates the listing to match shipped code
(a factual correction, matching this whole session's own established "keep docs honest" discipline)
without asserting a design opinion on their behalf. Marking `effect-atom-map.md` §20's row
"accepted" is explicitly not this spec's or this program's call to make.

**Never:** repurpose `Enemy` or any of the six pre-existing kinds' semantics to approximate one of
the four new ones as a stopgap — D27 asked for five distinct kinds because each has a distinct
authoring/lifecycle rule (the table above), and squeezing `Gem` behavior out of `Item` (say) would
recreate exactly the "wrong-but-plausible" failure this codebase's own `AtomDerivedSubsystem` doc
comment names as the one to never do.

## Success criteria

- The 111 previously-homeless seeds (70 `charm` + 41 `insert`, per `item-todo.md`'s own count) each
  construct a real container of the correct new kind.
- Modules 12, 13, 16, 18, 21's own acceptance criteria that were shipped with "container kind
  deferred" are re-run and their previously-inert clause now passes for real.
- Zero regression against this session's own recorded baseline suite counts.
