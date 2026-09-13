# Prove: ActorHub / battle-combat SSOT (post-fuse)

Operator path: confirm `actor-hub-and-combat-power-solid-fixing`'s core claim holds — one ActorHub
compose gate, no reintroduced dual engine — without a live game.

Two scripts, both Core/Server-only console tools over `RpgStore.InMemory()`:

## `scripts/prove-aptitude.ps1` — aptitude resolve, overlay vs battle

Proves `AptitudeResolver.Resolve` composes identically whether consumed by the overlay
(`DerivedComposer`) or by battle (`BattleHubCompose`, post `battle-hub-fuse` T6 — was
`BattleStatComposer` pre-fuse).

```powershell
.\scripts\prove-aptitude.ps1                              # Might -> combat.power.omni (scoped)
.\scripts\prove-aptitude.ps1 -Channels ""                  # every channel the allocation touches
```

Exit 0 on zero delta across compared channels; exit 1 (and a printed per-channel diff) otherwise.

## `scripts/prove-hub-combat.ps1` — battle Hub vs sheet Hub, plus Standing honesty

Two independent proves in one run (`tools/ProveHubCombat`):

1. **Battle Hub ≡ sheet Hub for the SAME equip + tree bound atoms**, for one fixture `UniqueActor`.
   Both sides read the real, shipped compose gates (`UniqueActorHubCompose.Build` for sheet,
   `BattleHubCompose.Compose` for battle) fed the identical `EquippedBoundAtoms.DerivedFromStore` +
   `TreeBoundAtoms.ForPlayer` atom list. Scoped to the channels those atoms actually write, never a
   full-snapshot diff: sheet and battle deliberately register different subsystem sets (battle alone
   seeds a contest-shaped accuracy/crit/dodge baseline from Atk/Defense and a resource-pool baseline
   the sheet never computes — a real, accepted structural difference, not a defect). Aptitude's own
   battle-vs-overlay parity is `prove-aptitude.ps1`'s job, not re-proven here.
2. **Standing rises via a real Hub combat writer, never via Θ alone** — grants a shipped
   `skill.cooldown`/`skill.effectiveness` aptitude edge (a genuine non-atom writer with no equip/tree
   analog) and asserts some Standing axis moves; a separate fixture proves a real, nonzero specimen
   level with zero grants leaves Standing at exactly zero on every axis.

```powershell
.\scripts\prove-hub-combat.ps1
.\scripts\prove-hub-combat.ps1 -OutJson "path\to\result.json"
```

Exit 0 when both prove bullets hold; exit 1 (with the JSON result printed, showing which bullet and
which channel/axis failed) otherwise. Result also written to
`docs/research/actor-hub-and-combat-power/_prove-hub-combat.json` by default.

**Not covered — deliberately, not an oversight.** Bullet 3 of the program's own `prove-hub-combat`
spec ("Bound lawn aptitude input matches Server UniqueCreature compose") is not in this script: the
Injector has zero UniqueCreature aptitude fetch/cache to compare against yet (`aptitude-sheet`
program's own `unique-lawn-wire`, task AS-1.1, unbuilt) — there is nothing to prove until that lands.
See `tasks/actor-hub-and-combat-power-solid-fixing-evidence-map.md` T12/T19.

## When to run these

- After any change to `EquippedBoundAtoms`, `TreeBoundAtoms`, `AptitudeResolver`, `BattleHubCompose`,
  or `UniqueActorHubCompose`.
- Before closing a wave in this program's `/solid-run` (owner pre-merge check; no CI job wired yet —
  optional future work, not required for this program to close).
