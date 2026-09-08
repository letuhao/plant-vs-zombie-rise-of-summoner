# `rarity/`

✅ **Seeded 2026-09-04 (item-ideal.md, `rarity-bands`, module 7).** `ladder.v1.json` carries the ten
authored rungs from `ssot-rarity.md` §3.3 — `chaff` through `almanac`, ordinals 10…100. Was empty on
purpose (completeness-audit.md C3) while `E14b`'s budget check (`ContentValidation.Budget`) had zero
containers to evaluate; that gap is exactly what seeding this folder closes.

⚠ **`prefixRolls`/`suffixRolls` are the FLOOR of §3.3's published half-ranges**, not the full range —
`RarityRow`'s schema has no `_max` column today (an Ask-first under effect-atom E5's boundaries, not
decided in this pass). §3.3's own recommended fallback names the cost: this loses one of the three
variances the overlap invariant (§3.5) is measured on. `sprout` and `heirloom` carry the **E3-corrected**
halves (`0–1`/`1–1` and `1–2`/`2–2`) — the originally-published halves for those two rungs did not sum
to their own count band and were fixed before this file was written, per `ssot-rarity.md`'s own note.

`tools/AtomImporter` sweeps this folder recursively; `data/tuning/item-rarity.v1.json` carries the
non-`rarity`-row values this ladder also needs (drop weights, enhancement caps, power-ceiling shares) —
see `RarityLadder.cs` for the single source those two files must agree with.
