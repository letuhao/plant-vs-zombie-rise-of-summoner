# Spec: `motif-prose-filter`

Module `motif-prose-filter` in the [seedsmith map](../seedsmith-map.md) §3d.
Depends on `dependency-baseline`. **No model. No framework.**

Audit finding this exists because of: [R1](review/audit-agent-runtime-proposal.md).

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build.**

---

## 1. Objective

Make `motif-derive` read the **prose** in a demon's flavour text and ignore the **stat table**.

**This module exists because a measurement invalidated an earlier recommendation.** The proposal said
*"the taxonomy is thin because the input text is thin"* and prescribed an LLM workflow
(`lore-enrich`). Counting the real corpus proved the diagnosis wrong: the text is **not thin, it is
diluted.**

| `flavorInfo` across all 84 demons | chars | share |
|---|---|---|
| stat / mechanic lines | 4,276 | **70%** |
| prose | 1,815 | 29% |

`motif-derive` currently draws **70% of its input from a stat table**, so it emits stat vocabulary.
Real committed output, from the 2026-09-01 run:

| demon | derived motifs | what they actually are |
|---|---|---|
| `bucketnutzombie` | `一类`, `击杀` | **"armour-class one"** (from `韧性：270+2200（一类）`), "kill" |
| `cherrynut` | `伤害`, `僵尸` | "damage", **"zombie"** — a word in nearly every entry |
| `cactus` | `仙人掌`, `优先` | "cactus", **"priority"** (from `优先对空`, a targeting rule) |

**Done means:** motifs come from the thematic sentences a human would call flavour, and the stat
table contributes nothing.

**Why no model:** `spec-pipeline.md:109` — *"Before writing a pipeline, ask whether the task needs a
model at all… A pipeline for work a script can do is a slow, expensive, non-reproducible script."*
Separating `韧性：270+2200（一类）` from `铁桶坚果也能动起来？` is a line-shape decision, not judgement.

---

## 2. Design

### 2.1 Four line classes, decided by shape (**rules 3–4 added by audit S1**)

A flavour-text line is **mechanical** if it matches any rule:

1. **`label：value`** — a short label (≤12 chars, no colon) followed by a full-width `：` or ASCII `:`.
   Catches `韧性：270+2200（一类）`, `伤害：20/1.5秒`, `冷却：7.5秒`.
2. **A known mechanical section header** — `特点` (traits), `特性`, `弱点` (weakness), `使用条件`
   (usage condition), `融合配方` (fusion recipe).
3. **A line beginning with a circled numeral `①…⑫`** — this *is* the "continuation line" an earlier
   draft hand-waved. Measured: **13 such lines** were being classified prose, e.g.
   `②处于火力覆盖模式时，如果地图行数少于6行，则损失的子弹伤害会均分给其他子弹`.
4. **A line containing an ASCII digit** — threshold text, e.g.
   `对于血量高于50%的雪橇冰车类僵尸，改为造成其50%韧性的伤害` (2 such lines).

Everything else is **prose**. Prose feeds motif derivation; mechanical lines do not.

⚠️ **Rule 4 is ASCII-only, deliberately.** `可在三种攻击模式之间切换` uses the Chinese numeral `三`,
not `3`, and is genuinely thematic — verified to survive. A rule matching CJK numerals would delete
real prose.

**Measured effect:** the naive two-rule version left **15 of 99 "prose" lines mechanical**; all four
rules leave 84/99 genuinely thematic. The original 70/29 split was therefore *optimistic*.

**The ≤12-character label bound is structural, not tunable** — it distinguishes a field label from a
sentence containing a colon, and a balance pass would never touch it. Comment required
([tunables-ssot.md](../tunables-ssot.md)).

### 2.2 ⛔ Part-of-speech filtering — required, not optional (audit S2/S3)

**18 of 84** demons carry `flavorIntroduce` — lore with zero stat content. It is preferred input…
**but preferring it naively makes some demons worse**, measured:

