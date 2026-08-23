# `rarity/`

Empty on purpose, not by oversight (completeness-audit.md C3). The `rarity` table is real, fully
built, and hash-covered (E5/E14a) — the format is documented in
[../README.md](../README.md#rarity) — and this folder is where a rarity band goes when one is
actually authored (`common`/`rare`/`legendary` ordinals, pool-roll counts, tier ranges). Nothing
needs one yet: no shipped container currently names a `rarity` value, so `E14b`'s budget check
(`ContentValidation.Budget`) evaluates zero containers today — a `ceilingFor` with nothing to look up
is not a bug, it is this table having no content.

`tools/AtomImporter` sweeps this folder recursively and finds nothing, which is correct — the audit's
finding was that an *empty, undocumented* folder is indistinguishable from a forgotten one. This file
is that distinction. Add a `rarity` JSON file here when a container needs a rarity band; nothing else
changes.
