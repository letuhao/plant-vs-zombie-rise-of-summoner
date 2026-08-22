# Item seed build — running log

**Purpose:** what has been built, what was decided during the build, and what runs next. A session
picking this up needs [seed-contract.md](seed-contract.md), [authoring-fleet-plan.md](authoring-fleet-plan.md),
the registries under `data/seed/items/_registry/`, and this log — not the twenty-four design documents
that produced them.

Owner authorization 2026-08-22: **drive the fan-out to completion, make the open calls, do not pause for
approval.** Decisions taken under that authorization are marked ⚖ below.

---

## Status

| Wave | State |
|---|---|
| **0a** — six registries | ✅ complete |
| **0b** — word pools, validator | ✅ complete |
| **0c** — exemplars, entry shapes | ✅ complete |
| Freeze gate | ✅ closed — 7 registries frozen |
| Pilot (5, contract test) | ✅ **PASS** — 0 errors, 69 entries, 9 files. Took 3 rounds |
| **Stage 1a** — 14 affix groups + materials + curves | ▶ 16 agents running |
| Stage 1b — base types (62) + gems (3) | pending 1a freeze |
| Stage 1c — uniques, sets, charms, and the rest (44) | pending 1b freeze |
| Wave 2 (script + 8) | pending |

---

## Wave 0a — complete

Six frozen registries in `data/seed/items/_registry/`, all `frozen: false` pending final gate review.

| File | Contents |
|---|---|
| `core.v1.json` | 10 rarity rungs (Chaff → Almanac) · 15 roles, weights summing to 1000‰ · 10 categories |
| `bands.v1.json` | `powerBand` 5 · `costBand` 5 · `dropBand` 5 · `variance` 3, each resolving per channel-family group |
| `naming.v1.json` | 125 id namespaces · 3 name patterns · the collision normalizer |
| `tags.v1.json` | 19 tags across 7 axes; 8 candidates cut for having no consumer |
| `themes.v1.json` | 13 themes |
| `classes.v1.json` | 24 classes (4 groups × 2 frames) · implicit slates for all 15 roles |

### ⚖ Decisions taken during 0a

1. **Themes cut 15 → 13.** `field-manual` removed (a deliberately generic theme becomes the dumping
   ground for unplaced content); `permafrost-seed` merged into `frostbitten-vanguard` (two ice themes,
   one with an army and one with patience). Both were the authoring agent's own nominated weakest.
   Dangling `avoid` cross-references and the unique band assignment were purged and re-verified: 13
   referenced, 13 live, no orphans.
2. **Attributes deferred.** The requirements lane's five-attribute proposal only earns its place if
   growth curves are per-species divergent, and those do not exist. Five attributes gating nothing is the
   "row nothing consumes" defect. The requirement gate ships whole without them and the hook fields
   remain, so adding them later is not a re-author. **Stage 1a drops to 80 agents.**
3. **Three categories flipped to `authorNowV1: true`** — `consumable`, `insert`, `charm`. F6 had
   correctly applied the content-budget recommendation to ship them at zero; the owner's Diablo-2 scale
   decision overrules it. Each carries a note recording why.
4. **`signature` tag kept, with consumers assigned.** Authored with none, which by the registry's own
   rule would require cutting it. The presentation item card and the loot-filter default are real named
   readers, so it stays.

### Findings worth carrying forward

- **`elemental_power` was in the wrong units group** in the affix lane — its worked example was computed
  against the sigmoid calibration, but `combat.power.*` is flat game units. F7 reassigned the arithmetic
  to the sigmoid group where it belongs.
- **The flat-derived group has no numeric anchor.** Tolerable: all six families are `stat.derived`,
  quarantined until E12, so nothing can execute them, and authors write bands rather than numbers.
  Generator resolution for that group defers with E12.
- **Four of fifteen roles are hollow until E12** — `ward-array`, `mantle`, `head-guard`, `sense` have
  their entire named family cluster quarantined. They get stopgap implicit slates; two have no distinct
  identity until that module lands. Base types are still authorable (a base type is mostly frame, role,
  class, name and tags).
