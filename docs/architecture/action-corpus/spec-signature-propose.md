# Spec: `signature-propose` (A-P3)

**Module id:** `signature-propose` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: yes**
**Depends on:** `A-S1 distribution-planner` **and `A-S2 brief-assembly`** — a dependency, not a flag
(⛔ **DECIDED 2026-09-03:** it read `A-P2 family-propose`; A-S2 now owns assembling this stage's brief — §6)
⚠️ The capability map's gate still stands — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). Written ahead of approval on the owner's instruction.

**What it owns.** The third narrow model judgement, and the only one with two jobs at once: *"what makes THIS
ONE creature unlike its siblings?"* It reads a brief carrying a **species anchor** — species motifs,
anti-motifs, element, rarity, theme — the planner's mechanical slot, **and the accepted output of its own
family's P2 round**. That last input is why this cannot be `A-P2` with a `scope` field: P-family never has to
differ from anything, and P-signature exists to differ (`action-corpus-map.md:63`, `action-corpus-ideal.md:634-655`).
It forces the ordering **P1 ∥ P2 → P3**.

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
7. **Small-batch proof first** — `--dry-run` and a small `--count`. The call budget is a ceiling, not a plan.
8. **Tests never call a model** — the transport stub **raises**.
9. **The roster is 84 species (53 with family assignments), not 904.**

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| Species motif anchors — **84 species**, each with `motifs`, `antiMotifs`, `basis`, `tautological` | `data/seed/demons/_generated/motif-assignments.json` (84 keys, measured 2026-09-03) |
| Family assignments — 53 species, 19 families | `data/seed/demons/_generated/family-assignments.json` |
| `SpeciesBasics.InnateActionId` — per species, nullable, validated, assembled, persisted | `Actions/ActionRow.cs:87` · `Actions/ActionValidator.cs:107-115` · `Actions/Grants/ActionSetAssembler.cs:60-61` · `FusionRpg.Data/Sqlite/RpgStore.Actions.cs:546-549` |
| Closed action vocabularies | `ActionEnums.cs:26-49`, `ActionTargetSpec.cs:14-33`, `:42-48` |
| Rung table with per-row `structureBudget` | `data/tuning/action-rungs.v1.json:11-20` |
| Schema audit, permutation, vote, bounded self-heal | `pipeline/model.py:53-99`, `anchor/permute.py:16-30`, `anchor/vote.py:23-40`, `llm_caller.py:207-236` |
| The stage shape to copy exactly | `adapters/effects/affix/prompts.py:26-112`, `generate_affixes.py:74-96` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `Instantiator.TryInstantiate` — doc-comment references only, no production caller | `Instantiator.cs:92`; `InstanceProducer.cs:22`, `Resolver.cs:28`, `RpgStore.AtomInstances.cs:104` |
| `data/seed/actions/` unreadable — no `kind`/`entries` envelope | `corpus/model.py:159-185` |
| `StructureBudgetGuard` cannot detect `reaction` / `restriction`, the two axes that are the signature tier's only structural advantage over family | `action-corpus-ideal.md:1462-1468`; the axes appear only at rungs 9-10 in `data/tuning/action-rungs.v1.json:19-20` |

### Real gap

- **This stage** — no `signature-propose` adapter, schema, prompt or entrypoint exists.
- **`A-S1` and `A-P2` do not exist**, and this stage cannot run before P2's round is accepted.
- **Corpus sizing was computed against 904 species.** At the shipped 84 with 3 signature actions each the
  signature tier is **252**, not 2,712 (`action-corpus-ideal.md:1426-1436`). The per-species count is a
  tunable precisely so a re-run is a config change.
- **31 of 84 species have motifs but no family**, so their briefs carry no family output to differ from.
  The stage must handle an empty `familyActions` list as a first-class case, not an error.

## 2. The contract

### The JSON schema — no magnitude field anywhere

**⛔ The `description` strings are written out below — added 2026-09-03 (review F19).** AC2 has
always asserted *"every property has a `description` containing a negative clause — asserted
mechanically over the schema"*, and **the schema carried no `description` key at all**, so the
assertion had nothing to assert over. They are written here, in the schema, because a description that
lives in prose beside a schema is a description the audit cannot read. Each follows the hardened
`blocked` description at `adapters/demons/anchor/prompts.py:74-82`, rewritten after a real local model
filled that field with `"plant"` on 2026-09-01 (`prompts.py:64-70`): normal case first, then the
exception, then what must **not** go in the field. `differentiator`'s was already drafted in prose
below; it is now where the audit can read it.

```jsonc
SIGNATURE_ACTION_SCHEMA = {
  "type": "object",
  "properties": {
    "name": {
      "type": "string",
      "description":
        "The action's display name as a player sees it — two to five words, in the game's voice, "
        "reading as THIS creature's own. Do NOT restate the atom family ids you picked, and do NOT "
        "reuse or re-skin the name of any family action listed above. It is NOT a sentence — no "
        "imperative verb, no trailing punctuation."
    },
    "flavor": {
      "type": "string",
      "description":
        "One line a player would read under the name, under 140 characters, evoking what makes this "
        "one creature unlike its siblings. It is NOT a rules description: never say what the action "
        "does mechanically, and never write a number, a duration, a chance or a range — tables you "
        "never see decide all of those. It never expresses an anti-motif."
    },
    "atomFamilies": {
      "type": "array", "minItems": 1,
      "description":
        "Which of the atom families listed above this action is built from — choose one or more "
        "from that list. You are choosing WHICH families, never HOW MUCH of any of them: this is "
        "NOT a place for a magnitude, a weight or a count. Do NOT invent a family that is not in the "
        "list, do NOT write a concrete atom id — a family names a pool, resolved later by code, per "
        "player — and do NOT pick exactly the same set as any family action listed above.",
      "items": { "type": "string", "enum": [ /* brief.pool.allowedAtomFamilies,
                                permuted per (briefId, "atomFamilies", sampleIndex) */ ] }
    },
    "motifsExpressed": {
      "type": "array", "minItems": 1,
      "description":
        "Which of THIS species' motifs listed above this action actually expresses, chosen only from "
        "that list — or the single value \"none\" when it expresses none of them, which is a real "
        "and acceptable answer. This is NOT the anti-motif list: a motif named there is a refusal, "
        "and naming one here is a rejection, not an expression. Do NOT invent a motif and do NOT "
        "leave the key out — \"none\" is a value, a missing key is a defect.",
      "items": { "type": "string", "enum": [ /* brief.anchor.motifs + "none",
                                permuted per (briefId, "motifsExpressed", sampleIndex) */ ] }
    },
    "differentiator": {
      "type": "string",
      "description":
        "The ONE axis on which this action differs from the family actions listed above. It is NOT "
        "the action's category, NOT its power level, and NOT how good it is. Choose \"none\" when it "
        "does not meaningfully differ — saying \"none\" honestly is better than inventing a "
        "difference, it is never counted against this answer, and it is more useful to us than a "
        "guess. Do NOT name more than one axis and do NOT invent an axis outside the list.",
      "enum": [ /* "atoms" | "targetShape" | "condition" | "timing" | "resource" | "none",
                   permuted per (briefId, "differentiator", sampleIndex) */ ]
    },
    "rationale": {
      "type": "string",
      "description":
        "One sentence saying why these atom families make this creature's signature action, and how "
        "it differs from its family's. It is NOT a restatement of the name or the flavour, and it is "
        "NOT a justification of any number — you never see a number. Do NOT use it to add an effect "
        "you did not put in `atomFamilies`."
    },
    "blocked": {
      "type": "string",
      "description":
        "Leave this as the exact empty string \"\" when you WERE able to design the action above — "
        "this is the normal case for almost every brief. Only write a non-empty reason here when the "
        "brief genuinely gives you NOTHING to work from (for example, no motifs AND no eligible atom "
        "families). Do NOT put a name, a motif, a differentiator or any other real answer here — it "
        "is a blocked-flag, not a second answer field. Having no family to differ from is NOT a "
        "blocked case: it is stated in the brief and you design the action anyway."
    }
  },
  "required": ["name", "flavor", "atomFamilies", "motifsExpressed",
               "differentiator", "rationale", "blocked"],
  "additionalProperties": false
}
```

- **No `number`/`integer`, no numeric-string enum member, no `pattern` admitting digits.** All three enums
  are filled at call time from closed lists.

**⛔ DECIDED 2026-09-03 — where the `atomFamilies` enum's members come from.** `allowedAtomFamilies`
now has a stated source: the **98 authored affix families** in
`data/seed/items/affix-families/*.json` (`entries[].id`). Until this decision no spec said which set
`atomFamilies` names, and the tree holds three **completely disjoint** candidates — 17 demo families
under `data/seed/atoms/`, the 98 authored ones, and the 5 ids in `data/seed/actions/pairings.json`,
with **zero overlap between any pair** (measured 2026-09-03; the evidence table is in
`spec-distribution-planner.md` §2).

It matters most at this stage. `atomFamilies` is one of the two **voted** fields here, and the
"differ from every family action" validator compares atom-family **sets**: two sets drawn from a
namespace that resolves nowhere would still compare equal or unequal perfectly well, so the defect
would have survived every gate this spec has and surfaced at bind time. Every member of the enum is
now an id that exists and carries a `kindId`, a `params.channel` and a `powerBand`.
- **`differentiator` admits `none`**, and its negative clause is the load-bearing sentence — now
  written into the schema above rather than only quoted here. A `none` answer is a real, useful
  signal: it tells A-S3 the candidate is a near-duplicate before the hash sets do.
  **⛔ And nothing downstream penalises it — corrected 2026-09-03 (review).** A-S4's g3 carried the
  clause `differentiator != "none"` as a quality check, which scored down the exact honest answer this
  description asks for and teaches the pipeline to invent a difference. It now **records** the value
  and reports the `none` rate as a first-class round metric, contributing nothing to a verdict in
  either direction (`spec-validate-heal.md` §2 Stage 1).
- **`blocked` is required** by `audit_schema` (`model.py:92-97`), using the hardened empty-string convention
  (`anchor/prompts.py:61-83`).
- **`confidence` is not a model field** — the vote resolver writes it (`vote.py:16-20`).

### What the system prompt says

*You design the one action that makes a single creature unlike its siblings in the same family.* Four
negative clauses:

- **You never write a number.** Rung, cost, duration, chance and magnitude come from tables you never see.
- **You never repeat a family action.** The family's actions are listed for you to differ from, not to
  reuse or to re-skin with a new name.
- **You never express an anti-motif**, species or family.
- **You never invent an atom family, a motif or a differentiator.** You pick from the lists given, or you
  set `blocked`.

### What the brief inlines

Literal values, no file citation (`affix/prompts.py:60-61` states the reason). In order:

- the species key and its **element** and rarity as labels;
- the species' motifs in permuted order, then its anti-motifs each carrying *"an action expressing this is
  rejected"*;
- **its family's accepted actions**, each as `name + sorted atomFamilies + fingerprint`, under the heading
  *"your action must differ from every one of these"* — and, when the species has no family, the explicit
  sentence *"this creature has no family; there is nothing to differ from"* rather than an empty section
  the model can read as an omission;
- the planner's slot (`category`, `targetMode`, `areaShape`, `relation`, `kind`) and the rung band as a
  label, never numbers;
- eligible atom families in permuted order; forbidden families with the reason;
- pairing role and, when it is `payoff`, the **payoff atom family** it pays off and the enabler
  families that would satisfy it. ⛔ **CORRECTED 2026-09-03 (review F7):** this read *"the status it
  pays off"*, and the pairing surface has no status in it — `pairings.json` maps
  `atom.chill-punisher`/`atom.rot-punisher` to enabler **atom families**, and
  `EnablerPayoffPairings.IsPayoff(string atomFamily)` (`EnablerPayoffPairings.cs:26`) takes families
  throughout. The role is **optional**, and with two payoff keys in the table it is `none` for most
  briefs (`spec-distribution-planner.md` §3 step 6), in which case nothing about pairing is inlined;
- `avoidNeighbours` fingerprints as *"do not produce anything like these."*

`build_context` returns those inputs read-only so the validators read the same object the brief rendered from.

### Which fields are voted, and why those

**Two voted fields — `atomFamilies` and `differentiator`** — three permuted samples each.

- **`atomFamilies`**, as in A-P1/A-P2: it is the mechanical identity, it is what tier-1 dedup hashes, and
  being wrong is expensive to fix after acceptance.
- **`differentiator` is voted only here**, and the argument is specific rather than inherited: this is the
  one judgement the pipeline exists to make. If it is wrong, the species' signature action is a family
  action with a different name — the exact failure P3 was split out to prevent — and it is invisible to
  every deterministic gate downstream, because tiers 1 and 2 hash mechanics and tier 3 is advisory.
- **`motifsExpressed` is not voted** for the same reason as in A-P2: it feeds the advisory tier-3 review
  queue and never rejects anything, so tripling its cost buys nothing.
- Everything mechanical stays planner-owned and never reaches the model.

A 1-1-1 split on either voted field writes `confidence: "unresolved"` for the candidate. Sample 0 is never
taken by default.

## 3. What it must NOT do

- **Never pick a magnitude** — no rung, cost, cooldown, duration, chance, weight, tier or stack count.
- **Never run before its family's P2 round is accepted.** A brief whose `familyActions` key is *absent*
  raises; a brief whose `familyActions` is an *empty list* is legal and means "no family". The two cases
  must not be collapsed — collapsing them is how P3 silently runs early.
- **Never reuse a family action's atom-family set verbatim** — a hard validator, not prompt advice.
- **Never invent a motif, anti-motif, atom family, differentiator or slot value.**
- **Never emit a concrete atom id.** An atom names a pool; element, tier and cell resolve at layer 4, per
  player, at roll time. A cell is a target, never an identity (`action-corpus-map.md:37-42`).
- **Never promote its own output to `Innate`.** That is `A-S6`, model-free permanently — the innate is a
  free sixth slot outside `LoadoutSet.MaxSize = 5` (`Loadout/LoadoutSet.cs:40`), so choosing it is a
  magnitude decision (`action-corpus-map.md:67`).
- **Never re-roll** (`Instantiator` is the roll, Law 1), never carry state between calls, never write to
  `data/seed/actions/`, never grade itself.

## 4. Testing strategy

1. **Stubbed transport that raises.** Context, brief, schema audit and every validator run under a transport
   whose only behaviour is to raise (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8` is the precedent).
2. **Determinism / replay.** Same brief + same `sampleIndex` → byte-identical brief text and identical
   permuted order for **all three** enums, by hash; same samples → same `VoteResult` for both voted fields;
   a recorded transcript replayed produces a byte-identical candidate file under canonical serialisation.
   A separate test asserts the **family output ordering is fixed** (sorted by action id) before it is
   inlined — an unsorted list makes the brief, and therefore the run, order-dependent.
3. **Planted violations**, one test each, all rejected:
   - a schema with `"potencyMilli": {"type": "integer"}` → `audit_schema` defect;
   - a numeric-string enum → defect; a missing `blocked` → defect;
   - a `differentiator` enum omitting `none` → this module's schema test fails;
   - a draft whose `atomFamilies` exactly equals a listed family action's set → hard reject, re-prompt names
     the colliding action;
   - a draft expressing a species anti-motif → hard reject, re-prompt names the motif;
   - a brief with **no** `familyActions` key → raises; a brief with `familyActions: []` → runs, and the
     rendered brief contains the explicit "no family" sentence;
   - three distinct samples on `differentiator` → `unresolved`, `value is None`.

## 5. Acceptance criteria

1. `audit_schema(SIGNATURE_ACTION_SCHEMA)` returns an empty defect list, under a test CI runs.
2. Every property has a `description` containing a negative clause — asserted mechanically over the
   schema. ⛔ **The strings are written into §2's schema as of 2026-09-03 (review F19)**; before that
   the schema carried no `description` key at all, so this criterion asserted over nothing.
3. `motifsExpressed` and `differentiator` both admit `none`; `additionalProperties` is `false`; every field
   is `required`.
4. The brief inlines the family's accepted actions in a **fixed sorted order**, and contains no file path
   or `.md` reference.
5. A missing `familyActions` key raises; an empty list renders the explicit no-family sentence.
6. Both `atomFamilies` and `differentiator` are voted over three permuted samples; 1-1-1 on either yields
   `unresolved` with `value is None`; a 2-1 records the minority.
7. A draft duplicating a family action's atom-family set is rejected and the re-prompt names it.
8. `--dry-run` renders briefs and makes zero model calls; `--count N` bounds the run.
9. The full test module passes with the transport stubbed to raise.
10. A second run over unchanged inputs is byte-identical by hash; `_provenance` records model id, prompt
    version, brief hash, **the P2 candidate-set hash this round differed against**, and the candidate-set hash.
11. Repairs bounded at **two** (`max_heal=2` passed explicitly; the config default is 3,
    `llm_caller.py:45`), then `unresolved`. ⛔ **CORRECTED 2026-09-03 (review F9):**
    `call_with_self_heal` does not produce `unresolved` — it *"never raises"* and substitutes
    `default_for(key, original_value)` (`llm_caller.py:229-234`), whose shipped default returns the
    original item, which for a generation stage is a **brief field**. This stage passes
    `default_for=lambda key, original: None` explicitly; `unresolved` is the verdict A-S4 writes from
    the helper's `FAILED:<reason>` soft entries (`llm_caller.py:255-258`). The adapted contract is
    stated once, in `spec-validate-heal.md` §2 Stage 3.
11b. A `differentiator: "none"` is **accepted and recorded**, never scored down — asserted by a test,
    because the schema description above promises exactly that and a gate that broke the promise
    would teach the model to lie (`spec-validate-heal.md` §2 Stage 1).
12. No emitted candidate contains a numeric value of any kind.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| **The assembled P3 brief**, carrying `familyActions` | **A-S2** `brief-assembly` | does not exist — this is the hard in-program ordering |
| The plan behind it — species anchor and slot | **A-S1** `distribution-planner` (via A-S2) | does not exist |
| The accepted family round A-S2 reads | **A-P2** `family-propose` | does not exist |
| Species motifs / anti-motifs / element / rarity | **seedsmith D2/D5** | 84 species on disk; rarity for the unrostered remainder is unspecced |
| Quality gates, bounded repair | **A-S4** `validate-heal` | does not exist |
| Innate promotion | **A-S6** `innate-picker` | model-free, downstream, not this stage's business |
| Channel pools · binding production | **effect-atom E30** · **effect-pipeline module 4** | outside this program; `effect_binding` has zero rows |

**⛔ DECIDED 2026-09-03 — a new module assembles this stage's brief, and it is A-S2.** This spec
depended on **A-P2** for a brief field, which is not a thing a model stage can produce: `familyActions`
does not exist until A-P2's round has been generated, validated, deduped and **id-assigned**, and
A-S1 builds briefs in a static, token-free phase before any of that. Nobody owned the step in
between, and this stage raises on a brief whose `familyActions` key is absent (§3), so **100% of its
input would have raised.**

**A-S2 `brief-assembly` owns it.** It reads A-S1's plan and A-P2's **accepted, deduped, id-assigned**
round — never A-P2's raw output — and emits this stage's brief. The distinction is the whole point: a
candidate A-S4 rejected or A-S3 deduped away must not appear in the list this stage is told to differ
from, or the pipeline differentiates against content that was never accepted.

**This closes F15 recurring.** Family *motifs* had the identical defect one field over in the same
brief — A-P2 said *"A-S1 owns it"*, A-S1 never mentioned it, and A-P2's AC5 rejected 100% of A-S1's
output; the review's words were *"ownership passed in a circle."* That one was closed by writing the
derivation into A-S1 (§3 step 2b). `familyActions` was the same defect, unnoticed, and it closes the
same way: a named owner.

**Hazards.**

1. **The family/signature structural split is PARTLY enforceable — ⛔ CORRECTED 2026-09-03 (review
   F3/F4).** The earlier wording put `reaction` and `restriction` in one bucket and cited the ideal
   rather than the guard. Read against `StructureBudgetGuard.cs:27-34` they are in different states,
   and the difference decides how much of this hazard is real:
   - **`reaction` is unspendable, not undetectable.** The guard verified `ActionKind` has exactly
     three members and none is reaction-shaped, so it is *correctly* never flagged. A-S1 refuses to
     emit a brief naming it and A-S4's g2 hard-rejects a draft claiming it
     (`spec-tier-access-gate.md` AC5) — it is not a hole in the split, it is not in the split.
   - **`restriction` is genuinely undetectable** — it needs the effect-atom program's per-atom
     payload/target data, outside the three tables the guard reads. It **is** assignable, it is the
     signature tier's **one** exclusive axis under A-S1's union-to-ceiling rule
     (`spec-distribution-planner.md` §3 step 5: general 2 axes, family 5, signature 6), and no gate
     checks it.

   So *"signature actions are structurally richer"* is a claim exactly one axis wide and nothing
   verifies that axis. `differentiator` and the tier-3 review queue remain the only signal that a
   signature action is actually distinct, which is why `differentiator` is voted. **Under the old
   intersection rule this hazard had zero instances** — `restriction` was unreachable in every tier —
   so the note described a case that could not occur; it can now.
2. ~~**A `minRung` floor at 5 for the signature tier is unpriced.**~~ ⛔ **RESOLVED 2026-09-03 — the
   floor is dropped.** The signature window is `[1,10]`, not `[5,10]`
   (`spec-distribution-planner.md` §3 step 4; `spec-rung-semantics.md` §3.2). The floor at issue was
   **A-S1's authored `rungBand` floor** — `minRung` never existed in code or data (grepped
   2026-09-03: zero hits across `src/` and `data/`; the only `MinRung` is `AuraTuning.cs:20`, the
   aura ladder's unrelated rung-7 floor). With no floor a first-ever signature unlock arrives at
   rung 1 and pays `costMulti: 1000`, so **the `costMulti: 3627` argument is moot** — it priced a
   case that no longer occurs. The **ceiling stays 10**, so this stage's structural context is
   unchanged: signature still draws from 6 assignable axes.
3. **C1's family-access widening is gated** on a per-rung `powerBudget` row, a family-aware non-additive
   price (needs D2), and a budget check with a production caller (`action-corpus-ideal.md:707-728`). Until
   then, briefs are structure-gated. This stage never branches on tier.
4. **Sizing rests on an unsourced rate** (`~1,162 calls/h`, `action-corpus-ideal.md:1448`; the figure itself is at `:561`) and on the
   corrected 84-species roster. Both belong in the plan's own arithmetic, not in this stage's assumptions.
