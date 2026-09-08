# Combat

**Status:** Shipped (elements, statuses, shields, crit, shared damage math) · actor meters, skills, interactive battles **WIP**

---

## One fight language

Whether the hit lands on the **lawn** or in a **web battle**, the roster you built is the roster that performs. Elements, crits, shields, and statuses resolve through the same combat language — so raising a demon at home means something when you field it.

The lawn game still runs its own pea-and-bite world. Rise of Summoner adds an RPG combat layer on top: typed power, defence, matchups, and timed conditions.

---

## Elements

Six concrete elements, plus **omni** as the baseline “all elements” case:

**Fire · ice · air · earth · light · dark**

The **ring:** fire → ice → earth → air → fire.

- Fire melts ice
- Ice cracks earth
- Earth blocks air
- Air blows out fire

**Light** and **dark** counter each other and nothing else on the ring. Cross pairs outside that story stay neutral.

A creature can carry up to two types. Traits like void or chaos are traits — not extra elements on the ring.

---

## Shields, hits, and crits

Layered **shields** absorb RPG damage before it reaches HP. They are element-typed pools, not flavour bars.

Hits can miss. Crits can land. Resistance is two-phase: a status can fail to apply, or apply weaker. Internal cooldowns stop the same interaction from chaining into soup.

One resolver produces the damage number. Lawn and server battles share that honesty.

---

## Statuses

Timed conditions you will see in play:

**Crowd control and familiar lawn wraps:** Butter, Freeze, Chill, Poison, Hypno, Ember, Jala, Kelp

**Extra pressure:** Wither, Bond, Rally, Leech, Expose, Command, Shatter, Charm Pulse

**Contagion:** Blight, Rot, Spark, Pact Mark, Spore

**Vision (delve nerve):** Unsettled → Shaken → Afflicted

Not every status is common in every mode yet. The catalog is real; reachability grows as content does.

---

## Meters

Actors share six pools. Faction difference is a **label**, not a different system:

| Pool | Plant label | Zombie label |
|---|---|---|
| Health | HP | HP |
| Body fuel | Stamina | Stamina |
| Hunger | **Sun** | Hunger |
| Nerve | Spirit | Spirit |
| Stance / buff fuel | **Yang** | **Yin** |
| Guard | Poise | Poise |

Two things called “sun”: the **lawn sun bank** in a lawn match, and the plant label for the hunger meter. Different scopes — do not mix them in your head.

Poise and the full skill-cost loop are **WIP**. When they land, skills cost something you can see, and guard is a real stance — not a free shrug.

---

## Skills and loadouts (**WIP**)

You equip a small set of **actions** — innate skills, family skills, signatures — with costs, cooldowns, and targeting. Loadouts live on the creature sheet. Unlocking a skill is progression, not a second power curve.

Guard, movement skills, and reaction windows belong here with the meters above.

---

## Interactive battles (**WIP**)

Turn-based fights on a **battle stage** you play yourself: initiative track, action bar, targets you pick, wind-up and interrupt feel, reactions and counters.

Today, expeditions and many web fights still **auto-resolve** with the same math. You feel the roster. You do not yet pilot every turn.

---

## Named reactions and combo recipes (**Vision**)

A short **learnable** list on top of the element ring — the wet-and-lightning shape — so players can plan interactions, not only read matchup tables. This is not a second ring and invents no new elements. Interactive battles already name reaction windows; this is the catalog you can study. See [The loops](the-loops.md).

---

## In this build

Elements, statuses, shields, and RPG combat math are in. Skills, poise, and hands-on battle UI are **WIP**. Named reaction recipes stay **Vision**.

Next: [Expeditions](expeditions.md) · [Creatures](creatures.md) · [The loops](the-loops.md).
