# Seedsmith content-completeness — `passive-tree-identity-content`

**Status:** Proposed 2026-09-08. Module 7 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on Checkpoint 5b
(closed 2026-09-08 — the wiring for a node's own `name`/`flavor` to reach the player exists, so
tree-level content built here has somewhere real to go).

Genuinely NEW content generation — a tree's own display name and description — not a retrofit like
Phases 1-5. Mirrors species' own real, proven, already-shipped `codexSummary` stage
(`adapters/trees/species/generate_codex.py`) as closely as the shape allows, per this program's
"ask first before forking any piece of reused machinery" boundary (stated verbatim in
`workflow/graphs/species_codex.py`'s own docstring).

## 1. Objective

Every one of the 42 real, committed passive trees has real node-level content (`name`/`flavor` per
node, confirmed: every tree under `data/seed/passive-tree/nodes/*.json` has generated content) but
NO tree-level identity at all — confirmed, `report/cli.py:1176` and
`adapters/trees/species/generate_tree.py:120`: `tree_reading` always equals the raw `treeId`
verbatim at every real call site. Species trees already have the analog for THEIR OWN identity
(`codexSummary` — "what building into this creature rewards"); this module gives every primary,
elemental and status tree the same kind of thing, answering a different, broader question: "what
is this tree" (a display name) and "what kind of build does it reward" (a one-sentence
description) — not a species' own narrower "what does this bloodline's build pay off," but the
same shape of content.

## 2. What already exists — real content this stage grounds on, does not invent from nothing

- All 42 real tree plans exist with real `category` values (confirmed: `primary` × 12, `elemental`
  × 6, `status` × 24 — enumerated directly from `data/seed/passive-tree/plan/*.v1.json`).
- All 42 trees have real, committed node-level `name`/`flavor` content
  (`data/seed/passive-tree/nodes/*.json`) — this session's own earlier H9 work generated it. This
  is the SOURCE MATERIAL this stage's brief reads from (a sample of a tree's own real node
  names/flavors), exactly as `generate_codex.py`'s own brief reads a species' own anchor fields
  rather than inventing independently.
- `generate_codex.py`/`workflow/graphs/species_codex.py`/`species/schemas.py`'s
  `codex_summary_defects` (no digit, no channel-id-shaped token) is the reusable machinery this
  module mirrors: `build_generation_graph` + `make_generate_node`/`make_validate_node`/
  `make_persist_node`, 3-sample voting via `resolve_vote` (`adapters/creatures/anchor/vote.py`).

## 3. The new schema

`adapters/trees/identity/schemas.py`, mirroring `species/schemas.py`'s own shape:

```python
TREE_NAME_MAX_LENGTH = 40
TREE_DESCRIPTION_MAX_LENGTH = 160

def tree_identity_defects(name: str, description: str) -> list[str]:
    """Reuses the exact digit/channel-id patterns species' own codex_summary_defects already
    proved — generalized to two fields instead of one, never a second pattern invented."""

TREE_IDENTITY_RESPONSE_SCHEMA = {
    "type": "object", "additionalProperties": False,
    "required": ["name", "description", BLOCKED_FIELD],
    "properties": {
        "name": {"type": "string", "minLength": 1, "maxLength": TREE_NAME_MAX_LENGTH, ...},
        "description": {"type": "string", "minLength": 1, "maxLength": TREE_DESCRIPTION_MAX_LENGTH, ...},
        BLOCKED_FIELD: {...},
    },
}
```

