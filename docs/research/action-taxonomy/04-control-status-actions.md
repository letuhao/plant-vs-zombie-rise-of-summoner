# Control, status and debuff actions — how shipped games taxonomise and bound them

Research note. Observation of other games, not a product decision for this project. Nothing here is a
proposal.

Companion reading, already written and **not repeated here**:
[docs/research/arpg-effects/04-ailments-status.md](../arpg-effects/04-ailments-status.md) covers
Diablo-like ARPG ailments (Last Epoch, Diablo IV, Path of Exile, Grim Dawn), the buff/aura/debuff
lifetime split, hit-vs-ailment scaling, and five apply/refresh policies. This note deliberately leans
to MMO, MOBA, tactics and JRPG sources instead, and only touches ARPGs where a number is needed that
the older note does not carry.

Method note: `docs/research/game-design/06-unsourced.md` was read first. Nothing in it is re-searched
here. Two of its access notes proved reusable and are used below — `r.jina.ai` as a reader proxy, and
the preference for wiki.gg over Fandom.

---

## The finding in one paragraph

Every game surveyed keeps its control vocabulary far smaller than its status vocabulary, and the two
are governed by different machinery. Control words are a **closed, short list** — League of Legends
names 19, World of Warcraft names about 13, Guild Wars 2 names about 9 hard-control effects, Diablo II
names 10 curses — because each one must be individually taught, individually countered, and
individually bounded. Status words are **open and large**: Final Fantasy XIV's shipped status table has
4,220 rows. The bound on control is almost never a resist roll alone; it is a **second, orthogonal
clock** — diminishing returns in WoW and Diablo IV, a consumable stack in Guild Wars 2's Stability, a
depletable bar in the defiance bar, a hard duration floor in League's tenacity, an application ceiling
in Genshin's ICD. And nobody publishes a conversion from control to damage. The one exception found is
Guild Wars 2's defiance bar, where **one second of hard control is worth exactly 100 defiance damage**
— a published, unit-bearing price for control. Everything else prices control in *seconds of control*
and stops there. On enabler/payoff, the shipped guarantee is almost always structural rather than
statistical: Darkest Dungeon's Mark is unresistable, Final Fantasy XIV's raid buffs are aligned by
cooldown arithmetic rather than by chance, and Genshin — which does *not* structurally guarantee its
enablers — has a known dead pairing (Frozen with no blunt damage source in the party) as the direct
consequence.

---

## 1. The status vocabulary, game by game

### 1.1 Sizes at a glance

Self-tallied counts are marked **(computed)** — they are my count over the listed source, not a figure
the source states.

| Game | Control words | Status/ailment words | Total status table | Notes |
|---|---|---|---|---|
| League of Legends | **19** (computed: 7 hard + 12 soft) | — (no separate DoT/debuff family) | n/a | Control *is* the taxonomy |
| World of Warcraft | **13** (computed: 9 hard + 4 soft) | unbounded (spell auras) | unlimited since 3.0.2 | 6–8 DR categories depending on source |
| Guild Wars 2 | **~9 hard control**, ~7 soft | **12 boons + 14 conditions** | closed and small on purpose | The cleanest 2×N in the survey |
| Path of Exile | (chill/freeze double as control) | **9 ailments** (3 damaging, 6 not) | separate curse/exposure families | |
| Diablo II | — | **10 Necromancer curses** | one curse per monster | |
| Diablo IV | **11–12** CC effects (sources differ) | Burn/Bleed/Poison + Vulnerable | | |
| Final Fantasy XIV | ~5 usable in PvE, mostly boss-immune | — | **4,220 Status rows** | The largest table found |
| Pokémon | — | **6 non-volatile**, 40+ volatile | 21 real move-ailments in the API (computed) | Major/minor split is explicit |
| SMT V | — | **8 status changes** | one per category | Strict overwrite |
| Darkest Dungeon | Stun, Move | Bleed, Blight, Debuff, Mark, Horror, Disease, Stress | 7 resistance stats | |
| Slay the Spire | Entangled, No Draw, Confusion | **26 debuffs** (computed: 18 intensity, 6 duration, 2 neither) | | |
| Genshin Impact | Frozen, Petrification | 7 transformative + 2 amplifying + 3 additive reactions | element auras, not statuses | |
| **This project** | (`cc` category) | **21 declared statuses** | 8 `StatusKind` values | For scale comparison only |

The project's 21 sits between Guild Wars 2's 26 (12 boons + 14 conditions) and Slay the Spire's 26
debuffs. It is not an unusual size. FACT, from
[`StatusCatalogBootstrap.cs`](../../../src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs) — the file's
own summary comment says "Register all 21 locked status ids".

### 1.2 League of Legends — control is the entire taxonomy

League has no DoT family, no stat-debuff family and no buff family in its formal vocabulary. It has
**crowd control**, and the taxonomy is a list of 19 named types split hard/soft.

FACT, from the League wiki's Crowd control page (second-tier: wiki, but it is the game's canonical
reference and the terms are the ones patch notes use) —
<https://wiki.leagueoflegends.com/en-us/Crowd_control>:

| Hard CC (7) | Blocks |
|---|---|
| Airborne | movement, attacks, abilities, item actives; interrupts channels |
| Forced Action (Berserk, Charm, Flee, Taunt) | movement control, abilities, item actives; interrupts channels |
| Root | movement and mobility spells; partially blocks abilities |
| Sleep | everything; interrupts channels |
| Stasis | everything **including summoner spells** |
| Stun / Suspension | movement, attacks, abilities, item actives |
| Suppression | everything **including summoner spells** |

| Soft CC (12) | Blocks |
|---|---|
| Blind | basic attacks miss |
| Cripple | attack speed down |
| Disarm | basic attacks only |
| Disrupt | interrupts channels/charges |
| Drowsy | slows, then becomes Sleep |
| Ground | mobility spell activation |
| Knockdown | interrupts dashes/displacements |
| Kinematics | drags the unit |
| Nearsight | sight radius down |
| Polymorph | attacks and abilities |
| Silence | ability casts and item actives |
| Slow | movement speed down |

The hard/soft line here is unusually crisp and worth stating precisely: **hard CC removes agency, soft
CC removes an option.** Polymorph blocks attacking *and* abilities and is still classed soft, because
the player keeps movement — so the line is not "how much does it block" but "can you still walk."
INFERENCE, from the table above.

Note the design tell: Blind and Nearsight are CC in League, not stat debuffs. A game whose only
vocabulary is control classifies everything as control.

### 1.3 World of Warcraft

FACT, <https://warcraft.wiki.gg/wiki/Crowd_control>:

- Hard CC (loss of control): **Charm, Fear, Stun, Incapacitate, Sleep, Disorient, Polymorph, Banish,
  Horror** — 9 (computed).
- Soft CC (positional): **Snare, Root, Daze, Grip** — 4 (computed).
- Incapacitate is defined as "a stun which breaks on damage to the target." That is the whole
  difference between two categories.
- Raid bosses are immune to all forms.

The DR *categories* are a different and shorter list than the CC *words*, which is the important
structural point: **the vocabulary a player learns and the buckets the engine bounds are not the same
list.** More in §3.

### 1.4 Guild Wars 2 — the clean 2×N