- **Role ids were contradictory across two lanes.** The equip-slots lane's 15 are binding; the categories
  lane's worked examples used the ideal's older 12 and now carry a banner with the mapping.
- ⚠ **A briefing rule learned the hard way, now in the plan:** an agent briefed only on the lane
  documents will faithfully reproduce every decision made since the design round *in reverse*. Every
  brief must carry the owner's scope decisions, not just pointers to the SSOTs.

---

## Wave 0b — in progress

`words.v1.json` landed: **1 245 canonical ids / 93+2 pools**, disjointness machine-checked rather than
asserted — exact-duplicate checks on ids *and* surface forms, plus a near-duplicate stem check after
suffix stripping that caught 24 real pairs (`guard`/`guarding`, `root`/`rooted`, `weighted`/`weighty`).

### ⚖ Decisions taken during 0b

5. **The normalizer gains rule 2a — whole-token resolution precedes fusion decomposition.** F1 hit the
   case building the pools: an atomic seed like `Thistledown` was being split into unrelated halves and
   colliding with names sharing neither idea. Recorded in `naming.v1.json`; the validator must implement
   it exactly or every `uniqueSetSeedPools` entry false-positives.
6. **The commander `standard` role was missing everywhere except `core.v1.json`.** Found by
   cross-checking registries against each other: `core` carries **16** roles (the 15 plus the
   commander-only `standard`), while the word pools covered 30 role-frame combinations and the base-type
   partitions covered 15 roles × 2 frames × 2 bands. So commander banners had no vocabulary, no
   namespace, and no partition.
   **This was not cosmetic.** The affix lane makes `standard` the *only* legal home for
   `warding`/`resilience`, because `match` is the one scope where primary defense works — so the
   banner's signature capability, "+armour for your whole army", was unauthorable. Fixed: 32 nouns added
   across two pools (collision-checked; one clash, `pennant`, replaced with `labarum`), two namespaces
   allocated, two partitions added. **Fleet 142 → 143.**

### The validator, and the four gaps it refused to guess at

`tools/ItemSeedValidator/` + `tests/FusionRpg.ItemSeedValidator.Tests/` — **41 tests green**, wired into
`.github/workflows/ci.yml` and verified in Release. Against an empty seed tree it prints
`!! NO SEED FILES WERE SCANNED` and **exits 1**, so it can never pass vacuously. Against a deliberately
broken file it produced 23 errors each naming file, entry and rule — including *Heartbloom Crown*
colliding with *Crown of Heartbloom*, which proves the normalizer.

It surfaced four contract gaps instead of inventing answers. All four are now closed:

| Gap | Resolution |
|---|---|
| §10 defined entry shapes for only 4 of ~14 kinds | ten more specified in `entry-shapes.md`; the contract points at it |
| No registry owned the `element` vocabulary — it was being derived from themes' `elementAffinity` | added to `core.v1.json`: six concrete elements plus omni, ordinals marked append-only because the shipped code depends on them |
| No registry owned `roleGroups`, and the contract's own example used **`manipulator-offense`, which is not a role id** | the field is a list of role ids; renamed to `roles` and the stale value corrected in both places it appeared |
| `kind` ↔ directory ↔ `idNamespaces` was stated nowhere, so the code was its only home | authoritative table in `entry-shapes.md` |

The third of those was an error in the contract itself, propagated from the stale 12-role set. It would
have been copied by every affix and set partition.

---

## Wave 0c and the freeze gate — complete

**Exemplars** — four worked files plus a README in `data/seed/items/_exemplars/`, each covering a hard
case rather than a nice one. The unique deliberately breaks a generator rule (an affix family whose
role-matrix weight is zero) while breaking no machine rule, and says so in the file.

