# Spec: `family-tags-closure`

**Module id:** `family-tags-closure` · **Program:** [item](../item-map.md) · **Build order:** independent, no dependents block on it landing first
**Depends on:** nothing · unblocks module 8 (`affix-legality`)'s own tag-gated rules, which are inert without this
**Rulings:** **D28** (`item-map.md` §3) · lane: [ssot-affixes.md](ssot-affixes.md), `effect-pipeline/spec-eligibility-tags.md`

## Objective

Make a family's own authored `tags` (`data/seed/items/affix-families/*.json`'s `"tags"` array —
today exactly three real values across the whole corpus: `offensive`, `defensive`, `utility`) actually
reach `AtomRow.TagsJson`, so `EligibilityRule.RequireTags`/`AnyOfTags` — module 8's real,
already-shipped tag-gate mechanism — stops being permanently inert.

⛔ **This is not a request to another program. It is finishing a thread effect-atom's own code
already named as ours.** `AffixFamilyFile.cs`'s own doc comment (the parser E43/`family-expand`
reads affix-family files through) says verbatim: *"Everything else on an entry (roles, frames,
nameWords, **tags**, notes, variants...) is the item program's own authored surface and out of
scope here — reconcile and expand only, never re-author."* Effect-atom's `FamilyExpansion.cs`
(E43) shipped and closed emitting only `{generatedFrom, generator}` provenance tags
(`FamilyExpansion.cs:194-198`) — it never carried the family's own `tags` through, exactly as its
own sibling file said it wouldn't. Nobody on item's side picked up the thread since. **D28 has
been carried as "Owner: effect-atom" for as long as this program's own docs exist, but effect-atom's
module already finished without delivering it and nothing is scheduled to revisit it** — this spec
is item claiming that thread, not overriding anyone's active work.

**Users:** module 8 (`affix-legality`) is the direct consumer (`EligibilityRule`/`EligibilityResolver`,
already shipped, `spec-eligibility-tags.md`); indirectly, every downstream module that draws from
module 8's eligible-affix pool (9, 10, 11, 13, 15, 17, 21).

## Design

### The real shape, traced end to end (not assumed)

1. **Source of truth**: `data/seed/items/affix-families/*.json` entries' own `"tags"` array. Sampled
   the full corpus (all 112 entries as of 2026-09-07): exactly three distinct values —
   `offensive` / `defensive` / `utility` — no `key:value` convention anywhere in real content.
2. **The parser that drops it**: `AffixFamilyFile.Read` (`src/FusionRpg.Core/Effects/Atoms/Generation/
   AffixFamilyFile.cs:18-50`) builds a `FamilyEntryInput` per entry from exactly five JSON properties
   (`id`, `name`, `kindId`, `params.channel`, `params.op`, `powerBand`) plus the caller-supplied
   `sourceFileName` — `tags` is read by nothing.
3. **The record with no room for it**: `FamilyEntryInput` (`FamilyExpansionTypes.cs:19`) — seven
   positional fields, none named `Tags`.
4. **Where the stamp happens today**: `FamilyExpansion.cs:194-198` builds `tagsObj` with exactly two
   keys, `generatedFrom`/`generator` — both provenance, matching `E43`'s own promise
   (`spec-family-expand.md:146`, *"Each emitted row carries `tags: {generatedFrom, generator}`"*) —
   never more than that promise, by design.
5. **The reader, already correct and needing NO change**: `AffixTags.ParseTags`
   (`src/FusionRpg.Core/Effects/Atoms/AffixTags.cs:106-123`) reads `TagsJson` as a flat
   `{string: string}` object and unions every string-valued key it finds — it does not care how many
   keys exist or who put them there. `EligibilityRule.RequireTags`
   (`EligibilityRule.cs:41-44`) checks bare `ContainsKey`, any value — exactly what a flat category
   tag like `offensive` needs, no `key:value` structure required. **This means the fix is entirely
   on the STAMPING side; the reading/matching side is already correct and already shipped.**

### The fix

1. `FamilyEntryInput` gains an eighth field: `Tags` (`IReadOnlyList<string>`, defaulting to empty for
   any caller not yet updated — additive, not a breaking rename).
2. `AffixFamilyFile.Read` parses the entry's `"tags"` array (already present in every real file) into
   it — a `JsonValueKind.Array` read next to the five it already does, refusing (not silently
   dropping) a non-string array element, matching this file's own existing refusal style for a
   missing `id`.
3. `FamilyExpansion.cs`'s `tagsObj` construction (the loop building `AtomRow` per tier) adds one entry
   per family tag: `tagsObj[tag] = "1"` — a placeholder value, since `RequireTags`/`ContainsKey` never
   inspects it; `"1"` chosen over `""` only for readability in a raw `TagsJson` dump, not because
   either reader treats it specially. Applied identically to every tier a family expands into (a
   family's tags describe the FAMILY, not one magnitude tier — matching how `generatedFrom`/
   `generator` already apply per-tier from one per-family source).
4. **No change to `AffixTags.cs`, `EligibilityRule.cs`, or `EligibilityResolver`** — verified above,
   they already read whatever keys exist.