The cleanest design in the survey. Two closed lists, symmetric, with the stacking rule declared per
entry rather than per system.

FACT, <https://wiki.guildwars2.com/wiki/Boon> (via reader proxy; direct fetch 403):

| Boon (12) | Effect | Stacks by | Cap |
|---|---|---|---|
| Aegis | block the next incoming attack | duration | indefinite |
| Alacrity | skills recharge faster | duration | 30s |
| Fury | crit chance up | duration | 30s |
| **Might** | outgoing damage up | **intensity** | **25** |
| Protection | incoming damage −33% | duration | 30s |
| Quickness | skills and actions faster | duration | 30s |
| Regeneration | health per second | duration | indefinite |
| Resistance | non-damaging conditions become ineffective | duration | 30s |
| Resolution | incoming condition damage −33% | duration | 30s |
| **Stability** | prevents control effects | **intensity** | **25** |
| Swiftness | movement +33% | duration | 60s |
| Vigor | endurance regen +50% | duration | 30s |

FACT, <https://wiki.guildwars2.com/wiki/Condition> (via reader proxy):

| Condition (14) | Effect | Stacks by | Cap |
|---|---|---|---|
| Bleeding | damage per second | **intensity** | 1,500 |
| Burning | damage per second | **intensity** | 1,500 |
| Confusion | damage on skill activation | **intensity** | 1,500 |
| Poisoned | damage + healing reduction | **intensity** | 1,500 |
| Torment | damage per second, more when the target is still | **intensity** | 1,500 |
| Blinded | next outgoing attack misses | duration | 10s |
| Chilled | movement −66%, cooldowns +66% | duration | 10s |
| Crippled | movement −50% | duration | 10s |
| Fear | involuntary retreat, unable to act | duration | 10s |
| Immobilized | unable to move | duration | 10s |
| Slow | skills and actions slower | duration | 10s |
| Taunt | involuntarily attack foes | duration | 10s |
| Weakness | endurance loss + glancing blows | duration | 10s |
| **Vulnerability** | incoming damage and condition damage up | **intensity** | **25** |

The split is not arbitrary. **Every damaging condition stacks in intensity; every control condition
stacks in duration; Vulnerability, the one pure enabler, stacks in intensity with a small cap.**
(computed from the table above.) The rule is legible enough that a player can predict the stacking
model of a new condition from what it does — which is the property most other games lack.

### 1.5 Path of Exile — ailments split by whether damage sets the magnitude

FACT (already covered in arpg-effects/04; the split is restated only because §2 needs it):
9 ailments, split 3 damaging / 6 non-damaging.

- **Damaging** (magnitude set by hit damage): Ignite, Bleeding, Poison.
- **Non-damaging** (magnitude set separately): Chill, Freeze, Shock, Scorch, Brittle, Sap.

Source: the ailment definition as reproduced in search results for <https://www.poewiki.net/wiki/Ailment>
— *"Bleeding, poison and ignite are damaging ailments"*, *"Chill, freeze, shock, scorch, brittle, and
sap are collectively referred to as non-damaging ailments"*, and *"There is no limit on the number of
different ailments a target can have at any given time."* The poewiki pages themselves were
unreachable this pass (see §8).

The design point PoE makes and nobody else states as plainly: **whether a status's magnitude is a
function of the hit that applied it is the primary axis of the taxonomy**, more fundamental than
hard/soft or buff/debuff.

### 1.6 Diablo II — one curse per monster

FACT, Blizzard's own Arreat Summit,
<http://classic.battle.net/diablo2exp/skills/necromancer-curses.shtml>: 10 curses — Amplify Damage,
Dim Vision, Weaken, Iron Maiden, Terror, Confuse, Life Tap, Attract, Decrepify, Lower Resist — and
**"Generally only one Curse can affect a monster at a time"**; a second curse replaces the first.

This is a whole design in one sentence. It makes each curse an exclusive *choice* rather than an
additive layer, which is why a 10-word list stays legible and why curse stacking never became a
balance problem. It also means a curse's opportunity cost is another curse, not mana.

### 1.7 Diablo IV

FACT, <https://www.icy-veins.com/d4/guides/crowd-control-status-effects/> (via reader proxy): 12 CC
effects — Slow, Immobilize, Stun, Knockback, Knockdown, Taunt, Fear, Pull In, Tether, Daze, Chill,
Freeze. A second source lists 11 (no Pull In). Recorded as a live conflict, not resolved.

### 1.8 Final Fantasy XIV — the biggest table, the smallest usable control set

FACT, XIVAPI: `https://xivapi.com/Status?limit=1` reports **ResultsTotal 4,220**. That is the shipped
Status sheet, and it includes encounter-specific, NPC and unused rows — it is not 4,220 things a
player tracks.

FACT: enemies in high-end content are immune to essentially all of it. Above level 50, Duty Finder
enemies are immune to Sleep; raid bosses are immune to the rest. Player-facing CC in PvE has been
reduced to near-zero, and control instead appears as **encounter-authored statuses applied to players**
(the "Temporary Misdirection" family — statuses that restrict which direction you may move).

INFERENCE: FFXIV is the clearest case of a game whose status system is a **content-authoring surface**
rather than a player-facing combat vocabulary. 4,220 rows is not a design failure at that scale,
because no single fight uses more than a handful, and the player never learns the list.

### 1.9 Pokémon — the major/minor split, stated explicitly

The pattern the brief asks about is real and is in the game's own terminology.

FACT, <https://bulbapedia.bulbagarden.net/wiki/Status_condition> (via reader proxy; second-tier wiki):

**Non-volatile (major) — 6:** Burn, Freeze, Frostbite (Gen IX), Paralysis, Poison, Sleep. Badly
Poisoned is a variant of Poison.

Rules that make it a *major* class:
- **Only one non-volatile status at a time.** A burned Pokémon cannot also be paralysed.
- They **persist through switching out** and after the battle ends.
- They have a dedicated cure-item and cure-move economy.

**Volatile (minor) — 40+:** Confusion, Flinch, Leech Seed, Bound, Infatuation, Taunt, Encore, Curse,
Nightmare, Disable, and many more.

Rules:
- **Many simultaneously**, no exclusivity.
- **Cleared on switch-out** and at end of battle.
- Not even displayed with icons before Generation VII.

Numbers, current generations: Burn 1/16 max HP per turn plus halved physical damage; Poison 1/8;
Badly Poisoned escalating 1/16, 2/16, 3/16…; Frostbite 1/12 plus halved special damage; Paralysis
Speed to 50% with a 25% chance to lose the turn; Freeze 20% thaw chance per turn; Sleep 2–4 turns.

FACT, PokéAPI `https://pokeapi.co/api/v2/move-ailment/?limit=100`: **23 move-ailment rows**, of which
`unknown` and `none` are sentinels — **21 real ailments** (computed). The 21 are paralysis, sleep,
freeze, burn, poison, confusion, infatuation, trap, nightmare, torment, disable, yawn, heal-block,
no-type-immunity, leech-seed, embargo, perish-song, ingrain, silence, tar-shot, protect. This is a
machine-readable, first-party-derived enum of exactly the size this project ships.

The design lesson is the **exclusivity gradient**, not the count. Six statuses carry the weight of
being permanent, exclusive and cure-gated; forty carry no weight at all and are free to be numerous
because they are cheap, temporary and self-clearing.

