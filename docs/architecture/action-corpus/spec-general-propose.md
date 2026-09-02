# Spec: `general-propose` (A-P1)

**Module id:** `general-propose` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: yes**
**Depends on:** `A-S1 distribution-planner` · **Runs in parallel with:** `A-P2 family-propose` (map §5)
⚠️ The capability map still carries its own gate — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). This spec is written ahead of that approval on the owner's instruction; it is
not buildable until the gate lifts.

**What it owns.** The first of three narrow model judgements: *"what is a good role-based action that any
creature could hold?"* It reads exactly one brief and returns exactly one action seed — a name, a flavour
line, and a pick of atom **families** from the pool the planner already fixed. It has **no anchor at all**
(`action-corpus-map.md:61`): no family, no element, no motifs, no species. That absence is the pipeline's
whole identity, and it is why this cannot be `A-P2` with a `scope` flag.

**⛔ Binding constraints, restated here because a downstream session reads this file and not its links.**

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
7. **Small-batch proof first.** `--dry-run` and a small `--count` ship with the stage. The call budget is a
   ceiling, not a plan; a full run is an owner decision behind a quality gate.
8. **Tests never call a model** — the transport stub **raises**.
9. **The roster is 84 species (53 with family assignments), not 904** — measured, see §1.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| The five closed action categories | `src/FusionRpg.Core/Actions/ActionEnums.cs:26-33` |
| Six target modes · four area shapes | `src/FusionRpg.Core/Actions/ActionTargetSpec.cs:14-33`, `:42-48` |
| Eight action tags | `src/FusionRpg.Core/Actions/ActionEnums.cs:37-47` |
| Rung table, 10 rows, `structureBudget` per row | `data/tuning/action-rungs.v1.json:12-21` |
| `ActionSeeder.Generate` → `Instantiator.Draw` (the one roll, Law 1) | `src/FusionRpg.Core/Actions/Seeding/ActionSeeder.cs:32-66` |
| Schema audit rejecting numeric fields **and** a missing `blocked` escape | `tools/seedsmith/seedsmith/pipeline/model.py:53-99` |
| Option permutation seeded on `(id, field, sample_index)` | `tools/seedsmith/seedsmith/adapters/demons/anchor/permute.py:16-30` |
| Vote resolution: 3-0 `high`, 2-1 `split` + minority, 1-1-1 `unresolved` | `tools/seedsmith/seedsmith/adapters/demons/anchor/vote.py:23-40` |
| Bounded self-heal that names the defect | `tools/seedsmith/seedsmith/pipeline/llm_caller.py:207-236` |
| The stage shape this spec copies — system prompt, schema, `build_context`, `build_brief`, `entry_for`, validators | `tools/seedsmith/seedsmith/adapters/effects/affix/prompts.py:26-112` |
| `--dry-run` / `--count` entrypoint precedent | `tools/seedsmith/seedsmith/adapters/effects/affix/generate_affixes.py:74-96` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `Instantiator.TryInstantiate` — referenced only from doc comments, no production caller | `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:92`; mentions at `InstanceProducer.cs:22`, `Resolver.cs:28`, `RpgStore.AtomInstances.cs:104` |
| `data/seed/actions/` is invisible to the loader — both files lack the `kind`/`entries` envelope | `data/seed/actions/name-templates.json`, `pairings.json`; `tools/seedsmith/seedsmith/corpus/model.py:159-185` |
| `AFFIX_SCHEMA` carries no `blocked` property, so it would fail today's own audit | `adapters/effects/affix/prompts.py:26-38` vs `pipeline/model.py:92-97` |

### Real gap

- **This stage.** No `general-propose` adapter, schema, prompt or entrypoint exists anywhere under
  `tools/seedsmith/seedsmith/adapters/`.
- **`A-S1` does not exist**, so the brief this stage consumes has no producer yet.
- **The roster is 84, not 904** — `data/seed/demons/_generated/motif-assignments.json` has 84 keys and
  `family-assignments.json` has 53 across **19** family tokens. The general tier is the one tier that does
  **not** care: it reads no anchor, so it is the only pipeline the roster finding leaves untouched.