| demon | before | after prose filter alone |
|---|---|---|
| `cherrynut` | `伤害`, `僵尸` | ✅ `喜爱`, `坚果`, `樱桃`, `爆炸` |
| `cactus` | `仙人掌`, `优先` | ✅ `仙人掌`, `发射`, `尖刺`, `地`, `空` |
| `bucketnutzombie` | `一类`, `击杀` | ❌ **`为什么`, `他练成`, `是因为`, `不过`** — narrative connectives |

`flavorIntroduce` is often a joke narrative, so it imports *"why… because… however…"*.

**A corpus-frequency floor cannot fix this** — measured, and it fails in **both** directions:

| token | doc. frequency | a frequency floor would… |
|---|---|---|
| `僵尸` "zombie" | 34/84 | ✅ correctly drop |
| `为什么` "why" | **3/84** | ❌ **keep** it |
| `樱桃` "cherry" | 9/84 | ❌ risk dropping a **real family motif** |

Connectives are rare-but-meaningless; family words are common-but-meaningful.

**The correct instrument is part of speech.** `jieba.posseg` separates them cleanly on the exact
failing text:

```
KEEP (content) : 铁桶/n 坚果/n 核桃/n 补脑/n 含铁/n 练成/v 铁头功/n
DROP (function): 为什么/r 其实/d 是因为/c 但是/c 不过/c 也许/d 至少/d 现在/t
```

**Keep** content classes `n*` (nouns), `v*` (verbs), `a*` (adjectives), `i` (idiom), `l` (phrase).
**Drop** `r` (pronoun), `c` (conjunction), `d` (adverb), `p` (preposition), `u` (particle), `t`
(time). `_CJK_STOPWORDS` shrinks to a small override list for cases POS mis-tags — it is no longer
the primary mechanism.

Order: `flavorIntroduce` first, then prose lines from `flavorInfo`, then the name. `basis` semantics
are unchanged.

### 2.3 A demon can become `blocked` that was not before, and that is correct

Filtering removes input. A demon whose `flavorInfo` is **entirely** a stat table has no prose, and if
it also lacks `flavorIntroduce` it will now derive from its name only (`basis="name"`) or, with an
empty name, become `blocked`.

**This is the honest outcome, not a regression.** A demon whose "motifs" were `一类` and `击杀` had no
real motifs; it had stat words wearing the label. Reporting `basis="name"` says something true that
the corpus previously obscured. The acceptance criteria expect the blocked count to **rise**.

### 2.4 Stopwords stay, and grow slightly