**Entry shapes** — `entry-shapes.md` specifies the ten kinds the contract had left undefined, plus the
authoritative `kind` → directory → `idNamespaces` table for all fifteen, which had lived only in the
validator's code.

**The validator caught the exemplars before the exemplars could teach anything wrong.** Two defects, both
in the tool rather than the content — which is the pilot's "validator gap" class, arriving early:

- `_exemplars/` matched no kind directory. Exemplars must validate exactly like real content — that is
  what makes them trustworthy — but they do not live in a kind directory, so kind now comes from the
  file's own declaration and the corpus-level "unreferenced" lint skips them.
- `words` was not a nameable registry in `_meta.registryVersions`, though the validator loads 1 245 words
  from it.

Three more findings from the shapes pass, all closed: `powerCategories` added to `core.v1.json` (charm
axes and socket resonances key on the five-category vector and no registry owned it); four structural
count fields generalised on the validator's allowlist; and **the one deliberate exception to §3 written
down** — a curve file's whole content is `(input, multiplierPerMille)` points, authored by a single
reviewed partition, guarded narrowly by kind rather than by a blanket exemption.

And the exemplar pass found that only `narrow` counter-pressure was expressible without a number.
`drawback` wanted a magnitude and `conditional` wanted a threshold, both banned. `core.v1.json` now
carries severity bands and a **closed nine-condition list, each mapped to a predicate-tree leaf the atom
layer already ships** — so no condition is a promise the runtime cannot keep.

### ⚖ Decision 7 — the freeze, and an immediate v2

All seven registries carry `frozen: true`. The validator's seven `RegistryNotFrozen` warnings cleared;
exemplars pass at **0 errors**, and the remaining warnings are all expected (22 `ClassRungEmpty` because
only one base-type partition exists, 13 markup-in-notes).

`naming.v1.json` then went to **registryVersion 2** within minutes, on purpose. The commander-standard
addendum hardcoded the id segment as `standard` while the general template derives it from the frame's
display name — `banner` and `root-totem`. Two readings of one rule is exactly the ambiguity the pilot
exists to find, and it was found before the pilot rather than after. **Zero partitions had consumed v1,
so the v2 path cost nothing** — which is the mechanism working, not a failure of it.

## Pilot — five partitions, chosen for risk not representativeness

| Agent | Partition | What it tests |
|---|---|---|
| P1 | `base.head-guard.plant.b` (Haiku) | the exemplar is the same role at band **a**; can an agent transpose to band b, on a role whose implicit slate is a quarantine stopgap? |
| P2 | `base.armament-primary.humanoid.a` (Haiku) | a different frame **and** a different ladder — weapons, not armour — with the heaviest affix budget |
| P3 | `base.standard.plant` (Haiku) | the late-added commander role: defined in a different section of `core.v1.json`, namespaced in a v2 addendum |
| P4 | `g.on-hit` (Sonnet) | a vocabulary **inventor**; tests the `roleGroups` → `roles` rename and the derived-`affixClass` omission |
| P5 | `gems.g1` (Haiku) | **no exemplar exists** — tests whether `entry-shapes.md` stands alone |

P5 is the sharpest test: if a kind with only a written shape and no worked example cannot be authored
correctly, that affects every kind in the same position, and it is worth knowing on one agent.

---

## Pilot round 1 — FAILED, and that is the correct outcome

All five agents returned `OK` tokens. **Four of the five files failed validation.** That gap between
what an agent believes it did and what the corpus will accept is the entire argument for the pilot, and
for the validator being the reporting channel rather than agent summaries.

Classified against the seven categories:

| Finding | Count | Class | Response |
|---|---|---|---|
| `TagUnknown` — an agent invented `medium-heavy` between `medium` and `heavy` | 6 | **Authoring error** | re-run that agent |
| `MetaIncomplete` — `exemplarVersion` omitted | 1 | **Authoring error** | re-run |
| `TagAxisNotApplicable` — gems tagged `offensive`/`defensive` | 20 | **Registry gap** | tags → v2 |
| `UnknownKeyShapeUndefined` — ten kinds still `Undefined` in the validator | 66 | **Validator gap** | in flight |
| `IdOutsideNamespace` — every commander id rejected | 10 | **Validator gap** | fixed |
| `RequiredFieldMissing: class` — no class ladder covers the commander role | 20 | **Registry gap** | classes → v2 |
| `PartitionMetaMismatch` — `gems/g1` vs `gems/1` | 1 | **Brief defect** | template fixed |
| `MetaRegistryVersionMismatch` | 13 | **Self-inflicted** | see below |

**Five of the seven classes fired. Only two were authoring errors.** Had this been the full fleet,
roughly 120 agents would each have improvised past the same five gaps in slightly different ways.

### ⚖ Decisions 8–11, all forced by the pilot

8. **`tags.v1.json` → v2.** `combat-posture` excluded gems, charms and consumables, while naming its own
   consumers as vendor stock and loot filters — which are exactly what those three kinds are. The agent
   needed to say "this gem is offensive" and had no legal way to. Exclusion was an oversight, not a rule.
9. **`classes.v1.json` → v2.** The commander `standard` role had **no class ladder and no implicit
   slate**. A two-rung ladder per frame was added (commander gear is single-band, so four rungs would
   have nothing to distinguish), plus the slate that carries `warding`/`resilience` — the two families
   legal *only* at match scope, which is what makes the banner's signature exist at all.
10. **`naming.v1.json` → v3.** The commander namespaces were described with a key called `pattern`; the
    expander reads `idTemplate`, so it never saw them. The prefixes were right and simply in a shape
    nothing read. Fixed properly in the expander instead: commander roles live in
    `roles.commanderOnly`, deliberately outside the fifteen-role budget, so the base-type expander now
    walks them separately and single-banded.
11. **A mistake of my own, recorded because the validator caught it.** I bumped two registries to v2
    *while the pilot was running*, producing 13 `MetaRegistryVersionMismatch` errors. That is precisely
    the "silently edited registry splits the corpus" hazard the freeze rule exists to prevent, and I
    walked into it hours after writing the rule. **The registry lifecycle must be enforced against the
    orchestrator too, not only against agents.** Re-pilot resolves it.

### ⚖ Decision 12 — the finding no validator could have made

The pilot files all **validated** on structure long before they were any good, so I read the names.

`plant-head-guard-b` produced **calyx, cupule, capitulum, inflorescence, thyrse, cyme** — real
flower-structure terms, frame-true, exactly right. But its twelve adjectives were Dense, Petrified,
Ancient, Obdurate, Stony, Oaken, Rough, Gnarled, Sunken, Mossy, Cracked, Coarse: **twelve words to say
"hard and old" once.** The pool saves an author on the head-noun and cannot save them on the modifier.
That is now a named failure in the brief template with the actual example in it.

`humanoid-armament-primary-a` produced **Keen Sword, Fleet Sabre, Honed Falchion** — generic fantasy in
a Plants-vs-Zombies game, which the brief explicitly called a failure of the task. **The agent was not
at fault.** Its pool contains `sword, sabre, dirk, falchion, glaive, mace, flail, lance, pike, arbalest,
culverin`.

Checking further, the defect is systematic and has a clean explanation. F1 applied one rule to both
frames — *use the real domain vocabulary* — and chose **botany** for plants and a **historical European
armour glossary** for humanoids:

| | plant pools | humanoid pools |
|---|---|---|
| core-guard | cambium · phloem · culm · haulm · bast · xylem · sapwood | gambeson · corselet · aketon · hauberk · brigandine · byrnie |
| head-guard | (botanical) | coif · sallet · burgonet · morion |
| footing | radicle · stolon · haustorium · hypocotyl · rhizome | buskin · clog |

