# Wave 2 review — cross-kind voice

**Lane:** do the fourteen item kinds speak one language — same words for the same idea, no
kind-local dialect a script cannot see. **Method:** read every affix-family entry (98, across all
14 `affix-families/*.json` files) as the anchor vocabulary, then diffed it programmatically against
every other kind's own text and id fields across the full corpus (all 125 files, 1,438 entries) —
not sampled — for four specific things: (1) whether a gem/socket-word/charm's `family` reference
actually resolves to a real affix-family id, (2) whether a `display-template` row says the same
thing as the affix-family `displayTemplate` it mirrors, (3) whether a charm/gem/consumable's own
combat-posture tag agrees with the tag on the family it grants, and (4) manual reading of gems,
consumables, charms, socket-words, recipes, materials and display-templates in full, sampling
base-types/uniques/sets by grep-driven cross-reference. I also built and ran
`tools/ItemSeedValidator` against the live corpus to confirm what it does and does not currently
flag, rather than trust my own reading of its checks. **Verdict:** one real, validator-blind class
of broken cross-kind references (10 entries, narrow but genuine), one MINOR-severity but
corpus-wide wording fork between affix-family text and its presentation-layer mirror (32 of 98
pairs), and a smaller combat-posture labelling disagreement (10 of 150 checked). No BLOCKER — the
corpus is broadly coherent, and the defects found are real but bounded.

---

## MAJOR — gem and socket-word kinds spell four family ids differently than the affix-family kind that owns them, and the validator cannot see it

`data/seed/items/gems/g3.json` (4 entries) and `data/seed/items/socket-words/sockwords.json`
(6 entries) reference affix families by an underscored spelling —
`atom.keen_edge`, `atom.shield_capacity`, `atom.shield_toughness`, `atom.shield_regen` — that does
not exist anywhere in the corpus. The real, authored ids (in `g-precision.json` and
`g-shield-stat.json`) are `atom.keen-edge`, `atom.shield-capacity`, `atom.shield-toughness`,
`atom.shield-regen` — hyphenated, like every other affix-family id in the corpus.

Affected entries:

| Kind | Entries | Wrong spelling used |
|---|---|---|
| gem | `gem.g3-003` (Sharp Spore), `gem.g3-010` (Durable Rind), `gem.g3-011` (Regenerant Husk), `gem.g3-020` (Capacity Core) | one each |
| socket-word | `sockword.007` (Piercing Thorn), `sockword.011` (Shield Pact), `sockword.015` (Chitinous Guard), `sockword.019` (Cruel Precision), `sockword.020` (Regenerative Pulse), `sockword.021` (Wounding Strike) | `sockword.011` and `sockword.020` each use their broken family twice (once as an ingredient, once as the fixed bonus) |

**This is not the validator "deliberately narrowing" anything — it's a real gap, and I checked.**
`entry-shapes.md` §1 and §2 both state the contract in plain language: a gem/socket-word `family`
"must resolve to a shipped or sibling-authored `atom.*` family — `ReferenceUnresolved` else."
`ReferenceCheck.cs`'s `idLike` regex, which gates whether a string is even considered for reference
resolution, is `^(prefix)\.[a-z0-9]+(-[a-z0-9]+)*$` — it accepts hyphens between segments but not
underscores. `atom.keen_edge` fails that pattern outright, so `ResolveReference` is never called on
it at all; it isn't resolved-and-passed, it is silently skipped. I confirmed this isn't theoretical
by building and running the validator against the live corpus: **zero** of these 10 entries produce
any error or warning, not even in the 141 warnings the tool currently emits.

**Root cause, and why two different kinds picked two different spellings for the same idea:**
`naming.v1.json`'s own `idNamespaces.affixFamilies.groups[].existingFamilies` lists these families
in snake_case — `keen_edge`, `shield_capacity`, `shield_toughness` — because that registry predates
the corpus and was never updated to the id grammar the corpus actually uses. All eight
`affix-families/*.json` partitions independently converted snake_case to kebab-case when minting
the real id (correctly matching every other id in the corpus, e.g. `atom.hit-mend`,
`atom.death-harvest`). The gem and socket-word authors, needing to *reference* an existing family
rather than mint one, read the registry's literal spelling and used it verbatim — this is visible
directly in `RegistrySet.cs:353-355`, which builds `ShippedFamilies` by reading the same registry
and normalizing `_` to `-` itself, proving the codebase already knows these two spellings are the
same idea. Every other kind that references these four families elsewhere in the corpus
(`consumable.k3-014` references `atom.keen-edge` correctly, for instance) uses the correct
hyphenated form, so this is isolated to `gems/g3.json` and `socket-words/sockwords.json`.