`audit_schema(TREE_IDENTITY_RESPONSE_SCHEMA)` must return `[]`, proven the same way every other
schema in this program is proven (`Pipeline.__post_init__`'s own construction-time check).

## 4. The brief

`adapters/trees/identity/prompts.py`, mirroring `build_codex_context`/`build_codex_brief`:

```python
def build_identity_context(tree_id: str, category: str, branches: Sequence[str],
                           sample_nodes: Sequence[tuple[str, str]]) -> dict:
    """sample_nodes: up to 6 (name, flavor) pairs from the tree's OWN real generated nodes — the
    tree's own already-written content is what grounds this brief, never invented independently."""

def build_identity_brief(context: Mapping) -> str: ...
```

`TREE_IDENTITY_SYSTEM_PROMPT`: *"You name a passive skill tree and write ONE sentence describing
what kind of build it rewards, based ONLY on the tree's own already-written trait names/flavor text
below. Never write a number. Never name a stat, channel, aptitude, element, or status by its game
name."* — the same negative-clause discipline `CODEX_SYSTEM_PROMPT` already states, applied to a
tree instead of a creature.

## 5. The generation stage

`adapters/trees/identity/generate_identity.py`'s `resolve_tree_identities`, mirroring
`resolve_codex_summaries`'s shape: 3 samples per tree, EXACT-MATCH `resolve_vote` — the SAME
machinery `codexSummary` already uses successfully — applied to `name` ONLY.

**This is a real, measured recalibration, not the original design.** The first draft voted `name`
and `description` independently, each requiring its own 3-way majority. The real PoC run against
the live model (Task 17) resolved only 1 of 3 real trees — both failures were the description vote
alone hitting a 1-1-1 split, even though `name` converged on both. This is the exact same failure
shape this session already diagnosed and fixed once today for favour-fit: exact-match agreement
across independent free-text generations is achievable for a short, identifier-like field (a name)
but gets strictly harder for a full descriptive sentence, where three independent phrasings of the
same idea are unlikely to match byte-for-byte. **Fixed**: `name` alone is voted; once it resolves
(3-0 or 2-1), `description` is taken directly from the first sample that produced the winning name
— guaranteeing the returned pair genuinely came from one real, coherent generation together,
without demanding three independent creative sentences converge exactly. `fresh`/`unresolved`/
`results` return shape mirrors `resolve_codex_summaries` unchanged; `unresolved` now has one
reason (`name_vote_unresolved`) plus `insufficient_valid_samples`, not two. `workflow/graphs/
tree_identity.py` mirrors `species_codex.py`'s thin-wiring shape exactly (no new `StateGraph(`
call).

## 6. Storage

Extends `TreeCatalogMeta`/`TreeRecord` with `name`/`description` (nullable — additive, same
justification as Task 14's node-level fields: `spec-tree-catalog.md`'s R1-R6 govern id/magnitude
changes, never additions, and the loader parses leniently). Read from a real, committed per-tree
identity file (`data/seed/passive-tree/identity/<treeId>.json`, matching the existing
`nodes/<treeId>.json`/`plan/<treeId>.v1.json` per-tree-file convention) by `TreeBinder`'s
`PlanReader`, carried through `ReportWriter.Serialize` the same additive way Task 14 already
carried node-level `name`/`flavor`.

## 7. Commands

```powershell
python -m seedsmith.adapters.trees.identity.generate_identity --tree ferocity --dry-run
python -m seedsmith.adapters.trees.identity.generate_identity --tree ferocity fire poison
```

## 8. Testing strategy

A real PoC run against the real local model, for at least 3 real trees across the 3 categories
(one primary, one elemental, one status — real committed trees with real node content already
exist for all three, confirmed §2), producing real, coherent name/description content — the same
bar every other real generation stage in this program was proven against this session (a live
model call, not a stub).

## 9. Boundaries

- **Always:** ground the brief in the tree's own real node content (never invent independently);
  reuse `resolve_vote`/`build_generation_graph` unchanged; keep `name`/`description` additive and
  nullable at every layer.
- **Never:** fabricate identity content for a tree with no real node content yet to ground on
  (there are none today — all 42 trees qualify, but this rule holds for any FUTURE 43rd tree).
- **Ask first:** promoting this stage's own review pass beyond "read the PoC output directly" (a
  real stratified-sample review queue, mirroring J2/J3's own passive-tree census convention) — this
  spec's own PoC is a proof of mechanism, not a claim that every one of 42 trees is reviewed.

## Open questions

None — the shape is a direct mirror of already-proven machinery; the one real design choice (which
existing voting/validation shape to reuse) is answered in §5 by citing the precedent directly.
