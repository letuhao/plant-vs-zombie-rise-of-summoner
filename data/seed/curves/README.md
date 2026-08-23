# `curves/`

Empty on purpose, not by oversight (completeness-audit.md C3). `effect_curve` is a real, fully built,
hash-covered table (E2/E14a) — the format is documented in [../README.md](../README.md#curve) — and
this folder is where a `curve` seed file goes when a scaling formula is actually authored (`curve.hp
.level`, `curve.dmg.tier`, that kind of thing). Nothing needs one yet: no shipped atom currently
declares a `curve` reference in its value spec.

`tools/AtomImporter` sweeps this folder recursively and finds nothing, which is correct — the audit's
finding was that an *empty, undocumented* folder is indistinguishable from a forgotten one. This file
is that distinction. Add a `curve` JSON file here when a value spec needs one; nothing else changes.
