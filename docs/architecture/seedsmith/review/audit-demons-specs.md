# Seedsmith — demons feature, spec audit (D1–D4)

**Lens:** handed the seven demons specs and told to build them — what stops that, what is
self-contradictory, and what is assumed rather than checked?

**Method:** read all seven specs plus the code and data they cite —
`DemonSpeciesCatalog.Generated.cs`, `RpgStore.AlmanacSeed.cs`, `RpgStore.cs`'s `recipes` DDL,
`adapters/items/kinds.py`, `adapters/base.py`, `numerics/model.py`, and the live item corpus under
`data/seed/items/`.

**Date:** 2026-08-31. **Eight findings.** One is a contradiction inside a spec; three are blockers or
near-blockers; two are dissolved by checking; two are risks to carry.

---

## S1 — ✅ DISSOLVED: `almanac_seed` is built, not merely specced

**The worry.** `demon-corpus-emit` depends on `almanac_seed`, which the ideal cites as a *spec*
([spec-almanac-seed.md](../../almanac/spec-almanac-seed.md)). A specced-but-unbuilt table would block
D1 entirely.

**Checked:** `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs` exists with real `INSERT INTO
almanac_seed(...)` and read paths. The almanac program's own todo is at zero open items.

**Verdict: not a blocker.** Recorded because "is the dependency real code or a document?" is the
question this repo's history says to ask first, and the answer here happens to be good.

---

## S2 — ⛔ BLOCKER for D2's aspect kind: `aspect-scope` is approved, not built

`spec-adapter-demons` §2.2 declares an `aspect` kind, and `spec-aspect-scope.md` was approved
2026-08-31. **Approved is not built.** The tier those aspects live on does not exist in code —
`DemonSpeciesDef` still carries `ElementPrimary`/`ElementSecondary`/`TraitPool` on the *species*.

**Why this is sharper than the open question I filed.** `adapter-demons` §8 Q2 asks whether `aspect`
should ship in D1 and answers "declaring the kind early is harmless". That is true and insufficient:
the specs never say **who builds `aspect-scope`, or when**. It belongs to the demon program, which
has its own queue, and this feature's D2 silently depends on it.

**Fix:** the map must carry `aspect-scope` as an **explicit cross-program dependency with an owner**,
not as a footnote in one module's open questions. A dependency on another program's unscheduled work
is the kind that is discovered late, at the worst moment.

---

## S3 — ⚠️ The corpus may be too thin to classify, and the specs assume it is not

Every D2 module reads `flavorInfo`/`flavorIntroduce`. B3 measured stat coverage
(**66/677 plants, 18/227 zombies**) but **nobody has measured flavour-text coverage for the 24
species that actually matter.**

**One fact does improve the outlook, checked rather than hoped:** the shipped roster is **18 zombies
and 6 plants**. Zombies carry both `info` *and* `introduce` in the game's almanac —
`almanac_seed`'s own schema notes `flavor_introduce` is *"null for plants"* — so the roster is 75%
weighted toward the text-rich side, which is a consequence of `DemonSpeciesGenerator` preferring
zombies (*"demons wear zombie bodies"*), not luck.

**The residual risk is real and unquantified.** If most of the 24 still have thin text, D2 produces
`basis = "name"` for nearly everything, D3 correctly reports *"cannot be measured"* for the whole
corpus (that is the metric working), and the feature ships having measured nothing. That is not a
failure of the design — it is the honest outcome — but it should be a **known** outcome, not a
discovery.

**Fix:** measure flavour coverage for the 24 shipped species **before D2 is scheduled**. It is one
query, and it decides whether D2 is worth running yet or whether `lore-enrich` should come first.
This inverts the build order under one plausible measurement, which is exactly why it is worth
knowing early.

---

## S4 — ⚠️ n=24 is small for the metrics this feature leans on

seedsmith's distribution machinery was built against **1,438 item entries**. The demon roster is
**24** (`DemonSpeciesGenerator.DefaultMaxSpecies`). With perhaps 5–8 families, that is 3–5 demons
each.

`Distribution/MotifSharing` (D3) computes demons-per-motif over that. On n=24, minus whatever S3's
tautology exclusion removes, the number is closer to an anecdote than a measurement. The metric is
still worth having — it catches the *catastrophic* case where every motif is private — but it will
not resolve the fine distinctions its name implies.

**Not a blocker.** Recorded so nobody reads a sharing figure from a 24-entity corpus as a balance
signal. Raising `DefaultMaxSpecies` is a demon-program decision, not this feature's.

---

## S5 — ⛔ CONTRADICTION inside `spec-demon-themes`: two success criteria cannot both hold

**The finding.** That spec's testing table asserts both:

- *"The items adapter **rejects** a `themeKey` absent from it — proving the vocabulary is enforced"*
- *"Existing themed content (31 sets, 8 uniques) **still validates**"*

**Measured in the live corpus:** there are **5 distinct `themeKey` values**, all `theme.*`-prefixed —
`theme.frostbitten-vanguard`, `theme.rusted-legion`, `theme.sunwoven-almanac`,
`theme.thorned-chassis`, `theme.verdant-graft` — across 31 sets. **None is a demon.** They are an
authored item vocabulary, ~6 sets each, and clearly deliberate.