`_CJK_STOPWORDS` already filters particles and common verbs. Filtering stat lines removes the
*source* of `伤害`/`一类`, but words like `僵尸` ("zombie") appear in genuine prose too while carrying
no discriminating power — a motif shared by nearly every demon is not a motif. Corpus-frequency-based
exclusion is **out of scope here** (it changes what a motif *means*, and belongs with
`Distribution/MotifSharing`'s own measurement); this module only fixes the *source*.

### 2.5 What this module does not do

It does not call a model, does not change `basis`'s semantics, does not touch `family-extract` or
`family-consolidate`, and does not re-tune the 3–5 motif count. It changes **which text is read**.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_motif_derive.py -q
python -m pytest -q
# then, deliberately, a real re-derivation over the committed corpus:
python -m seedsmith demons motifs        # regenerates motifs.v1.json + motif-assignments.json
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/
    motifs.py                 → EDIT: add prose/mechanical split; prefer flavorIntroduce
tools/seedsmith/tests/test_motif_derive.py   → EDIT: line-classification + regression cases
data/seed/demons/_registry/motifs.v1.json          → REGENERATED (append-only rules apply, §7)
data/seed/demons/_generated/motif-assignments.json → REGENERATED
```

Single-file change. The classification is a pure function, exported for direct testing rather than
hidden inside `own_motifs`.

---

## 5. Code style

A pure `classify_line(line) -> Literal["prose","mechanical"]` and a pure
`prose_of(entry) -> str`, composed into `own_motifs`. Each rule testable alone, so a surviving mutant
points at one rule rather than "the filter". Match `consolidate.py`'s existing shape (a pure function
per merge rule).

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| `韧性：270+2200（一类）` | classified **mechanical** |
| `伤害：20/1.5秒` | **mechanical** |
| `特点：防具被磁力菇吸引后死亡` | **mechanical** (section header) |
| `融合配方：坚果+樱桃炸弹` | **mechanical** |
| `铁桶坚果也能动起来？` | **prose** |
| `仙人掌发射尖刺，可以对地或对空。` | **prose** |
| A prose sentence containing a colon mid-clause | **prose** — the ≤12-char label bound is what separates them, asserted directly |
| ⛔ `②处于火力覆盖模式时…` (circled numeral) | **mechanical** — rule 3, the 13-line leak S1 found |
| ⛔ `对于血量高于50%的…` (ASCII digit) | **mechanical** — rule 4 |
| ⛔ `可在三种攻击模式之间切换` (**CJK** numeral `三`) | **prose** — proves rule 4 is ASCII-only and does not over-filter |
| ⛔ POS: `为什么`/r, `是因为`/c, `不过`/c | **dropped** — the `bucketnutzombie` regression (S2), pinned by name |
| ⛔ POS: `铁头功`/n, `坚果`/n, `练成`/v | **kept** — content words survive; proves the filter is not just deleting everything |
| ⛔ **The three regression cases** — `bucketnutzombie`, `cherrynut`, `cactus` | motifs **no longer** contain `一类`, `伤害`, `优先`; the exact defect is pinned by name |
| A demon with `flavorIntroduce` | derives from it in preference to `flavorInfo` |
| A demon whose `flavorInfo` is 100% stat lines, no `flavorIntroduce` | falls back to name, `basis="name"` — **not an error** (§2.3) |
| Same corpus filtered twice | byte-identical (determinism preserved) |
| Whole-clause guard (from D2.3) | still passes — max token length ≤4 |

**The three named regression cases are the module.** Without them this is a refactor nobody can prove
worked; with them, the exact real-world defect cannot silently return.

---

## 7. Boundaries

- **Always:** keep classification pure and separately testable; prefer `flavorIntroduce`; treat a
  newly-`blocked` demon as a correct outcome; preserve determinism.
- **Ask first:** adding a new mechanical section header; corpus-frequency stopword exclusion (§2.4);
  changing the 3–5 motif bound.
- **Never:** call a model here; change what `basis` means; hand-author a motif to make a demon look
  covered.

⚠️ **Append-only interaction, flagged not hidden.** `motifs.v1.json` is append-only
([spec-motif-derive.md](spec-motif-derive.md) §4), but this module **removes** motifs that should
never have existed. Regeneration will drop ids like `一类` from the vocabulary. That is a deliberate,
owner-visible correction of bad data, **not** a routine re-run — it is exactly the "supersede rather
than duplicate" case the core backlog already names as `provenance-supersede` (map §3c). **The
regeneration is a reviewed act, and no generated content is bound to those motif ids yet** (the
`Coverage/DemonUncovered` run reports 84/84 demons with no content), so nothing downstream breaks.
**This window closes the moment `commander-effect` writes its first row** — so this module must land
before it, which the build order already requires.

---

## 8. Success criteria

1. Stat lines contribute **zero** tokens to any demon's motifs.
2. The three named regression cases (`一类`, `伤害`, `优先`) are pinned by test.
3. `flavorIntroduce` is preferred where present (18/84 demons).
4. Determinism and the ≤4-character token guarantee both hold.
5. A rise in `basis="name"`/`blocked` counts is reported as a **result**, not treated as a failure.
6. Full seedsmith suite green.

---

## 9. Open questions

**Closed 2026-09-01 by measurement** ([audit S3](review/audit-generation-runtime-specs.md)).

1. ~~Should a corpus-frequency floor exclude near-universal words like `僵尸`?~~
   ✅ **CLOSED — no, and the question's premise was wrong.** Measured across all 84 demons, a
   frequency floor fails in **both** directions: it keeps `为什么` (df 3/84) and `是因为` (df 2/84)
   because they are *rare*, while risking `樱桃` (9/84) and `坚果` (7/84), which are *real family
   motifs*. **Part-of-speech filtering is the correct instrument** and is now §2.2, promoted from
   "later question" to **required mechanism** — without it, preferring `flavorIntroduce` regresses
   `bucketnutzombie`.