**Consequence, hedged honestly:** as *content*, these 10 entries claim to grant a family that is not
in the corpus under that spelling. Whether this is inert at runtime depends on whatever
downstream C# resolves a gem's `family`/a socket-word's `ingredients[].family` against the atom
catalog — that lookup is outside a seed-content review's scope, and I did not trace it. If that
lookup is a literal string match (which is what every other piece of evidence in this corpus
suggests — ids are matched, not fuzzed), these 10 items grant nothing when the affected slot
resolves, and the two multi-ingredient socket words (`sockword.011`, `sockword.020`) can never be
completed by a player at all, since one of their required ingredient families does not exist to be
socketed.

**Fix:** replace `_` with `-` in the 10 flagged fields. Separately, `naming.v1.json`'s
`existingFamilies` lists should be updated to the corpus's actual kebab-case ids (or annotated as
"informal/pre-corpus spelling") so a future author reading that registry directly doesn't repeat
this. The validator's `idLike` regex should also accept `_` as a reference character (or reject it
outright as a naming violation) rather than silently ignoring it — right now it does neither.

---

## MAJOR — one display-template entry describes the opposite mechanic from the family it renders

`disptpl.p2-009` (`display-templates/derived.json`, `runtimeFamily: atom.stalwart`) reads:

> `+{value} status potency`

The affix-family it renders, `atom.stalwart` (`g-ward.json`), is tagged `defensive`, resolves to
channel `status.resist` (a flat additive resistance term per `ResistanceEvaluator.cs`), and carries
its own `displayTemplate`:

> `+{value} resistance to negative status effects`

"Status potency" reads as making *your own* inflicted statuses stronger — an offensive/support idea
— which is the opposite of what this family does (resisting statuses inflicted *on you*). This
isn't a wording nit like the ones below; it's the presentation layer describing a different
mechanic than the one the family actually is. `disptpl.p2-009`'s own `status` field is `"pending"`
rather than `"live"`, so no player sees this text today, but the row is authored and would ship
exactly this way the moment it's promoted to live. **Fix:** correct `disptpl.p2-009.name` to match
`atom.stalwart`'s own `displayTemplate` (or a faithful paraphrase of it) before that promotion.

---

## MINOR — 31 further display-template / affix-family pairs disagree on wording (32 of 98 total, including the one above)

Every affix-family entry with a `displayTemplate` field has a sibling row in
`display-templates/*.json` for the same `runtimeFamily`, and for 98 of 98 families a sibling row
exists. But the two copies of the same sentence agree word-for-word in only 66 of 98 cases (67%).
The other 31 (beyond `atom.stalwart` above) are wording drift, not meaning drift, split into two
patterns:

**Dropped articles ("a"/"the"), 25 pairs** — entirely inside `display-templates/triggered.json`
(the `g.on-hit`, `g.on-death`, `g.board`, `g.sustain` groups), all authored by the same batch
(`claude-haiku-4-5`, `display-templates/3`). Examples: `atom.freezing`'s family text is `"{value}%
chance to freeze **the** target on hit"`; its display-template (`disptpl.p3-022`) drops the
article: `"{value}% chance to freeze target on hit"`. Same pattern on `atom.venomous`,
`atom.mesmerizing`, `atom.withering`, `atom.sporing`, `atom.entangling`, `atom.cherry-bloom`,
`atom.dooming`, `atom.firelining`, `atom.flash-freeze`, `atom.gravemaking`, `atom.gravedigging`,
`atom.terraforming`, `atom.summoner`, `atom.gardener`, `atom.death-harvest`, `atom.death-glean`,
`atom.death-salvo`, `atom.retribution`, `atom.volley`, `atom.hit-retort`, `atom.cleansing`,
`atom.sust-husk`, `atom.sust-callus`, `atom.sust-grit`.

**Dropped `%` sign on an `Increased`-op template, 6 pairs** — all inside `display-templates/derived.json`
(`g.ward`, `g.precision`, `g.shield-stat`): `atom.shld-surge`, `atom.shld-cycle`,
`atom.shld-breach`, `atom.padding`, `atom.immunity`, `atom.ward-harden`. E.g. `atom.ward-harden`'s
family text is `"{value}% increased {element} resistance"`; `disptpl.p2-011` renders
`"{value} increased {element} resistance"` — no `%`. This one is worth a second look from whoever
owns the units question in `ssot-presentation.md` (that document already tracks a live
`Increased`/`More` unit-boundary question at a different layer) rather than a blind find-and-replace,
since a `%` sign is a claim about the unit, not just phrasing — but at minimum the two copies of the
same family's card text should not disagree with each other regardless of which one is right.