5. **No change to the real corpus files** — this reads `tags` that are ALREADY authored on all 112
   entries; it is a code fix to a decode gap, never new content or a schema bump.

### What this does not do, on purpose

- Does not invent new tag vocabulary. If a future affix-family entry needs a tag word beyond
  `offensive`/`defensive`/`utility`, authoring it is exactly like authoring the entry's `name` —
  the item program's own content surface, unaffected by this fix.
- Does not add `key:value` tag support to the stamping path. `AnyOfTags`'s `"key:value"` shape stays
  reachable by a future family that authors `"tags": ["element:fire"]` (the code splits on the first
  `:` if present, else treats the whole string as a bare key — see acceptance #3) but no real content
  needs that today, so it is exercised only by a synthetic test, not shipped content.
- Does not touch `spec-eligibility-tags.md`'s own text. That spec's premise (*"E43 stamps them onto
  every emitted row"*) is corrected here in effect, not in that document's prose — the document's own
  words become true once this ships, rather than needing an edit.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~FamilyExpansion
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~AffixTags
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~EligibilityRule
```

## Project structure

- `src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansionTypes.cs` — `FamilyEntryInput` gains
  `Tags`.
- `src/FusionRpg.Core/Effects/Atoms/Generation/AffixFamilyFile.cs` — parse `"tags"`.
- `src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs` — stamp real tags alongside
  provenance.
- `tests/FusionRpg.Core.Tests/Effects/Atoms/Generation/FamilyExpansionTests.cs` (existing file,
  extended) and/or a new `FamilyTagsClosureTests.cs` if the existing file's fixtures don't fit the
  new field cleanly.

## Code style

Match `FamilyExpansion.cs`'s existing style exactly: no new abstractions, the tag-stamp loop is a
straight-line addition beside the existing two-key block, not a new helper class. `Tags` on
`FamilyEntryInput` is `IReadOnlyList<string>`, matching the record's existing immutable-collection
convention elsewhere in this file family (`FamilyExpansionResult.Rows`/`.Refusals`).

## Testing strategy

1. **Red-first, real corpus**: before the fix, assert a real family's atom row (e.g. `atom.tempo-
   wildgrowth`'s tier-1 row, or any of the 3 shipped families carrying `offensive`) has NO
   `offensive` key in `TagsJson` — proves the gap is real, not assumed.
2. **Unit — parser**: `AffixFamilyFile.Read` on a hand-built fixture with `"tags": ["offensive",
   "utility"]` returns a `FamilyEntryInput` whose `Tags` contains both, in order.
3. **Unit — stamping**: `FamilyExpansion.Expand` on a family with `Tags = ["offensive"]` produces an
   `AtomRow` whose `TagsJson` parses to a dict containing `"offensive"` (any value) AND still
   contains `generatedFrom`/`generator` — the fix is additive, not a replacement.
4. **Zero-tags family**: a family with `Tags = []` (or a caller not yet passing tags) still emits the
   two provenance keys and nothing else — no crash, no phantom key.
5. **End-to-end — the real gate**: `EligibilityRule(RequireTags: ["offensive"])` through
   `EligibilityResolver.IsEligible` now returns `true` for an affix whose only concrete ref resolves
   to a real, offensive-tagged atom, and `false` for one that resolves to a `utility`-only atom —
   proving module 8's own shipped mechanism, not just the stamp, now works end to end.
6. **Colon-form tag, synthetic only**: `Tags = ["element:fire"]` produces `TagsJson` containing
   `{"element": "fire"}`, proving `AnyOfTags`'s key:value path is reachable — not exercised by real
   content today, named so a future family author knows the mechanism exists.
7. **Full-corpus regression**: re-run `family-expand` against the real 112-family corpus (or the
   equivalent Python-side reconcile, if one exists) and confirm the three real tag values appear on
   exactly the atoms their source families name — no more, no fewer.

## Boundaries

**Always:** treat `data/seed/items/affix-families/*.json`'s `tags` field as read-only input here —
this closure reads it, never authors it (matching `AffixFamilyFile.cs`'s own established
"reconcile and expand only" rule, unchanged by this spec).

**Ask first:** none — every touched file is item/effect-pipeline shared code already read/written by
this exact generation path, and the change is additive to a JSON object no consumer treats as a
closed enum.

**Never:** stamp a tag key that collides with the two provenance keys (`generatedFrom`, `generator`)
— refuse construction (or drop with a named reason, matching this codebase's "safe direction"
convention) rather than let a family author accidentally shadow provenance metadata.

## Success criteria

- `EligibilityRule.RequireTags`/`AnyOfTags` (module 8, already shipped) produces a real, non-empty,
  non-universal eligible-affix pool when constrained by a real family tag — proven by test #5 above,
  not merely "the field is populated."
- `effect-pipeline/spec-eligibility-tags.md:40-43`'s premise becomes true in shipped behavior.
- Zero regressions in the existing `FamilyExpansion`/`AffixTags`/`EligibilityRule` suites.