## 2. The contract

### The JSON schema — no magnitude field anywhere

**⛔ The `description` strings are written out below — added 2026-09-03 (review F19).** AC2 has
always asserted *"every property has a `description`, and every one contains a negative clause —
asserted mechanically"*, and **the schema carried no `description` key at all**, so the assertion had
nothing to assert over. They are written here, in the schema, because a description that lives in
prose beside a schema is a description the audit cannot read. Each is modelled on the hardened
`blocked` description at `adapters/demons/anchor/prompts.py:74-82` — the one that was rewritten after
a real local model filled the field with `"plant"` on 2026-09-01 (`prompts.py:64-70`): state the
normal case first, then the exception, then say plainly what must **not** go in the field.

```jsonc
GENERAL_ACTION_SCHEMA = {
  "type": "object",
  "properties": {
    "name": {
      "type": "string",
      "description":
        "The action's display name as a player sees it — two to five words, in the game's voice. "
        "Do NOT restate the atom family ids you picked: 'Burn Spread' is a label, not a name. It is "
        "NOT a sentence and NOT an instruction — no imperative verb, no trailing punctuation. It "
        "never names a creature, a family, an element or a species; a general action belongs to "
        "everyone."
    },
    "flavor": {
      "type": "string",
      "description":
        "One line a player would read under the name, under 140 characters, evoking what the action "
        "feels like. It is NOT a rules description: never say what the action does mechanically, and "
        "never write a number, a duration, a chance or a range — tables you never see decide all "
        "of those. It never names a creature, a family, an element or a species."
    },
    "atomFamilies": {
      "type": "array", "minItems": 1,
      "description":
        "Which of the atom families listed above this action is built from — choose one or more "
        "from that list. You are choosing WHICH families, never HOW MUCH of any of them: this is "
        "NOT a place for a magnitude, a weight or a count. Do NOT invent a family that is not in the "
        "list, and do NOT write a concrete atom id — a family names a pool, and which member of "
        "the pool a player gets is decided later, by code, per player.",
      "items": { "type": "string", "enum": [ /* the brief's allowedAtomFamilies,
                                permuted per (briefId, "atomFamilies", sampleIndex) */ ] }
    },
    "rationale": {
      "type": "string",
      "description":
        "One sentence saying why these atom families make a good action for the role described "
        "above. It is NOT a restatement of the name or the flavour, and it is NOT a justification of "
        "any number — you never see a number, so there is nothing numeric to justify. Do NOT use "
        "it to add an effect you did not put in `atomFamilies`."
    },
    "blocked": {
      "type": "string",
      "description":
        "Leave this as the exact empty string \"\" when you WERE able to design the action above — "
        "this is the normal case for almost every brief. Only write a non-empty reason here when the "
        "brief genuinely gives you NOTHING to work from (for example, an empty list of eligible atom "
        "families). Do NOT put a name, a family id, a rationale or any other real answer here — it "
        "is a blocked-flag, not a second answer field."
    }
  },
  "required": ["name", "flavor", "atomFamilies", "rationale", "blocked"],
  "additionalProperties": false
}
```

Four properties that make it survive `audit_schema`, each for a stated reason:

- **No `number` or `integer` anywhere.** No rung, no cost, no duration, no chance, no weight.
- **No `string` whose `pattern` admits a bare number**, and no enum member that is a numeric string. The
  `atomFamilies` enum is filled at call time from the brief's own `allowedAtomFamilies` — family ids, never
  digits — so a model cannot name a family the planner did not open.
- **`blocked` is required**, because `audit_schema` rejects a top-level schema without it
  (`pipeline/model.py:92-97`) and because a model with no way to decline invents instead. Its description
  states the empty-string convention explicitly, with a worked example of each case — the exact defect a real
  local model hit on 2026-09-01 (`adapters/demons/anchor/prompts.py:61-83`).
- **`confidence` is NOT a model field.** §16 of the ideal shows it in the return block; the vote resolver
  writes it (`vote.py:16-20`). A model grading its own certainty is a self-report, not a measurement.