So if the registry is demon-published only, all five legacy themes fail validation and 39 entries
break. The two criteria are not in tension; they are inconsistent.

**The fix is better than the compromise I filed as an open question.** The id grammar already
prefixes: legacy themes are `theme.*`. Give demon themes their own prefix (`demon.*`), and the
`themeKey` vocabulary becomes a **union of two append-only populations** that cannot collide, each
with its own provenance and its own rules. Coexistence stops being a migration deferred and becomes a
principled namespace split — which is what the existing prefix was already implying.

`spec-demon-themes` §9 Q1 ("migrate or coexist?") is therefore **answered**, and its success criteria
need rewriting to match.

---

## S6 — ⛔ Nothing specifies what happens when the roster changes

`DemonSpeciesGenerator` selects the top **24 by observed HP** and assigns rarity by rank
(`RarityForRank`). A future capture with better `spawn_stats` coverage — which
`almanac-spawn-coverage` exists precisely to produce — **can change which species are selected and
what rarity they hold.**

Consequences no spec addresses:

- A demon can leave the roster. Its families, motifs and **published theme** dangle. Items themed to
  it are now themed to nothing.
- A demon's rarity can change, altering anything derived from it.
- `speciesId` is stable per type, but *membership* is not.

**This is the one gap that produces silent corruption rather than a failed run.** Everything else
here fails loudly.

**Fix:** the specs need a roster-churn contract — at minimum, published themes are append-only and a
departed demon's theme is **retired, never deleted**, so existing items keep resolving. That is the
same append-only discipline already applied to families and motifs, extended to the one artifact that
crosses a corpus boundary.

---

## S7 — ⛔ `family-consolidate`'s core merge rule does not work on the actual data

§2.1 specifies merging by **head noun**: `wall-nut`, `defensive-nut`, `nut-type` → head `nut`. That
is an English-shaped rule.

**The roster's display names are Chinese** — `钻石套娃僵尸`, `黄金套娃僵尸` (checked in
`DemonSpeciesCatalog.Generated.cs`). Head-noun extraction over those tokens is not merely inaccurate;
it is undefined.

Two escapes, and the specs pick neither:

1. **Extraction emits English labels** — then head-noun merging works, but a translation step now
   exists that nothing specs, and label quality depends on a model translating game-specific Chinese
   compound nouns.
2. **Extraction emits native labels** — then `family-consolidate`'s merge rule must be replaced with
   something that works on Chinese compounds, and the synonym map carries the load.

`spec-family-consolidate` §9 Q1 raises this as an open question. **It is not a question, it is a
blocker**: the module's central algorithm is inoperable until it is answered, and the answer changes
what `family-extract`'s prompt must produce — so it must be settled *before* D2's first module, not
between its second and third.

---

## S8 — ⚠️ "No core file changed" is a D1 claim, and the map reads as if it covered the feature

`spec-adapter-demons` §1 sets a good success criterion: *"not one line of core code changed."* True
for D1.

But `demon-metrics` (D3) adds two files under `metrics/`, and `demon-themes` (D4) edits
`adapters/items/registries.py`. Both are justified in their own specs — the metrics are genuinely
generic, and the items change adds a vocabulary rather than a concept — yet the map's framing
(*"Nothing below adds a planner, a briefkit or a pipeline"*) invites the reader to carry D1's
stronger claim across the whole feature.

**Fix:** state the scope of the claim where it is made. "No core change **in D1**" is a real and
testable property; "no core change ever" is not what these specs deliver, and the difference should
not have to be reconstructed from four documents.

---

## What the audit did not find

No violation of the DAL boundary (the emitter is C# through `FusionRpg.Data`). No numeric path for a
model to invent a magnitude (`channels()` empty, `audit_schema` mechanical). No PvZ-layer dependency.
No circular module dependency — the graph is a DAG and `reference_fields` derive the order rather
than declaring it. No second planner, briefkit or pipeline.

---

## Disposition

| # | Severity | Lands on | Action |
|---|---|---|---|
| S1 | dissolved | — | none |
| S2 | blocker (D2) | map §3b | add `aspect-scope` as an explicit cross-program dependency with an owner |
| S3 | risk, unquantified | scheduling | measure flavour coverage for the 24 species before scheduling D2 |
| S4 | risk | `spec-demon-metrics` | note that n=24 bounds what the sharing metric can resolve |
| S5 | **contradiction** | `spec-demon-themes` | prefix split (`theme.*` / `demon.*`); rewrite the two conflicting criteria |
| S6 | **blocker** | all D2–D4 specs | roster-churn contract; retire, never delete, a published theme |
| S7 | **blocker** | `spec-family-extract` + `spec-family-consolidate` | decide label language before D2 starts |
| S8 | wording | map §3b | scope the "no core change" claim to D1 |

**Three blockers, one contradiction.** None invalidates the design; all four are the kind that cost a
rewrite if found during the build instead of before it.