### 1.10 SMT V — one slot per category, newest wins

FACT, <https://megamitensei.fandom.com/wiki/Status_Changes_in_Shin_Megami_Tensei_V> (via reader proxy;
Fandom is HTTP 402 on direct fetch): 8 status changes — Sleep, Mirage, Poison, Confusion, Charm, Seal,
Mud, Shroud — plus Death.

The governing rule, quoted: **"Only 1 condition of each category can be maintained at a time, such as
with ailments, shields and charge effects: the newest condition will always override the old."**

A second-tier source (Game8, <https://game8.co/games/Shin-Megami-Tensei-V/archives/350035>) reports a
strength ordering weakest→strongest as Sleep, Mirage, Poison, Confusion, Charm, Seal. **These two
claims conflict** — "newest always wins" and "there is a strength order" cannot both be the full rule.
Unresolved; see §8.

### 1.11 Darkest Dungeon

FACT, <https://darkestdungeon.wiki.gg/wiki/Status_effects>: Bleed and Blight are 3-turn DoTs (5 turns
from a critical) that **ignore protection**; Stun skips the whole turn; Debuff reduces accuracy, damage
or dodge; Mark enables bonus damage from mark-consuming skills; Move/Shuffle repositions; Horror is
stress-over-time.

Two mechanics matter more than the list:

1. **Stun grants 50% stun resistance after it wears off, stacking to 100%.** A per-target,
   self-imposed diminishing return with no window bookkeeping.
2. **"No character or monster can resist [Mark]."** The one status that exists purely to enable other
   skills is the one status with no resist roll. See §6.

The resist formula is additive and unusually blunt. FACT via community reproduction (second-tier —
Steam discussion, <https://steamcommunity.com/app/262060/discussions/0/3191364450220862319/>):

```
final chance = skill chance% − target resistance%
# additive; can go below 0.
# a 130% stun skill vs 40% stun resistance = 90%.
# to beat 100% resistance you need >100% chance.
```

Resistance stats are per-family: Stun, Blight, Bleed, Disease, Debuff, Move, Trap — 7 (computed).

### 1.12 Slay the Spire — the stacking model is printed on the debuff

FACT, <https://slaythespire.wiki.gg/wiki/Debuffs>: **26 debuffs** — 18 Intensity, 6 Duration, 2 neither
(computed from the source's own per-row type labels).

- **Duration:** Vulnerable (+50% attack damage taken), Weak (−25% attack damage), Frail (−25% block
  from cards), Lock-On, No Block, Draw Reduction.
- **Intensity:** Strength Down, Shackled, Dexterity Down, Focus−, Choked, Corpse Explosion, Wraith
  Form, Bias, Block Return, Fasting, Mark, Constricted, Hex, Slow, and the negative stat forms.
- **Neither:** Confusion, No Draw, Entangled — flags, not counters.
- Poison is explicitly **both**: the stack count is the damage *and* it decays by 1 per turn.

The game teaches the stacking model in the keyword itself. A player who reads "Weak 3" knows it means
three turns, not triple the effect, because Weak is a Duration debuff and Duration debuffs always mean
turns. FACT: *"2 Weak will last 2 turns, and 5 Weak will last 5 turns, but in both cases they will deal
25% less damage."*

Ordering is also published: *"Strength is applied before multiplicative effects like Vulnerable"* — 2
Strength + Strike into Vulnerable is (6+2)×1.5 = 12, not 6×1.5+2 = 11.

### 1.13 Genshin Impact — elements, not statuses

Genshin has almost no status list. It has **elemental auras** with a gauge, and a reaction matrix. See
§6, which is where it belongs.

---

## 2. Where each game draws the hard-CC / soft-CC / DoT / stat-debuff line

There is no shared vocabulary, but there are only four lines actually in use, and each game picks one
as primary.

| Line drawn on | Games that use it as primary | What falls out |
|---|---|---|
| **Can the target still act at all** | WoW, League, Diablo IV | Hard vs soft CC. Polymorph is soft in League (you can walk), hard in WoW (you cannot act). |
| **Does the status deal damage** | Guild Wars 2, Path of Exile | Damaging conditions/ailments stack in intensity; non-damaging ones stack in duration. |
| **Is the magnitude a function of the applying hit** | Path of Exile | Ignite/Bleed/Poison scale off hit damage; Chill/Shock/Brittle do not. |
| **How long does it survive** | Pokémon | Non-volatile persists through switching; volatile does not. |

Mechanically, the four families differ in exactly these ways across every game surveyed:

| Family | Bounded by | Stacks how | Broken by | Counterplay |
|---|---|---|---|---|
| Hard CC | a second clock (DR, bar, stack, floor) | never stacks; longest or newest wins | damage (sometimes), stun-break, immunity | pre-emptive: immunity, positioning |
| Soft CC | usually only by its own cap | duration extends or refreshes | rarely anything | reactive: cleanse, out-heal, walk it off |
| DoT | a stack cap, or nothing | intensity, usually per-source | cleanse, out-heal | reactive |
| Stat debuff | a cap on the stat | intensity with a small cap (GW2 Vulnerability 25) | cleanse, expiry | reactive |

INFERENCE, from the table: **the only family that gets a dedicated second bounding system is hard CC.**
Nobody has built a diminishing-returns system for damage-over-time. The reason is that hard CC removes
the player's ability to respond, so the failure mode is "I did not get to play"; every other family's
failure mode is "I lost some numbers," and a resist stat plus a cleanse handles that.

---

## 3. Diminishing returns, immunity and how control gets bounded

Five distinct mechanisms are in shipped use. They are not variations on one idea.

### 3.1 Halving DR with a rolling window — World of Warcraft

The canonical system, and the numbers depend on which side of "reduced by" you read. Both sources are
correct and they say the same thing:

FACT, <https://warcraft.wiki.gg/wiki/Diminishing_returns> — *"the first effect has full PvP duration.
If the same category of effect… is used on that target within 18 seconds, that effect's duration is
reduced by 50%. On the third use, the duration is reduced by 75%."*

FACT, <https://maxroll.gg/wow/resources/crowd-control-diminishing-returns> (patch 12.0.7, second-tier
but current) — applications run 100% → 50% → 25% → 12.5% → 6.25%, resetting **16 seconds after the last
application ends**.

```
# same rule, two phrasings
duration_n = base * 0.5^(n-1)        # remaining, maxroll
reduction_n = 1 - 0.5^(n-1)          # reduced-by, wiki
```

Category-specific exceptions, all FACT from those two pages:

| Category | Rule |
|---|---|
| Stuns, Roots, Incapacitates, Disorients, Silences, Disarms | standard halving |
| Knockbacks / displacements | **immune immediately after the first**, 10s reset (not 18) |
| Taunts | **5 applications before immunity**, roughly 3 → 2 → 1.4 → 0.9 → 0.6 seconds |
| **PvE** | halving **continues down to 1/16 duration with no immunity** |
| PvP (current) | **full immunity after 2 applications** |

The PvE/PvP split is the single most transferable fact in this section for a game with no PvP: **WoW
does not give PvE targets immunity at all.** It keeps halving. Control against a monster gets
asymptotically worthless rather than hitting a wall.

Category count is a live conflict: the DR page lists 8 (roots, stuns, incapacitates, disorients,
silences, knockbacks, disarms, taunts); the CC page says post-Warlords *"only six DR categories remain:
Roots, Stuns, Incapacitates, Disorients, Silences, and AoE Knockbacks"*; maxroll's 12.0.7 page lists 6
standard plus displacements and taunts as separate special cases. INFERENCE: 6 standard + 2 special is
probably the reconciliation, but no source states it that way.

Notice what this costs the designer: **a category assignment for every control ability in the game, and
a per-target, per-category timer.** That is the real price of DR, and it is why the DR bucket list (6)
is shorter than the CC word list (13).

### 3.2 Accumulating resistance with decay — Diablo IV

FACT, <https://www.vhpg.com/diablo-4-crowd-control/> (second-tier, but it quotes the mechanic in
numbers no other source gave):