Half the corpus's vocabulary was aimed at the wrong world. PvZ zombies wear traffic cones, buckets,
newspapers, screen doors and football helmets — the humanoid frame is **scavenged domestic junk, not
forged kit**. `words.v1.json` → v2 revoices all 16 humanoid noun pools and the humanoid class-rung
adjectives; the plant pools are untouched, because they are the standard the others must meet.

**This is the single most valuable thing the pilot found**, and no schema check could have found it:
every one of those names is a legal string from an allocated pool. It was caught by reading twelve
names out of a five-agent pilot instead of thirty partitions.

### Validator fixes — four rounds, each one finding the next

The validator was corrected five times against real pilot content, and each fix exposed the next
problem. All 56 tests green throughout; errors 72 → 52 with no content edited at all.

| Fix | What was wrongly rejected |
|---|---|
| Ten kinds `Undefined` → `Defined` from `entry-shapes.md` | 66 warnings, and a **hard rejection** of the affix file for using `roles` — the contract's own renamed field |
| `roles` / `roleGroups` moved to a kind-gated OR check | `charm.roleGroups` means pool-group ids, not role ids — a blanket rename would have broken it |
| Commander base types expanded from `roles.commanderOnly` | every commander id, rejected as outside any namespace |
| `{variant}` accepted in template fields | a generated affix family writes `params.element = "{variant}"` **so that** one authored row expands per element — the exact mechanism the design depends on |
| Shipped family names exempt from pool grammar | `Lifesteal`, `Retribution`, `Volley` predate the word pools; the author neither chose those names nor may change them |

The last two are worth naming as a class: **the validator was rejecting the design working correctly.**
A stricter validator is not automatically a better one, and both would have blocked legitimate content
across every affix partition.

**All 52 remaining errors clear on the re-pilot** — 23 registry-version mismatches I caused, 20 commander
fields that now have a ladder to reference, 6 invented tags, 1 partition id.

### What the pilot proved about the pieces

- **The token contract works.** Five agents, ~510k tokens of work, five one-line replies.
- **The validator is the reporting channel, exactly as designed.** Every one of these findings came from
  one run of it, not from reading five reports.
- **`entry-shapes.md` was sufficient for a kind with no exemplar** — P5's 20 gems were structurally
  sound. Its failures were the tag axis and the partition id, neither of which is a shape question.

---

## Pilot round 2 — the contract converged

Errors **72 → 14**, and the character of them changed completely. Round 1 fired five of the seven
problem classes; round 2 leaves only authoring errors and one ordering artifact. **No contract,
registry or validator gap remains.** That is the gate.

### ⚖ Decisions 13–16

13. **`mass-class` widened from three values to five** (tags v3). In round 1 an agent invented
    `medium-heavy`; in round 2 a *different* agent on a *different* partition invented `medium-light`.
    Two authors who cannot see each other reaching for the same missing rung is not carelessness twice
    — it means three buckets cannot describe both an armour ladder and a weapon ladder. Additive, so
    nothing already authored became invalid.
14. **Registries gained `minCompatibleVersion`** — the version rule itself was broken. Every bump
    invalidated everything authored before it, which means a 125-agent build could never converge: any
    mid-run fix moves the target. Now an **additive** bump warns and a **breaking** one fails.
    `words v1→v2` genuinely breaks (the humanoid pools a v1 file drew from no longer exist);
    `tags v2→v3` genuinely does not.
15. **Wave 1 restaged from two stages to three.** A gem references the affix family it grants, and both
    were in 1a — so a gem cited a family while its partition was mid-write, and a re-run renamed the
    family out from under it. Base types have the same dependency through implicits and escaped only
    by happening to cite *shipped* families. Now: **1a defines** (affix families, materials, curves),
    **1b builds** (base types, gems), **1c composes** (everything else).
16. **The commander-standard partition needed three attempts, and was right to fail twice** — first no
    class ladder existed, then the ladder existed but its rungs had no adjective pools, so the compound
    naming pattern had no adjective source. Both gaps were mine. An agent that stops and names the gap
    is worth much more than one that fills it plausibly.

### The two remaining failures are the good kind