None of these 31 change what the mechanic does, and none of the elemental words (`{element}`) or
values (`{value}`) drift — only connective words and one glyph. I'd fix these in a polish pass, not
re-run either kind: they read as one batch of the `display-templates` partition retyping instead of
copying the string it was handed.

---

## MINOR — a wrapping item's combat-posture tag disagrees with the family it grants in 10 of 150 checked cases

I compared every charm/gem/consumable entry that both (a) carries an `offensive`/`defensive`/`utility`
tag itself and (b) grants a family that itself carries one of those three tags. 150 such pairs exist
across `charms/`, `gems/`, `consumables/`; 10 disagree (6.7%). Three of the ten are inside
`gems/g3.json` alone, which is the same file carrying the underscore-id defect above — worth flagging
to whoever re-touches that file, since it's now the file with two independent classes of defect:

- **`gem.g3-017`** ("Cultivator Sprout", grants `atom.gardener`) tags itself `offensive`.
  `atom.gardener`'s own entry in `g-on-death.json` is tagged `utility` with an explicit, reasoned
  note: *"a new plant on the board is board presence and tempo, not a direct damage swing"* — the
  affix-family author considered exactly this question and answered it in writing. The gem
  contradicts that answer outright.
- **`gem.g3-007`** ("Flourish Bud", grants `atom.flourishing`) tags itself `offensive`.
  `atom.flourishing` in `g-tempo.json` is tagged `utility`, again with a written reason:
  *"category E (economy) has no direct combat-posture tag, so `utility` is used per
  tags.v1.json's own definition."* Same pattern as above.
- **`charm.res-control-2`** ("Binding Accord") and **`charm.res-control-3`** ("Tangled Crescendo"),
  both granting `atom.freezing`, tag themselves `utility`; `atom.freezing` in `g-affliction.json`
  is tagged `offensive`. A CC lock being filed as "utility" is a defensible design opinion on its
  own, but it's the opposite of what the family that defines the mechanic calls itself.
- **`gem.g3-019`** ("Purifying Sap", grants `atom.cleansing`) tags `defensive`; the family tags
  `utility`. Softer disagreement — cleansing genuinely has defensive value — but still a mismatch.
- **`consumable.k3-013`** ("Spore Quickening", grants `atom.quickening`) tags `utility`; the family
  tags `offensive` (attack-speed is filed as offense, per `g-attack`/`g-tempo`'s own convention).

The remaining four (`charm.econ-019`, `charm.econ-020`, `charm.off-ctrl-010`, `charm.off-ctrl-020`)
are all "signet" charms whose flagged family is a `sign: negative` drawback riding alongside the
charm's real, differently-tagged payload — I checked each and consider these explainable rather than
a defect: the charm's own tag describes its primary purpose, not its downside atom.

Since `tags.v1.json`'s combat-posture axis feeds vendor-stock and loot-filter grouping, not
gameplay balance, this doesn't break anything a player can point at — it just puts five items in a
filter bucket that disagrees with the mechanic's own registered category. I'd correct the five
named above in a polish pass; I would not treat this as a corpus-wide problem, since 140 of 150
comparisons agree.

---

## What I did not check, and why

- **Full manual read of base-type flavor text (≈740 entries) and all uniques/sets for tone/theme
  consistency** — out of reach at this depth; I instead ran corpus-wide, code-driven checks (id
  resolution, template-text diffing, tag-posture diffing) that cover 100% of entries for those
  specific axes, and sampled base-types/uniques/sets only via targeted grep once the cross-kind
  family-reference check surfaced them. A slower, prose-level read of theme voice across all
  base-type partitions could still find drift these mechanical diffs can't see.
- **Runtime resolution behavior for the 10 broken references in Finding 1** — I confirmed they are
  unresolvable *within the seed corpus and its own validator*, and grounded that against
  `entry-shapes.md`'s explicit contract for the field. Whether the actual item-generation C# code
  normalizes underscores before doing its own lookup (which would make these inert-but-harmless
  rather than broken) is outside a content review and untraced.
- **Whether the `%`-sign question in Finding 2's second half is a units bug or a template bug** — I
  flagged the disagreement; resolving which side is correct belongs with whoever owns the
  `Increased`/`More` unit-boundary work already tracked in `ssot-presentation.md`, not this review.