```
# monster hard-CC resistance
resistance += 10% per second of hard CC suffered      # cap 95%
resistance -= 5% per second while not under hard CC   # floor 0%

# effective cut-off: a hard CC whose reduced duration falls below
#   0.65s  (normal / champion / minion)
#   0.85s  (rare / boss)
# simply does not apply.
```

Soft CC is exempt from the accumulation, but not from everything: elites take 25% less relative value
from slows, attack-speed slows are cut 65%, and knockback stops working once hard-CC resistance passes
65%.

This is a *continuous* version of WoW's *discrete* system. It has no windows and no categories — one
scalar per monster. Cheaper to implement, and it degrades smoothly, but it cannot express "stuns and
fears diminish separately."

The **duration floor** (0.65s / 0.85s) is worth isolating. It is not a resistance; it is a statement
that a control effect shorter than a fifth of a second of useful play should not fire at all. League
does the same thing from the other side: FACT,
<https://wiki.leagueoflegends.com/en-us/Tenacity> — *"any disable's duration cannot be reduced below
0.3 seconds."* Two games, opposite conclusions: Diablo IV deletes the sub-threshold control, League
clamps it up to a minimum. Both agree that a 0.05-second stun is a bug.

### 3.3 A consumable stack — Guild Wars 2 Stability

FACT, <https://wiki.guildwars2.com/wiki/Stability> (via reader proxy):

- Intensity stacking, max 25.
- **Each incoming control effect removes one stack** and is fully negated.
- **Only one stack can be consumed every 0.75 seconds.**
- Does *not* prevent Crippled, Immobile, Chilled or Slow — soft control passes straight through.
- Does *not* remove control already applied (that is a stun-break, a separate thing).
- **No immunity window when the last stack is consumed** — the very next control lands.
- Boon-strip removes all stacks at once.

The 0.75-second internal cooldown on stack consumption is doing quiet, load-bearing work: without it a
single multi-hit AoE would eat 25 stacks in one frame. This is the same shape as a per-status ICD — a
rate limit on how fast a defensive resource can be spent, distinct from how much of it there is.

### 3.4 A depletable bar with a published price — Guild Wars 2 defiance

The most quantitatively useful mechanism found in the whole survey, and the only published control-to-
resource conversion.

FACT, <https://wiki.guildwars2.com/wiki/Defiance_bar> (via reader proxy):

```
defiance bar damage from hard control = 100 × (control effect duration in seconds)
# minimum 25
```

| Hard control | Defiance damage |
|---|---|
| Stun | 100 (<1s), 150 (1.5s), 200 (2s), 300 (3s), 500 (5s) |
| Daze | 100 (≤1s), 150 (1.5s), 200 (2s), 300 (3s) |
| Knockdown | 100 (1s), 200 (2s), 300 (3s) |
| Launch | 232 (fixed) |
| Knockback | 150 (fixed) |
| Pull | 150 (fixed) |
| Float | 100 (1s), 125 (1.25s), 250 (2.5s), 300 (3s) |

| Soft control | Defiance damage per second |
|---|---|
| Fear | 100 |
| Taunt | 100 |
| Immobilize | 50 |
| Slow | 50 |
| Chilled | 33 |
| Crippled | 15 |
| Weakness | 20 |

While the bar is up (locked), the target is **immune to the effect** but the control still damages the
bar. When the bar empties, the target is interrupted and usually stunned.

Three things this design gets that a flat immunity does not:

1. Control abilities remain **useful** against immune targets rather than being dead buttons.
2. It gives control a **currency** — an exchange rate against a resource pool, which is the closest
   anyone comes to pricing control.
3. It makes soft control and hard control **commensurable**: three seconds of Chilled (99) is worth
   about one second of Stun (100).

### 3.5 Multiplicative duration reduction — MOBA tenacity

FACT, <https://wiki.leagueoflegends.com/en-us/Tenacity>:

```
total tenacity =
  (1 − (1−A1)(1−A2)…) + (1 − (1−B1)(1−B2)…) + (1 − (1−C1)(1−C2)…)
# within a group: multiplicative
# across groups:  additive
# capped at 100%; floor of 0.3s on any disable
```

FACT, <https://liquipedia.net/dota2/Status_Resistance> (as reported in search results): Dota 2's status
resistance multiplies the base duration by `(1 − resistance)`; 25% resistance turns a 4-second stun
into 3 seconds; sources stack multiplicatively across three named groups so **100% is unreachable by
stacking**.

Both games chose multiplicative-within-group specifically so that the stat has diminishing value to
itself. That is the shape a resistance stat needs if it is not going to become mandatory.

**Live source conflict, unresolved.** League's Crowd control page lists tenacity as *not* affecting
Sleep, Polymorph, Ground, Kinematics, Knockdown or Disrupt, and *as* affecting Suppression. League's
Tenacity page says tenacity reduces everything *except* airborne, drowsy, nearsight, stasis and
suppression. The two pages disagree on Suppression, Sleep and Polymorph at minimum. Recorded, not
resolved.

### 3.6 An application ceiling rather than a duration cut — Genshin ICD

Genshin does not bound control by shortening it. It bounds how often a source may apply an element at
all.

FACT, <https://library.keqingmains.com/combat-mechanics/internal-cooldown>:

```
default ICD: an attack applies its element once every 3 hits
             OR once every 2.5 seconds, whichever comes first

# asymmetry that matters:
#   the 3-hit rule does NOT reset the 2.5s timer
#   the 2.5s timer DOES reset the 3-hit count
```