### What the system prompt says

One judgement, stated as a role: *you design the identity of an action any creature in the game could hold —
a name, a line of flavour, and which of the given atom families it is built from.* Three negative clauses,
because a description without one is half-written:

- **You never write a number.** Not a rung, not a cost, not a duration, not a chance. Tables you never see
  decide every magnitude.
- **You never name a creature, a family, an element or a species.** A general action belongs to everyone;
  the moment it reads as *"the fire one"* it is a family action and belongs to a different pipeline.
- **You never invent an atom family.** You pick from the list given, or you set `blocked`.

### What the brief inlines

`build_brief(context)` renders literal values and **cites no file** — the same discipline
`affix/prompts.py:66-80` states a reason for. Inlined, in this order: the mechanical slot the planner fixed
(`category`, `targetMode`, `areaShape`, `relation`, `kind`) and the rung **band** as a plain label rather
than numbers the model could copy; the eligible atom families in permuted order; the forbidden families with
the sentence saying why they are forbidden; the pairing role and, when the role is `payoff`, the status it
must pay off; and the `avoidNeighbours` fingerprints as *"do not produce anything like these."* Nothing else
— no anchor, no motifs, no element, no species key. `build_context` returns those inputs read-only, exactly
as `affix/prompts.py:59-65` does, so the validators read the same object the brief was rendered from.

### Which fields are voted, and why those

**One voted field: `atomFamilies`.** Three samples, permuted per `(briefId, "atomFamilies", sampleIndex)`,
resolved by `vote.resolve_vote` over the sorted-tuple form of the pick.

- It is the only field whose wrongness is **expensive to fix later** — the family set is the mechanical
  identity, it is what tier-1 dedup hashes, and changing it after acceptance invalidates every downstream
  round.
- `name`, `flavor` and `rationale` are prose. A three-way disagreement on prose is not ambiguity, it is
  three valid answers; voting them would triple the cost of the cheap half.
- `category`, `targetMode`, `areaShape`, `relation`, `kind` and the rung band are **planner-owned** and
  never reach the vote set at all. Moving `category` out of the model is the single largest saving in the
  program (`action-corpus-ideal.md:559-576`).

A 1-1-1 split on `atomFamilies` writes `confidence: "unresolved"` and the candidate goes to the review
queue. It never silently takes sample 0.

## 3. What it must NOT do

- **Never pick a magnitude.** Not a rung, cost, cooldown, duration, chance, weight, tier or stack count.
- **Never read an anchor.** If a brief handed to this stage carries `anchor`, the stage **raises** rather
  than ignoring the field — silently accepting it is how P-general quietly becomes P-family.
- **Never invent an atom family, a category, a target mode, a shape or a relation.**
- **Never emit a concrete atom id.** An atom names a **pool**; element, tier and cell resolve at layer 4,
  per player, at roll time. A cell is a target, never an identity (`action-corpus-map.md:37-42`).
- **Never re-roll.** `Instantiator` is the roll and it already exists. Law 1.
- **Never carry state between calls.** One brief in, one candidate out — the stage is pure and parallel so
  the run stays replayable.
- **Never write into `data/seed/actions/`.** Acceptance is `A-S3`'s; persistence is `A-S6`'s.
- **Never grade itself.** `confidence` comes from the vote, never from the model.

## 4. Testing strategy