`SequenceGrammar` ×10 — an agent put the band letter into the id where the namespace has no band
segment. `element: lightning` / `nature` — an agent invented two elements against a closed roster of
seven. Both are ordinary authoring errors, both re-run, and neither implies anything about the contract.

---

## Stage 1a — the corpus starts existing

**174 entries across 23 files, 0 errors.** 14 of 15 affix groups, materials, and curves.

Three more validator fixes, and the pattern from the pilot held exactly: **every one was the tool
rejecting correct content**, none was an agent at fault.

| Fix | What was wrongly rejected |
|---|---|
| Physical tag axes widened to `material` (tags v4) | a shard tagged `mineral`, a substrate tagged `organic` — a material is the most natural thing in the corpus to carry material-nature, but the axis was written before the material kind had a shape |
| Shipped family ids exempt from the namespace rule | `atom.freezing`, `atom.venomous`, `atom.bloodletting` — ids the registry explicitly says are kept verbatim, sitting outside every mint namespace *by design* |
| Pool naming grammar no longer applies to affix families | `Harvest`, `Stampede` — but the real point is that the shipped families are **Lifesteal, Retribution, Volley**, none of which is assembled from a word pool. Item grammar was being applied to mechanic labels |

### What the agents did that no rule told them to

The briefs carried each group's specific hazards, and several agents went further:

- **g.ward** deliberately excluded `susceptibility` and recorded why in `_meta.scopeNote` — that family
  has zero readers, and authoring it would be the "row nothing consumes" defect.
- **g.attack shipped 3 families against a nominal target of ~7**, with a structural argument: `atk` ×
  {Flat, Increased, More} is fully saturated by the shipped trio, and the only derived channel that
  could mean "raw attack" belongs to another partition. It refused to pad and said so.
- **g.shield-stat found a genuine conflict between two wave-0 documents** — the affix lane's role matrix
  predates the `ward-array` role that the equip-slots lane created by carving shield weight out of four
  others. It sourced from the newer document and wrote the reasoning into the entry for review.
- **g.precision** noticed that `stat.derived`'s schema has no `element` param and embedded the variant in
  the channel string instead, verifying against the real per-element channel constants in code.

Those are the four behaviours that separate a corpus from a pile of valid JSON, and none of them was
requested. What was requested was the standing instruction that a blocked or reduced partition is
cheaper than a guessed one.

---

## Stage 1b dispatch tracker

61 partitions. Two were authored during the pilot and are already validated:
`base-types/head-guard/plant/b` and `base-types/armament-primary/humanoid/a`, plus `gems/1`.

**Batch 1 — dispatched:** armament-primary h/b · armament-primary p/a · armament-primary p/b ·
core-guard h/a · core-guard h/b · core-guard p/a · core-guard p/b · ward-array h/a

> **Concurrency ceiling: 20 subagents.** Six dispatches were refused on hitting it. Dispatch in
> slot-sized batches and drain before refilling; the limit is raised with
> `CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS` if the owner wants a wider fan-out.

**Still to dispatch: `gems/3` only.** Everything else in stage 1b is dispatched or landed.

**Original full list (53):**

| Role | Partitions outstanding |
|---|---|
| ward-array | h/b · p/a · p/b |
| armament-secondary | h/a · h/b · p/a · p/b |
| jewel-major | h/a · h/b · p/a · p/b |
| manipulator | h/a · h/b · p/a · p/b |
| mantle | h/a · h/b · p/a · p/b |
| head-guard | h/a · h/b · p/a *(p/b done in pilot)* |
| girdle | h/a · h/b · p/a · p/b |
| sense | h/a · h/b · p/a · p/b |
| footing | h/a · h/b · p/a · p/b |
| infusion | h/a · h/b · p/a · p/b |
| retinue | h/a · h/b · p/a · p/b |
| jewel-minor-a | h/a · h/b · p/a · p/b |
| jewel-minor-b | h/a · h/b · p/a · p/b |
| commander | humanoid-standard *(plant-standard done in pilot)* |
| gems | g2 · g3 |

