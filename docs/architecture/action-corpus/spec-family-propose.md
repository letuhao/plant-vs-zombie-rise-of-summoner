# Spec: `family-propose` (A-P2)

**Module id:** `family-propose` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: yes**
**Depends on:** `A-S1 distribution-planner` · **Feeds:** `A-P3 signature-propose` (its output is P3's context)
⚠️ The capability map's own gate still stands — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). Written ahead of approval on the owner's instruction; not buildable until it lifts.

**What it owns.** The second narrow model judgement: *"what expresses THIS family?"* It reads one brief
carrying a **family anchor** — family id, its motifs, its anti-motifs, its theme — plus the planner's
mechanical slot, and returns one action seed: a name, flavour, the atom families it is built from, and which
of the family's own motifs it expressed. It never sees a species, and it never has to differ from a sibling —
that second job belongs to `A-P3`, which is why these are two pipelines and not one with a flag
(`action-corpus-map.md:62-63`).

**⛔ Binding constraints, restated inline — a downstream session reads this file, not its links.**

1. **The LLM writes identity. Deterministic code writes magnitude.** No model picks a number, weight,
   probability, duration, tier or rung. Enforced by `audit_schema`
   (`tools/seedsmith/seedsmith/pipeline/model.py:53-99`), never by review.
2. **Three pipelines, not one parameterised stage.** P-general (role + slot, no anchor), P-family (family
   motifs/anti-motifs/themes), P-signature (species motifs + element + its family's output).
3. **Permute every enum**, seeded from `(entity_id, field, sample_index)` — `sample_index` **inside** the
   seed, or three votes are one sample (`adapters/demons/anchor/permute.py:16-30`).
4. **Majority-vote only load-bearing fields.** 1-1-1 → `unresolved`, never the first option
   (`adapters/demons/anchor/vote.py:23-40`).
5. **Every enum description carries a negative clause** saying what the field is NOT. `none` is a value; a
   missing key is a defect.
6. **TRANSIENT ≠ QUALITY.** A pause is transient — replay, no new call. Name the defect when re-prompting;
   bound repairs at **two**, then `unresolved`.
7. **Small-batch proof first** — `--dry-run` and a small `--count`. The call budget is a ceiling, not a
   plan; a full run is an owner decision behind a quality gate.
8. **Tests never call a model** — the transport stub **raises**.
9. **The roster is 84 species, 53 with family assignments — not 904.** This is the constraint that bites
   *this* module hardest; see §1.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| Family assignments — **53 species across 19 family tokens** | `data/seed/demons/_generated/family-assignments.json` (measured 2026-09-03: 53 keys; values are lists of family tokens, e.g. `bucketnutzombie: ["bucket"]`) |
| Motif assignments — **84 species**, each with `motifs`, `antiMotifs`, `basis`, `tautological` | `data/seed/demons/_generated/motif-assignments.json` (84 keys) |
| Closed action vocabularies (5 categories, 6 target modes, 4 shapes, 8 tags) | `ActionEnums.cs:26-47`, `ActionTargetSpec.cs:14-33`, `:42-48` |
| Rung table with per-row `structureBudget` | `data/tuning/action-rungs.v1.json:12-21` |
| Schema audit — numeric fields **and** a missing `blocked` escape both rejected | `pipeline/model.py:53-99` |
| Permutation, vote resolution, bounded self-heal | `anchor/permute.py:16-30`, `anchor/vote.py:23-40`, `llm_caller.py:207-236` |
| The stage shape to copy exactly | `adapters/effects/affix/prompts.py:26-112`, `generate_affixes.py:74-96` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `Instantiator.TryInstantiate` — doc-comment references only, no production caller | `Instantiator.cs:92`; `InstanceProducer.cs:22`, `Resolver.cs:28`, `RpgStore.AtomInstances.cs:104` |
| `data/seed/actions/` unreadable by the loader — no `kind`/`entries` envelope | `corpus/model.py:159-185` |

### Real gap

- **This stage** — no `family-propose` adapter, schema, prompt or entrypoint exists.
- **`A-S1` does not exist**, so nothing produces the brief.
- **⛔ The premise this pipeline was sized on is wrong, and it is measurable.** §20 of the ideal justified
  P-family partly because a family brief is *"shared across ~48 species"* (904 / 19). Part VIII already
  corrected that to **4.4** at the shipped 84 (`action-corpus-ideal.md:1420-1424`). Measured against the
  real file it is smaller still: **53 assigned species over 19 families = 2.8 mean**, largest family
  `cherry` = **7**, **eleven families hold exactly 2**, and `nut` holds exactly **1**. A judgement shared by
  two species is barely a family judgement; shared by one it is a signature judgement wearing a family
  label. **This spec does not resolve that** — see §6, hazard 1.
- ~~**Family motifs do not exist as a distinct artifact.**~~ ⛔ **CLOSED 2026-09-03 (review F15).**
  `motif-assignments.json` is still keyed by *species* (84 keys), and a family's motif set still has
  to be derived — but the derivation **now exists**, written into A-S1
  (`spec-distribution-planner.md` §3 step 2b) rather than merely assigned to it. This spec said
  *"A-S1 owns it"* and A-S1's spec never mentioned it, so ownership passed in a circle and **AC5
  rejected 100% of A-S1's output**. The rule is: `familyMotifs` = the intersection of member motifs,
  `familyAntiMotifs` = the union of member anti-motifs, `familyMotifBasis` recording which of
  `intersection`/`majority`/`frequency` produced it. This stage still refuses a brief whose
  `anchor.familyMotifs` **key is absent** — but an empty list is a legal value, not a refusal, which
  is the correction AC5 needed.

## 2. The contract

### The JSON schema — no magnitude field anywhere

**⛔ The `description` strings are written out below — added 2026-09-03 (review F19).** AC2 has
always asserted *"every property has a `description` and every description contains a negative clause
— asserted mechanically over the schema"*, and **the schema carried no `description` key at all**.
They are written here, in the schema, because a description that lives in prose beside a schema is a
description the audit cannot read. Each follows the hardened `blocked` description at
`adapters/demons/anchor/prompts.py:74-82`, rewritten after a real local model filled that field with
`"plant"` on 2026-09-01 (`prompts.py:64-70`): normal case first, then the exception, then what must
**not** go in the field.

```jsonc
FAMILY_ACTION_SCHEMA = {
  "type": "object",
  "properties": {
    "name": {
      "type": "string",
      "description":
        "The action's display name as a player sees it — two to five words, in the game's voice, "
        "reading as something the WHOLE family would use. Do NOT restate the atom family ids you "
        "picked, and do NOT name a single species: if the name only makes sense for one member, it "
        "belongs to a different pipeline. It is NOT a sentence — no imperative verb, no trailing "
        "punctuation."
    },
    "flavor": {
      "type": "string",
      "description":
        "One line a player would read under the name, under 140 characters, evoking what this family "
        "is like. It is NOT a rules description: never say what the action does mechanically, and "
        "never write a number, a duration, a chance or a range — tables you never see decide all "
        "of those. It never names one species and never expresses an anti-motif."
    },
    "atomFamilies": {
      "type": "array", "minItems": 1,
      "description":
        "Which of the atom families listed above this action is built from — choose one or more "
        "from that list. You are choosing WHICH families, never HOW MUCH of any of them: this is "
        "NOT a place for a magnitude, a weight or a count. Do NOT invent a family that is not in the "
        "list, and do NOT write a concrete atom id — a family names a pool, and which member of "
        "the pool a player gets is decided later, by code, per player.",
      "items": { "type": "string", "enum": [ /* brief.pool.allowedAtomFamilies,
                                permuted per (briefId, "atomFamilies", sampleIndex) */ ] }
    },
    "motifsExpressed": {
      "type": "array", "minItems": 1,
      "description":
        "Which of the FAMILY motifs listed above this action actually expresses, chosen only from "
        "that list — or the single value \"none\" when it expresses none of them, which is a real "
        "and acceptable answer. This is NOT the anti-motif list: a motif named there is a refusal, "
        "and naming one here is a rejection, not an expression. Do NOT invent a motif, do NOT name a "
        "species-specific one, and do NOT leave the key out — \"none\" is a value, a missing key "
        "is a defect.",
      "items": { "type": "string", "enum": [ /* brief.anchor.familyMotifs + "none",
                                permuted per (briefId, "motifsExpressed", sampleIndex) */ ] }
    },
    "rationale": {
      "type": "string",
      "description":
        "One sentence saying why these atom families express this family. It is NOT a restatement of "
        "the name or the flavour, and it is NOT a justification of any number — you never see a "
        "number. Do NOT use it to add an effect you did not put in `atomFamilies`, and do NOT use it "
        "to single out one member of the family."
    },
    "blocked": {
      "type": "string",
      "description":
        "Leave this as the exact empty string \"\" when you WERE able to design the action above — "
        "this is the normal case for almost every brief. Only write a non-empty reason here when the "
        "brief genuinely gives you NOTHING to work from (for example, a family with an empty motif "
        "list AND an empty list of eligible atom families). Do NOT put a name, a motif, a family id "
        "or any other real answer here — it is a blocked-flag, not a second answer field."
    }
  },
  "required": ["name", "flavor", "atomFamilies", "motifsExpressed", "rationale", "blocked"],
  "additionalProperties": false
}
```

- **No `number`/`integer`, no numeric-string enum member, no `pattern` admitting digits.** Both enums are
  filled at call time from the brief's own lists, so the model can only choose, never invent.
- **`none` is a member of `motifsExpressed`.** A model that expressed no family motif must be able to say so;
  a silently omitted key would hand the entry a hidden pass through the quality gate — tag absence is a stat.
- **The enum is filled from `brief.anchor.familyMotifs`**, the derived family set, **not** from a
  species' own motifs. ⛔ **CORRECTED 2026-09-03 (review F15):** it read `brief.anchor.motifs`, which
  on a family brief was undefined — the derivation had no owner. It has one now
  (`spec-distribution-planner.md` §3 step 2b): `familyMotifs` is the **intersection** of the family's
  member species' motifs and `familyAntiMotifs` the **union** of their anti-motifs, both sorted
  byte-wise, with `familyMotifBasis` recording which rule produced the set. Measured 2026-09-03, all
  19 families intersect to a non-empty set and every one of them is exactly **2 motifs** (`cherry`
  over its 7 members gives `["僵尸", "樱桃"]` against a union of 6), so the enum is tight by
  construction rather than by hope.
- **`blocked` is required** by `audit_schema` (`model.py:92-97`) and uses the empty-string convention whose
  description was hardened after a real local model filled it with a plausible wrong answer
  (`anchor/prompts.py:61-83`).
- **`confidence` is not a model field.** The vote resolver writes it (`vote.py:16-20`).

### What the system prompt says

*You design an action that expresses one family of creatures — what makes the whole family recognisable, not
what makes one member special.* Four negative clauses:

- **You never write a number.** Rung, cost, duration, chance and magnitude come from tables you never see.
- **You never name a single species.** This action belongs to every member of the family; if it only makes
  sense for one of them, it is the wrong pipeline's job.
- **You never express an anti-motif.** The anti-motif list is what this family must NOT be, not a hint.
- **You never invent an atom family or a motif.** You pick from the lists given, or you set `blocked`.

### What the brief inlines

`build_brief(context)` renders literal values and cites no file (`affix/prompts.py:66-80` states the reason).
In order: the family id and its theme label; the family's motifs, in permuted order; the family's
**anti-motifs**, each with the sentence *"an action expressing this is rejected"*; the planner's mechanical
slot (`category`, `targetMode`, `areaShape`, `relation`, `kind`) and the rung band as a label, never numbers;
the eligible atom families in permuted order and the forbidden ones with the reason they are forbidden; the
pairing role and, when it is `payoff`, the **payoff atom family** it pays off and the enabler
families that would satisfy it; and `avoidNeighbours` fingerprints as *"do not produce anything like
these."* No species key, no element, no per-species motif ever reaches this brief.
⛔ **CORRECTED 2026-09-03 (review F7):** this read *"the status it pays off"*. The pairing surface has
no status in it — `pairings.json` maps `atom.chill-punisher`/`atom.rot-punisher` to enabler **atom
families**, and `EnablerPayoffPairings.IsPayoff(string atomFamily)` (`EnablerPayoffPairings.cs:26`)
takes families throughout. The role is also **optional**: with two payoff keys in the table it is
`none` for most briefs (`spec-distribution-planner.md` §3 step 6), and a brief carrying `none`
inlines nothing about pairing at all.
`build_context` returns the same inputs read-only for the validators.

### Which fields are voted, and why those

**One voted field: `atomFamilies`**, three permuted samples, resolved by `vote.resolve_vote` over the
sorted-tuple form.

- Same argument as A-P1: the family set is the mechanical identity, it is what tier-1 dedup hashes, and
  being wrong is expensive to fix after acceptance.
- **`motifsExpressed` is deliberately NOT voted.** It is a self-report feeding the tier-3 review queue, and
  tier 3 is advisory by design — a stochastic component inside an acceptance decision is how a
  non-reproducible run gets built by accident. Voting it would add a third of the run's cost for a field
  that never rejects anything.
- Everything mechanical — category, target mode, shape, relation, kind, rung band — is planner-owned and
  never reaches the model.

A 1-1-1 split writes `confidence: "unresolved"`; the candidate goes to review and sample 0 is never taken by
default.

## 3. What it must NOT do

- **Never pick a magnitude** — no rung, cost, cooldown, duration, chance, weight, tier or stack count.
- **Never read a species anchor.** A brief carrying `anchor.speciesKey`, species motifs or an element
  **raises**. Accepting it silently is how P-family becomes P-signature with worse context.
- **Never differentiate from siblings.** That is A-P3's judgement and it needs this stage's output first.
- **Never invent a motif, an anti-motif, an atom family or any slot value.**
- **Never emit a concrete atom id.** An atom names a pool; element, tier and cell resolve at layer 4, per
  player, at roll time. A cell is a target, never an identity (`action-corpus-map.md:37-42`).
- **Never re-roll** — `Instantiator` is the roll (Law 1).
- **Never carry state between calls**, and never write to `data/seed/actions/`.
- **Never grade itself** — `confidence` comes from the vote.

## 4. Testing strategy

1. **Stubbed transport that raises.** Context building, brief rendering, schema audit and every validator run
   under a transport whose only behaviour is to raise, so *"makes no call"* is proven
   (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8` is the precedent).
2. **Determinism / replay.** Same brief + same `sampleIndex` → byte-identical brief text and identical
   permuted option order for **both** enums, asserted by hash; same three samples → same `VoteResult`; a
   recorded transcript replayed produces a byte-identical candidate file under canonical serialisation.
3. **Planted violations**, each its own test, each expected to be rejected:
   - a schema carrying `"tierCount": {"type": "integer"}` → `audit_schema` defect;
   - a schema with a numeric-string enum (`"1"`, `"2"`) → defect;
   - a schema with no `blocked` → defect;
   - a schema whose `motifsExpressed` enum omits `none` → this module's own schema test fails;
   - a draft naming a motif not in the brief → validator rejects and the re-prompt **names that motif**;
   - a draft whose `motifsExpressed` contains an **anti-motif** → hard reject, re-prompt names it;
   - a brief carrying `anchor.speciesKey` or `anchor.element` → the stage raises;
   - three distinct samples → `unresolved`, `value is None`, asserted so a later "take the first" refactor
     fails loudly.
4. **A roster test, not a claim.** A test reads `family-assignments.json` and asserts the per-family member
   counts the plan was sized on, so the 53/19/2.8 numbers cannot silently drift.

## 5. Acceptance criteria

1. `audit_schema(FAMILY_ACTION_SCHEMA)` returns an empty defect list, under a test CI runs.
2. Every property has a `description` and every description contains a negative clause — asserted
   mechanically over the schema. ⛔ **The strings are written into §2's schema as of 2026-09-03
   (review F19)**; before that the schema carried no `description` key at all, so this criterion
   asserted over nothing.
3. `motifsExpressed` admits `none`; `additionalProperties` is `false`; every field is `required`.
4. `build_brief` output contains no file path, no `.md`, no species key and no element token.
5. A brief with a species-scoped anchor raises. A brief whose `anchor.familyMotifs`,
   `anchor.familyAntiMotifs` or `anchor.familyMotifBasis` **key is absent** raises; a key present
   with an **empty list** is legal and renders the explicit *"this family has no shared motif"*
   sentence, exactly as A-P3 handles an empty `familyActions`
   (`spec-signature-propose.md:156-158`). ⛔ **CORRECTED 2026-09-03 (review F15):** *"a brief without
   derived family motifs raises"* rejected every brief A-S1 could produce, because no spec owned the
   derivation; A-S1 §3 step 2b now owns it, and absent-versus-empty is the distinction that makes
   this criterion satisfiable.
6. `atomFamilies` is voted over three permuted samples; 1-1-1 yields `unresolved` with `value is None`; a
   2-1 records the minority.
7. A draft expressing an anti-motif is rejected, and the recorded re-prompt text names the offending motif.
8. `--dry-run` renders briefs and makes zero model calls; `--count N` bounds the run at N candidates.
9. The full test module passes with the transport stubbed to raise.
10. A second run over unchanged inputs is byte-identical by hash; `_provenance` records model id, prompt
    version, brief hash and candidate-set hash.
11. Repairs bounded at **two** (`max_heal=2` passed explicitly; the config default is 3,
    `llm_caller.py:45`), then `unresolved`. ⛔ **CORRECTED 2026-09-03 (review F9):**
    `call_with_self_heal` does not produce `unresolved` — it *"never raises"* and substitutes
    `default_for(key, original_value)` (`llm_caller.py:229-234`), whose shipped default returns the
    original item, which for a generation stage is a **brief field**. This stage passes
    `default_for=lambda key, original: None` explicitly; `unresolved` is the verdict A-S4 writes from
    the helper's `FAILED:<reason>` soft entries (`llm_caller.py:255-258`). The adapted contract is
    stated once, in `spec-validate-heal.md` §2 Stage 3.
12. No emitted candidate contains a numeric value of any kind.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| The brief, including **derived family motifs/anti-motifs** | **A-S1** `distribution-planner` | does not exist; the derivation is unwritten |
| Family assignments | **seedsmith D2/D5** | 53 species, 19 families — real, on disk |
| Quality gates, bounded repair | **A-S4** `validate-heal` | does not exist |
| A loadable `data/seed/actions/` | **A-C1** `corpus-loader` | files silently skipped (`corpus/model.py:159-185`) |
| Channel pools | **effect-atom E30** | outside this program |
| Binding production | **effect-pipeline module 4** | `effect_binding` has zero rows |

**Hazards.**

1. **The family tier may not earn a pipeline at the shipped roster.** 2.8 species per family; eleven of the
   nineteen families hold two species and one holds one. This is a real design question the 48-per-family
   figure hid, and it is **not resolved here** — the plan phase must either raise the roster first,
   consolidate families, or accept that the thin families produce signature briefs by another name.
   Whatever it decides, the *architecture* is unchanged: P-family stays a distinct pipeline because
   P-signature reads its output.
2. **The other 31 species have no family.** 84 species carry motifs, 53 carry a family. This stage simply
   never receives a brief for the unassigned 31 — that is A-S1's quota problem, not a defect here, but a
   coverage report that does not say so would read as success.
3. **C1's family-access widening is gated** on a per-rung `powerBudget` row, a family-aware non-additive
   price (needs D2) and a budget check with a production caller (`action-corpus-ideal.md:707-728`). Until
   all three hold, briefs are **structure-gated**. This stage must not branch on tier — it reads the pool
   it is given.
4. **The `~1,162 calls/h` rate is unsourced** (`action-corpus-ideal.md:1447`), so any hour figure derived
   from this stage's budget is unverified.
