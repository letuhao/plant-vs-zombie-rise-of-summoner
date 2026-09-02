# Cost-side mistuning — documented cases, mechanisms, and fixes

**Captured 2026-09-02.** Companion to [06-action-cost-models.md](06-action-cost-models.md), whose §7
("what breaks") is the short version. This file is the long one: the cases, with patch numbers, dates,
and first-party quotes where they survive.

Research only. No proposals, no design.

**Source labels:** **[official]** = developer patch notes, announcements or dev columns.
**[wiki]** = second-tier, transcribes official notes — generally reliable for numbers, weaker on dates.
**[3rd]** = third-tier, used only where nothing better was reachable and flagged inline.

---

## The finding in one paragraph

**Every case below is the same defect wearing different clothes: the cost channel stopped measuring the
thing that was actually growing.** Sometimes a route bypassed the channel entirely (Path of Exile's
triggers), sometimes a second subsystem reimbursed the cost (Magic's Grief, Yu-Gi-Oh's Painful Choice),
sometimes the cost stat was wired into a second output so that paying more made you stronger (Archmage,
Indigon, Captain Crimson's), and sometimes the operators simply did not commute (Hearthstone's Naga Sea
Witch). The two fixes that actually held across games are **change the stat's shape so no cap is
needed** — Riot replacing percentage CDR with additive Ability Haste — and **bound the frequency rather
than the effect**, which was enough to take Yu-Gi-Oh's Firewall Dragon from Forbidden all the way back
to Unlimited. And one operational pattern recurs everywhere: **cost errors get reverted far faster than
damage errors**, because players feel them on every button press.

---

## 1. Net-negative cost — the engine that pays for itself

### 1.1 Magic: the Gathering, Urza block and "Combo Winter"

