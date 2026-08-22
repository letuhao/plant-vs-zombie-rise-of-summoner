# Spec: content-hash (E8)

**Status: BUILT 2026-08-22.** Deviations found while building are marked **[built]** below.

Module **E8** in the [atom effect map](../effect-atom-map.md). Depends on **E4**, **E5**. Small module, load-bearing consequence.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Make a changed number **visible**. Once effect content lives in rows, determinism depends on a content revision — so a hash over the content tables is computed at load and stamped into the report beside the four version stamps the battle report already carries.

Without this, editing one atom row silently changes every golden and nobody can tell whether a diff is a bug or a balance edit.

## Design (locked on approval)

### What the hash covers

| Included | Excluded |
|---|---|
| `effect_atom` | `effect_instance` — per-player, not content |
| `effect_container` | `effect_instance_atom` — same |
| `effect_container_atom` | `effect_binding` — per-player |
| `effect_container_pool` | runtime state — RAM, not durable |
| `effect_curve` | `power_coefficient_proposal` — a sweep artefact, not shipped content |
| `effect_element` + both matrix tables (**E18**) | |
| `power_coefficient` + `power_trigger_frequency` | |
| `rarity` | |
| `content_meta` is **not** hashed — it holds the revision, not content | |

The line is exactly the code-or-data rule's line: **content is hashed, player state is not.** An instance is a consequence of content plus a seed, so hashing content and storing `roll_seed` already pins it.

### How

Stable, order-independent, integer-friendly:

1. Serialise each row canonically — columns in declared order, each **length-prefixed** as `{byteLen}:{bytes}`; JSON keys sorted ordinal; numbers emitted as integers when integral; strings NFC. **[built] NULL is the sentinel `N:` with no payload, not a literal `\x00`** — a column holding exactly `"\0"` is also one byte of `0x00` under prefix `1:`, so the original rule let a value forge a NULL. `N` is not a digit, so no length can spell it. See definitions §8.
2. Hash each row, then **sort the digests and concatenate**. **XOR-fold is banned**: XOR cancels duplicates, so a non-idempotent import that doubled every row would leave the hash *unchanged* — and E14's "import twice, hash unchanged" test would pass while the database doubled. The cheaper option is the broken one.
3. Combine per-table digests in a fixed table order.
4. Emit a short hex prefix for humans (`content:a3f91c`) and keep the full digest for comparison.

Disabled rows (`enabled = 0`) **are** included — disabling an atom is a content change that can move a golden, and pretending otherwise would hide exactly the edit we want visible.

### Where it is stamped

Alongside the stamps the report already carries — `engineVersion`, `rngAlgoVersion`, `rulesetVersion`, `seed` — as `contentHash`. Same discipline as the platform stamp: a replay or sweep that re-resolves across a **different** `contentHash` is refused, not silently re-run.

**Power in the report is the stamped power, never a recomputed one** (owner default). If difficulty or rewards read actor power, the report must show the number that was actually used.

### What it changes about goldens

A content edit now produces a **loud, attributable** diff: the hash moves, and the re-bless is a decision with a written predicted delta rather than an accident. That is the whole point — [effect-atom-ideal.md](../effect-atom-ideal.md) lists silent content drift as a refused outcome.

**E18 interaction:** once the element roster is data, adding an element changes the generated channel set *and* the hash. That is correct and desirable — it means an element addition can never be mistaken for a code regression.

### Covered tables — a versioned registry, not a fixed list

Modules **register** into an ordered set, versioned as `contentHashSchemaVersion`.

This matters concretely: E9 adds `power_coefficient` and `power_trigger_frequency`, and E18 adds three element tables — all **after** E8 ships. A fixed list would silently invalidate every hash E11 stamped, and E8's own refuse-cross-hash-replay rule would turn that into a hard failure of the Checkpoint D corpus. An added table is an explicit, attributable version bump.

Covered: `effect_atom` · `effect_container` · `effect_container_atom` · `effect_container_pool` · `effect_curve` · `effect_element` + both matrices · `power_coefficient` · `power_trigger_frequency` · `rarity`.
Not covered: instances · bindings · runtime state · `power_coefficient_proposal`.

**[built] Version 1 registers the six tables that exist today** — `effect_atom`, `effect_container`, `effect_container_atom`, `effect_container_pool`, `effect_curve`, `rarity`. `effect_element` and both matrices arrive with **E18** (version 2) and `power_coefficient` / `power_trigger_frequency` with **E9** (version 3). Registering a table before its DDL exists would make `ComputeContentHash` throw on every call.

