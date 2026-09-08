# Support, healing and buff actions — how games taxonomise them, and how they keep them honest

**Research pass, 2026-09-02.** Scope: the restoration/prevention taxonomy, the buff taxonomy, the
healer-mandatory failure mode, the opposite failure (support nobody takes), sustain in games with no
healer, support fraction in creature-collection rosters, and cleanse/dispel design.

**Method.** [docs/research/game-design/06-unsourced.md](../game-design/06-unsourced.md) was read first;
the searches it records as dead were not re-run. Sources are marked **[1st]** first-party
(developer statement, official job guide, official patch note), **[data]** machine-readable game data
or API, **[2nd]** community wiki, **[3rd]** aggregator/news site. Every self-tallied number is marked
**(computed)**. Claims are marked **FACT** or **INFERENCE**.

**Access note.** `WebSearch` budget for this session was exhausted at 200 calls partway through, and
several sources are behind Cloudflare/Anubis (`poewiki.net`, `wiki.guildwars2.com` direct,
`na.finalfantasyxiv.com` direct, `bungie.net` direct). `https://r.jina.ai/<url>` was used as a reader
proxy and worked for the GW2 wiki, the FFXIV Lodestone job guides and Bungie — this is the same trick
recorded in `06-unsourced.md` §2 and it is still working. See **What I could not find** at the end.

---

## The finding in one paragraph

Support is the only action category where **being correct is a failure state**. Every other category
degrades gracefully: an attack that is too weak is skipped, an attack that is too strong is nerfed.
Support has two cliffs and a narrow shelf between them — if a heal or a buff is strong enough to be
worth an action, it is usually strong enough to be *required*, and once it is required the game has
silently added a roster tax, a queue-time problem and a compulsory slot. Bungie stated the failure
exactly, about Well of Radiance: its healing and damage resistance *"offer effective invulnerability,
which removes any other defensive option from consideration"* **[1st]**. Blizzard stated the opposite
end just as plainly, about tanks and healers in the WoW dungeon finder: *"We don't feel the tanking and
healing roles have any inherent issues... but simply that fulfilling them is more responsibility"*
**[1st]**. The three structural answers the industry has converged on are (a) **make support a rider,
not an action** — Guild Wars 2 pushed its whole buff vocabulary into effects that a damage build emits
while doing its normal rotation; (b) **give everyone baseline sustain** so the support role is an
amplifier rather than a dependency — Overwatch 2 Season 9 gave every hero a self-heal passive
explicitly *"to take some of the pressure off Support players to keep everyone alive"* **[1st]**; and
(c) **split restoration from prevention** so they can be tuned against different failure modes, which
this project has already done by shipping shields as a separate system. The arithmetic that governs
whether a buff is ever worth a turn is simple and published only in fragments: a buff costing one turn
and multiplying damage by `m` for `N` subsequent turns pays only when `N ≥ 1/(m−1)`, and the threshold
divides by the number of allies it lands on — which is why every turn-based game with a party-wide buff
tunes it far weaker than its self-buff equivalent, and why single-target buffs in small parties are
nearly always a trap (computed, §4).

---

## 1. The taxonomy of restoration — and where the restoration/prevention line falls

### 1.1 The line itself

**FACT.** Games do not agree on one word for this space, but every system that ships more than two
sustain mechanics ends up splitting them along the same axis:

| | **Restoration** | **Prevention** |
|---|---|---|
| Acts on | HP already lost | Damage not yet taken |
| Applied | After the hit | Before the hit |
| Wasted when | Target is at full HP (**overheal**) | The hit never comes (**expired shield**) |
| Scales badly against | Burst that kills between ticks | Chip damage that erodes it for free |
| Counterplay | Healing reduction, execute thresholds | Shield-break bonuses, strip/dispel, pierce |
| Numeric anchor | Missing HP | Incoming damage |

**INFERENCE.** The reason this split keeps re-appearing independently is that the two halves have
*different waste modes*, so they cannot be balanced against the same number. Overheal wastes on a
healthy target; an expired shield wastes on an unattacked target. A designer who merges them gets one
knob that is simultaneously too strong in one scenario and dead in the other. **This project already
ships shields as a separate system from healing, which is the arrangement the sourced games converge
on** — the finding here is not "should we split" but "where do the sub-mechanisms sit on either side".

### 1.2 The nine mechanisms

| Mechanism | Side | Ships in | How it differs from its neighbours |
|---|---|---|---|
| **Direct heal** | Restoration | Everything. FFXIV `Cure II` = 800 potency, 2s cast, 1000 MP **[1st]** | Instant, full-value, but capped by missing HP. Costs an action and is the most overheal-prone |
| **Heal over time (HoT)** | Restoration | FFXIV `Medica II` = 250 immediate + 150-potency regen over 15s **[1st]**; GW2 `Regeneration` = `130 + 0.125 × Healing Power` HP/s at level 80 **[2nd]** | Cheap per point, but *cannot answer burst*. Value depends on the target surviving to collect it |
| **Regeneration (passive)** | Restoration | PoE life regen; OW2 all-hero passive 20 HP/s after 5s undamaged (2.5s for Support) **[3rd]**; Souls-likes' none | No action cost at all. The designer's lever is the out-of-combat delay, not the rate |
| **Lifesteal / leech** | Restoration | PoE (per-instance ~2%/s of max life, total cap 20% of max life per second) **[2nd]**; FFXIV Sage `Kardia` = 170 cure potency on landing magic attacks **[1st]**; Pokémon `damage-heal` category = **11 moves** of 937 **[data]** | Restoration with *zero action cost*, paid for by attacking. Converts offence into sustain, which is why it is the standard answer for games with no healer |
| **Shield / absorb — pre-emptive** | Prevention | FFXIV `Adloquium`: 300 cure potency **plus** a barrier nullifying *"damage equaling 180% of the amount of HP restored"*, 30s **[1st]**; GW2 `Barrier`, capped at 50% of max health in PvE, decays after 5s **[2nd]** | A pool applied *before* the hit, on a timer. Requires the healer to predict; rewards knowing the fight |
| **Shield / absorb — reactive** | Prevention | GW2 `Aegis` — *"Block the next incoming attack"*, indefinite duration **[2nd]**; PoE Energy Shield (auto-recharging pool) | Waits for the hit, so needs no prediction. Nearly always compensated with a strip vulnerability or a recharge delay |
| **Damage reduction** | Prevention | GW2 `Protection` −33% incoming, `Resolution` −33% incoming condition damage **[2nd]**; Destiny `Well of Radiance` DR **[1st]**; FFXIV `Temperance` −10% party **[1st]** | Multiplicative and *uncapped in total value* — this is the one that breaks first, because it scales with the size of the hit rather than a fixed pool |
| **Cleanse / dispel** | Neither (removal) | §7 | Removes a state rather than restoring or preventing a number |
| **Resurrection** | Restoration (discontinuous) | FFXIV `Raise` / `Ascend`: 8s cast, 2400 MP, *"Resurrects target to a weakened state"* **[1st]** | Converts a permanent loss back into a temporary one. Always gated hard — cast time, cost, or a charge budget — because it undoes the mistake the fight was testing |
| **Overheal conversion** | Bridge | FFXIV `Adloquium`: *"When critical HP is restored, also grants Catalyze"* — a second 180% shield **[1st]**; Sage `Eukrasian Diagnosis` 180%, `Eukrasian Prognosis II` **360%** **[1st]** | The explicit fix for overheal waste: healing done to a full-HP target becomes a shield instead of nothing |