**Standing brief shape for a base-type partition** — read
[agent-brief-template.md](agent-brief-template.md) plus:

```
partition id : base-types/{role}/{frame}/{band}
id namespace : item.{frame}-{displayName}-{band}-{seq:03}
entries      : 12, kind base-type
class band   : {a=lighter | b=heavier} rungs of the {armour|weapon|offhand|jewel} ladder
implicits    : implicitSlates.{role} only
nouns        : nounPools['{role}.{frame}']
registryVersions: core 1 · bands 1 · themes 1 · tags 4 · classes 2 · naming 4 · words 4
```

Display names are in `core.v1.json` `roles.list` — `humanoidName` / `plantName`, used verbatim in the
id. The role-to-ladder mapping is in `classes.v1.json` `classLadders`.

---

## Stage 1b / early 1c — what the BLOCKED path bought

Six agents reported `BLOCKED` rather than guessing. **Every one was correct, and every one caught a
defect in my brief rather than in the design.** This is the single clearest evidence that making the
blocked path cheaper than the guessed path was worth doing.

| Block | What it caught |
|---|---|
| `retinue/humanoid/b` | **The most valuable block of the build.** My brief said retinue uses the *armour* ladder. `words.v1.json poolAccess.roleToLadders` maps retinue → **jewel**, exclusively. This agent read the registry, found the contradiction and refused — while **three sibling partitions complied with the same wrong instruction and produced content that validated cleanly** |
| `consumables/*` ×3 | Consumables are partitioned by **slot** (`consumables/1|2|3`), not by gameplay class. I invented `consumables/restorative` and friends. One agent even derived the correct mapping (restore → slot 1) before refusing |
| `infusion/plant/b` | Its band-a sibling did not exist yet — I dispatched both in the same window. The staging rule covered stages but not **band siblings inside a stage** |
| `gems/2` | No word pools exist for gems at all. `words.v1.json` covers base types, uniques, sets and charms — the gem kind was never given a vocabulary |

### ⚖ Decision 17 — the validator gains a role↔ladder check

Three retinue partitions authored against the wrong ladder **and passed validation**, because a class id
is legal on its own and nothing checked it against the entry's role. That is exactly the silent-wrong
class of defect the validator exists for, and it had a hole.

`ClassNotInRoleLadder` now reads `poolAccess.roleToLadders` and rejects a base type whose class belongs
to a ladder its role does not draw from. Had it existed, no agent would have needed to catch this by
reading a registry unprompted.

**The general lesson, and it is the sharpest of the build:** *a wrong answer that three independent
agents agree on is indistinguishable from a right one, unless something mechanical can tell them apart.*
Consensus among agents is not evidence. Only the registry and the validator are.

### ⛔ External blocker — the build is broken by another session

`src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs` references a `ValidateOp` that does not exist.
The file is **uncommitted-modified** against `c4c9908` and is not this program's work — the effect-atom
program is being built in parallel (its `AtomImporter`, `AtomRunner` and `CapPerMatchTests` all appeared
during this run). `tools/ItemSeedValidator` references `FusionRpg.Core`, so **validation is paused until
that edit compiles.** Authoring is unaffected and continues.

Not fixed here deliberately: editing another stream's in-flight work is how two sessions corrupt each
other.

---

## Stage 1b/1c cleanup — from 1,092 errors to zero

The corpus reached 93 files and 1,129 entries red. Working the error classes from most-frequent down
turned out to sort them into two piles of very different value, and the split is the finding:

**Nine of the eleven classes were validator defects, not authoring defects.** Every one had the same
shape — a check written against `base-type` and then applied to a kind it does not describe. The
partitions were right and the gate was wrong.