**Action tables register as a later version — approved 2026-08-22.** The action program's `rpg_action`, `rpg_action_cost`, and `rpg_action_effect_scope` are content by the same definition as everything above: authored rows whose values change battle outcomes. Left out, **a balance tweak to an action would silently invalidate every golden and every recorded battle**, and the report would claim a reproducibility it no longer has — the exact drift this module exists to prevent.

They register **after** `A1` ships its DDL, as their own version bump, because registering a table before its DDL exists makes `ComputeContentHash` throw on every call. Same rule already applied to `effect_element` and `power_coefficient`. Instances stay excluded, as everywhere else. See [action/spec-action-catalog.md](../action/spec-action-catalog.md) (A6) §3.

**[built] Column lists are explicit, not read from `PRAGMA table_info`.** Reading them from the database would make adding a column move every stamp silently — the exact accident the versioned registry exists to prevent. Adding a column is a registry edit and a version bump.

**[built] The stamp carries per-table digests**, truncated to 16 hex characters, in the durable form `v{version}|{hash}|{table}={digest},…`. Without them the cross-version rule below has nothing to compare and collapses into the blanket refusal it exists to avoid. The full combined hash decides *whether* content moved; the per-table digests say *where*.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ContentHash"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContentHash"
dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~WebMatchContentHash"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/ContentHash.cs           (canonical serialise + sorted concat)
src/FusionRpg.Core/Effects/Atoms/ContentHashRegistry.cs   (the versioned covered set)
src/FusionRpg.Core/Effects/Atoms/ContentHashStamp.cs      (the stamp + the replay verdict)
src/FusionRpg.Data/Sqlite/RpgStore.ContentHash.cs         (read the covered tables)
tests/FusionRpg.Core.Tests/Atoms/ContentHashTests.cs      (44 tests)
tests/FusionRpg.Data.Tests/ContentHashStoreTests.cs       (17 tests)
tests/FusionRpg.E2E.Tests/WebMatchContentHashSweepTests.cs (4 tests — the consumer)
```

**[built] The consumer is the boot sweep.** `WebMatchService.SweepUnresolved` already refuses to re-resolve
a logged match across a different engine/ruleset/rng version or a different `BattleEnvironment.Stamp`; the
content stamp joins that list, stored in a new nullable `rpg_web_match_log.content_hash` column. Report
stamping stays **E12**'s — a new stamped field in a report *is* a golden diff. Without this the module would
have shipped computing a hash nothing consulted.

## Testing strategy

| Case | Expect |
|---|---|
| Same content, two loads | identical hash |
| Rows inserted in a different order | identical hash |
| One magnitude changed by 1 | hash changes |
| A row disabled | hash changes |
| An instance created / a binding added | hash **unchanged** |
| A sweep proposal written | hash **unchanged** |
| JSON key order shuffled in `params_json` | hash unchanged — canonical serialisation |
| Whitespace added to a JSON column | hash unchanged |
| Element added (E18) | hash changes, and the generated channel count changes with it |
| Replay across differing hashes, **same `contentHashSchemaVersion`** | **refused**, with both hashes reported |
| Replay across differing `contentHashSchemaVersion` | **not** refused outright — compare the per-table digests both versions share and report added/removed tables. E11 stamps at position 12; E18 and E9 add covered tables at 14 and 15, so a blanket refusal hard-fails the Checkpoint D corpus by construction |
| Registry order | pinned to the **table name**, ordinal. If it were module-registration order, reordering code initialisation would move the hash with no content change |
| Empty covered table | digests as `SHA256("")` — an empty catalog produces a recognisable hash, not a stable-looking accident |
| Duplicated rows | hash **changes** — the XOR-fold failure mode, asserted |
| NULL vs a one-character `\0` string | **[built]** different digests — the sentinel, not a payload |
| NULL vs empty string | different digests |
| A row's column count not matching the registry | throws, never hashes a short row |
| Unparseable JSON in a JSON column | hashed as text, never an exception on the boot path |
| A covered table missing from the database | throws naming the table, never `SHA256("")` |
| An unknown `contentHashSchemaVersion` | refused before any query runs |
| A row logged with no stamp at all | **not** refused — rows predating this module still heal |

## Boundaries

**Always:** hash content only; canonicalise before hashing; include disabled rows; refuse cross-hash replay rather than re-resolving.

**Ask first:** adding or removing a covered table; changing the canonical form or the digest algorithm.

**Never:** hash instances, bindings, or runtime state; let row order affect the result; recompute power for a report instead of stamping what was used; treat a hash change as noise.