**FACT.** FFXIV's shield numbers are stated as a *percentage of healing done*, not as a flat pool. That
is a deliberate coupling: one Healing Magic Potency stat drives both halves, so a "barrier healer" and
a "pure healer" gear identically and the split is expressed entirely in actions.
Source: [Scholar job guide](https://na.finalfantasyxiv.com/jobguide/scholar/) and
[Sage job guide](https://na.finalfantasyxiv.com/jobguide/sage/) **[1st]**.

**FACT.** GW2 puts its absorb on the *other* side of the strip line from its damage reduction:
`Protection` and `Aegis` are **boons** (strippable, corruptible — see §7), while `Barrier` *"is not a
boon"*, it is a miscellaneous effect and therefore not removed by boon-strip. Source:
[GW2 wiki: Barrier](https://wiki.guildwars2.com/wiki/Barrier) **[2nd]**.

**INFERENCE.** That asymmetry is the cleanest shipped statement of the design rule: *the mechanism
that is hardest to counter should be the one that is smallest and decays fastest.* GW2's uncounterable
absorb is capped at 50% max health and gone in 5 seconds; its uncapped-value damage reduction is fully
strippable.

### 1.3 Per-mechanism: player problem / designer cost / failure when mistuned

| Mechanism | Solves for the player | Costs the designer | Breaks when tuned wrong |
|---|---|---|---|
| Direct heal | "I am about to die right now" | An action-economy hole: it must be worth a turn (§4) and it must not trivialise the damage check | Too strong → damage checks stop existing, and the healer becomes compulsory. Too weak → it is never the right button and the class is a passenger |
| HoT / regen | Attrition; keeps the party topped without a reaction | Needs a tick cadence and a snapshot rule, both of which leak into every other system | Too strong → all incoming chip damage is free and encounters must be redesigned around burst only. Too weak → pure filler |
| Passive regen | Removes the "I am at 40% and there is no healer" dead time | Reduces the value of every active heal in the game by exactly its rate | Too strong → the support role's whole job evaporates (OW2 had to also add a **damage passive reducing healing received by 20% for 2s** to compensate **[3rd]**). Too weak → invisible |
| Lifesteal | Sustain without spending an action | Couples defence to offence, so any damage buff is now also a survivability buff — a double-scaling trap | Too strong → immortality on any high-DPS build (the reason PoE caps *total* leech rate at 20% of max life/s **[2nd]**). Too weak → a dead stat |
| Pre-emptive shield | Rewards knowing what is coming | Requires telegraphed damage to be worth reading | Too strong → the fight's mechanics are pre-solved. Too weak → it expires unused, which reads as "I wasted a turn" — the worst feeling in the category |
| Reactive shield | Removes the prediction requirement | Must be counterable or it is strictly better than pre-emptive | Too strong → nothing lands, ever. This is why GW2 makes `Aegis` a strippable boon |
| Damage reduction | Survives the single biggest hit | Multiplies with everything; stacking rules are mandatory | The most common catastrophic case, because DR value grows with incoming damage — see Well of Radiance in §3 |
| Resurrection | Undoes a party wipe cascade | Every encounter must now be tuned assuming N revives | Too cheap → death stops being a fail state. Too expensive → it is never cast and the slot is dead weight |
| Overheal conversion | Makes a "wasted" heal do something | Adds a second resource that must be capped separately | Too generous → the optimal play is to spam heals on healthy targets, which inverts the whole role |

---

## 2. The buff taxonomy

### 2.1 Guild Wars 2's boon list — the cleanest shipped example

**FACT.** GW2 has a **closed, named, twelve-entry** buff vocabulary. Every boon is visible in the same
UI slot, every boon obeys one of two stacking rules, and every boon has a defined corruption target.
This is the closest thing in the industry to an enum. Source:
[GW2 wiki: Boon](https://wiki.guildwars2.com/wiki/Boon) plus the individual boon pages **[2nd]**.

| Boon | Exact effect | Stacks | Cap | Corrupts into |
|---|---|---|---|---|
| **Might** | `0.3125 × Level + 5` Power **and** Condition Damage per stack → **+30/+30 at level 80**, +750/+750 at 25 stacks | Intensity | 25 stacks; **no duration cap** | Weakness (5s) |
| **Fury** | **+25%** Critical Chance in PvE (**+20%** in WvW/PvP) | Duration | 30s | Blinded (5s) |
| **Quickness** | **+50%** skill/action activation speed (= **−33%** cast time) | Duration | 30s | Slow (3s) |
| **Alacrity** | **+25%** skill recharge rate (= cooldowns run in **80%** of the time) | Duration | 30s | Chilled (3s) |
| **Protection** | Incoming damage **−33%** | Duration | 30s | Vulnerability (3 stacks, 8s) |
| **Resolution** | Incoming **condition** damage **−33%** | Duration | 30s | Confusion (3 stacks, 5s) |
| **Regeneration** | `(5 + 1.5625 × Level) + 0.125 × Healing Power` HP/s → **130 + 0.125×HP** at 80 | Duration (max 5 sources; **highest healing-power source wins**) | Indefinite | Poisoned (6s) |
| **Aegis** | Block the next incoming attack | Duration | Indefinite | Burning (1 stack, 3s) |
| **Stability** | Immune to knockdown, pushback, pull, launch, stun, daze, float, sink, fear, taunt | Intensity | 25 stacks | Fear (1s) |
| **Swiftness** | Movement speed **+33%** | Duration | **60s** | Crippled (10s) |
| **Vigor** | Endurance (dodge resource) regeneration **+50%** | Duration | 30s | Bleeding (2 stacks, 8s) |
| **Resistance** | Non-damaging conditions currently on you are ineffective | Duration | 30s | Chilled (3s) |

Four structural rules worth copying:

1. **FACT — the target cap is 5.** *"An area of effect skill may only affect a maximum of 5 targets"*,
   and for allies *"closest party members are affected first"*
   ([Area of effect](https://wiki.guildwars2.com/wiki/Area_of_effect)) **[2nd]**. This is the single
   most important number in GW2 support design: it is why raid squads are organised into subgroups of
   five, and it converts "party-wide buff" from an unbounded multiplier into a bounded one.
2. **FACT — two stacking rules only.** Intensity (Might, Stability) or duration (everything else). No
   per-boon special cases.
3. **FACT — every boon has a named inverse.** The corruption table above is the whole counterplay
   system; there is no boon without an answer.
4. **FACT — the duration cap is 30s for nearly everything**, 60s for Swiftness, indefinite for Aegis
   and Regeneration. A cap on *duration* rather than on *strength* is what makes 100% uptime the design
   target rather than a stacking arms race.

**FACT — history shows the numbers came down, hard.** `Alacrity` shipped in October 2015 at **66%**
recharge reduction, was cut to **33%** in January 2016, then to **25%** in February 2018 when it was
reclassified as a boon **[2nd]**. `Quickness` was cut from **+100%** attack speed to **+50%** in 2013
**[2nd]**. **INFERENCE:** a tempo buff is the buff most likely to ship overtuned, because its value is
multiplicative with the entire rest of the character sheet and therefore grows as the game's other
numbers grow.

### 2.2 The general buff taxonomy

| Class | What it does | Shipped examples with numbers | Why it is different |
|---|---|---|---|
| **Flat / percentage stat buff** | Raises a stat | Summoners War `Increase ATK` **+50%**, `Increase DEF` **+70%** **[data]**; GW2 Might +30 Power/stack **[2nd]** | Additive with gear, so its relative value *shrinks* as the game ages — the safest buff to ship |
| **Haste / tempo** | More actions per unit time | GW2 Quickness +50% action speed, Alacrity +25% recharge **[2nd]**; Summoners War `Increase ATK SPD` **+30%** **[data]** | Multiplies *everything*, including other buffs. The most dangerous class |
| **Resource generation** | Fills a resource other actions spend | FFXIV Sage Addersgall; MOBA support gold items | Doesn't change any combat number directly; changes what the recipient can afford to do |
| **Damage amplification on a target (mark / vulnerability)** | The *enemy* takes more | GW2 `Vulnerability` (condition, the Protection corruption); Pokémon `Helping Hand` — *"Boosts the power of the target's moves by 50% until the end of this turn"*, priority +5 **[data]** | Scales with the number of attackers hitting that target — a party-size multiplier hiding in a debuff |
| **Action-economy buff** | Extra turns / extra actions | FFXIV Dancer `Closed Position` designating a Dance Partner (30s) **[1st]**; extra-turn effects in turn-based CCGs | The only buff class whose value is *not* bounded by a percentage. Almost always given a hard charge limit instead |
| **Positioning support** | Moves allies or controls space | GW2 `Swiftness`; Pokémon `Follow Me`/redirection | Value is entirely encounter-dependent, so it is nearly impossible to price with a single number |
| **Burst-window amp** | A large buff on a long cooldown | FFXIV `Divination` — *"Increases damage dealt by self and nearby party members by 6%"*, 20s duration, **120s recast** **[1st]**; Dancer `Devilment` +20% crit and direct-hit rate, 20s, 120s recast **[1st]**; Dancer `Technical Finish` +5% party damage, 20s **[1st]** | Small percentages, long cooldowns, aligned durations. See §4 |

**FACT — aura vs targeted vs party-wide, as shipped:**

| Delivery | Example | Consequence |
|---|---|---|
| Aura (passive radius) | Arknights `Bard` branch — restores nearby allies at **10% of self ATK per second** **[2nd]** | Zero action cost; value is a positioning problem, not a timing one |
| Targeted single-ally | FFXIV Dancer `Closed Position` (exactly one partner) **[1st]**; Pokémon `Helping Hand` **[data]** | Creates a pairing decision; almost always underpowered per the §4 maths unless the recipient is far stronger than the caster |
| Party-wide, capped | GW2 boons at **5 targets** **[2nd]**; FFXIV `Divination` "nearby party members" **[1st]** | The standard. The cap is what stops the buff scaling with group size |
| Party-wide, uncapped | Rare in modern design | **INFERENCE:** absent from every system surveyed here, because an uncapped party buff's value is linear in party size and therefore un-tunable across content types |

---

## 3. The healer-mandatory problem

This is the most-documented failure mode in the whole space, and unlike roster design (see
`06-unsourced.md`), **developers do talk about it on the record.**

### 3.1 The first-party statements

| Source | Statement | What it diagnoses |
|---|---|---|
| Blizzard, *4.1 Preview — Dungeon Finder: Call to Arms*, 2011 **[1st]** ([link](https://worldofwarcraft.blizzard.com/en-gb/news/10002882/41-preview-dungeon-finder-call-to-arms)) | *"The long queue times are, of course, caused by a very simple lack of representation in the Dungeon Finder by tanks, and to some extent healers."* — and crucially: *"We don't feel the tanking and healing roles have any inherent issues that are causing the representation disparity, but simply that fulfilling them is more responsibility. Understandably players prefer to take on that responsibility in more organized situations."* | The shortage is not a power problem. It is a **responsibility** problem. Buffing the healer does not fix it |
| Blizzard, *Introducing Role Queue* (Overwatch), 2019 **[1st]** ([link](https://news.blizzard.com/en-us/overwatch/23060961/introducing-role-queue)) | *"It's not uncommon for players—who may all have different goals and play styles—to feel tension, pressure, disappointment, or even hostility as a team composition comes together."* … *"matchmaking will be calculated using the SR of players' selected roles."* | Solved it by **removing the composition decision from the players entirely** (a hard 2-2-2 lock) and by rating each role separately |
| Aaron Keller, *Director's Take*, 12 Jan 2024 **[1st]** ([link](https://overwatch.blizzard.com/en-us/news/24053284/director-s-take-our-development-values-part-1/)) | *"In Season 9, both Tank and Damage heroes will get a modified, tuned-down version of the Support self-heal passive."* … *"This should take some of the pressure off Support players to keep everyone alive since individual players now have more control of their own health pool."* | Five years after locking the role in, they reduced the *dependency* on it by giving everyone baseline sustain |
| Bungie, *Dev Insights: The Final Shape Abilities Tuning Preview*, May 2024 **[1st]** ([link](https://www.bungie.net/7/en/News/article/tfs-abilities-tuning-preview)) | Well of Radiance *"in its current form, its healing and damage resistance offer effective invulnerability, which removes any other defensive option from consideration on top of providing a sizable boost to your fireteam's offensive output."* | The purest statement of the failure mode: one support option so good it deletes the category |
| Cal Cohen (ArenaNet Skills & Balance Lead), *Guild Wars 2 Balance Philosophy*, Oct 2022 **[1st]** ([link](https://en-forum.guildwars2.com/topic/123508-guild-wars-2-balance-philosophy/)) | Defines PvE roles as **Damage Dealer, Boon Support, Healer**, with Boon Support *"a hybrid role focused on providing high uptime of key offensive boons, though a single build should not provide both quickness and alacrity."* | GW2 re-admitted a role taxonomy ten years after launching without one — but split the *support* role in two so no single build is mandatory |
| ArenaNet, *Studio Update: June 2022* **[1st]** ([link](https://www.guildwars2.com/en/news/arenanet-studio-update-june-2022/)) | *"Having high uptime on critical boons is the strongest indicator of success when playing in group content. Might, fury, quickness, and alacrity all strongly define your ability to successfully tackle challenges."* … *"This means that damage builds will help maintain uptime on these boons and reduce the burden on support roles."* | The **rider strategy**: make damage builds emit buffs as a side effect, so the support slot is topped up rather than solely responsible |

### 3.2 The Well of Radiance numbers — a worked example of "tuned wrong"

**FACT** (Bungie, *The Final Shape* tuning preview) **[1st]**:

| Well of Radiance | Before | After |
|---|---|---|
| Healing | 100 HP/s | **50 HP/s** |
| Damage resistance vs combatants | 40% | **20%** |
| Damage resistance vs bosses | 40% | **10%** |
| Heal on cast | 40 | **300** |

**INFERENCE.** The shape of that change is the interesting part, and it is transferable. They did not
just cut the numbers — they **moved value from sustained prevention into a one-shot restoration burst**
(heal on cast 40 → 300, a 7.5× increase) while cutting the DR by 4× against bosses. That converts a
"stand here and be invulnerable" tool into a "reposition and top up" tool. *Sustained* prevention is
the shape that produces mandatory support; *burst* restoration is not, because it does not remove the
damage check, it only pays for one instance of failing it.

### 3.3 Guild Wars 2's launch experiment, and its partial reversal

**FACT.** GW2 launched in 2012 with no dedicated healer profession; healing was distributed across all
professions and the stated role triad was **Damage / Control / Support** rather than DPS/Tank/Healer
**[3rd]** (contemporaneous coverage:
[Game Developer](https://www.gamedeveloper.com/design/is-i-guild-wars-2-i-the-answer-to-stagnant-mmo-design-),
[Ten Ton Hammer](https://www.tentonhammer.com/articles/guild-wars-2-no-more-healers)).

**FACT.** By October 2022 ArenaNet's own published balance philosophy lists **Healer** as a first-class
PvE role again, alongside **Boon Support** **[1st]**.

**INFERENCE — the most useful lesson in this document.** Removing the dedicated healer did not remove
the mandatory support slot; it **relocated** it. Ten years later the compulsory thing in a GW2 group is
not a healer, it is *quickness and alacrity uptime* — ArenaNet says so themselves: high boon uptime is
*"the strongest indicator of success"* **[1st]**. The lesson is that group content will always develop
a mandatory support axis if support effects are multiplicative and uptime-based; the design choice is
only **which** axis, and **how many different builds can supply it**. ArenaNet's answer was to spend
the entire June 2022 patch spreading alacrity and quickness sources across professions.

### 3.4 The counterplay lever: anti-heal

**FACT.** OW2 Season 9 added a Damage-role passive that *"decreases healing done to players that
they've damaged recently"* — Aaron Keller: *"The idea here is to give Damage players an increased
ability to secure kills as well as to mitigate the abilities of Support heroes to keep targets alive"*
**[1st]** ([Director's Take – Building on Feedback](https://news.blizzard.com/en-us/article/24064843/directors-take-building-on-feedback)).
Reported values: **−20% healing received for 2s** **[3rd]**.

**INFERENCE.** Anti-heal is the structural counterpart to the shield-strip. A restoration system with
no healing-reduction channel has no way to make a kill land against a competent healer other than
raising damage until it one-shots — which breaks every other tuning target at once. **A healing-received
multiplier is cheaper than a damage buff.**

---

## 4. The opposite failure — support nobody takes, and the arithmetic of a turn

### 4.1 The break-even threshold (computed)

This is the sharpest question for a turn-based game, and the published material on it is thin. The one
worked example found is a forum thread on the RPG Maker forums, by user **LordOfPotatos** **[3rd]**
([thread](https://forums.rpgmakerweb.com/threads/balancing-stats-buffs-debuffs.145029/)):

> 25% damage buff: `100+100+100+100+100+100 = 600` (six turns of attacking) versus
> `0+125+125+125+125+125 = 625` (buff once, then attack five times) — *"you have to attack for at least
> 5 turns with the buff active to get any benefit."*
>
> 50% via two buff turns: `600` versus `0+0+150+150+150+150 = 600` — *"you must attack 4 times after
> buffing to break even, 5 times to get any benefit."*

The general principle is stated (but not computed) by *final boss blues*, "RPG Skills: What You're Doing
Wrong" **[3rd]** ([link](https://finalbossblues.com/skills-what-youre-doing-wrong/)): *"the damage that
would be dealt (or prevented, in the case of sleep or paralysis) as a long-term result of the status
must be more than the damage that could be dealt that turn just by attacking."*

**The general form (computed here; not found published anywhere):**

Let a buff cost `k` turns, multiply damage by `m`, and last for `N` of the caster's subsequent attack
turns. Self-buff, one character:

```
buffed total   = N · m · d
unbuffed total = (N + k) · d
break-even:      N · m ≥ N + k    →    N ≥ k / (m − 1)
```

| Buff strength `m` | Self-buff (`k = 1`) turns needed to break even | Minimum fight length for it to ever pay |
|---|---|---|
| +10% (1.10) | **10** | 11 turns |
| +25% (1.25) | **4** | 5 turns |
| +50% (1.50) | **2** | 3 turns |
| +100% (2.00) | **1** | 2 turns |

**The party term is the one that matters.** If the buff lands on `P` allies, each dealing `d_a`, and
the caster gives up its own turn worth `d_b`:

```
gain = N · P · (m − 1) · d_a      cost = k · d_b
with d_a = d_b:    N ≥ k / (P · (m − 1))
```

| `m` | P = 1 | P = 2 | P = 3 | P = 5 |
|---|---|---|---|---|
| +10% | 10 turns | 5 | 3.3 | 2 |
| +25% | 4 | 2 | 1.3 | 0.8 |
| +50% | 2 | 1 | 0.7 | 0.4 |

**Four conclusions follow directly, and all four match shipped games:**

1. **A single-target damage buff is almost always a trap.** Pokémon's `Helping Hand` gives +50% to one
   ally's move for one turn **[data]** — `gain = 0.5 × d_a` against `cost = d_b`. It only pays if the
   ally hits for **more than twice** what the user would have hit for (computed). That is exactly why it
   is a niche move used for its +5 priority and its role in specific combos, not as a generic action.
2. **Party-wide buffs can be much weaker and still correct.** FFXIV's `Divination` is only **+6%** — but
   it hits the whole party for 20s on a 120s recast **[1st]**, and Astrologian pays a fraction of a GCD,
   not a whole turn. With P = 7 and a ~7-GCD window, the arithmetic is overwhelming.
3. **The break-even count sets a minimum encounter length.** A +25% self-buff in a game where fights
   end in four turns is *mathematically dead* no matter how it is presented. **INFERENCE: encounter
   length is a support-balance parameter, and it is usually tuned by a different person than the one
   tuning the buff.**
4. **The real fix is to make the buff cost zero turns.** Every shipped solution below is a way to move
   the buff off the action economy.

### 4.2 The five shipped ways to make support worth the action

| Strategy | Shipped as | Effect on the arithmetic |
|---|---|---|
| **Rider** — the buff is a side effect of an action taken anyway | ArenaNet, June 2022: *"damage builds will help maintain uptime on these boons and reduce the burden on support roles"* **[1st]**; FFXIV Scholar `Adloquium` heals **and** shields in one cast **[1st]** | `k → 0`. The threshold vanishes entirely. **The single most effective answer found** |
| **Off-GCD / free action** | FFXIV `Divination`, `Devilment` are abilities, not spells — they do not consume the global cooldown **[1st]** | `k → ~0`. Same effect, different mechanism |
| **Aura** — permanent, positional | Arknights `Bard` branch heals nearby allies at 10% of ATK per second, passively **[2nd]** | `k = 0`, cost paid in deployment slot and positioning |
| **Long duration vs short fight** | GW2 boons cap at 30s duration and are re-applied constantly, targeting 100% uptime **[2nd]** | Maximises `N` rather than `m` |
| **Big `m` on a long cooldown** | Dancer `Devilment` +20% crit/direct-hit, `Technical Finish` +5% party, 20s windows on ~120s parent cooldowns **[1st]** | Large enough that even `k = 1` clears the bar |

**FACT — the alignment consequence.** Because burst-window buffs are only worth their action if the
party's damage is concentrated inside the window, FFXIV's raid buffs converged on a common **120-second**
recast: `Divination` 120s, `Devilment` 120s **[1st]**. **INFERENCE:** any game that ships several
cooldown-gated party buffs will have their periods converge on a common multiple whether it intends to
or not, because non-aligned buffs are strictly worse. If several are shipped, **give them the same
period on purpose.**

---

## 5. Sustain in games with no dedicated healer

| Game / family | Sustain model | Numbers | The trade being made |
|---|---|---|---|
| **Path of Exile** | Leech + regen + flasks + recoup, all separate recovery *types* | Per-instance leech recovers ~**2% of the pool per second**, capped at 10% of the pool per instance; **total leech rate capped at 20% of max life per second**, raised only by explicit *"increased Maximum total Recovery per second from Life Leech"* modifiers **[2nd]** | Sustain is a build stat, not an action. The cap exists because leech scales with damage, and damage in PoE is unbounded |
| **Diablo-likes** | Cooldown potion + on-kill globes | (see *What I could not find*) | Removes the pre-combat potion-stacking chore; makes sustain a rhythm rather than an inventory |
| **Souls-likes** | Finite charges (Estus), refilled at checkpoints | — | Converts healing from a resource-management problem into a **risk/timing** problem: the cost is the animation, not the charge |
| **Overwatch 2** | Universal passive regen | **20 HP/s after 5s undamaged**, halved delay (2.5s) for Support **[3rd]** | Removes the "wait for a healer" dead time. Paid for with an anti-heal passive on Damage **[1st]** |
| **Guild Wars 2 (2012 design)** | Every profession has a self-heal on its own slot; `Vigor` boosts dodge-endurance regen **+50%** **[2nd]** | — | **Dodge as sustain**: damage avoided is damage not needing healing. Only works if attacks are telegraphed and avoidable |
| **Roguelites** | Healing as a scarce reward drop | — | Sustain becomes a *run-level* economy rather than a fight-level one |

**INFERENCE — the transferable rule.** Every no-healer game replaces the healer with something that has
**no action cost and a hard rate cap**: leech (capped as a rate), passive regen (capped by a delay),
charges (capped by count), dodge (capped by an endurance pool). None of them replaced the healer with a
stronger self-heal button, because a self-heal button is still an action and still faces the §4
threshold — it just moves the "wasted turn" feeling from the support player to everyone.

**FACT — lifesteal is the mechanically distinct one** because it is the only sustain in the list whose
value scales with *offence*. PoE's hard total cap of 20% of max life per second exists precisely because
of that double-scaling **[2nd]**.

---

## 6. Support fraction in creature-collection rosters — the transferable number

This is the section where hard counts exist. **Roster fraction is the number that transfers**, because
it says how much of a generated roster should be support-shaped.

### 6.1 Summoners War (computed from the SWARFARM API — [data])

Queried `https://swarfarm.com/api/v2/monsters/?obtainable=true&archetype=<x>&limit=1` and read the
`count` field. Com2uS assigns every monster exactly one `archetype`.

| Archetype | Obtainable monsters | Share |
|---|---|---|
| Attack | 892 | 45.1% |
| **Support** | **476** | **24.1%** |
| HP | 358 | 18.1% |
| Defense | 228 | 11.5% |
| Material | 23 | 1.2% |
| **Total** | **1,977** | 100% |

(computed; the five archetype counts sum to exactly the unfiltered `obtainable=true` count of 1,977,
so the tally is complete and self-consistent. Counts are per element and per awakening stage, which is
how SWARFARM stores them — not per family.)

**Excluding the 23 non-combat Material monsters: Support = 476 / 1,954 = 24.4%** (computed).

**FACT — Summoners War's buff numbers are large and flat.** From the SWARFARM skill-effect table
**[data]**: `Increase ATK` **+50%**, `Increase DEF` **+70%**, `Increase ATK SPD` **+30%**, plus
`Immunity`, `Invincible`, `Shield`, `Soul Protection` (auto-revive at 30% HP), `Heal`, `Recovery`
(HoT), `Revive`, `Cleanse`, `Remove Buff`, `Decrease Buff Duration`. 140 distinct skill effects in
total.

**INFERENCE.** Those percentages are far larger than GW2's or FFXIV's because Summoners War is
**turn-based**: a buff must clear the §4 threshold against a whole turn, and it does so by being big.
A +6% party buff would be worthless in a turn-based game with 10-turn fights; +50% ATK for 2–3 turns on
four allies clears the bar by a wide margin. **This is the clearest cross-genre confirmation that the
buff-magnitude constant is set by the action economy, not by taste.**

### 6.2 Arknights

Source: `arknights.wiki.gg` **[2nd]**, corroborating the `character_table.json` count of 425 recorded
in `06-unsourced.md`.

| Class | Operators | Share |
|---|---|---|
| Guard | 76 | 17.8% |
| Specialist | 55 | 12.9% |
| Sniper | 54 | 12.6% |
| Caster | 51 | 11.9% |
| **Supporter** | **51** | **11.9%** |
| Defender | 48 | 11.2% |
| **Medic** | **47** | **11.0%** |
| Vanguard | 46 | 10.7% |
| **Total** | **428** | 100% |

**Medic + Supporter = 98 / 428 = 22.9%** (computed). Note a snapshot discrepancy: the individual
[Medic](https://arknights.wiki.gg/wiki/Medic) and [Supporter](https://arknights.wiki.gg/wiki/Supporter)
pages state 43 and 49 respectively, giving 21.5%. **Honest range: 21.5–22.9%.**

**FACT — Arknights sub-taxonomises healing into 7 named Medic branches** with distinct mechanical
identities **[2nd]**:

| Branch | Mechanic |
|---|---|
| Medic | Single-target; heals for 100% of ATK |
| Multi-target Medic | Heals 3 allies per heal, shorter range, lower per-target output |
| Therapist | Long range; healing on farther targets reduced to **80%**; specialises in status removal |
| Wandering Medic | Heals **and** recovers Elemental Injury for **50% of ATK** — can act on a full-HP target |
| Incantation Medic | Attacks enemies for Arts damage and heals an ally for **50% of the damage dealt** (lifesteal-by-proxy) |
| Chain Medic | Bounces between 3 allies; healing reduced **25% per bounce** |
| Watchman | Single-target healer that can avoid ground attacks |

Supporter has **8** branches: Decel Binder (slow), Summoner, Hexer (debuff), Bard (passive aura,
**10% of self ATK/s**), Abjurer (attacks heal allies for **75% of ATK**), Artificer (deployable
devices), Ritualist, Supportive Ranger **[2nd]**.

**INFERENCE — this is the strongest structural finding for a generated roster.** Arknights gets 15
mechanically distinct support identities out of ~98 units by varying **four axes only**: target count
(1 / 3 / bounce / aura), a range-based or bounce-based falloff percentage, whether the heal is a rider
on an attack, and what secondary state it removes. That is a small generator vocabulary producing a
large distinct set — the exact property this project needs from a seed contract.

### 6.3 Epic Seven and Pokémon

| System | Support-shaped share | Confidence |
|---|---|---|
| **Epic Seven** | **Soul Weaver** is 1 of 6 classes; a count of **33** Soul Weavers is reported by game8 **[3rd]**, against a total hero count that `06-unsourced.md` already records as contested (277 / 299 / 255 / "over 300"). Against the honest ~280–300 range that is **11–12%** | **Low.** `api.epicsevendb.com` is dead (DNS failure); `epic7db.com/heroes` returned truncated |
| **Pokémon (move pool, not roster)** | **937** moves total. **338** are status moves (36.1%). The `heal` meta-category holds **14** moves (1.5%); `damage-heal` (drain moves) holds **11** (1.2%) **[data, PokéAPI]** | **High** (computed from PokéAPI) |

**FACT.** Only **25 of 937** Pokémon moves (2.7%, computed) are direct restoration. Pokémon's support
space is almost entirely **stat manipulation, field effects and redirection**, not healing.

**INFERENCE.** Pokémon is a useful negative control: in a format where fights are short (§4 threshold
bites hard) and both sides can attack the healer, direct healing is deliberately rare and mostly
appears as a *rider* on damage (drain moves) or as a passive (`Leftovers`, `Regenerator`). A
creature-collection game with short fights should expect its restoration vocabulary to be small and its
buff vocabulary to be large — the reverse of an MMO.

### 6.4 The transferable band

**Two independent, differently-built creature-collection rosters land in the same place:**

| Game | Support-shaped share of roster |
|---|---|
| Summoners War (`Support` archetype) | **24.1%** (computed) |
| Arknights (Medic + Supporter) | **21.5–22.9%** (computed) |
| Epic Seven (Soul Weaver only — healers, no buffers) | ~11–12% (low confidence) |

**INFERENCE.** Roughly **one unit in four** is support-shaped when "support" is defined broadly
(healing + buffing + debuffing + utility), and roughly **one in eight to one in nine** when it is
defined narrowly as *healer*. Epic Seven's lower number is consistent rather than contradictory: Soul
Weaver is the healer class specifically, while Summoners War's `Support` and Arknights' `Supporter`
both fold in debuffers and utility units.

---

## 7. Cleansing and counterplay

### 7.1 Is cleanse its own action, or a rider?

**FACT.** In Guild Wars 2 it is overwhelmingly a **rider**. The wiki's own summary: condition removal
is distributed across weapon skills, healing skills, utility skills, profession mechanics, traits and
equipment, and most cleanses *"function as secondary effects... rather than as standalone cleansing
abilities"* **[2nd]**. Removal order is **most-recently-applied first**, and a typical cleanse removes
**one** condition, with named exceptions removing three **[2nd]**.

**FACT.** In Summoners War it is a **discrete effect with its own id** — `Cleanse` (34) — alongside
`Immunity` (9), which pre-empts rather than removes **[data]**.

**FACT.** In Arknights it is a **branch identity**: the Therapist branch is defined by status removal,
and the Wandering Medic branch by removing Elemental Injury — and can do it *on a full-HP target*, which
means the cleanse is decoupled from the heal **[2nd]**.

**INFERENCE.** Three shipped answers, and the choice determines everything downstream:

| Cleanse as… | Consequence |
|---|---|
| A rider on heals | Cleansing is never a turn cost, so debuffs must be individually weak or applied in volume. GW2's condition system exists in this shape: many small stacking conditions, cleansed one at a time |
| Its own action | Each debuff must be worth an enemy action *and* an ally action to remove — so debuffs must be individually strong. Summoners War's shape |
| A unit's whole identity | Cleansing becomes a roster-slot decision, checked before the fight, not during it. Arknights' shape |

### 7.2 The apply/remove arms race, as shipped

**FACT — Guild Wars 2 built the arms race into the vocabulary itself.** Every one of the 12 boons has a
defined **corruption** target — the boon is not removed, it becomes its inverse condition **[2nd]**:

| Boon | Becomes |
|---|---|
| Aegis | Burning (1 stack, 3s) |
| Alacrity | Chilled (3s) |
| Fury | Blinded (5s) |
| Might | Weakness (5s) |
| Protection | Vulnerability (3 stacks, 8s) |
| Quickness | Slow (3s) |
| Regeneration | Poisoned (6s) |
| Resistance | Chilled (3s) |
| Resolution | Confusion (3 stacks, 5s) |
| Stability | Fear (1s) |
| Swiftness | Crippled (10s) |
| Vigor | Bleeding (2 stacks, 8s) |

**INFERENCE — this is the most elegant solution found in the whole pass.** Corruption is a *closed
mapping* rather than a new mechanic: it costs one table, it makes buff-stacking self-punishing (the
more boons you carry, the more debuffs a single corrupt applies), and it means no new counter-action
needed to be designed. It also means **every buff automatically has counterplay by construction** —
you cannot ship a boon without answering "and what does it become when corrupted?"

**FACT — the ladder in a turn-based collector, from the Summoners War effect list [data]:**

```
apply debuff  →  Cleanse (34) removes it
              →  Immunity (9) prevents it landing at all
              →  Remove Buff (37) strips the Immunity
              →  Decrease Buff Duration (68) shortens what survives
```

Four distinct effect ids, each answering the one before it. **INFERENCE:** this is what an arms race
looks like when it is *deliberate* — every rung is a separate, nameable effect with its own tuning
knob, rather than a percentage on an existing one. The alternative (a single "debuff resistance" stat)
collapses the whole ladder into one number and removes every interesting decision on it.

**FACT — a protection that does not block removal.** GW2's `Resistance` makes non-damaging conditions
*ineffective* while active but does **not** prevent them being applied or removed **[2nd]**. Suppression
and removal are separate channels.

### 7.3 Per-mechanism ledger

| Mechanism | Solves for the player | Costs the designer | Breaks when tuned wrong |
|---|---|---|---|
| Cleanse (removal) | "I am under a state I cannot play through" | Every debuff must now be priced *net of* expected cleanse rate | Too available → debuffs stop existing as a design space. Too scarce → a single debuff is a death sentence and the game becomes a coin flip on who applies first |
| Immunity (pre-emption) | Removes the reaction requirement entirely | Creates a binary: the debuff either lands or does nothing | Too long → whole enemy kits read as blank. This is the effect most likely to make an entire roster axis unplayable |
| Strip / purge | Answers the opponent's buffs | Needs a removal *order* rule, which players will optimise against | Too strong → buffing is never correct, and the support role dies from the other direction |
| Corruption (convert) | Punishes over-buffing | One mapping table, permanently | Too strong → stacking buffs becomes a liability and boon-support builds are unplayable into that matchup |
| Duration reduction | A soft answer that avoids the binary | An extra multiplier on every effect's duration | Mostly safe; this is the least dangerous rung on the ladder |

---

## What I could not find

**This section is mandatory and is genuinely non-empty. Add to it rather than re-running these.**

1. **⛔ No published break-even mathematics for buffs in turn-based combat, from any developer.** This
   was searched hardest. The only worked arithmetic found anywhere is a **forum post by a hobbyist**
   (LordOfPotatos, RPG Maker forums) and a qualitative statement of the principle on a small game-dev
   blog (*final boss blues*). **No studio, no GDC talk, and no academic paper stating the threshold was
   located.** The general formula in §4.1 is derived here, not borrowed. If this project wants a number
   for how strong a buff must be, **it is deriving it.** (This mirrors the `06-unsourced.md` finding
   about counter-strength targets — designers ship the numbers and never publish the model.)

2. **⛔ No first-party Square Enix statement on FFXIV healer design was retrieved.** The discourse is
   heavily covered by press (PC Gamer, Kotaku) and by the community "healer strike" of 2024, and
   Yoshida is widely paraphrased as acknowledging job homogenisation and an 8.0 overhaul — but **every
   route to a verbatim, attributable Yoshida quote on healer design was a paraphrase in a secondary
   outlet.** Live Letter transcripts were not reachable, and the WebSearch budget ran out before the
   Lodestone dev-blog route could be tried. **The FFXIV *numbers* in this document are first-party**
   (the official Lodestone job guides for White Mage, Scholar, Sage, Astrologian and Dancer, all
   readable via the `r.jina.ai` proxy) — **only the design commentary is missing.** Next attempt should
   go straight to `na.finalfantasyxiv.com/lodestone/special/` and the Live Letter digest pages.

3. **⛔ No first-party Riot or Valve statement on MOBA support-role redesign was retrieved.** The
   Ancient Coin / Relic Shield / Spellthief's Edge line and its rationale surfaced only through wikis
   and third-party guides. The original Riot dev-corner posts appear to be on decommissioned boards
   (`boards.na.leagueoflegends.com`, `forums.na.leagueoflegends.com`) — the same class of dead-link
   problem `06-unsourced.md` records for `us.battle.net`. **The Wayback CDX index trick recorded there
   is the obvious next move and was not attempted** (budget).

4. **⛔ No Blizzard statement on Discipline Priest's Atonement redesign** (heal-by-dealing-damage) was
   retrieved — the search budget ran out on this exact query. This is likely findable; it is the
   canonical first-party answer to "healing is boring", and it belongs in §4 when someone gets it.

5. **⛔ Diablo's sustain numbers are unsourced here.** The potion cooldown, health-globe design and the
   life-steal-to-life-per-hit conversion at level 60 in Reaper of Souls are all well known but **no
   citation was obtained**; the official Diablo III game-guide pages are decommissioned and search
   budget was gone. §5's Diablo row is deliberately left without numbers rather than filled from memory.

6. **⛔ Path of Exile's leech numbers are second-tier only.** `poewiki.net` is behind Anubis and returns
   403 both directly and through the `r.jina.ai` proxy; `pathofexile.fandom.com` is behind the sitewide
   Fandom 402 already recorded in `06-unsourced.md`. The figures in §5 (2%/s per instance, 10% per
   instance cap, 20% of max life per second total) come from **search-result extracts of the poewiki
   page, not from the page itself.** Treat them as approximately right and re-verify before using them
   as a model.

7. **⛔ Epic Seven's roster totals remain unresolved** — `api.epicsevendb.com` no longer resolves (DNS
   failure) and `epic7db.com/heroes` truncated. The 33-Soul-Weaver figure is a **[3rd]** aggregator
   number. This compounds the four-way hero-count conflict already logged in `06-unsourced.md` §4.

8. **⛔ No TFT or auto-battler support-fraction count was attempted** — the section 6 budget went to the
   three rosters with machine-readable data. TFT's "support" is expressed through traits and items
   rather than unit roles, so the roster-fraction question may not even be well-posed there.

9. **⛔ The OW2 Season 9 self-heal numbers (20 HP/s, 5s / 2.5s delay, −20% healing for 2s) are [3rd].**
   Aaron Keller's Director's Take confirms the *design intent* first-party but *"does not include
   numerical data"*; the official patch-notes page is a rolling URL and the S9 archive was not located.

10. **⛔ GW2's `Boon removal` and `Target cap` wiki pages returned 403 even through the proxy.** The
    5-target cap in §2.1 is sourced from the `Area of effect` page instead, which does carry it
    verbatim. Boon-strip *ordering* rules (which boon goes first) were not obtained — only the
    corruption mapping, which was.

---

## Source ledger

| Tier | Source | Used for |
|---|---|---|
| **[1st]** | [Blizzard, *4.1 Preview — Dungeon Finder: Call to Arms*](https://worldofwarcraft.blizzard.com/en-gb/news/10002882/41-preview-dungeon-finder-call-to-arms) | Why nobody queues as healer/tank |
| **[1st]** | [Blizzard, *Introducing Role Queue*](https://news.blizzard.com/en-us/overwatch/23060961/introducing-role-queue) | Role lock, per-role SR |
| **[1st]** | [Aaron Keller, *Director's Take: Our Development Values, Part 1*](https://overwatch.blizzard.com/en-us/news/24053284/director-s-take-our-development-values-part-1/) | Universal self-heal passive, support pressure |
| **[1st]** | [Aaron Keller, *Director's Take – Building on Feedback*](https://news.blizzard.com/en-us/article/24064843/directors-take-building-on-feedback) | Anti-heal Damage passive |
| **[1st]** | [Bungie, *Dev Insights: The Final Shape Abilities Tuning Preview*](https://www.bungie.net/7/en/News/article/tfs-abilities-tuning-preview) | Well of Radiance / Ward of Dawn numbers and rationale |
| **[1st]** | [ArenaNet, *Studio Update: June 2022*](https://www.guildwars2.com/en/news/arenanet-studio-update-june-2022/) | Boon uptime as success predictor; the rider strategy |
| **[1st]** | [Cal Cohen, *Guild Wars 2 Balance Philosophy*](https://en-forum.guildwars2.com/topic/123508-guild-wars-2-balance-philosophy/) | The Damage / Boon Support / Healer taxonomy |
| **[1st]** | FFXIV Lodestone job guides — [White Mage](https://na.finalfantasyxiv.com/jobguide/whitemage/), [Scholar](https://na.finalfantasyxiv.com/jobguide/scholar/), [Sage](https://na.finalfantasyxiv.com/jobguide/sage/), [Astrologian](https://na.finalfantasyxiv.com/jobguide/astrologian/), [Dancer](https://na.finalfantasyxiv.com/jobguide/dancer/) | All FFXIV potencies, shield percentages, buff percentages, recasts |
| **[data]** | [SWARFARM API v2](https://swarfarm.com/api/v2/monsters/) | Summoners War archetype counts (computed), 140 skill effects with exact buff percentages |
| **[data]** | [PokéAPI](https://pokeapi.co/api/v2/) | 937 moves, 338 status, 14 heal, 11 damage-heal, Helping Hand text |
| **[2nd]** | GW2 wiki — [Boon](https://wiki.guildwars2.com/wiki/Boon), [Might](https://wiki.guildwars2.com/wiki/Might), [Fury](https://wiki.guildwars2.com/wiki/Fury), [Quickness](https://wiki.guildwars2.com/wiki/Quickness), [Alacrity](https://wiki.guildwars2.com/wiki/Alacrity), [Regeneration](https://wiki.guildwars2.com/wiki/Regeneration), [Barrier](https://wiki.guildwars2.com/wiki/Barrier), [Condition removal](https://wiki.guildwars2.com/wiki/Condition_removal), [Area of effect](https://wiki.guildwars2.com/wiki/Area_of_effect) | The full boon table, corruption mapping, 5-target cap |
| **[2nd]** | [arknights.wiki.gg — Class](https://arknights.wiki.gg/wiki/Class), [Medic](https://arknights.wiki.gg/wiki/Medic), [Supporter](https://arknights.wiki.gg/wiki/Supporter) | Per-class operator counts, 7 Medic + 8 Supporter branches with mechanics |
| **[2nd]** | [poewiki.net — Leech](https://www.poewiki.net/wiki/Leech) (fetch blocked; figures via search extract) | PoE leech rates and caps |
| **[3rd]** | [RPG Maker forums, *Balancing stats buffs/debuffs*](https://forums.rpgmakerweb.com/threads/balancing-stats-buffs-debuffs.145029/) | The only worked buff break-even arithmetic found |
| **[3rd]** | [final boss blues, *RPG Skills: What You're Doing Wrong*](https://finalbossblues.com/skills-what-youre-doing-wrong/) | The break-even principle, stated qualitatively |
| **[3rd]** | [Game Developer](https://www.gamedeveloper.com/design/is-i-guild-wars-2-i-the-answer-to-stagnant-mmo-design-), [Ten Ton Hammer](https://www.tentonhammer.com/articles/guild-wars-2-no-more-healers) | GW2's 2012 no-healer launch framing |
| **[3rd]** | [game8 — Soul Weaver list](https://game8.co/games/Epic-Seven/archives/272486) | Epic Seven Soul Weaver count (low confidence) |