**⚖ Decision 18 — a display template's `name` may carry `{placeholder}` braces.**
`entry-shapes.md` §10 defines the kind's localized string AS the template (`+{value} max health`).
The placeholder ban exists to keep substitution out of item names. Three partitions, 98 rows, all
correctly authored. Real markup — tags, entities, emphasis, backticks — still rejects there.

**⚖ Decision 19 — `frame: "any"` is legal on a recipe and nowhere else.**
`entry-shapes.md` §4 gives recipe frames three values: `humanoid | plant | any`. On a recipe `frame`
is the scope the recipe applies to; `core.v1.json`'s roster is the list of *bodies*, and correctly
has no `any` in it. Eighteen of thirty recipes are frame-agnostic and had nothing else to write.

**⚖ Decision 20 — `TagAxisNotApplicable` drops from error to warning.**
`tags.v1.json`'s own `appliesToNote` says the field is "authoring guidance, not an enforced
constraint". The validator was enforcing it as a gate, which contradicts the registry it enforces.
It stays reported, because an off-axis tag is usually still worth a glance.

**⚖ Decision 21 — a minted runtime id is not a reference.**
`entry-shapes.md` §0 names five kinds that carry a runtime-facing id beside their tracking id, and
§6 requires a milestone's `atom.enhance-*` to **not** match an existing family. The generic resolver
demanded the exact opposite and failed all ten correctly-authored rows. Replaced with the three
rules §6 actually states: reserved stem, no collision with an affix family, no duplicate among
milestones.

**⚖ Decision 22 — a lowercase run inside a hyphenated compound is not a connective.**
The name-pattern regex accepts `Wind-borne Inlay`; the connective check then flagged `borne`. Two
rules in the same method disagreeing. Only a free-standing lowercase word counts now.

**⚖ Decision 23 — an affix family may be named with a single word.**
Five independent partitions produced one-word mechanic labels — Harvest, Grit, Callusing, Stampede,
Graftplate — and every affix family that already ships is exactly that shape: Lifesteal,
Retribution, Volley. The two-pool-word fusion rule was demanding that new mechanics look unlike
every mechanic in the game. This is the third time in this build that independent convergence has
turned out to be the corpus reporting a wrong constraint rather than several agents making the same
mistake, and it is worth stating as a rule: **when partitions that cannot see each other produce
the same rejected output, check the constraint before checking the agents.**

The remaining pile was genuine, and small — about a dozen rows: three invented elements
(`lightning`, `shadow`, `wind` against a roster of six plus omni), a possessive, two three-word
names, a rarity word inside a name, one duplicate name, two doubled exclusive-axis tags, and 39 tags
on the recipes partition that re-encoded its own `operation` field as vocabulary. Six recipe
`outputRef`s pointed at `item.humanoid-core-a-001`, derived from the role name; the real id is
`item.humanoid-torso-a-001`. All fixed in place, all mechanical.

Eleven regression tests in `ContractCorrectionTests.cs` pin each correction, each paired with a
negative case proving the rule was narrowed rather than removed — a base type still cannot be
`frame: any`, an unknown tag is still an error, `<b>` in a display template still rejects. 67 tests
green; corpus **PASS at 1,129 entries across 93 files**.

### ⚖ Decision 24 — the validator generates the briefs

Four of the six BLOCKED reports this build, and every partition-id error, came from the same place:
a brief transcribing an id template or partition key by hand. `--list-partitions` dumps the
allocation table the validator itself derives, and stage 1c's 32 briefs are generated from it. No
partition id, id prefix, or pool key passes through a human or a summary on its way to an agent.

---

## Next

- **0b** — F1 word pools (Opus), V1 validator (Opus, code under `tools/ItemSeedValidator/`).
- **0c** — F5 exemplars, four files covering the hardest variations, using frozen registry values.
- **Freeze gate** — the 18-item checklist in the fleet plan §7.1, then flip `frozen: true`.
- **Pilot** — five agents spanning a Haiku partition, a Sonnet partition and one making
  cross-references. It tests the **contract**, not the authors: five of seven problem classes stop the
  fleet.
