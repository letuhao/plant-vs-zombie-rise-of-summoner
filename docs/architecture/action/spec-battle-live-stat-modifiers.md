# Spec: battle-live-stat-modifiers (A18e)

Module **A18e** in the [action map](../action-map.md) §12.1. Depends on A18a
(`spec-action-container-binding.md`) and A18b (`spec-on-activate-trigger.md`). Lands **last** of the
five — highest-risk, most novel, per the map's own build-order note.

> **Read both dependency specs and `action-map.md` §12/§12.1 first.**

## Objective

Make `stat.modify` (FA1) actually affect ongoing combat when a bound grant fires — the one A18
sub-module that needs genuinely new machinery, not just new wiring, because of two things verified
against code that materially shrink and reshape its honest scope from how it was first framed.

**What "done" looks like:** a real skill's `stat.modify` atom, once its owning grant has fired at
least once, measurably changes the actor's own combat numbers (Atk feeding `calculator.Compute`,
or a primary channel already exposed) for the rest of the battle — composed correctly through the
SAME phased Flat→Increased→More→Override math the overlay's primary stat system already uses,
reused rather than reinvented.

### Two findings that reshape this module's scope — both verified against code

**1. `stat.modify` has no duration param.** Its schema (`AtomKindRegistry.cs:92-102`) is exactly
`{channel, op, amount}` — no `durationMs`, no expiry. *"Revertible"* cannot mean "wears off after N
seconds" without giving `stat.modify` a fourth param — a **separate** cross-program vocabulary
change from `OnActivate` (A18b), which this spec does not fold in unasked. **What this module builds
instead:** *sourced and revertible-on-grant-removal* — a triggered `stat.modify` contributes from its
first trigger fire onward, for the rest of the battle, and would revert if its owning grant were ever
withdrawn (the `OnRemoved` lifecycle Foundation already has). This is the honest reading of
"revertible" the schema actually supports today; a literal timed buff is named, explicit future work
(§ Open questions), not silently approximated.

**2. Two DIFFERENT phased composers already exist, and `stat.modify`'s target belongs to the one
battle doesn't currently use at all.** `stat.modify` validates its `channel` against
`PrimaryChannels = StatChannels.All` (the eight primary channels: `hp·maxHp·atk·defense·arm1·arm1Max·arm2·arm2Max`)
— the SAME channel space `Stats/StatComposer.cs`'s `PhasedComposeStrategy` composes
(Flat → Increased(sum) → More(product) → Override, `StatComposer.cs:12-35`). This is a **different**
system from `Stats/Derived/DerivedComposer.cs` (Flat/Increased/**Replace**/**Flag**, no More — that one
belongs to `stat.derived`, the OTHER stat kind, already wired into battle since E12). Battle's own
`BattleStatComposer.Compose` (`Battle/BattleStatComposer.cs:128-133`) reuses **neither** — it flat-sums
every mod source directly into `ActorDerivedSnapshot`, a third, simpler, battle-specific model that
has never needed to distinguish Flat/Increased/More because nothing has fed it that distinction
before now. **This module reuses `PhasedComposeStrategy.ComposeChannel` directly** — it is a pure,
stateless function over `IEnumerable<StatModifier>`, no Unity coupling, already tested — rather than
inventing a fourth composition model or forcing `stat.modify` through `DerivedComposer`'s wrong op
vocabulary.

**Atk is the one channel that doesn't live in `ActorDerivedSnapshot` at all.** `RunBasicAttackStep`
reads `attacker.Setup.Atk` directly (`BasicAttack.cs`, `BaseOverlayDamage = attacker.Setup.Atk`) — a
plain field on the setup record, never composed. Defense, by contrast, already lives in `Derived`
(`CombatDefenseOmni`, set once at spawn). So a live Atk buff and a live Defense buff need different
plumbing — covered in §2.

## Design

### 1. `BattleStatModifierLedger` — sourced, per actor, per channel

