# Wave 2 — semantic review, and what it found

Eight reviewers, each reading **across** partitions rather than within one, because every file in
this corpus was written by an agent that could see no other file. The deterministic half of the
fleet plan's verification table is the validator's job and was green before this ran: schema, id and
`nameKey` uniqueness, tag vocabulary, computed-field rejection, reference resolution, naming
patterns, the collision normalizer, the unique role/axis allocation, the set threshold rules.

**Result: no BLOCKERs. 41 findings, 19 of them MAJOR.** Corpus at the time: 1,438 entries, 125
files, 0 errors.

| Lane | Report | Findings |
|---|---|---|
| Theme coherence | [theme-coherence](wave2-theme-coherence.md) | 5 |
| Name sameness | [name-sameness](wave2-name-sameness.md) | 5 |
| Frame tone | [frame-tone](wave2-frame-tone.md) | 4 |
| Role fit | [role-fit](wave2-role-fit.md) | 3 |
| Flavour quality | [flavour-quality](wave2-flavour-quality.md) | 7 |
| Rarity legibility | [rarity-legibility](wave2-rarity-legibility.md) | 7 |
| Cross-kind voice | [cross-kind-voice](wave2-cross-kind-voice.md) | 4 |
| Coverage gaps | [coverage-gaps](wave2-coverage-gaps.md) | 6 |

---

## 1. Fixed already — the ones that were defects rather than judgements

**Ten invisible broken references.** `gems/g3.json` and `socket-words/sockwords.json` referenced
four affix families in snake_case — `atom.keen_edge`, `atom.shield_capacity` — that exist nowhere.
The reviewer root-caused it and verified by running the tool: `ReferenceCheck`'s `idLike` regex
gates whether a string is *considered* a reference at all, and it accepts hyphens but not
underscores, so these were not resolved-and-passed, they were **never looked at**. Zero errors, zero
warnings, ten dead references.

The spelling came from `naming.v1.json`, which lists the shipped families in snake_case while every
authored id in the corpus is kebab-case. The affix-family authors converted when *minting* an id;
the gem and socket-word authors, only *referencing* one, copied the registry verbatim. Both
readings are reasonable, which is why it happened twice independently.

Fixed: 15 references rewritten, and `idLike` now accepts underscores **on purpose** — a stricter
pattern does not reject a misspelling, it hides it. The underscore form now resolves against nothing
and reports `ReferenceUnresolved`, which is the entire point.

**A verbatim duplicate name.** `gem.g1-015` and `consumable.k1-007` both shipped as "Mending Pulse"
— not the same idea in different words, the identical string. `words.v1.json` exempts five kinds
from the word pools and spells out per kind what each still owes; for `gem` that is "global name and
nameKey collision checks, the naming patterns, and every tag/element/registry rule". The code
collapsed all five exemptions into one early return that skipped the collision check too. The
reviewer quoted the registry back at the validator, correctly.

Fixed: the exemption is now per kind, and the gem is renamed. It became `Salving Pulse` rather than
the obvious `Mending Gem`, because its family is `atom.hit-mend` and naming it for `atom.mending`
would trade a collision for a lie.

**Two families rendering one line.** Found while confirming the rename: `disptpl.p1-022` and
`disptpl.p1-023` both read `{value}% faster zombie advance`, for `atom.tempo-surge` (`Increased`)
and `atom.tempo-stampede` (`More`). That hides the most important stacking distinction in the
system behind identical text. The corpus already had the vocabulary — "increased max health" /
"more max health" — so they now read `increased` and `more`. A sweep of all 67 templates with a
known op found three others whose wording omits it (`×{value} attack` and two "faster" lines); none
has a twin sharing its phrasing, so none is ambiguous and none was touched.

Corpus-wide duplicate name strings: **0**. Validator tests: **71**, all green.

---

## 2. Owner decisions — content quality, not correctness

These are real and none of them is mechanical. They cost re-runs, and several are matters of taste.

**Flavour is absent from the late kinds.** All 60 consumables have none. 30 of 70 charms have none.
Three themes have no flavour text anywhere. Four of five sets are silent where their sibling uniques
speak. Half the gems read as generic RPG loot and the other half prove it did not have to. The
pattern is consistent: kinds authored later, with tighter briefs, came out mechanically correct and
tonally empty. **My briefs asked for flavour and did not require it**, and the validator cannot tell
prose from silence.

**The rarity ladder does not always climb.** `verdant-graft-90` reads flatter than its own
`verdant-graft-50`. `charnel-bloom-90` uses the same fixed-atom shape as `charnel-bloom-70`. The
rung-70 badge means different things in different themes. A player who collects would notice.

**Coverage skews.** Humanoid uniques are half as common as plant in four of the eight eligible
roles. The top rarity band is entirely dark or light — no fire, ice, air or earth. And 89% of
uniques declare the same counter-pressure flavour, which makes a mechanic meant to differentiate
into wallpaper.

**Role and axis fit.** Four uniques carry an atom that cannot function on their own frame — that one
is closest to a correctness bug and may deserve a validator rule if a registry states frame
legality. Three of `sunwoven-almanac-90`'s eight declare an axis their content never touches, which
is the predictable cost of allocating the axis centrally and letting the author choose the atoms.

**Name clusters the normalizer cannot see.** Five "Signet" charms compared against each other, plus
looser clusters flagged in the name-sameness report.

---

## 3. One thing left alone deliberately

`classes.v1.json` line 992 references `atom.elemental_defense` in snake_case; the real id is
`atom.elemental-defense`. It is the last underscore in the tree and the same root cause as the ten
references above. It sits in a **frozen registry**, so changing it is a v2 event and an owner call,
not a fix to make quietly. Code that reads families through `RegistrySet` is unaffected — it
normalizes `_` to `-` when building `ShippedFamilies`, which is itself evidence the codebase already
knows the two spellings are one idea.

Worth deciding on, because the snake_case in that registry is what caused this class of error twice
already and it will cause it again for the next author who references rather than mints.
