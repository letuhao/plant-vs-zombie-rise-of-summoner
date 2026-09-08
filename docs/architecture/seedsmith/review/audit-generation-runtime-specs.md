# Audit — feature 3 generation-runtime specs (5 modules)

**Lens:** handed these five specs and told to build them — what is underspecified, wrong, or
unmeasured?
**Method:** adversarial re-reading **plus execution** — the line classifier run over the real corpus,
motif output simulated before/after, document-frequency and POS-tag experiments, and two live probes
against the local model.
**Date:** 2026-09-01. **Ten findings.** Three close open questions (two of them answering the
*opposite* of what the question assumed); one invalidates a design in `quality-gates`.

---

## S1 — ⛔ `motif-prose-filter`'s classifier leaks, and the spec hand-waved the rule

§2.1 lists two rules and then says mechanical section headers *"and their continuation lines"* —
**without defining a continuation line.** Run over the real corpus, the classifier calls 99 lines
prose, of which **15 are mechanical**:

| leaked shape | count | example |
|---|---|---|
| numbered clause (`①②③…`) — the actual continuation of a `特点` list | 13 | `②处于火力覆盖模式时，如果地图行数少于6行，则损失的子弹伤害会均分给其他子弹` |
| ASCII-digit threshold text | 2 | `对于血量高于50%的雪橇冰车类僵尸，改为造成其50%韧性的伤害` |

**My own 70/29 measurement was therefore optimistic** — some of the "prose" bucket is mechanics.

**Fix — two explicit rules, replacing the hand-wave:**
3. a line beginning with a circled numeral `①…⑫` is **mechanical** (it *is* the continuation);
4. a line containing an **ASCII** digit is **mechanical**.

Rule 4 is deliberately ASCII-only: `可在三种攻击模式之间切换` uses `三`, not `3`, and is genuinely
thematic. Verified — it survives. After both rules, 84/99 lines are truly thematic.

---

## S2 — ⛔ `motif-prose-filter` §2.2 "prefer `flavorIntroduce`" makes some demons **worse**

Simulated the fix end to end. Most demons improve sharply:

| demon | before (committed) | after prose filter |
|---|---|---|
| `cherrynut` | `伤害`, `僵尸` ("damage", "zombie") | **`喜爱`, `坚果`, `樱桃`, `爆炸`** ("love", "nut", "cherry", "explosion") |
| `cactus` | `仙人掌`, `优先` ("cactus", "priority") | **`仙人掌`, `发射`, `尖刺`, `地`, `空`** ("cactus", "fire", "spike", "ground", "air") |

**But `bucketnutzombie` regresses**: `一类`, `击杀` → `为什么`, `他练成`, `是因为`, `铁头功`, `不过`
— *"why", "he trained", "because", "iron-head-technique", "however"*. Only `铁头功` is a motif.