1. **Stubbed transport that raises.** Every test in this stage's module installs a transport whose only
   behaviour is `raise AssertionError("a test called a model")`. Building the context, rendering the brief,
   auditing the schema and running every validator must all pass under it — so *"makes no call"* is proven
   rather than assumed (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8` is the precedent).
2. **Determinism / replay.** Same brief, same `sampleIndex` → byte-identical rendered brief and identical
   permuted enum order, asserted by hash. Then: the same three recorded samples → the same `VoteResult`.
   Then a whole-stage replay — the recorded transcript re-fed produces a byte-identical candidate file,
   canonical serialisation included (sorted keys, fixed indent, `\n`, explicit nulls).
3. **Planted violations**, one test each, all expected to be **rejected**:
   - a schema with `"rung": {"type": "integer"}` → `audit_schema` reports a defect;
   - a schema with `"rungMilli": {"type": "string", "pattern": "^[0-9]+$"}` → reported;
   - a schema whose enum members are `"1"`, `"2"` → reported;
   - a schema with no `blocked` property → reported;
   - a draft naming an atom family outside the brief's `allowedAtomFamilies` → validator rejects, and the
     re-prompt text **names that family**;
   - a brief carrying an `anchor` key → the stage raises;
   - three distinct vote samples → `confidence == "unresolved"` and `value is None`, asserted explicitly so
     a future "just take the first" refactor fails loudly.

## 5. Acceptance criteria

1. `audit_schema(GENERAL_ACTION_SCHEMA)` returns an empty defect list, under a test CI runs.
2. Every property in the schema has a `description`, and every one of those contains a negative clause
   saying what the field is not — asserted mechanically over the schema, not by review. ⛔ **The
   strings are written into §2's schema as of 2026-09-03 (review F19)**; before that the schema
   carried no `description` key at all, so this criterion asserted over nothing.
3. `build_brief` output contains no file path, no `.md`, and no anchor-derived token, asserted by a test.
4. A brief containing an `anchor` key raises; a brief missing any planner-owned slot field raises.
5. `atomFamilies` is voted over three permuted samples; a 1-1-1 split yields `unresolved` with `value is
   None`, and the minority is recorded on a 2-1.
6. `--dry-run` renders briefs, prints the count, and makes zero model calls; `--count N` bounds the run at
   N candidates.
7. The full test module passes with the transport stubbed to raise.
8. A second run over unchanged inputs is byte-identical by hash, and `_provenance` records model id, prompt
   version, brief hash and candidate-set hash.
9. Repairs are bounded at **two** (`max_heal=2`, passed explicitly — the config default is 3,
   `llm_caller.py:45`), and the third failure yields `unresolved`, never a silent accept.
   ⛔ **CORRECTED 2026-09-03 (review F9):** `call_with_self_heal` does **not** produce `unresolved`
   — its docstring says it *"never raises"* and substitutes `default_for(key, original_value)`
   (`llm_caller.py:229-234`), whose shipped default returns the original item, which for a generation
   stage is a **brief field**. This stage passes `default_for=lambda key, original: None` explicitly,
   and `unresolved` is the verdict **A-S4 writes** from the helper's `FAILED:<reason>` soft entries
   (`llm_caller.py:255-258`). The adapted contract is stated once, in
   `spec-validate-heal.md` §2 Stage 3; this criterion is asserted against that, not against an
   exception the helper never throws.
10. No candidate this stage emits contains a numeric value of any kind, asserted over the output file.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| The brief (slot, pool, pairing, avoidNeighbours) | **A-S1** `distribution-planner` | does not exist |
| Candidate acceptance, quality gates, bounded repair | **A-S4** `validate-heal` | does not exist |
| A loadable `data/seed/actions/` | **A-C1** `corpus-loader` | both files there are silently skipped (`corpus/model.py:159-185`) |
| Channel pools — an atom names a pool | **effect-atom E30** | outside this program |
| Binding production | **effect-pipeline module 4** `instance-producer` | `effect_binding` has zero rows, so the corpus would be authored into a runtime nothing reaches |

**Hazards.**

- **C1's family-access widening is gated** on three things that do not exist: a per-rung `powerBudget` row,
  a family-aware non-additive price (needs D2), and a budget check with a production caller
  (`action-corpus-ideal.md:707-728`). Until all three hold this stage receives **structure-gated** pools
  only. It must not special-case its behaviour on a tier — it reads whatever pool the brief hands it.
- **The `~1,162 calls/h` rate every cost figure rests on is unsourced** (`action-corpus-ideal.md:1447`).
  Any schedule this stage's budget implies is unverified until it is measured.
- **`AFFIX_SCHEMA` has no `blocked` property.** Copying that file's schema shape verbatim would inherit a
  defect the audit already catches. Copy the *structure*, add `blocked`.
