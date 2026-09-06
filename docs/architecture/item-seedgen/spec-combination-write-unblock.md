# Spec: `combination-write-unblock`

**Module id:** `combination-write-unblock` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 4 of 10
**Depends on:** `generator-harness` (1), `base-types-gen` (2), `sockets-gen` (2)

## Reference manifest

Confirmed against `combogen/schema.py`, not assumed:

| Field | Kind | Target | Real evidence |
|---|---|---|---|
| `ingredients[]` | **Categorical** | `sockets-gen` | `schema.py:76-98` — enum of gem FAMILIES, ≥1 real gem must satisfy each, not a specific gem id |
| `hostRole` | **Categorical** | `base-types-gen` | `schema.py:100-102` — an enum of roles whose socket ceiling fits the ingredient count |
| `granted_families` | **Unresolved — ask first** | Same open question as `consumables-gen`'s `family` field | `schema.py:51` — supplied by the caller; source not yet confirmed to be this program's own `affix-families-gen` output at all |

The owner's *"same [as set bonus]... strain too"* concern is real and confirmed: a combination naming a
`hostRole`/`ingredients` combination nothing in `base-types-gen`/`sockets-gen` satisfies is exactly the
unfillable-reference failure mode module 5's set bonus already names.

## Objective

Unblock module 21's `--write` refusal for the `combination` kind, completing the legacy socket-word
retirement `combogen/migrate.py` already rules for (*"✅ RULED 2026-09-04: regenerate, do not
retain"*). Today `items generate --kind combination --write` is refused outright, per the CLI's own
help text, for two named reasons: **its graph is unwired**, and **its kind rename touches a frozen
registry** (`naming.v1.json`, `registryVersion 4`, `frozen: true`). Until this unblocks, the legacy
`data/seed/items/socket-words/sockwords.json` (25 entries) stays the only shipped content, despite
already being ruled for retirement.

## Acceptance criteria

1. Identify precisely what "its graph is unwired" means in `combogen/`'s own source (`grid.py`,
   `catalogue.py`, `run.py`) — name the specific missing connection, not just restate the refusal.
2. Resolve the frozen-registry blocker EXPLICITLY, not silently: `naming.v1.json`'s `frozen: true` /
   `registryVersion 4` is a locked decision per this repo's own architecture-change rule
   (`decisions.md` first). This module's own acceptance criteria cannot include "bump the frozen
   registry" as a unilateral action — it must either find a path that doesn't require touching the
   frozen registry, or name the required `decisions.md` entry explicitly as a prerequisite, ask-first.
3. Once unblocked, `items generate --kind combination --write` produces real, valid combination entries
   — the 102 ids currently sitting with "no rows... by design, not by omission" get real content for at
   least a representative sample.
3a. Before any real generation, `items validate --deps` confirms every `hostRole` the run's brief could
    request has ≥1 real base-type with a matching socket ceiling, and every `ingredients` family has
    ≥1 real gem — a combination naming a hostRole/family nothing satisfies is refused, not generated
    with a dangling reference.
3b. **Ask first, before this module ships real content**: confirm `granted_families`'s real source
    (per the reference manifest above) — do not wire this generator against `affix-families-gen`'s
    output on the unverified assumption they're the same vocabulary consumables' `family` field
    already showed signs of NOT being.
4. `combogen/migrate.py`'s own retirement plan for `sockwords.json` (25 entries) executes cleanly once
   real combination content exists to replace it — confirm the migration path still matches what
   `migrate.py` already specifies, since it may have been written before this unblock was scoped.

## Commands

```
python -m seedsmith items generate --kind combination --write     # currently refused; this module
                                                                    # is what makes it succeed
python -m seedsmith items combogen-migrate --dry-run               # confirm migrate.py's plan still
                                                                    # holds once real content exists
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/combogen/grid.py       investigate + EDIT — the unwired graph
tools/seedsmith/seedsmith/adapters/items/combogen/run.py        EDIT — wire generator-harness
tools/seedsmith/seedsmith/report/cli.py                         EDIT — lift the --write refusal once
                                                                  both blockers are actually resolved
docs/architecture/decisions.md                                  possible new entry, if the frozen-
                                                                  registry blocker requires a real bump
```

## Code style

Read `combogen/`'s existing files fully before editing — this module explicitly investigates a stated
blocker rather than assuming its shape; DESIGN-GATE's own "test the constraint before you declare it"
rule applies directly here.

## Testing strategy

- A real combination entry, generated end to end, imports cleanly.
- `combogen/migrate.py`'s retirement of `sockwords.json` runs against real generated content and
  produces the result its own docstring already describes.
- Harness tests (resume/reconcile/overwrite), applied to the `combination` kind once unblocked.

## Boundaries

**Always:** investigate the two named blockers precisely before writing any fix — "unwired graph" and
"frozen registry" are each a specific, findable fact, not a category to guess at.

**Ask first:** any change to `naming.v1.json`'s `frozen`/`registryVersion` fields — this is exactly the
kind of architecture-locking change `decisions.md` governs, and this module does not have standing to
make that call unilaterally.

**Never:** work around the frozen-registry blocker by writing combination content that ignores or
duplicates the naming registry's own id grammar — that reproduces the exact drift a frozen registry
exists to prevent.