`flavorIntroduce` is a **joke narrative** ("Why is the bucket-nut zombie's head so iron? Actually
it's because everyone says walnuts are good for the brain…"), so preferring it imports narrative
connectives. The spec proposed the preference without testing what that text actually looks like.

---

## S3 — ✅ Open question Q1 CLOSED — and its premise was **wrong**

`motif-prose-filter` §9 asked: *"Should a corpus-frequency floor exclude near-universal words like
`僵尸`?"* **Measured across all 84 demons — frequency is the wrong instrument:**

| token | document frequency | verdict |
|---|---|---|
| `僵尸` "zombie" | 34/84 (40%) | a floor kills it ✅ |
| `他` "he" | 12/84 | a floor kills it ✅ |
| `为什么` "why" | **3/84** | a floor **keeps** it ❌ |
| `是因为` "because" | **2/84** | a floor **keeps** it ❌ |
| `樱桃` "cherry" | 9/84 | a floor risks killing a **real family motif** ❌ |
| `坚果` "nut" | 7/84 | same risk ❌ |

**The connectives are rare-but-meaningless; the family words are common-but-meaningful.** Frequency
cannot separate them; it gets both directions wrong.

**The correct instrument is part-of-speech.** `jieba.posseg` on the exact failing narrative:

```
KEEP (content) : 铁桶/n 坚果/n 核桃/n 补脑/n 含铁/n 练成/v 铁头功/n
DROP (function): 为什么/r 其实/d 是因为/c 但是/c 不过/c 也许/d 至少/d 现在/t
```

Clean separation. **Answer: not a frequency floor — POS filtering**, keeping content classes
(`n*`, `v*`, `a*`, `i`, `l`) and dropping function classes (`r`, `c`, `d`, `p`, `u`, `t`). It also
generalises better than the hand-maintained `_CJK_STOPWORDS` list, which shrinks to a small override.

---

## S4 — ⛔ `quality-gates` §2.3's CoVe is ambiguous enough that **I implemented it wrong**

The spec says *"answer each question against the source material."* Building a probe from that spec,
I wrote a **subjective quality judgement** ("does this use the keyword meaningfully?") instead of a
source-grounded check. Measured on the real shoehorned outputs tier-2 had passed:

| CoVe form | agreed with human judgement |
|---|---|
| **Subjective** ("is this meaningful?") | **1/3** — passed *both* shoehorned cases, rationalising them (*"'一类' defines a specific category of behavior"*) |
| **Source-grounded** ("what does the source say this demon does? is the draft consistent?") | **2/3** — caught **both** shoehorned cases |

**Any text can be rationalised**, so a subjective verifier defaults to charitable and is worthless.
A verifier answering a question **from source** has something to be wrong against.

**If the spec's own author mis-built it on the first attempt, the spec is underspecified.** Fix:
§2.3 must **explicitly forbid** subjective quality questions and require every verification question
be answerable from source text alone.

---

## S5 — ⚠️ CoVe has a false-positive cost the spec never mentions

Source-grounded CoVe's one miss was a **false positive**: it rejected the *good* control
(`wallnut`) because the source said *"nuts have hard shells"* and the verifier objected that the
source *"does not describe a demon."*

A verifier that rejects good content costs real generation budget and can loop. **Fix:** reject only
on **explicit contradiction**, and route a CoVe rejection to **escalate** (human review) rather than
auto-repair — an unreliable judge must not silently drive the repair loop.

---

## S6 — ⛔ Recommendation change: **defer CoVe; the root cause is upstream**

Combining S2 and S4: shoehorning happens **because the motifs are bad**. Given `一类`/`僵尸`, a model
*must* shoehorn — there is no meaningful way to use "armour-class one" in a doctrine. Given
`坚果`/`樱桃`/`外壳`, it does not need to.

**CoVe was treating a symptom `motif-prose-filter` removes at the source.** It also costs 3–4× calls
(S9) and carries a false-positive rate (S5).

**Recommendation:** `quality-gates` ships tiers 1–2 and **specifies** CoVe fully but **does not build
it**. Build it only if shoehorning is *measured to persist* after `motif-prose-filter` lands. This is
the same discipline `spec-pipeline.md:109` already applies — do not spend a model on a problem
something cheaper solves.

---

## S7 — ✅ Open question Q3 CLOSED by measurement: **one** commander effect per demon

`commander-effect` §9 asked whether to generate one or several. Generated **3 for the same demon** at
temperature 0.9:

```
1. Phalanx Shell (阵列外壳法则)
2. 坚实防线 (Stalwart Phalanx)
3. Phalanx Shell (方阵护壳)
```

**Two of three produced literally the same name.** Pairwise character-overlap (Jaccard) 0.42 / 0.68 /
0.45, **mean 0.52**. They are synonyms, not alternatives — exactly audit **A1's thesaurus failure**
at single-demon scale. **One per demon is correct**, and the question is closed with evidence rather
than deferred.

---

## S8 — ⚠️ `commander-effect`'s id must be namespaced, and the spec should say why

`Corpus.add` raises `CorpusLoadError` on a duplicate id **across all kinds** — `entries` is a single
global dict, only `by_kind` is partitioned. A commander effect keyed `wallnut` would **collide with
the demon `wallnut`** and fail corpus load.

The spec's `"id": "commander-effect.wallnut"` is correct, but by luck of formatting rather than by
stated rule. **Add an explicit acceptance row**: an unprefixed id must fail corpus load — asserted,
so the constraint cannot be lost in a later refactor.

---

## S9 — ⚠️ Two understated numbers

1. **`quality-gates` §2.1 says CoVe costs "~2× calls".** Real cost is plan + answer + revise on top
   of generate = **3–4×**, more if questions are answered individually. Understating cost in a
   cost/benefit table is the same defect as the "6 direct deps" framing R6 already caught.
2. **`dependency-baseline` §6 asserts "395/395".** That number drifts the moment any later module
   adds a test. The criterion should be *"the full suite passes in a fresh venv"*, with 395 quoted as
   the value **at the time of writing**.

---

## S10 — ⚠️ `workflow-runtime` §2.2 calls every state field "bounded"; `brief: str` is not

`brief` is a rendered prompt of arbitrary length. It is bounded *in practice* (one demon's data), but
the spec states a guarantee it does not enforce. Either state it precisely — *"bounded by
construction: one subject's brief, never accumulated across steps"* — or assert a length cap. The
substantive rule (**no `messages` accumulator**) is right and unaffected.

---

## What the audit did not find

No dependency-direction violation. No numeric field on any generated schema. No PvZ-layer
dependency. No contradiction with `guard-dal`'s scope (the owner's shipping argument holds). The
LangGraph seam rule and the three-way loop bound are sound and, in the probe, effective.

---

## Disposition

| # | Severity | Lands on | Action |
|---|---|---|---|
| S1 | **underspecified** | `motif-prose-filter` §2.1 | Add explicit rules 3 (circled numerals) and 4 (ASCII digits) |
| S2 | **regression risk** | `motif-prose-filter` §2.2 | Preferring `flavorIntroduce` needs S3's POS filter, or it degrades some demons |
| S3 | ✅ **closes Q1** | `motif-prose-filter` §9 | POS filtering, **not** a frequency floor — the question's premise was wrong |
| S4 | ⛔ **design defect** | `quality-gates` §2.3 | Forbid subjective verification questions explicitly; require source-grounding |
| S5 | risk | `quality-gates` | Reject only on explicit contradiction; CoVe rejection → escalate, not auto-repair |
| S6 | **recommendation change** | `quality-gates`, build order | **Specify CoVe, do not build it** until shoehorning is measured to survive `motif-prose-filter` |
| S7 | ✅ **closes Q3** | `commander-effect` §9 | One per demon — measured synonymy (mean Jaccard 0.52) |
| S8 | latent break | `commander-effect` §2.4/§6 | Namespaced id + an assertion that an unprefixed id fails load |
| S9 | understated | `quality-gates`, `dependency-baseline` | CoVe is 3–4×; suite criterion is "passes", not a fixed number |
| S10 | imprecise claim | `workflow-runtime` §2.2 | State the bound precisely or enforce it |

**Three open questions closed, two of them answering the opposite of what the question assumed
(S3, S6).** S1, S2 and S4 would each have cost a rebuild if found during implementation.