**Owns its own `PhasedComposeStrategy` instance, not a static singleton** — `Stats/StatComposer.cs`
exposes no `PhasedComposeStrategy.Instance`; `StatComposer`'s own constructor just does
`new PhasedComposeStrategy()` (`StatComposer.cs:41`, stateless, safe to share). The ledger owns one
instance and exposes a single `Recompose` entry point, so no caller (`ActorState.LiveAtk`, the
`Derived`-channel recompose in §2, `BattleEffectSink`'s own `ModifyStat` branch) needs to construct or
reference `PhasedComposeStrategy` directly:

```csharp
public sealed class BattleStatModifierLedger
{
    static readonly PhasedComposeStrategy Strategy = new();

    readonly Dictionary<(string ActorKey, string Channel), List<(string SourceGrantId, StatModifier Mod)>> _mods = new();

    public void Add(string actorKey, string channel, string sourceGrantId, StatModifier mod) { /* append */ }
    public void RemoveBySource(string actorKey, string sourceGrantId) { /* remove every (channel, mod) tuple this source added, across all channels */ }
    public IReadOnlyList<StatModifier> For(string actorKey, string channel) { /* the mods Recompose consumes */ }

    /// <summary>The one entry point every live-read call site uses — never `PhasedComposeStrategy`
    /// directly, so there is exactly one place this module's own recompose math lives.</summary>
    public long Recompose(string actorKey, string channel, long baseline) =>
        (long)Math.Round(Strategy.ComposeChannel(baseline, For(actorKey, channel)));
}
```

One instance per battle, on `BattleRunState`, alongside `Cooldowns`/`Shields` — same lifetime, same
"battle-local runtime state" category.

### 2. Two live-read paths, not one, because Atk and Defense are plumbed differently today

**Defense (and any future primary channel already routed through `Derived`):** targeted, in-place
recompose. `ActorDerivedSnapshot.Set` is `internal` (`ActorDerivedSnapshot.cs:55`) — reachable from
anywhere in `FusionRpg.Core`, no visibility change needed. When a `stat.modify` grant targeting
`defense` fires (or is removed):

```csharp
attacker.Derived.Set(DerivedStatChannels.CombatDefenseOmni,
    ledger.Recompose(attacker.Setup.Key, "defense", attacker.Setup.Defense));
```

Every existing reader of `Derived` (`calculator.Compute`'s `CombatActorSnapshot(attacker.Derived, ...)`)
sees the update on its next read automatically — `Derived` is already a live, mutable, shared-by-
reference object; this module does not change who reads it, only what a targeted `Set` call updates.

**Atk:** new, narrow live-read, since nothing about `Setup.Atk` is composed today. A small
`ActorState.LiveAtk(BattleStatModifierLedger ledger)` method:

```csharp
public long LiveAtk(BattleStatModifierLedger ledger) => ledger.Recompose(Setup.Key, "atk", Setup.Atk);
```

`RunBasicAttackStep`'s one call site changes from `attacker.Setup.Atk` to
`attacker.LiveAtk(state.Ledger)` — the **only** production read this module touches. Byte-identical
when the ledger holds no `atk` mods for that actor (every battle today, since nothing binds a
`stat.modify` grant yet) — `ComposeChannel` over an empty mod list returns the baseline unchanged
(`StatComposer.cs:24-33`: empty `flat`/`increased`/`more` sums are `0`/`0`/no multiply, so
`afterMore == baseline` exactly).

`hp`/`maxHp`/`arm1`/`arm1Max`/`arm2`/`arm2Max` have **no live consumer in battle at all** (no combat
math reads them today — `arm1`/`arm2` are PvZ-overlay-only channels, `hp`/`maxHp` are battle's own
`ActorState.Hp`/`MaxHp`, mutated through the funnel, never through this stat system). A `stat.modify`
atom targeting one of these validates and binds (A18a) but has **no observable effect in battle** —
named, not silently absorbed: matches this module's own "channel has no reader" refusal-shape
precedent from `AtomKindRegistry.Validate`'s G6 fix, except here the channel is real (validates fine
against `PrimaryChannels`) and simply has nothing in battle listening yet.

### 3. Reaching the ledger from `BattleEffectSink` — reusing A18d's wiring, not a third pattern

This module needs the SAME kind of access A18d already needed for `Status`/`StatusRng`: a reference
`BattleEffectSink` cannot have at construction time, wired in after `BattleRunState` builds it. **Not
a new pattern** — one more settable property on `BattleEffectSink`, forwarded through
`BattleEffectHost`, exactly like A18d's own `Status`/`StatusRng`:

```csharp
// BattleEffectSink: public BattleStatModifierLedger? Ledger { get; set; }
// BattleEffectHost:  public BattleStatModifierLedger? Ledger { set => _sink.Ledger = value; }
// BattleRunState ctor, after Ledger is constructed: Host.Ledger = Ledger;
```

Since this module lands **after** A18c and A18d (the map's own build order), `BattleEffectSink`
already has the `Status`/`StatusRng` forwarding shape A18d established by the time this module's own
`Ledger` property is added — the same file, the same shape, one more property, not a redesign.

**A second, real gap this section's own snippet glossed over:** `owner.Derived`/`owner.BaselineDefense`
(§4) need MORE than `Ledger` alone — they need a way to resolve an owner key to something exposing
`Derived`/`Defense`, and `ActorState` (the obvious answer) is **private** to `BattleEngine`, a
different, unrelated top-level class from `BattleEffects.cs`. This is the exact reason
`IBattleHpTarget` exists at all — found while implementing, not read from the code first. Fixed the
same way: a new, narrow `IBattleStatTarget` interface (`Derived`, `BaselineDefense`), `ActorState`
implements it via already-public members (no visibility widening on `ActorState` itself), and a
SEPARATE `Func<string, IBattleStatTarget?> ResolveStatTarget` is forwarded the same way `Ledger` is —
`resolveActor` (the existing ctor parameter) stays `IBattleHpTarget`-only, untouched.

### 4. Firing and removal

`BattleEffectSink.Execute` gains a `ModifyStat` branch: reads `channel`/`op`/`amount` from
`item.Params`, resolves `op` to a `ModifierOp` (`Flat`/`Increased`/`More` — `Override` is refused at
bind time per the atom kind's own doc: *"effects cannot emit Override"*). The owner actor comes from
**`ctx.Grant.OwnerKey`** (`EffectExecuteContext.Grant`, `EffectModels.cs:126-131`, is the bound
runtime `EffectGrant` — not the `EffectGrantDto` A18a's own binding step constructs; `Execute` only
ever sees the bound form) — `entity:{actorKey}` per `EffectOwnerKeys.Entity`, so the actor key is the
substring after the `entity:` prefix, the same strip every other owner-key consumer in this file
already does (`BattleRunState.DrainShieldEvents`'s own `rec.OwnerKey.StartsWith("entity:", ...)`
handling, reused rather than re-derived):

```csharp
if (Ledger is null) return true;   // not wired — same "quietly refuse" posture as A18d's Status/StatusRng guard
var ownerKey = ctx.Grant.OwnerKey.StartsWith("entity:", StringComparison.Ordinal)
    ? ctx.Grant.OwnerKey["entity:".Length..] : ctx.Grant.OwnerKey;
if (!_resolveState(ownerKey, out var owner)) return true;   // no live actor under this key (e.g. already dead)

Ledger.Add(ownerKey, channel, item.GrantId, new StatModifier(channel, op, amount, priority: 0, sourceId: item.GrantId));
if (string.Equals(channel, "defense", StringComparison.Ordinal))
    owner.Derived.Set(DerivedStatChannels.CombatDefenseOmni, Ledger.Recompose(ownerKey, channel, owner.Setup.Defense));
// "atk" needs no push here — LiveAtk recomposes on read, every time (§2)
```

`stat.modify`'s schema carries no `target` param, so it can only ever buff its own grant's owner,
matching a "permanent modifier buffs its own holder" model already implicit in the kind's existing
"permanent, no-trigger" case. `_resolveState` is a new, narrow lookup `BattleEffectSink` needs
(`Func<string, ActorState?>`, forwarded the same way `Ledger` is) — `resolveActor`
(`Func<string, IBattleHpTarget?>`) is insufficient here, since `owner.Derived`/`owner.Setup` are not
on `IBattleHpTarget`.

**Widening `stat.modify`'s allowed triggers is this module's own call, not A18b's.** Today it declares
`AtomTriggers.None` at its trigger-parameter position (`AtomKindRegistry.cs:98`, "permanent modifier:
declares no trigger"). This module changes that one line to the same local `AllTriggers` array A18b
already extended with `OnActivate` — a real, separate widening (a kind gaining trigger eligibility it
never had, distinct from A18b's *new trigger existing at all*) — flagged for the same review posture
as A18b's own change, even though no new trigger name is introduced.

**A naive widen breaks the permanent-modifier case entirely — found by running the existing test
suite, not by reading the code first.** `AtomRowValidator.ValidateWhen` infers "trigger REQUIRED" from
`kind.Triggers.Count > 0` (its own "mirror case": *"a kind that fires on events must say which"*) —
correct for every OTHER kind, where `Triggers` has only ever meant EITHER "no trigger allowed, none
required" (`Count == 0`) OR "some allowed, one required" (`Count > 0`). `stat.modify` is the first kind
that needs a THIRD shape: triggers allowed, but still not required, since the existing "permanent,
no-trigger" case (`definitions.md` §14.2) must keep validating. Caught by
`ChannelExtensionTests.The_three_new_channels_pass_atom_validation` failing with `"stat.modify requires
a trigger"` the moment `Triggers` went from empty to `AllTriggers`. Fixed with a new `AtomKind.TriggerOptional`
field (default `false`, so every other kind's existing inference is completely unchanged) —
`stat.modify`'s own entry sets it `true`, and `ValidateWhen`'s check becomes
`kind.Triggers.Count > 0 && !kind.TriggerOptional`.

**Removal is built, proven only synthetically.** `ledger.RemoveBySource` exists and is unit-tested
directly, and `EffectBag`'s own `OnRemoved` lifecycle would call it if anything in this reopening's
scope ever withdrew a grant mid-battle — **nothing does.** A18a binds once at loadout-compile and
nothing built by A17–A20 ever un-binds. Named honestly: this module proves the mechanism works, not
that any real battle flow exercises it yet — the same "built correct, not yet triggered by production
content" shape A18a's own binding already has relative to A20.

## Tunables

None new. No balance number authored.

## Numeric types

`StatModifier.Value` stays `double` (matches the existing `PhasedComposeStrategy`/`StatModifier`
shape); the final composed result rounds to `long` at the read site (`LiveAtk`, the `Derived.Set` call)
— `Setup.Atk`/`Setup.Defense` are already `long` per `CLAUDE.md`'s magnitude rule, and this module
introduces no new arithmetic path that multiplies before widening (`ComposeChannel`'s own math is
`double` throughout, rounded once at the boundary, matching how `StatComposer.Compose` already rounds
`Chan(...)`/`ChanHp(...)`).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BattleLiveStatModifiers"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1
```

## Project structure

```
src/FusionRpg.Core/Battle/BattleStatModifierLedger.cs   (new)
src/FusionRpg.Core/Battle/BattleEngine.cs                (ActorState.LiveAtk; RunBasicAttackStep's one read-site change)
src/FusionRpg.Core/Battle/BattleEffects.cs               (BattleEffectSink gains Ledger + an ActorState resolver,
                                                            both forwarded via BattleEffectHost, same shape A18d
                                                            established for Status/StatusRng; the ModifyStat branch)
src/FusionRpg.Core/Battle/BattleRunState.cs              (Host.Ledger = Ledger; after Ledger is constructed)
src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs     (stat.modify: AtomTriggers.None -> AllTriggers)
tests/FusionRpg.Core.Tests/Battle/Adoption/BattleLiveStatModifiersTests.cs
```

## Testing strategy

- **Flat/Increased/More compose correctly, matching `PhasedComposeStrategy`'s own contract** — three
  `stat.modify` grants on one actor's `atk` channel (one each op), asserting the exact
  `(baseline + flat) × (1 + increased) × (1 + more)` result — proven against the same formula
  `StatComposer.cs` already tests, not a parallel implementation that could silently drift.
- **`LiveAtk` is byte-identical to `Setup.Atk` with an empty ledger** — every existing golden/trait
  test's implicit assertion; explicit unit coverage too.
- **A fired `stat.modify` persists for the rest of the battle, across multiple rounds** — not
  re-applied, not decaying; proven by asserting the SAME `Derived`/`LiveAtk` value across N rounds
  after one trigger fire.
- **`RemoveBySource` reverts exactly its own contribution** — two sources on the same channel; removing
  one leaves the other's contribution intact, proven directly against the ledger (synthetic — no
  production call site exists yet, named in Design §3).
- **`Override` is refused at bind time, not silently accepted** — an atom authoring
  `op: "override"` on `stat.modify` fails `ActionValidator`/atom bind validation, matching the kind's
  own documented restriction.
- **Golden-neutral by construction, proven not assumed** — no content shipping today binds a
  `stat.modify` grant; full suite green, zero hashes moved, including on multi-round Stomp/Close/Wipe
  fixtures where a live-ledger bug would most likely surface as drift.

## Boundaries

- **Always:** compose through `BattleStatModifierLedger.Recompose` (which itself wraps
  `PhasedComposeStrategy.ComposeChannel` — never called directly by any other site) — never a parallel
  percent-math implementation for battle specifically.
- **Ask first:** giving `stat.modify` a `durationMs` param (a real timed buff) — a genuine schema
  change, separate from this module's own scope; giving it a `target` param (buffing someone other
  than the grant's own owner) — same category of change.
- **Never:** route a `stat.modify` grant through `DerivedComposer`/`DerivedModifierOp` — wrong op
  vocabulary for this kind, that composer belongs to `stat.derived`.

## Success criteria

1. A real `stat.modify` atom, bound and fired, changes `LiveAtk` (or `Derived`'s defense channel) by
   exactly what `PhasedComposeStrategy.ComposeChannel` would compute for its op and amount.
2. The change persists for the rest of the battle without re-triggering, and reverts correctly if its
   source is removed (proven directly against the ledger).
3. `hp`/`maxHp`/`arm1*`/`arm2*` targets validate and bind but are named as having no battle-side
   reader yet — not silently inert.
4. Zero goldens moved.

## Propagation once approved

Per Design Gate evidence rule 6: `stat.modify`'s `AtomKindRegistry.cs:97` cell
(`RuntimeSupportMatrix(Full, None, PlanOnly)`) flips Battle `None → Full`; the FA10-only comment on
`BattleEffectSink.Execute` (already stale once A18d lands) gets its final update here, naming all
three now-supported actions (`ApplyResourceDelta`, `ApplyStatus`, `ModifyStat`) rather than the
single opcode it originally documented.

## Open questions

- A genuine timed `stat.modify` (schema needs `durationMs`) and a targeted `stat.modify` (schema needs
  `target`) are both real, named follow-ups this module deliberately does not fold in — either would be
  its own small cross-program schema review, not a silent scope creep here.
- Whether `hp`/`maxHp`/`arm1*`/`arm2*` should ever gain a battle-side reader, and what reading them
  would even mean given `hp`/`maxHp` are already funnel-mutated by a completely different path — a
  question for whichever future module first has content that needs it.