And the tracking rules: **ICD is not shared between enemies. ICD is not shared between characters.
Multiple abilities may share an ICD** (a character's normal and charged attacks often do). There are
three separate ICD types — elemental application, damage (for transformative reaction damage), and
poise damage — all using the same machinery.

This is directly comparable to a design with several independent clocks. Genshin's lesson is that the
clocks must be **explicitly scoped** (per source, per target, per talent-group), and that when two
clocks govern the same gate you must state which one resets the other. Getting that asymmetry wrong is
a silent bug: everything still fires, just at the wrong rate.

### 3.7 A binary gate instead of a roll — Divinity: Original Sin 2

FACT, <https://www.gamepressure.com/originalsinii/environmental-effects-and-combinations/zea274>
(second-tier): status effects are gated by armour type, not by a resist roll.

- **Physical Armour** blocks Slowed, Decaying, Acid.
- **Magic Armour** blocks Burning, Poisoned, Shocked/Stunned, and cursed-fire effects.
- While the relevant armour is above zero, the status **cannot apply at all**.
- Web effects ignore armour and cannot be blocked.

INFERENCE: this converts control from a probability problem into a resource problem. There is no
"unlucky stun"; there is only "did the team spend enough of the right damage type first." It is
deterministic and completely legible, and its known cost is that it forces parties to be
mono-damage-type, because splitting damage across both armour bars means stripping neither.

### 3.8 Self-imposed escalating resistance — Darkest Dungeon

FACT (§1.11): being stunned grants **50% stun resistance, stacking to 100%**. No window, no category
table, no decay stated. The cheapest DR in the survey and the least expressive.

### 3.9 Summary — what each mechanism costs

| Mechanism | Player problem it solves | Designer cost | What breaks when tuned wrong |
|---|---|---|---|
| Windowed halving DR (WoW) | chain-CC lockout | category assignment for every ability + per-target per-category timers | Too aggressive: control becomes a one-shot opener and CC classes have no mid-fight role. Too weak: the target never plays. |
| Accumulating resistance (D4) | same, cheaper | one scalar per target | Decay too slow: control is a first-10-seconds tool only. Cap too low: infinite lock returns. |
| Consumable stack (GW2 Stability) | *anticipated* lockout | a boon economy, plus an ICD on consumption | No ICD on consumption: one AoE eats every stack. Cap too high: control stops existing. |
| Depletable bar (defiance) | dead buttons vs immune targets | per-encounter bar sizes and a damage value on every control | Bar too big: control is theatre. Too small: bosses are permanently stunned. |
| Multiplicative resistance (tenacity) | a stat you can build | a group taxonomy so stacking terminates | Additive instead: one build reaches 100% and control is deleted from the game. |
| Application ceiling (Genshin ICD) | rapid-hit sources trivialising application | per-source, per-target clock scoping | Scope wrong: a multi-hit skill applies 12× and the whole element economy inverts. |
| Armour gate (DOS2) | random lockout | two armour pools tracked separately | Armour too high: control never happens. Too low: it is not a gate. |
| Self resistance (Darkest Dungeon) | chain-stun | almost nothing | No decay: one stun permanently immunises. |

---

## 4. Stacking models

Four models, and every game surveyed uses at least two of them.

| Model | Meaning | Shipped examples |
|---|---|---|
| **Stack intensity** | N stacks = N× the effect, one timer | GW2 Bleeding (→1500), Might (→25), Vulnerability (→25); StS Poison, Constricted; PoE Poison |
| **Stack duration** | N stacks = N turns/seconds, effect unchanged | GW2 Chilled, Fear, all control conditions; StS Weak, Vulnerable, Frail |
| **Refresh / replace** | one instance; a new application resets or overwrites | D2 curses; SMT V ailments; PoE Bleed and Ignite; WoW DoTs (with pandemic) |
| **Independent instances** | many timers coexist | PoE Poison; GW2 damaging conditions from many players |

### 4.1 The pandemic rule — WoW

FACT (community-documented; Blizzard shipped it in patch 6.0.1):
<https://blog.askmrrobot.com/how-wow-works-periodic-damage-and-healing-dots-and-hots/>

```
on re-applying a periodic effect to a target that already has it:
  carried_over = min(remaining_duration, 0.30 × base_duration)
  new_duration = base_duration + carried_over
```

So a 15-second DoT refreshed with ≤4.5 seconds left loses nothing. Before 6.0.1 (from 4.0.1) the
carry-over was a single tick, which forced players to refresh in a narrow window between the
second-to-last and last tick.

Problem it solves: DoT classes were being asked to perform frame-accurate refresh timing for a damage
gain that had nothing to do with the fight. Designer cost: a per-effect base duration that the refresh
logic must know, distinct from the current duration. What breaks when tuned wrong: at 0% carry-over
you get the pre-6.0.1 timing minigame; at 100% carry-over DoTs become infinitely extendable and the
duration stat stops meaning anything.

### 4.2 Who resolves multiple sources of the same debuff

This is where games differ most, and the split is clean.

| Game | Same debuff, two sources | Resolution |
|---|---|---|
| **Path of Exile** | Poison | **All instances coexist**, unlimited stacks. One hit applies at most one stack, so >100% poison chance is wasted. |
| **Path of Exile** | Bleed | **All instances persist but only the highest DPS one ticks.** With the Crimson Dance keystone, up to 8 stack properly. |
| **Path of Exile** | Ignite | One at a time by default. |
| **Guild Wars 2** | Bleeding, Burning, etc. | Stacks pool across all applicators up to 1,500; each stack keeps its own duration and damage. |
| **Guild Wars 2** | Might, Stability | Intensity stacks pooled, cap 25. |
| **Diablo II** | Curses | **One only.** The newest replaces the previous, regardless of source or strength. |
| **SMT V** | Ailments | **One per category.** "The newest condition will always override the old." |
| **Pokémon** | Non-volatile | One only; a second application simply fails. |
| **Pokémon** | Volatile | Many coexist. |
| **Slay the Spire** | All | Every debuff is a single counter on the target; sources add into the same counter. |
| **WoW** | Same spell, two casters | Historically separate auras per caster; same spell from the same caster refreshes with pandemic. |

INFERENCE, the pattern: **games with many simultaneous actors (MMO, ARPG with allies) allow multiple
instances; turn-based games with a small cast do not.** The reason is UI and mental load, not
simulation cost — Slay the Spire's single-counter model exists so a player can read the whole board
state from a row of numbers.

The "highest DPS only ticks" rule in PoE Bleed is worth flagging separately. It keeps the *display*
honest (you can see all the bleeds) while keeping the *math* bounded, and it avoids the
"weaker overwrote stronger" feel-bad the older ARPG note identifies as a risk of intensity-overwrite.

---

## 5. How control is priced against damage

**The direct answer: no studio publishes a stun-to-damage conversion.** This is a genuine absence, and
it is consistent with the finding already recorded in `game-design/06-unsourced.md` that studios do not
publish quantified balance targets of any kind.

What does exist:

### 5.1 The one published conversion is control-to-resource, not control-to-damage

Guild Wars 2's defiance bar (§3.4) gives control an exact price in a resource:
`100 defiance damage per second of hard control`. That is a real, shipped, published exchange rate —
but the resource on the other side is a break bar, not health. It says how much stun equals how much
knockback. It does not say how much stun equals how much damage.

### 5.2 Games price control in *seconds*, not damage

Every bounding mechanism in §3 measures control in time. WoW halves duration. Tenacity multiplies
duration and floors it at 0.3 seconds. Diablo IV accrues resistance per second of control and deletes
control below 0.65 seconds. Guild Wars 2 charges the bar per second. Genshin rate-limits applications
per 2.5 seconds.

INFERENCE: the industry's revealed unit for control is **the second**, and no game found converts that
second into a damage number. League's own scoreboard reports a crowd-control score in seconds of CC
applied rather than in damage-equivalent — I could not reach a primary Riot document for that field
(see §8), so treat the specifics as unconfirmed, but the qualitative point stands from the bounding
systems alone, which are all time-denominated.

### 5.3 What designers say qualitatively

The strongest sourced statement found is about *category rigidity* rather than price. FACT, a Riot
post on the old League boards, quoted in search results: converting all suppression abilities into
stuns *"would break every single one of them because they all rely on their CC having a fixed duration
no matter what."* That is a designer saying, in effect, that some control is priced on its
**unshortenability**, not on its length — which is why Suppression sits outside the tenacity system in
one of the two conflicting wiki accounts.

FACT, second-order: Naoki Yoshida on balance philosophy, reported at
<https://www.rpgfan.com/2026/04/26/final-fantasy-xiv-evercold-conference/> — *"'balance' means
different things to different people. Some players view balance purely through the lens of damage
numbers, while the development team looks at it within specific categories."* Note the framing: the
studio explicitly declines to reduce everything to a damage number. The RPGFan article's own analysis
(the author's, **not** a Yoshida quote — the distinction matters) is that *"the rise of the raid buff
meta and the eventual standardization around 120-second burst windows have made many jobs feel nearly
identical."*

### 5.4 The implicit pricing that does exist

Three shipped signals, all INFERENCE from the mechanics rather than statements:

1. **Control gets a second bounding system; damage does not.** No game DRs a DoT. That is the
   strongest available statement that designers consider control categorically more dangerous than
   damage.
2. **Bosses are immune to control but not to damage.** WoW raid bosses, FFXIV raid bosses. Where
   control cannot be balanced, it is removed entirely — a step nobody takes with damage.
3. **Control has a floor, damage does not.** A 0.05-damage hit is fine; a 0.05-second stun is deleted
   (D4) or clamped (League). Control below a threshold has negative value because it costs a button
   press and a cooldown for nothing.

---

## 6. Enabler and payoff — how shipped games guarantee the setup is reachable

This is the section with the most transferable material, and the answer is consistent: **shipped games
guarantee reachability structurally, not statistically.** Where they do not, there is a known dead
pairing.

### 6.1 The five guarantee strategies actually in use

| Strategy | Mechanism | Shipped example |
|---|---|---|
| **Make the enabler unresistable** | the setup status has no resist roll at all | Darkest Dungeon Mark: *"No character or monster can resist them"* |
| **Bundle enabler and payoff in the same kit** | the class/subclass that has the payoff also has the setup | Genshin element-team construction; Necromancer curses + curse-scaling skills |
| **Align by cooldown arithmetic** | every enabler and every payoff share a cooldown multiple, so they meet deterministically | FFXIV's 120-second raid-buff standardisation |
| **Make the enabler an environment, not a status** | the setup persists on the ground and anyone can trigger it | DOS2 surfaces; Genshin auras on the target |
| **Make the payoff also produce the enabler** | the loop is self-priming | PoE Poison stacking; StS Catalyst-style multipliers on a stat you already build |

### 6.2 Frozen → Shatter

Two implementations with opposite reachability properties.

**Genshin:** Cryo + Hydro = Frozen. Frozen + **blunt damage** (claymore, Geo, plunge) = Shattered, a
transformative reaction with a reaction multiplier of **3** — tied for the highest in the game, above
Overloaded's 2.75. FACT,
<https://library.keqingmains.com/combat-mechanics/elemental-effects/transformative-reactions>:

```
TransformativeReactions =
  ReactionMultiplier × LevelMultiplier
  × (1 + 16×EM/(2000+EM) + ReactionBonus)
  × EnemyResistanceMultiplier
```

Multipliers: Burgeon / Hyperbloom / **Shattered = 3**, Overloaded 2.75, Electro-Charged 2 × triggers,
Bloom 2, Superconduct 1.5, Swirl 0.6, Burning 0.25.

**The reachability failure is real and documented by the mechanic itself.** A Cryo + Hydro team with no
claymore, no Geo and no plunge attack can produce Frozen every rotation and never once produce
Shattered. The enabler is trivially reachable; the payoff trigger is not in the pool. Genshin accepts
this because the team is player-composed and the payoff is a bonus, not a requirement.

**Path of Exile:** Freeze is itself the shatter — a frozen monster killed while frozen shatters,
removing the corpse. The payoff is bundled into the enabler's own resolution, so it cannot be
unreachable.

### 6.3 Wet → lightning, and the surface model

DOS2's surfaces are the purest enabler/payoff economy found. FACT (§3.7 source): Water + Air =
Electrified Water; Blood + Air = Electrified Blood; Oil + Fire = Fire; Poison + Fire = explosion;
Water + Fire = Steam Cloud; Blood + Ice = Frozen Blood.

Three properties make the setup always reachable:

1. **The enabler is a surface, not a status.** It persists on the ground, is visible, and is not
   consumed by being looked at.
2. **Damage itself creates surfaces.** Dealing physical damage creates Blood. Every party generates
   enablers as a side effect of playing normally, with no dedicated enabler slot.
3. **Multiple elements trigger the same surface.** Electrified Water responds to any Air source, not
   to one named skill.

The cost is severe and known: surfaces are persistent, spread, and are the single most-complained-about
element of the game's combat, because a payoff that fires from the environment also fires *at* you, and
the enabler you created two turns ago is still there.

### 6.4 Marks, brands and curses

**Darkest Dungeon Mark** is the cleanest guarantee in the survey. Mark does nothing by itself. Its
entire purpose is to enable mark-consuming skills. And it is the one status in the game that
**nothing can resist**. FACT, <https://darkestdungeon.wiki.gg/wiki/Status_effects>. INFERENCE: this is
a deliberate design rule — *if a status exists only to enable another action, it must not be able to
fail*, because a failed enabler wastes two turns rather than one.

**Diablo II curses** guarantee reachability by exclusivity in the other direction: a curse-scaling
build knows exactly which curse is up, because only one can be. Amplify Damage (−100% physical
resistance) and Lower Resist are pure enablers; the Necromancer's own damage skills are the payoff; and
because only one curse fits, the player is never choosing between two enablers for the same payoff.

**Guild Wars 2 Vulnerability** is the universal enabler: intensity-stacking to 25, increasing both
strike and condition damage taken, applied by essentially every profession. Reachability is guaranteed
by ubiquity — a payoff that keys on Vulnerability will find it in any composition.

**Path of Exile Exposure** sits in its own family, separate from ailments, precisely so that a
resistance-reduction enabler does not compete for the ailment budget. (Noted in
[arpg-effects/04](../arpg-effects/04-ailments-status.md).)

### 6.5 Damage-window buffs — FFXIV's cooldown-arithmetic guarantee

The most rigorous reachability guarantee found, and it uses no status logic at all.

FACT (reported analysis, RPGFan and community sources): in Endwalker, FFXIV moved raid buffs onto
120-second cooldowns and adjusted other abilities to multiples of 60 seconds, so that **everyone's
buffs line up automatically as long as they press buttons on cooldown.**

The guarantee is arithmetic. A payoff (a burst rotation) and its enablers (party damage buffs) meet
every 120 seconds by construction, with no coordination, no resist roll, and no drafting question.

The cost, which the game is openly criticised for: the RPGFan author's analysis is that
*"job identity has significantly deteriorated"* and *"many jobs feel nearly identical"* under it, and
Yoshida is reported as aware of the criticism. INFERENCE: when the enabler is guaranteed by cooldown
alignment, every job's optimal play converges on the same shape, because the shape is dictated by the
shared clock rather than by the job.

Yoshida's stated remedy, per the same article: *"the team first builds mechanics that reinforce what
makes a job feel unique, then balances around those mechanics afterward."*

### 6.6 What each enabler mechanism costs

| Mechanism | Player problem solved | Designer cost | What breaks when tuned wrong |
|---|---|---|---|
| Unresistable enabler | wasted setup turn | the enabler now has no counterplay of its own | If the payoff is also strong, the pair has no answer at any point. |
| Bundled enabler+payoff | reachability | duplicated content across every kit that wants the payoff | If the bundle is too tight, every build is the same build. |
| Cooldown alignment | coordination burden | every ability's cooldown must be a multiple of the window | Job identity collapses (FFXIV's stated case). |
| Surface/aura enabler | the setup persisting long enough to use | a whole spatial simulation | It fires at you too (DOS2's known complaint). |
| Self-priming payoff | needing a separate setup action | the loop can run away | Uncapped stacking; PoE Poison needs the one-stack-per-hit rule to stay bounded. |
| **No guarantee at all** | — | nothing | Dead pairings ship. Genshin's Frozen with no blunt source is the documented example. |

### 6.7 The transferable rule

INFERENCE, drawn across every case above: **shipped games make the enabler cheap, reliable and
frequently free, and put the tuning knob on the payoff.** Mark cannot be resisted. Vulnerability is on
every profession. Surfaces are created by ordinary damage. Raid buffs are on a clock nobody can miss.
Not one game found makes the *enabler* the rare or unreliable half. Where the setup half is
conditional — Genshin's blunt-damage requirement for Shatter — the pairing has a known dead case.

---

## 7. How many statuses is too many

### 7.1 The one hard historical data point: WoW's debuff limit

FACT, <https://warcraft.wiki.gg/wiki/Debuff>:

| Patch | Date | Debuff limit on a target |
|---|---|---|
| 1.7.0 | 2005-09-13 | raised to **8** |
| 1.11.0 | 2006-06-19 | raised to **16** |
| 2.0.1 | 2006-12-05 | raised to **40** |
| 3.0.2 | 2008-10-14 | *"There is no longer a limit on the amount of debuffs a target can have on them at any time"* |

This is the only case found where a status *count* was a first-class design constraint. At 8 and 16
slots, raids had to ration which debuffs went on a boss, and the limit shaped raid composition. The
limit was raised three times and then removed, which is a fairly complete verdict on how the studio
felt about it.

FACT, <https://warcraft.wiki.gg/wiki/API_UnitAura>: the client API still returns **up to 40 auras** per
unit per filter, and Classic Era retains a **16-debuff display limit**. So the constraint survives as a
UI/API artefact even after being removed as a rule.

### 7.2 Games that keep the visible list small on purpose

- **Guild Wars 2**: 12 boons and 14 conditions, unchanged in structure for over a decade. The
  discipline is not in the count but in the **rule**: every damaging condition stacks in intensity,
  every control condition stacks in duration. A player who learns two rules can predict 26 entries.
- **Slay the Spire**: 26 debuffs, but the three that appear in almost every fight — Weak, Vulnerable,
  Frail — are a tight, symmetric core (−25% damage / +50% damage taken / −25% block). The other 23 are
  enemy-specific or archetype-specific and are met one at a time.
- **SMT V**: 8, with a one-per-category rule that means the player never tracks more than one.
- **Pokémon**: 6 major statuses that a player must actually know, and 40+ minor ones that were not even
  given icons until Generation VII.

INFERENCE: the pattern is not a maximum count. It is a **maximum number simultaneously live on one
target**, and every game bounds that with a structural rule rather than with a smaller list:
exclusivity (Pokémon major, SMT V, Diablo II curses), category slots, or simply the fact that most
entries are enemy-specific and never co-occur.

### 7.3 Where a list was cut

- **WoW**: DR categories were reduced post-Warlords — the CC page states *"Only six DR categories
  remain."* The CC vocabulary itself was not cut; the bucket list was.
- **WoW again**: the DR reset window moved from 18 seconds to **16 seconds in patch 12.0.0**, per the
  DR page. Small, but it is a live tuning knob on the bounding system, not on the statuses.
- **FFXIV**: player CC in PvE has been reduced to near-nothing by making enemies immune, rather than by
  deleting the abilities. The 4,220-row table stayed; the *usable* set shrank to approximately zero.

INFERENCE: nobody in this survey deleted status *definitions* to reduce complexity. They reduced
**reachability** — immunity, exclusivity, category collapse — while leaving the vocabulary intact. That
is a cheaper edit and it preserves the content for later.

### 7.4 What breaks at each end

| Failure | Symptom | Example |
|---|---|---|
| Too many simultaneously live | the player cannot read the board; the UI truncates | WoW's 8/16-slot era, where raids rationed debuff slots |
| Too many defined but few live | large table, low load — this is fine | FFXIV's 4,220 rows |
| Too many with inconsistent stacking rules | the player must memorise per-status behaviour | the counterexample is GW2, where the rule is derivable from the effect |
| Too few | control has no texture; every fight resolves the same way | not directly observed in this survey |
| Declared but inert | looks implemented in traces, does nothing in play | observed in this project's own catalog — see the `charm_pulse` comment in [`StatusCatalogBootstrap.cs`](../../../src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs), where a status declared `UnityCc` named an execution path the host game does not have |

---

## 8. What I could not find

Mandatory section. Everything here was looked for this pass and not obtained.

### Genuine absences

- **No published conversion from control duration to damage, from anyone.** Checked across WoW,
  League, Dota 2, GW2, Diablo IV and FFXIV material. The closest thing that exists is Guild Wars 2's
  `100 defiance damage per second of hard control`, which prices control against a *break bar*, not
  against health or DPS. Do not let this be reported as a damage conversion.
- **No designer statement of a power budget for CC.** The only Riot designer statement recovered is
  about suppression's fixed duration being load-bearing, not about how much a stun is worth.
- **No published rule from any game requiring that a status pool contain an enabler for each
  conditional payoff.** Every guarantee found (§6) is an emergent property of how a specific kit or
  clock was built, never a stated authoring constraint. If this project wants such a rule, it is
  inventing it, not borrowing it.
- **No evidence of a researched threshold for how many simultaneous statuses a player can track.** The
  WoW debuff-limit history (§7.1) is a *technical* limit that had design consequences, not a
  usability study. No usability research on buff/debuff tracking was located.
- **No Blizzard rationale for removing the debuff limit** in 3.0.2 — only the patch line itself.

### Access blocks this pass

| Blocked | Effect |
|---|---|
| **poewiki.net — Anubis / Cloudflare 403**, both direct and via reader proxy | PoE's exact Shock and Chill magnitude formulas and the ailment-threshold definition could not be quoted. Ailment list and stacking rules recovered from search-result reproductions instead. |
| **wiki.guildwars2.com — direct fetch 403** | Everything GW2 came through the reader proxy. The `Control effect` and `Effect` pages 403'd through the proxy too, so GW2's hard/soft control list is reconstructed from the Defiance bar page rather than from the definitional page. |
| **wiki.leagueoflegends.com — direct fetch 403** | Both League pages came through the reader proxy. |
| **bulbapedia — direct fetch 403** | Pokémon data came through the reader proxy. |
| **icy-veins.com — 403 direct, 404 through proxy** | Diablo IV's DR numbers came from a second-tier site (vhpg) instead of a first-tier guide. |
| **Fandom — HTTP 402 sitewide** (as recorded in `game-design/06-unsourced.md`) | SMT V and Diablo material required the reader proxy or a Blizzard-official fallback. |
| **developer.riotgames.com** | Not attempted after the search budget ran out; League's crowd-control-score field name and definition are therefore **unconfirmed** and are flagged as such in §5.2. |
| **WebSearch budget exhausted at 200 calls** | The last three planned searches — WoW debuff-limit raid history, Destiny 2 Stasis freeze/shatter kit guarantees, and Diablo IV's Vulnerable rework — were not run. Destiny 2 is therefore absent from §6 entirely, and Diablo IV's Vulnerable history is absent from §6.4. |

### Live source conflicts, unresolved

| Conflict | Status |
|---|---|
| **League tenacity coverage** — the Crowd control page and the Tenacity page disagree on whether tenacity affects Suppression, Sleep and Polymorph | Unresolved. Both are the same wiki. |
| **WoW DR category count** — 8 (DR page), 6 (CC page, post-Warlords), 6+2 special (maxroll 12.0.7) | Probably reconcilable as 6 standard plus displacements and taunts as special cases, but **no source states it that way**. |
| **WoW DR immunity threshold** — wiki says immune after the 3rd application, maxroll 12.0.7 says *"fully immune after 2 applications"* in PvP | Likely a version difference; maxroll is the more current. The **PvE** rule (halving continues to 1/16, no immunity) is stated by only one source. |
| **SMT V ailment overwrite** — the wiki says *"the newest condition will always override the old"*; Game8 reports a weakest→strongest priority order (Sleep < Mirage < Poison < Confusion < Charm < Seal) | Unresolved. These cannot both be the complete rule. |
| **Diablo IV CC effect count** — 12 (icy-veins list) vs 11 (secondary summaries, missing Pull In) | Unresolved. |
| **Diablo IV DR numbers** (10%/sec to 95%, decaying 5%/sec, 0.65s/0.85s floors) | Recovered from **one second-tier source only** (vhpg). Not corroborated. Treat as unconfirmed. |

### Numbers that are computed here, not cited

| Figure | How derived |
|---|---|
| League **19** CC types (7 hard, 12 soft) | Counted over the wiki's own two lists |
| WoW **13** CC words (9 hard, 4 soft) | Counted over the CC page's two lists |
| Slay the Spire **26** debuffs, 18 intensity / 6 duration / 2 neither | Counted over the wiki's per-row type labels |
| GW2 **5** damaging / **8** control / **1** amp conditions | Counted over the condition table |
| GW2 "every damaging condition is intensity, every control condition is duration" | Observed across the whole table, not stated by the source |
| Pokémon **21** real move-ailments | 23 PokéAPI rows minus the `unknown` and `none` sentinels |
| Darkest Dungeon **7** resistance stats | Counted from the resistance list |
| This project's **21** statuses, **8** `StatusKind` values | Counted in `StatusCatalogBootstrap.cs` and `ResistanceEvaluator.cs` |

---

## 9. Sources

First-tier (official, API, or game-owned):

- Arreat Summit, Necromancer curses (Blizzard) — <http://classic.battle.net/diablo2exp/skills/necromancer-curses.shtml>
- XIVAPI Status sheet — <https://xivapi.com/Status?limit=1>
- PokéAPI move-ailment endpoint — <https://pokeapi.co/api/v2/move-ailment/?limit=100>
- Guild Wars 2 Wiki, Boon — <https://wiki.guildwars2.com/wiki/Boon>
- Guild Wars 2 Wiki, Condition — <https://wiki.guildwars2.com/wiki/Condition>
- Guild Wars 2 Wiki, Defiance bar — <https://wiki.guildwars2.com/wiki/Defiance_bar>
- Guild Wars 2 Wiki, Stability — <https://wiki.guildwars2.com/wiki/Stability>

Second-tier (community wikis and guide sites — reliable for mechanics, not for intent):

- Warcraft Wiki, Diminishing returns — <https://warcraft.wiki.gg/wiki/Diminishing_returns>
- Warcraft Wiki, Crowd control — <https://warcraft.wiki.gg/wiki/Crowd_control>
- Warcraft Wiki, Debuff — <https://warcraft.wiki.gg/wiki/Debuff>
- Warcraft Wiki, API UnitAura — <https://warcraft.wiki.gg/wiki/API_UnitAura>
- Maxroll, WoW crowd control diminishing returns 12.0.7 — <https://maxroll.gg/wow/resources/crowd-control-diminishing-returns>
- League of Legends Wiki, Crowd control — <https://wiki.leagueoflegends.com/en-us/Crowd_control>
- League of Legends Wiki, Tenacity — <https://wiki.leagueoflegends.com/en-us/Tenacity>
- Liquipedia Dota 2, Status Resistance — <https://liquipedia.net/dota2/Status_Resistance>
- Slay the Spire Wiki, Debuffs — <https://slaythespire.wiki.gg/wiki/Debuffs>
- Darkest Dungeon Wiki, Status effects — <https://darkestdungeon.wiki.gg/wiki/Status_effects>
- Bulbapedia, Status condition — <https://bulbapedia.bulbagarden.net/wiki/Status_condition>
- Megami Tensei Wiki, Status Changes in SMT V — <https://megamitensei.fandom.com/wiki/Status_Changes_in_Shin_Megami_Tensei_V>
- Game8, SMT V status ailments — <https://game8.co/games/Shin-Megami-Tensei-V/archives/350035>
- KeqingMains Library, Transformative Reactions — <https://library.keqingmains.com/combat-mechanics/elemental-effects/transformative-reactions>
- KeqingMains Library, Elemental Gauge Theory — <https://library.keqingmains.com/combat-mechanics/elemental-effects/elemental-gauge-theory>
- KeqingMains Library, Internal Cooldown — <https://library.keqingmains.com/combat-mechanics/internal-cooldown>
- Icy Veins, Diablo 4 crowd control status effects — <https://www.icy-veins.com/d4/guides/crowd-control-status-effects/>
- vhpg, Diablo 4 crowd control — <https://www.vhpg.com/diablo-4-crowd-control/>
- Ask Mr. Robot, periodic damage and healing (pandemic) — <https://blog.askmrrobot.com/how-wow-works-periodic-damage-and-healing-dots-and-hots/>
- gamepressure, DOS2 environmental effects and combinations — <https://www.gamepressure.com/originalsinii/environmental-effects-and-combinations/zea274>
- PoE Wiki, Ailment (content via search reproduction; page itself blocked) — <https://www.poewiki.net/wiki/Ailment>
- RPGFan, FFXIV Fan Fest 2026 press conference — <https://www.rpgfan.com/2026/04/26/final-fantasy-xiv-evercold-conference/>

In-repo:

- [docs/research/arpg-effects/04-ailments-status.md](../arpg-effects/04-ailments-status.md)
- [docs/research/game-design/06-unsourced.md](../game-design/06-unsourced.md)
- [src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs](../../../src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs)
- [src/FusionRpg.Core/Status/ResistanceEvaluator.cs](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs)