**FACT [official].** Mark Rosewater's own post-mortem names the cost mechanic as the defect. On the
Urza's Saga "free" spells: they *"untap a number of lands equal to the spell's converted mana cost,"*
which *"proved to be fundamentally broken"* because **"the free mechanic had the weird property of
improving as the mana cost was raised."**
([Make No Mistake](https://magic.wizards.com/en/news/making-magic/make-no-mistake-2003-11-10))

Same article, on Tinker: *"all the 'extra mana payment' text seemed a little clunky. So I took it off.
Woops."*

**FACT [official].** Elsewhere he lists Dream Halls as a mistake for exactly one reason: *"it breaks a
very fundamental rule of Magic. **It allows player to play a spell without paying its mana cost.**"*
([Mistakes? I've Made a Few](https://magic.wizards.com/en/news/making-magic/mistakes-ive-made-few-2002-11-11-0))

**The 1998–99 ban wave, all cost-side [wiki, mtg.wiki B&R timeline]:**

| Card | Action | Cost defect |
|---|---|---|
| Tolarian Academy | Banned Dec 1998 | A land producing arbitrary mana — inverts the one-land-per-turn cap |
| Time Spiral | Banned Mar 1999 | Untaps six lands on resolution — **net negative cost** |
| **Memory Jar** | **Emergency ban announced 1 Mar 1999, effective 1 Apr 1999** | Draw seven for `{4}`, where the `{4}` was notional with fast mana |
| Dream Halls | Banned Mar 1999 | **Cost-setting** — replaces every mana cost with a discard |
| Earthcraft · Fluctuator · Lotus Petal · Recurring Nightmare | Banned Mar 1999 | Untappers, cycling-to-`{0}`, zero-cost mana, free recursion |

Magic's only emergency ban is corroborated **[3rd]** by Randy Buehler: the card *"has the unfortunate
text 'draw seven cards' on it"*, and it was done because *"the very health of the Magic game was being
threatened by 'Combo Winter'."*

**FACT [official], and the transferable line.** Banning Candelabra of Tawnos in Legacy (29 Jun 2026):

> *"it rarely shows up unless it is in a problematically strong combo deck, where it is often the
> highest value-above-replacement card… We are concerned that trying to ban around Candelabra would
> result in similar situations in the future."*

**That is the explicit statement that nerfing payoffs while the free enabler stays legal is a
treadmill.**

### 1.2 Hearthstone

All **[wiki, hearthstone.wiki.gg]** — Blizzard's own reasoning posts were unreachable.

| Card | Change | Patch / date |
|---|---|---|
| **Innervate** | "Gain **2** Mana Crystals this turn only" → **1** | 9.1.0.20970, 18 Sep 2017 |
| **Preparation** | 0-cost, next spell costs (2) less → buffed to **(3)** in beta → **nerfed back to (2)** | 14.2.2.31022, 22 May 2019 |
| **Sorcerer's Apprentice** | cost 2 → **4** (Jan 2022) → back to **2 with a floor**: "Your spells cost (1) less **(but not less than 1)**" | 30.2.2.206433, 29 Aug 2024 |
| **Naga Sea Witch** | cost **5 → 8** | 11.1.1.24589, 22 May 2018 |
| **Time Warp** | "Take an extra turn." → **"(Once per game)"** added | 29.2.2.198608, 25 Apr 2024 |
| **Shudderwock** | **no text or cost change at all** — Battlecry cap 30 → **20**, plus **doubled animation speed** | 11.1.0.24377, 8 May 2018 |
| Shudderwock walk-back | cap raised back to **30** | 11.2.0.24769, 5 Jun 2018 |

Two cases worth isolating.

**⭐ Naga Sea Witch is the cost-operator commutativity bug.** The card reads "Your cards cost (5)" — a
*set-cost*. Patch 9.0.0.20457 (8 Aug 2017) changed evaluation order so its modifier applied **before**
other cost modifiers, which meant Giants' self-discounts subtracted from 5 and reached **0**. The card's
entire balance lived in the evaluation order, not in any number. **A set-cost operator and a
reduce-cost operator do not commute.** (Ordering change and nerf are FACT; the causal chain is
INFERENCE.)

**⭐ Shudderwock's fix was a runtime-budget fix, not a balance fix** — an iteration cap plus doubled
animation speed, and the cap was relaxed again once wall-clock was under control. **INFERENCE: the first
thing a cheap-to-iterate loop breaks is the frame budget, not the win rate.**

**Never nerfed [wiki]:** Mecha'thun, Kingsbane, Emperor Thaurissan (only ever made *cheaper*).
**INFERENCE:** these are infinite-*value* loops, not infinite-*cost* loops — slow, so they lose to a
clock. The revealed preference is that cost and tempo defects get text changes; value defects get left
alone.

### 1.3 Yu-Gi-Oh — Firewall Dragon, and the cleanest lesson in this file

**FACT [wiki/API, yugipedia `action=parse`].** The pre-errata text: *"If a monster this card points to
is destroyed by battle or sent to the GY: You can Special Summon **1 monster** from your hand"* — **with
no once-per-turn clause anywhere on the card.** Post-errata: *"1 **Cyberse** monster… **You can only use
each effect of 'Firewall Dragon' once per turn.**"*

Status: TCG Limited Feb 2018 → **Forbidden Dec 2018** → **Unlimited Mar 2021**.

> **⭐ The fix was frequency, not effect.** Adding "once per turn per effect" was enough to return a
> Forbidden card all the way to Unlimited. An unlimited-frequency free refund is the bug; the refund
> itself was fine.

**FACT [wiki].** **Painful Choice** (Forbidden since 2004) is the purest cost-design failure available.
The intended cost is "your opponent picks, so you get the worst of five." The actual cost is zero,
because in a graveyard-resource game the four "discarded" cards go exactly where you wanted them.
**A cost is only a cost if no other subsystem reimburses it.**

Konami's consistent choice elsewhere is to forbid the *outlet* (Cannon Soldier, Mass Driver) rather than
the generator — the opposite of the Firewall decision, and plausibly why those are still banned twenty
years later.

---

## 2. Cooldown reduction stacking toward zero

### 2.1 ⭐ League of Legends — the 40% cap, and why it was removed

**FACT [official], Riot's own diagnosis:**

> *"CDR's power stacks exponentially… **10% CDR is 11% more casts, while 40% CDR is 66% more casts, and
> 50% CDR is 100% more casts**,"* and *"CDR's multiplicative scaling becomes so powerful that we have to
> have a 40% cap to limit its power."*
> ([Preseason 2021 Champion Class Item Goals](https://www.leagueoflegends.com/en-us/news/riot-games/preseason-2021-champion-class-item-goals/))

**FACT [official].** Patch **10.23** (10 Nov 2020) shipped the replacement, and named the cap's own cost:

> *"CDR had to be capped at 40% to avoid going out of control, **locking you out of a lot of items once
> you reached that cap**"*; *"Every point of ability haste lets you cast 1% faster"*; *"This linear
> scaling allows us to remove the cap, so you can buy as much as you want."*
> ([Patch 10.23](https://www.leagueoflegends.com/en-us/news/game-updates/patch-10-23-notes/))

```
old:  effective casts ∝ 1/(1 − CDR)        →  superlinear, needs a cap
new:  cooldown = base × 100/(100 + haste)  →  linear in casts, no cap needed
```

The cap was already being carved out before removal — patch 9.23 gave Prototype: Omnistone ultimate CDR
*"ignoring the CDR cap"* — and the conversion was **not value-neutral**: patch 11.1 (5 Jan 2021) had to
re-tune Ionian Boots, *"ABILITY HASTE 15 ⇒ 20"*.

### 2.2 Dota 2 — the other fix for the same defect

**FACT [official, `dota2.com/datafeed`].** Patch **7.31** (23 Feb 2022) carries the top-level gameplay
line ***"Cooldown reduction now stacks diminishingly"***, plus Lotus Orb *"Active no longer grants
cooldown reduction"* and Timeless Relic 12% → 10%.

**FACT [official].** Patch 7.33 (20 Apr 2023) reworked Octarine Core to a flat **−25% cooldown
reduction**. Patch 7.36 (22 May 2024) shows the standing policy — Tinker's innate is *"1% item cooldown
reduction per 4 Intelligence, **up to 60%**"*: a declared hard cap on any new percentage source at
declaration time.

> **INFERENCE — two shipped fixes for one defect.** Riot changed the stat's **shape** (percentage →
> additive haste) so no cap is needed. Valve kept percentages, made multiple sources **stack
> diminishingly**, stripped the stat off a second item, and hard-caps each new source when it is
> declared.

### 2.3 Diablo III — why floors do not help

**FACT [2nd-tier, Maxroll, patch 2.7.3].** Percentage CDR sources stack **multiplicatively**:

```
CDR      = 1 − (1 − CDR₁)(1 − CDR₂)          "no diminishing returns" — two 50% sources give 75%
Cooldown = max(0.5, (1 − CDR) × (Base − Flat))
Cost     = max(1,   (1 − RCR) × (Base − Flat))
```

Max general CDR attainable: **83.77%** (97.97% with shrines).

**⭐ The floors are irrelevant to the builds that actually went free.** Channeling Pylon, Kekegi's
Unbreakable Spirit and Land of the Dead – Invigoration **remove** cost rather than reduce it, so they
never enter the formula and never meet its floor. And Captain Crimson's Trimmings wires the cost stats
into a *second output* — damage equal to your CDR, damage reduction equal to your RCR.

---

## 3. Resource-generation combos

### 3.1 ⭐ Path of Exile — GGG states the design stake exactly

**FACT [official], the 3.15 "Expedition" manifesto, 20 Jul 2021**
([forum/view-thread/3147157](https://www.pathofexile.com/forum/view-thread/3147157)):

> *"When we're designing skills for Path of Exile, the mana cost of the skill is a mechanism to allow us
> to have large impactful effects. Bigger skills should cost more mana to cast. **Unfortunately, this
> entire mechanism is currently bypassed by triggering skills as this skips their mana cost. This
> basically means that we can't design really powerful spells.**"*

> *"…most notably **Cast on Damage Taken, which previously had no cost at all**, outside of taking up
> sockets."*

**That is the best statement of the stake anywhere in this research: when the cost channel can be
bypassed, you lose the ability to scale magnitude at all — not just one number.**

**FACT [wiki].** Cast on Critical Strike was throttled by **rate** four times across eight years before
it was ever charged mana: 10 ms cooldown (1.0.5) → chance −20% (1.2.0) → 50 ms (1.3.0) → one skill per
trigger event + 500 ms (2.4.0) → 150 ms (3.5.0) → **"Trigger skill gems now cost mana"** (3.15.0).
Cast when Damage Taken got a **250% cost multiplier**, against ~120% for the others.

**FACT [wiki]. The walk-back is five days, not a league.** Patch **3.15.0d (28 Jul 2021)** reverted the
cost multipliers on ~130 support gems, almost all by 10 percentage points. **The damage nerfs were not
touched.**

**⭐ Archmage is the cost-coupling lesson.** It paid damage off *mana cost*, so every cost modifier in
the game became a damage lever: 127% (3.10) → 108% (3.11) → 60% (3.15.0) → 75% (3.15.0d) → **reworked in
3.24 to scale off unreserved maximum mana instead of cost.** Indigon got the same treatment — 50–60% →
**20–25%** increased spell damage per 200 mana spent, with a 2000% cap made retroactive.

### 3.2 World of Warcraft — cost-free rotations

All **[wiki, warcraft.wiki.gg]**, which quotes official notes.

| Mechanic | Defect | Fix |
|---|---|---|
| **Illumination** (Paladin) | *"**100%** chance to gain Mana equal to the **base cost of the spell**"* — a crit heal was **literally free** | 100% → 60% (2.1.0, 2007) → 30% (3.2.0, 2009) → removed (4.0.1, 2010) |
| **Judgements of the Wise** | immediate mana on judgement | **33% → 15%** of base mana **three weeks after launch** (3.0.3, 4 Nov 2008) |
| **Divine Plea** | 25% of total mana over 15 s | healing penalty 20% → 50%; mana 25% → 10%; removed 6.0.2 with *"**Mana costs for paladins have been adjusted accordingly**"* |
| **Replenishment** | raid-wide, fed by 5 classes | 0.25%/s → 1% over 5 s → 0.1%/s → **removed** (5.0.4, 2012) |
| **Innervate** | Legion redesign: *"**allows a friendly target to cast spells without spending mana** for 10 seconds"* | ran **a decade**; 12.1.0 (11 Aug 2026) → *"regenerates 25% of maximum mana over 8 seconds, **rather than causing spells to be free**"* |
| **Mistweaver Mana Tea** | spending Chi generated the mana to spend more Chi | Blizzard hit the **cost side of the rotation**, not the regen: *"Jab now costs 8% (was 4%) of base mana"* (5.2.0) |
| **Warrior Rage** | generation scaled with an uncontrolled input (incoming hits) | normalized off auto-attack damage (4.0.1) → removed (5.0.4) → reinstated (7.0.3) → **"normalized to 3 per hit with an internal cooldown of 1 second"** (8.0.1, 2018) |

---

## 4. "Free" abilities priced only by cooldown

### 4.1 ⭐ Overwatch — Mercy's Resurrect, and inventing a time cost

All **[official]**, `overwatch.blizzard.com/en-us/news/patch-notes/live/`. Resurrect moved from an
Ultimate to a **30-second E ability** on 19 Sep 2017 and needed three corrections:

1. **19 Sep 2017** — becomes a basic ability, 30 s cooldown.
2. **17 Oct 2017** — the ultimate stops resetting it: Valkyrie *"No longer resets or reduces
   Resurrection's cooldown."*
3. **16 Nov 2017** — **a time cost is invented to stand in for the missing resource cost**: *"Cast time
   increased from 0 seconds to 1.75 seconds"*, movement speed −75% while casting, interruptible. Dev
   comment: *"Now that it has a cast time, enemies are more able to counter the ability."*
4. **9 Aug 2018** — when cooldown levers ran out, throughput was cut: healing 60/s → 50/s, *"Mercy's
   previous healing output made her nearly irreplaceable in any team composition."*

**Brigitte Shield Bash**, 22 May 2018 — the most on-point dev quote in the shooter set: *"Her Shield
Bash is a **very strong ability on a fairly short cooldown**, making it difficult for her opponents to
play around."* 5 → 6 s, then 6 → 7 s, then a **counterplay** fix instead of a third bump.

**Sombra**, 27 Feb 2018 — *"No longer gains ultimate charge from health pack healing."* **INFERENCE:**
the fix severed the free generator rather than raising the ultimate's cost.

### 4.2 Magic — free spells as a permanent format tax

**FACT [official].** **Grief**, banned in Modern and Legacy 26 Aug 2024: *"Starting the game down two or
three cards from the various **one-mana ways it can be returned** is quite brutal."*

> **⭐ INFERENCE, the most transferable defect in this file.** Evoke prices "sacrifice this creature" as
> the cost. A one-mana recursion spell **buys that drawback back**, so a turn-one Grief truly costs
> `0 + 1 = 1 mana`. **Neither card is mispriced alone — the defect lives in the composition of two
> independently reasonable costs**, and it took Wizards from 2021 to 2024 to act.

**FACT [official].** Force of Will's own cost was corrected in development: *"the just-card version of
Force of Will proved too strong."* And R&D's self-assessment: *"it's fraught with dangers, and yes,
**R&D has a history of undervaluing how good they are**."*

---

## 5. The opposite failure — cost too steep, or a lower rank being better

### 5.1 ⭐ WoW downranking — the canonical case, and how it actually died

**Mechanic [wiki].** Lower spell ranks cost less mana while retaining most effectiveness, so they had
higher healing-per-mana — decisive in long fights. The three-stage kill:

| Patch | Date | Change |
|---|---|---|
| 1.10.0 | 28 Mar 2006 | +healing coefficient made dependent on cast time |
| 2.0.1 | 5 Dec 2006 | *"**Low-level spells cast by high-level players will receive smaller bonuses**"* — Flash Heal rank 4 ends at **0.529**, and **0.227** after cast-time penalties |
| 3.0.2 | 14 Oct 2008 | **Costs re-based as a percentage of base mana** — constant or decreasing across ranks, so downranking now costs *more* |
| 4.0.1 | 12 Oct 2010 | *"**Spells and abilities no longer have multiple ranks**"* — substrate removed |

> **⭐ INFERENCE — the fix that finally worked was making cost *proportional* rather than *absolute*.**
> As long as cost was a flat number attached to a rank while output scaled with gear, some lower rank
> was always the efficiency winner. Percentage-of-base makes the ratio rank-invariant.

### 5.2 Re-pricing so the big spell is worth casting

**FACT [wiki, patch 4.0.6, 8 Feb 2011].** Blizzard deliberately re-cut the ratio to stop cheap-heal spam:

- Power Word: Shield — *"mana cost… increased by approximately **31%**, but its effect has been
  increased by **208%**"*
- Greater Healing Wave — cost +10%, healing +20%
- Penance — cost +7%, healing +20%
- Meanwhile utility got cheap: Power Word: Fortitude **−68%**, Renew **−24%**

### 5.3 FFXIV — a rank inversion still live

**FACT [2nd-tier, Gamer Escape]:**

| Spell | Level | MP | Potency | Potency/MP |
|---|---:|---:|---:|---:|
| Cure | 2 | 400 | 500 | **1.25** |
| Cure II | 30 | 1000 | 800 | **0.80** |

The higher rank is **36% less MP-efficient**, and the **Freecure** trait actively rewarded opening with
the cheap rank. **INFERENCE:** Square Enix never removed the inversion; they changed *why* you press each
button and deleted the trait chain that made cheap-rank-spam the rewarded opener.

### 5.4 When no price works, the thing gets deleted

**FACT [official].** LoL patch 9.23 (19 Nov 2019) removed **Ohmwrecker**: *"been underused and
underpowered for a long time. Turret disabling is an interesting mechanic… but not one that potentially
shows up in every team's toolkit, which is the risk a good version of Ohmwrecker presents."* — i.e.
**there was no viable price at all**: too weak and nobody buys it, correctly priced and everybody does.
Same patch removed **Spear of Shojin**, a CDR item, for *"reduc[ing] the downtime of their CC, mobility,
and immunity spells beyond what we think leaves healthy room for counterplay."*

**FACT [2nd-tier].** City of Heroes, Issue 19 (30 Nov 2010): endurance costs were high enough that the
**Stamina** power was a universal tax on every build. The fix was not to nerf Stamina but to make the
whole Fitness pool **inherent from level 1**.

> **When a cost is high enough that one mitigation becomes universal, the mitigation has stopped being a
> choice and become a tax.**

---

## 6. Developer post-mortems — "our cost was wrong"

- **Rosewater on cost-reduction as a mechanic class [official]**, *Storm Scale: Mirrodin* (11 Jun 2018):
  *"**There are few mechanics in the history of Magic that have caused more tournament problems than
  affinity for artifacts.** … Play Design says **it would be much safer if we only put it on
  non-artifacts because that way they could control the minimum cost and it wouldn't be free.**"* And:
  *"**Players like getting effects for a lot cheaper than normal.**"*
- **Wizards on discounting as a ban criterion [official]** (26 Aug 2024): *"**Paying a card's printed
  mana cost is generally a safe and fair strategy, while being able to discount a card by several mana
  is sometimes too strong.**"*
- **Wizards on cost-setting effects aging badly [official]**, on Fires of Invention (1 Jun 2020):
  *"**Because of the flexible nature of the cost reduction effect, Fires of Invention decks would
  continue to gain power as new high-mana-cost spells are added to the environment.**"*
- **Michael Majors on Nadu [official]:** *"**Nadu, Winged Wisdom was a design mistake.** … **I missed the
  interaction with zero-mana abilities that are so problematic.** … **We didn't playtest with Nadu's
  final iteration**, as we were too far along in the process, and it shipped as-is."*
- **⭐ The whole-mechanic re-cost: MtG companions [official]**, 1 Jun 2020. Rules text: *"**Once per
  game… you can pay 3 generic mana to put your companion from your sideboard into your hand.**"*
  Reason: *"Rather than go down the path of making several individual adjustments to the banned list for
  each format, we feel the better solution is to **reduce the advantage gained from using a companion
  across the board.** … **It's rare that we use a rules change to address metagame balance.**"*
  Rosewater's verdict: *"**This wasn't just the biggest mistake of the set, this was the biggest mistake
  of the year.**"*

---

## 7. ⭐ The cross-game pattern — nine recurring failure modes

INFERENCE, drawn from the sourced material above. Each is backed by a case.

1. **Net-negative cost.** A thing whose own cost is less than what it returns is an engine, not an
   action. The fix is always to move the *net* toward zero, never to touch the thing's own cost.
2. **A bypassed cost destroys magnitude design, not just one number.** GGG's exact words: if triggers
   skip the cost channel, no skill can ever be designed to be expensive-and-powerful.
3. **Percentage cost-reducers force a cap, and the cap then bricks the content.** Two shipped fixes:
   change the stat's shape to additive (Riot), or stack diminishingly and hard-cap each new source
   (Valve).
4. **Cost operators do not commute.** A set-cost and a reduce-cost in either order give different
   answers. Naga Sea Witch's entire balance lived in the evaluation order.
5. **Unlimited frequency on a free effect is usually the whole bug.** Firewall Dragon's missing "once per
   turn" was sufficient to take it from Forbidden to Unlimited.
6. **A drawback another subsystem pays you for is not a cost.** Painful Choice prices "discard four" in a
   graveyard economy; Grief prices "sacrifice this" in a format that sells the body back for one mana.
7. **Cost mistuning is reverted far faster than damage mistuning.** GGG walked back ~130 cost multipliers
   in **five days** while leaving the damage cuts standing all league. Blizzard cut Judgements of the
   Wise **three weeks** after the expansion. Cost errors are felt on every button press.
8. **⭐ Floors do not stop loops; coupling does.** Diablo III's 0.5 s and 1-resource floors are real and
   irrelevant, because the effects that went free **remove** cost rather than reduce it and never enter
   the formula. Every runaway here came from a cost stat wired into a second output — Archmage and
   Indigon paying damage off mana cost, Captain Crimson's paying toughness off cost reduction.
9. **Runtime cost is a separate budget.** Shudderwock was fixed with an iteration cap and doubled
   animation speed and **zero text changes**. A cheaply-iterated loop breaks the frame budget before it
   breaks the win rate.

**The operational rule**, from Wizards on Candelabra: when a free enabler only ever appears in the decks
that are breaking, **ban the enabler** — nerfing payoffs one at a time is a treadmill both Blizzard and
Konami have run for years.

---

## 8. What I could not find

Recorded so the same searches are not re-run.

- **Slay the Spire 1** — no evidence Megacrit ever changed Corruption, Dead Branch, Snecko Eye or Ice
  Cream to break infinites. No wiki.gg exists for STS1 (404) and Fandom was blocked. The STS2
  v0.100.0 / v0.101.0 numbers are **third-tier only** and sources disagree by a day on the date.
- **Skullclamp's development story** — not sourceable. **Every pre-~2007 `magic.wizards.com/en/articles/archive/…`
  URL now 404s**, taking the whole *Latest Developments* column with it. The widely-circulated
  "+1/+1 with equip {2} until late in development" version was **deliberately not repeated** here
  because it could not be sourced.
- **The 1 March 1999 DCI announcement** — text not online at any reachable location; Wizards' live
  archive indexes back only to ~2020.
- **LoL Cosmic Insight / Ultimate Hat / the "45% cap"** — unverified. **Do not cite the 45% figure.**
- **Dota Octarine's 6.84 introduction values** — Valve's own archive starts at 7.08 (Feb 2018).
- **A named Diablo III free-spender build nerf** (Critical Mass, Arcane Power on Crit) — every pre-2015
  official D3 patch archive was unreachable.
- **Valorant Guardian price cut** — 12 patch pages checked, no hit.
- **FFXI / RuneScape / Guild Wars / EverQuest rank-efficiency fixes** — all wikis 403. The
  commonly-repeated FFXI "Cure III over Cure IV" claim is explicitly **not verified**.
- One unresolved conflict: yugipedia's TCG list page dates Firewall Dragon's Forbidden status to
  28 Jan 2019, against 3 Dec 2018 elsewhere; Konami's own archive 404s for those dates.

### Access notes

**Blocked in this environment:** `fandom.com` (402), `liquipedia.net`, `mtggoldfish.com`, `kotaku.com`,
`pcgamesn.com`, `scryfall.com` and its API, `wiki.leagueoflegends.com`, `web.archive.org` (429),
`store.steampowered.com` (DNS).

**Working and worth reusing:**
`overwatch.blizzard.com/en-us/news/patch-notes/live/<YYYY>/<M>/` ·
`dota2.com/datafeed/patchnotes?version=<X.YZ>&language=english` ·
`warcraft.wiki.gg` · `hearthstone.wiki.gg` · `mtg.wiki` ·
`poewiki.net` (behind a proof-of-work gate) ·
`magic.wizards.com/en/sitemap.xml` ·
`yugipedia.com/api.php?action=parse&page=…&prop=wikitext&format=json`

**Recoverable elsewhere:** the Skullclamp and Blogatog material is reachable from a network where
`web.archive.org` responds, via `web.archive.org/web/2020*/magic.wizards.com/en/articles/archive/latest-developments/*`.
