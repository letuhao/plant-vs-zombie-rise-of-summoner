# Mechanism teach-page content — authoring contract

## Two tracks until PG-F6 finishes

| Track | What | Edit |
|---|---|---|
| **A — Teach pages** | JSON here → `_render.py` writes both `mechanisms/<slug>.md` and `site/mechanisms/<slug>.html` | Edit the JSON only; never hand-edit those two outputs for a rendered slug |
| **B — Short stubs** | Hand-written `mechanisms/<slug>.md` (~status / what / how / related) until promoted | Edit the markdown stub directly |
| **Hand teach exception** | **Local control room** is a full teach page without JSON yet | Edit MD + HTML by hand until migrated into `_content/` |

Today **every mechanism in the handbook index has a teach page** except the hand-authored Local control room (migrate to JSON when convenient). Edit JSON here and run `_render.py` — never hand-edit rendered `.md` / `.html` outputs for track-A slugs. Sibling links fall back to `.md` when a target has no HTML teach page yet (should be rare now).

One JSON file per mechanism on track A. `_render.py` turns each file into **both**
`mechanisms/<slug>.md` and `site/mechanisms/<slug>.html`, so the markdown and the
site page can never drift.

```powershell
python _render.py            # render everything, refresh both indexes
python _render.py souls      # render one page
python _render.py --check    # validate only; exit 1 on any error
```

---

## Who this is written for

Someone who has **never seen this project** and does not have the design docs in their head.
They will guess wrong about words this repo uses casually — *lawn*, *fusion*, *loam*, *specimen*,
*local*, *turn*. The **Blind spots** block exists to catch those guesses before the page teaches
anything. That block is required, and 4 entries is the floor, not the target.

Rules that keep a page honest:

- **Status is a promise.** `Shipped` means a player can do it in the current local build.
  `WIP` means designed and partly built. `Vision` means locked as design, not playable.
  A `Vision` page must say plainly, near the top, that this is not something you can open yet.
- **No invented numbers.** If the exact value is a tunable that a balance pass will move,
  describe the shape ("cost climbs with each pull"), not a fake constant.
- **Cite what you read.** `sources` never ships to the page — it is the audit trail for review.
- **Vocabulary comes from the guide**, not from the engine. Never put `typeId`, `Θ`, `P(Θ)`,
  `DerivedStatChannels`, `FA10`, or a class name on a player page.

---

## Fields

| Field | Required | What it is |
|---|---|---|
| `slug` | yes | Must equal the filename. Also the page's link id. |
| `title` | yes | Page heading, matches the index row. |
| `status` | yes | Exactly `Shipped`, `WIP`, or `Vision`. |
| `statusNote` | no | Qualifier the index uses, e.g. `thin`, `fiction`. Renders as `Shipped (thin)`. |
| `kicker` | yes | Short line under the badge, e.g. `Combat · every place`. |
| `pillar` | yes | `{"text": "Combat", "href": "combat.md"}` — the pillar page this hangs on. |
| `loopLine` | yes | Loop names from [the-loops.md](../../the-loops.md), e.g. `Summon and fusion · Level up and power`. |
| `hook` | yes | **One sentence.** What this is, in plain words. |
| `blindSpotsLead` | no | Replaces the default lead sentence above the table. |
| `blindSpots` | yes | ≥4 `{term, meaning}`. The word, and what it actually means *here*. |
| `alsoTrue` | no | Short bullets for corrections that are not a single word. |
| `sections` | yes | ≥2 body sections — see below. |
| `doTitle` | no | Defaults to `What you do`. Use `What you do (first time)` where that fits. |
| `doSteps` | yes | ≥3 numbered steps a player can actually follow. |
| `doNote` | no | One closing note; renders as a callout. |
| `mixUps` | yes | ≥4 `{q, a}` — the questions a new player really asks. |
| `next` | no | Slugs to read next. |
| `related` | yes | Sibling mechanism slugs. |
| `sources` | yes | Files you read, ideally `path §section` or `path:line`. Not rendered. |

### Section bodies

A section needs `title` plus at least one body field. Mix freely.

```jsonc
{
  "title": "How a pull works",
  "intro": "One line of setup.",           // optional
  "paras": ["Plain paragraphs."],           // optional
  "diagram": ["  [ A ]  ->  [ B ]"],        // optional, monospace block
  "cards": [{"name": "Souls", "body": "…"}],// optional, card list
  "cardCols": ["Stock", "What it buys"],    // optional, header for the markdown table form
  "steps": ["First…", "Then…"],             // optional, numbered
  "groups": [                                // optional, sub-walkthroughs with their own heading
    {"title": "On the altar", "intro": "…", "steps": ["…"], "note": "…"}
  ],
  "table": {"cols": ["Thing", "What happens"], "rows": [["a", "b"]]},
  "note": "Closing callout for the section."
}
```

**Suggested spine** — most pages want roughly this, in this order:

1. **What it is** — prose, one or two paragraphs.
2. **How it works** — the mechanic itself: `cards`, `table`, or `diagram`.
3. **Where you find it** — the actual GUI: `groups` with `steps`, naming real screens and buttons.
   For `WIP`/`Vision`, say what exists today and what does not, in that section, not in a footnote.
4. Optional fourth — costs, risks, what it feeds, how it fails.

### Inline markup

Allowed inside any prose string, and nowhere else:

- `**bold**`
- `` `code` `` — use sparingly, for literal on-screen text
- `[text](target)` where target is one of:

| Target form | Means | Example |
|---|---|---|
| `slug` | another mechanism page | `[Souls](souls)` |
| `^page.md` | a guide page | `[Combat](^combat.md)` |
| `~path` | anything under `docs/` | `[player runbook](~runbook/players.md)` |
| `!url` | verbatim href | `[repo](!https://example.com)` |

The renderer rewrites each form correctly for markdown *and* for the site page — never write a
relative path yourself, and never write `.md`/`.html` on a mechanism slug.

---

## Checklist before you save

- [ ] `python _render.py --check` passes.
- [ ] Every claim traces to something in `sources`.
- [ ] The status badge matches what the build can actually do today.
- [ ] Blind spots name the words *this page* will be misread through.
- [ ] Mix-ups answer real questions, not restatements of the body.
- [ ] No engine vocabulary, no invented constants, no vendor or tooling names.
