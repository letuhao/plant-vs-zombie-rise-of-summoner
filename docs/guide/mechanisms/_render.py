#!/usr/bin/env python
"""Render mechanism teach pages from _content/*.json into markdown + site HTML.

One content file per mechanism is the single source; this script writes both
`mechanisms/<slug>.md` and `site/mechanisms/<slug>.html` so the two can never
drift. It also refreshes the Detail column in `mechanisms/README.md` and in the
Mechanisms tab of `site/index.html`.

    python _render.py            # render every content file
    python _render.py souls      # render one
    python _render.py --check    # validate only, write nothing (exit 1 on error)

Authoring contract: _content/README.md
"""

from __future__ import annotations

import html
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
CONTENT = os.path.join(HERE, "_content")
GUIDE = os.path.dirname(HERE)
SITE_MECH = os.path.join(GUIDE, "site", "mechanisms")

STATUS_CLASS = {"Shipped": "shipped", "WIP": "wip", "Vision": "vision"}

FONT_LINK = (
    "https://fonts.googleapis.com/css2?family=Lilita+One&family=Nunito:wght@400;700;800"
    "&family=Noto+Sans+SC:wght@400;700&display=swap"
)

REQUIRED = ("slug", "title", "status", "kicker", "pillar", "loopLine", "hook",
            "blindSpots", "sections", "doSteps", "mixUps", "related", "sources")

# ---------------------------------------------------------------- link targets

def known_slugs() -> set[str]:
    return {f[:-3] for f in os.listdir(HERE) if f.endswith(".md") and f != "README.md"}


def resolve(target: str, fmt: str) -> str:
    """Resolve an authoring link target for markdown ('md') or site HTML ('html').

    `slug`        -> sibling mechanism page
    `^page.md`    -> guide root (docs/guide/)
    `~path`       -> docs root (docs/)
    `!url`        -> verbatim (external / in-page anchor)
    """
    if target.startswith("!"):
        return target[1:]
    if target.startswith("^"):
        return ("../" if fmt == "md" else "../../") + target[1:]
    if target.startswith("~"):
        return ("../../" if fmt == "md" else "../../../") + target[1:]
    return target + (".md" if fmt == "md" else ".html")


# ------------------------------------------------------------- inline markup

INLINE = re.compile(r"\*\*(.+?)\*\*|`(.+?)`|\[(.+?)\]\((.+?)\)")


def inline_md(text: str) -> str:
    def sub(m: re.Match[str]) -> str:
        if m.group(1) is not None:
            return f"**{m.group(1)}**"
        if m.group(2) is not None:
            return f"`{m.group(2)}`"
        return f"[{m.group(3)}]({resolve(m.group(4), 'md')})"

    return INLINE.sub(sub, text)


def inline_html(text: str) -> str:
    out, pos = [], 0
    for m in INLINE.finditer(text):
        out.append(html.escape(text[pos:m.start()]))
        if m.group(1) is not None:
            out.append(f"<strong>{html.escape(m.group(1))}</strong>")
        elif m.group(2) is not None:
            out.append(f"<code>{html.escape(m.group(2))}</code>")
        else:
            href = html.escape(resolve(m.group(4), "html"), quote=True)
            out.append(f'<a href="{href}">{html.escape(m.group(3))}</a>')
        pos = m.end()
    out.append(html.escape(text[pos:]))
    return "".join(out)


def plain(text: str) -> str:
    """Strip inline markup — for <title>/meta description."""
    return INLINE.sub(lambda m: m.group(1) or m.group(2) or m.group(3) or "", text)


def anchor(title: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-") or "section"


# ------------------------------------------------------------------ validate

def validate(doc: dict, slugs: set[str], path: str) -> list[str]:
    errs: list[str] = []

    def bad(msg: str) -> None:
        errs.append(f"{os.path.basename(path)}: {msg}")

    for key in REQUIRED:
        if key not in doc:
            bad(f"missing required key '{key}'")
    if errs:
        return errs

    if doc["slug"] != os.path.basename(path)[:-5]:
        bad("slug does not match filename")
    if doc["status"] not in STATUS_CLASS:
        bad(f"status must be one of {sorted(STATUS_CLASS)} (use statusNote for 'thin'/'fiction')")
    if not isinstance(doc["pillar"], dict) or "text" not in doc["pillar"] or "href" not in doc["pillar"]:
        bad("pillar must be {text, href}")
    if len(doc["blindSpots"]) < 4:
        bad(f"blindSpots has {len(doc['blindSpots'])} entries — the page needs at least 4")
    if len(doc["mixUps"]) < 4:
        bad(f"mixUps has {len(doc['mixUps'])} entries — the page needs at least 4")
    if len(doc["sections"]) < 2:
        bad("needs at least 2 body sections")
    if len(doc["doSteps"]) < 3:
        bad("doSteps needs at least 3 steps")
    if not doc.get("sources"):
        bad("sources is empty — every page cites what it was written from")

    for i, sec in enumerate(doc["sections"]):
        if "title" not in sec:
            bad(f"sections[{i}] missing title")
            continue
        bodies = [k for k in ("paras", "cards", "steps", "table", "diagram", "groups") if sec.get(k)]
        if not bodies:
            bad(f"sections[{i}] '{sec['title']}' has no body (paras/cards/steps/table/diagram/groups)")
        if sec.get("table"):
            t = sec["table"]
            if len(t.get("cols", [])) < 2 or not t.get("rows"):
                bad(f"sections[{i}] table needs cols and rows")
            for r in t.get("rows", []):
                if len(r) != len(t["cols"]):
                    bad(f"sections[{i}] table row width != cols")

    # link targets
    def check_links(text: str, where: str) -> None:
        for m in INLINE.finditer(text):
            if m.group(4) is None:
                continue
            tgt = m.group(4)
            if tgt.startswith(("^", "~", "!")):
                continue
            if tgt not in slugs:
                bad(f"{where}: link target '{tgt}' is not a mechanism slug")

    for text, where in walk_text(doc):
        check_links(text, where)

    for key in ("related", "next"):
        for s in doc.get(key, []):
            if s not in slugs:
                bad(f"{key}: '{s}' is not a mechanism slug")
            if s == doc["slug"]:
                bad(f"{key}: page links to itself")

    return errs


def walk_text(doc: dict):
    yield doc["hook"], "hook"
    for b in doc["blindSpots"]:
        yield b.get("meaning", ""), f"blindSpot '{b.get('term')}'"
    for a in doc.get("alsoTrue", []):
        yield a, "alsoTrue"
    for sec in doc["sections"]:
        where = f"section '{sec.get('title')}'"
        for k in ("intro", "note"):
            if sec.get(k):
                yield sec[k], where
        for p in sec.get("paras", []) or []:
            yield p, where
        for c in sec.get("cards", []) or []:
            yield c.get("body", ""), where
        for s in sec.get("steps", []) or []:
            yield s, where
        for g in sec.get("groups", []) or []:
            for s in g.get("steps", []) or []:
                yield s, where
            if g.get("note"):
                yield g["note"], where
        if sec.get("table"):
            for row in sec["table"].get("rows", []):
                for cell in row:
                    yield cell, where
    for s in doc["doSteps"]:
        yield s, "doSteps"
    if doc.get("doNote"):
        yield doc["doNote"], "doNote"
    for f in doc["mixUps"]:
        yield f.get("q", ""), "mixUps"
        yield f.get("a", ""), "mixUps"


# ------------------------------------------------------------------ markdown

def render_md(doc: dict) -> str:
    L: list[str] = []
    status = doc["status"] + (f" — {doc['statusNote']}" if doc.get("statusNote") else "")
    L += [
        f"# {doc['title']}",
        "",
        f"**Status:** {status}  ",
        f"**Loop:** {doc['loopLine']} — see [The loops](../the-loops.md)  ",
        f"**Pillar:** [{doc['pillar']['text']}](../{doc['pillar']['href']})  ",
        f"**HTML guide:** [site/mechanisms/{doc['slug']}.html](../site/mechanisms/{doc['slug']}.html)",
        "",
        "---",
        "",
        "## In one sentence",
        "",
        inline_md(doc["hook"]),
        "",
        "---",
        "",
        "## Blind spots (if you are new)",
        "",
        doc.get("blindSpotsLead", "These words get guessed wrong. Read them once before the rest of the page."),
        "",
        "| Word | What it actually means here |",
        "|---|---|",
    ]
    for b in doc["blindSpots"]:
        L.append(f"| **{b['term']}** | {inline_md(b['meaning'])} |")
    L.append("")
    if doc.get("alsoTrue"):
        L.append("**Also true:**")
        L.append("")
        for a in doc["alsoTrue"]:
            L.append(f"- {inline_md(a)}")
        L.append("")
    L += ["---", ""]

    for sec in doc["sections"]:
        L += [f"## {sec['title']}", ""]
        if sec.get("intro"):
            L += [inline_md(sec["intro"]), ""]
        for p in sec.get("paras", []) or []:
            L += [inline_md(p), ""]
        if sec.get("diagram"):
            L += ["```text"] + list(sec["diagram"]) + ["```", ""]
        if sec.get("cards"):
            L += [f"| {sec.get('cardCols', ['Piece', 'What it does'])[0]} | "
                  f"{sec.get('cardCols', ['Piece', 'What it does'])[1]} |", "|---|---|"]
            for c in sec["cards"]:
                L.append(f"| **{c['name']}** | {inline_md(c['body'])} |")
            L.append("")
        if sec.get("steps"):
            for i, s in enumerate(sec["steps"], 1):
                L.append(f"{i}. {inline_md(s)}")
            L.append("")
        for g in sec.get("groups", []) or []:
            L += [f"### {g['title']}", ""]
            if g.get("intro"):
                L += [inline_md(g["intro"]), ""]
            for i, s in enumerate(g.get("steps", []) or [], 1):
                L.append(f"{i}. {inline_md(s)}")
            if g.get("steps"):
                L.append("")
            if g.get("note"):
                L += [f"> {inline_md(g['note'])}", ""]
        if sec.get("table"):
            t = sec["table"]
            L.append("| " + " | ".join(t["cols"]) + " |")
            L.append("|" + "---|" * len(t["cols"]))
            for row in t["rows"]:
                L.append("| " + " | ".join(inline_md(c) for c in row) + " |")
            L.append("")
        if sec.get("note"):
            L += [f"> {inline_md(sec['note'])}", ""]
        L += ["---", ""]

    L += [f"## {doc.get('doTitle', 'What you do')}", ""]
    for i, s in enumerate(doc["doSteps"], 1):
        L.append(f"{i}. {inline_md(s)}")
    L.append("")
    if doc.get("doNote"):
        L += [f"> {inline_md(doc['doNote'])}", ""]
    L += ["---", "", "## Common mix-ups", ""]
    for f in doc["mixUps"]:
        L += [f"**{inline_md(f['q'])}**  ", inline_md(f["a"]), ""]
    L += ["---", "", "## Related", ""]
    if doc.get("next"):
        L.append("- Next: " + " · ".join(f"[{link_title(s)}]({s}.md)" for s in doc["next"]))
    for s in doc["related"]:
        L.append(f"- [{link_title(s)}]({s}.md)")
    L += [
        f"- Pillar: [{doc['pillar']['text']}](../{doc['pillar']['href']})",
        "- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)",
        "- [Mechanism index](README.md)",
        "",
    ]
    return "\r\n".join(L)


TITLES: dict[str, str] = {}


def link_title(slug: str) -> str:
    return TITLES.get(slug, slug)


# ---------------------------------------------------------------------- html

def h_para(text: str, cls: str = "") -> str:
    c = f' class="{cls}"' if cls else ""
    return f"          <p{c}>{inline_html(text)}</p>"


def render_html(doc: dict) -> str:
    status = doc["status"]
    cls = STATUS_CLASS[status]
    badge_text = html.escape(status + (f" ({doc['statusNote']})" if doc.get("statusNote") else ""))
    desc = plain(doc["hook"])[:180]

    P: list[str] = []
    P.append("<!DOCTYPE html>")
    P.append('<html lang="en">')
    P.append("  <head>")
    P.append('    <meta charset="utf-8" />')
    P.append('    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />')
    P.append(f'    <meta name="description" content="{html.escape(desc, quote=True)}" />')
    P.append(f"    <title>{html.escape(plain(doc['title']))} — Rise of Summoner</title>")
    P.append('    <link rel="preconnect" href="https://fonts.googleapis.com" />')
    P.append('    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />')
    P.append(f'    <link href="{FONT_LINK}" rel="stylesheet" />')
    P.append('    <link rel="stylesheet" href="../styles.css" />')
    P.append("  </head>")
    P.append('  <body class="teach-body">')
    P.append('    <a class="skip-link" href="#main">Skip to content</a>')
    P.append("")
    P.append('    <div class="stage teach-page">')
    P.append('      <header class="site-header">')
    P.append('        <a class="brand" href="../index.html#welcome">')
    P.append('          <span class="brand-name">Rise of Summoner</span>')
    P.append('          <span class="brand-tag">Mechanism guide</span>')
    P.append("        </a>")
    P.append('        <nav class="header-links" aria-label="Guide navigation">')
    P.append('          <a href="../index.html#mechanisms">← Mechanisms</a>')
    P.append(f'          <a href="../../mechanisms/{doc["slug"]}.md">Markdown</a>')
    P.append('          <a href="../../README.md">Full catalog</a>')
    P.append("        </nav>")
    P.append("      </header>")
    P.append("")
    P.append('      <main id="main" class="teach-main">')
    P.append('        <p class="teach-kicker">')
    P.append(f'          <span class="mech-status mech-status--{cls}">{badge_text}</span>')
    P.append(f"          {html.escape(doc['kicker'])}")
    P.append("        </p>")
    P.append(f'        <h1 class="teach-title">{html.escape(plain(doc["title"]))}</h1>')
    P.append(f'        <p class="teach-hook">{inline_html(doc["hook"])}</p>')
    P.append("")

    # blind spots
    P.append('        <aside class="blind-spots" role="note" aria-labelledby="blind-title">')
    P.append('          <h2 id="blind-title">Blind spots (if you are new)</h2>')
    P.append(f'          <p>{inline_html(doc.get("blindSpotsLead", "These words get guessed wrong. Read them once before the rest of the page."))}</p>')
    P.append('          <dl class="def-list">')
    for b in doc["blindSpots"]:
        P.append("            <div>")
        P.append(f"              <dt>{html.escape(b['term'])}</dt>")
        P.append(f"              <dd>{inline_html(b['meaning'])}</dd>")
        P.append("            </div>")
    P.append("          </dl>")
    if doc.get("alsoTrue"):
        P.append('          <ul class="blind-also">')
        for a in doc["alsoTrue"]:
            P.append(f"            <li>{inline_html(a)}</li>")
        P.append("          </ul>")
    P.append("        </aside>")
    P.append("")

    used: set[str] = set()
    for sec in doc["sections"]:
        aid = anchor(sec["title"])
        while aid in used:
            aid += "-x"
        used.add(aid)
        P.append(f'        <section class="teach-section" aria-labelledby="{aid}-title">')
        P.append(f'          <h2 id="{aid}-title">{html.escape(plain(sec["title"]))}</h2>')
        if sec.get("intro"):
            P.append(h_para(sec["intro"]))
        for p in sec.get("paras", []) or []:
            P.append(h_para(p))
        if sec.get("diagram"):
            P.append('          <pre class="teach-diagram" aria-label="diagram">')
            P.append(html.escape("\n".join(sec["diagram"])))
            P.append("</pre>")
        if sec.get("cards"):
            P.append('          <ol class="pieces">')
            for c in sec["cards"]:
                P.append('            <li class="piece">')
                P.append(f'              <strong class="piece-name">{html.escape(c["name"])}</strong>')
                P.append(f"              <p>{inline_html(c['body'])}</p>")
                P.append("            </li>")
            P.append("          </ol>")
        if sec.get("steps"):
            P.append('          <ol class="steps-plain">')
            for s in sec["steps"]:
                P.append(f"            <li>{inline_html(s)}</li>")
            P.append("          </ol>")
        for g in sec.get("groups", []) or []:
            P.append('          <div class="gui-walk">')
            P.append(f"            <h3>{html.escape(plain(g['title']))}</h3>")
            if g.get("intro"):
                P.append(f"            <p>{inline_html(g['intro'])}</p>")
            if g.get("steps"):
                P.append("            <ol>")
                for s in g["steps"]:
                    P.append(f"              <li>{inline_html(s)}</li>")
                P.append("            </ol>")
            if g.get("note"):
                P.append(f'            <p class="teach-note">{inline_html(g["note"])}</p>')
            P.append("          </div>")
        if sec.get("table"):
            t = sec["table"]
            P.append('          <table class="teach-table">')
            P.append("            <thead>")
            P.append("              <tr>")
            for c in t["cols"]:
                P.append(f'                <th scope="col">{html.escape(c)}</th>')
            P.append("              </tr>")
            P.append("            </thead>")
            P.append("            <tbody>")
            for row in t["rows"]:
                P.append("              <tr>")
                for cell in row:
                    P.append(f"                <td>{inline_html(cell)}</td>")
                P.append("              </tr>")
            P.append("            </tbody>")
            P.append("          </table>")
        if sec.get("note"):
            P.append(h_para(sec["note"], "teach-note"))
        P.append("        </section>")
        P.append("")

    P.append('        <section class="teach-section" aria-labelledby="do-title">')
    P.append(f'          <h2 id="do-title">{html.escape(doc.get("doTitle", "What you do"))}</h2>')
    P.append('          <ol class="steps-plain">')
    for s in doc["doSteps"]:
        P.append(f"            <li>{inline_html(s)}</li>")
    P.append("          </ol>")
    if doc.get("doNote"):
        P.append(h_para(doc["doNote"], "teach-note"))
    P.append("        </section>")
    P.append("")

    P.append('        <section class="teach-section" aria-labelledby="mix-title">')
    P.append('          <h2 id="mix-title">Common mix-ups</h2>')
    P.append('          <dl class="faq-list">')
    for f in doc["mixUps"]:
        P.append("            <div>")
        P.append(f"              <dt>{inline_html(f['q'])}</dt>")
        P.append(f"              <dd>{inline_html(f['a'])}</dd>")
        P.append("            </div>")
    P.append("          </dl>")
    P.append("        </section>")
    P.append("")

    P.append('        <footer class="teach-foot">')
    if doc.get("next"):
        links = " ·\n            ".join(
            f'<a href="{s}.html">{html.escape(link_title(s))}</a>' for s in doc["next"])
        P.append(f"          <p>\n            Next:\n            {links}\n          </p>")
    rel = " ·\n            ".join(
        f'<a href="{s}.html">{html.escape(link_title(s))}</a>' for s in doc["related"])
    P.append(f"          <p>\n            Related:\n            {rel}\n          </p>")
    P.append("          <p>")
    P.append('            <a href="../index.html#mechanisms">← Mechanisms index</a> ·')
    P.append(f'            <a href="../../mechanisms/{doc["slug"]}.md">Markdown (editable SSOT)</a> ·')
    P.append(f'            <a href="../../{doc["pillar"]["href"]}">{html.escape(doc["pillar"]["text"])}</a> ·')
    P.append('            <a href="../../the-loops.md">The loops</a>')
    P.append("          </p>")
    P.append("        </footer>")
    P.append("      </main>")
    P.append("")
    P.append('      <footer class="site-footer">')
    P.append("        Rise of Summoner · mechanism teach page · mirrors the")
    P.append(f'        <a href="../../mechanisms/{doc["slug"]}.md">markdown guide</a>')
    P.append("      </footer>")
    P.append("    </div>")
    P.append("  </body>")
    P.append("</html>")
    return "\r\n".join(P) + "\r\n"


# ---------------------------------------------------------------------- index

def refresh_indexes(docs: dict[str, dict]) -> list[str]:
    """Point the Detail column of both indexes at the HTML guide + markdown."""
    touched = []

    readme = os.path.join(HERE, "README.md")
    with open(readme, encoding="utf-8-sig") as fh:
        text = fh.read()

    def md_row(m: re.Match[str]) -> str:
        slug = m.group("slug")
        if slug not in docs:
            return m.group(0)
        return (f"| {m.group('name')} | {m.group('status')} | "
                f"[Guide](../site/mechanisms/{slug}.html) · [{slug}]({slug}.md) |")

    new = re.sub(
        r"^\| (?P<name>[^|]+?) \| (?P<status>[^|]+?) \| [^|]*?"
        r"\[(?P<slug>[a-z0-9\-]+)\]\((?P=slug)\.md\)[^|]*\| *$",
        md_row, text, flags=re.M)
    if new != text:
        with open(readme, "w", encoding="utf-8-sig", newline="") as fh:
            fh.write(new.replace("\n", "\r\n").replace("\r\r\n", "\r\n"))
        touched.append("mechanisms/README.md")

    index = os.path.join(GUIDE, "site", "index.html")
    with open(index, encoding="utf-8") as fh:
        text = fh.read()

    def html_row(m: re.Match[str]) -> str:
        slug = m.group("slug")
        if slug not in docs:
            return m.group(0)
        name, status = m.group("name"), m.group("status")
        return (f'<tr><td><a href="mechanisms/{slug}.html">{name}</a></td>'
                f"<td>{status}</td>"
                f'<td><a href="mechanisms/{slug}.html">HTML guide</a> · '
                f'<a href="../mechanisms/{slug}.md">Markdown</a></td></tr>')

    new = re.sub(
        r"<tr><td>(?:<a href=\"mechanisms/[a-z0-9\-]+\.html\">)?(?P<name>[^<]+)(?:</a>)?</td>"
        r"<td>(?P<status><span class=\"mech-status[^>]*>[^<]*</span>)</td>"
        r"<td>(?:<a href=\"mechanisms/[a-z0-9\-]+\.html\">HTML guide</a> · )?"
        r"<a href=\"\.\./mechanisms/(?P<slug>[a-z0-9\-]+)\.md\">[^<]*</a></td></tr>",
        html_row, text)
    if new != text:
        with open(index, "w", encoding="utf-8", newline="") as fh:
            fh.write(new.replace("\n", "\r\n").replace("\r\r\n", "\r\n"))
        touched.append("site/index.html")

    return touched


# ---------------------------------------------------------------------- main

def main(argv: list[str]) -> int:
    check_only = "--check" in argv
    wanted = [a for a in argv if not a.startswith("--")]

    slugs = known_slugs()
    files = sorted(f for f in os.listdir(CONTENT) if f.endswith(".json"))
    if wanted:
        files = [f for f in files if f[:-5] in wanted]
        missing = set(wanted) - {f[:-5] for f in files}
        for m in missing:
            print(f"ERROR: no content file _content/{m}.json")
        if missing:
            return 1

    docs: dict[str, dict] = {}
    errors: list[str] = []
    for f in sorted(os.listdir(CONTENT)):
        if not f.endswith(".json"):
            continue
        path = os.path.join(CONTENT, f)
        try:
            with open(path, encoding="utf-8-sig") as fh:
                doc = json.load(fh)
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            errors.append(f"{f}: not valid JSON — {exc}")
            continue
        errs = validate(doc, slugs, path)
        if errs:
            errors.extend(errs)
            continue
        docs[doc["slug"]] = doc
        TITLES[doc["slug"]] = doc["title"]

    # titles for slugs that have no content file yet come from the page heading
    for slug in slugs - set(TITLES):
        page = os.path.join(HERE, slug + ".md")
        if os.path.exists(page):
            with open(page, encoding="utf-8-sig") as fh:
                head = fh.readline().strip()
            TITLES[slug] = head[2:] if head.startswith("# ") else slug

    for e in errors:
        print("ERROR:", e)
    if check_only:
        print(f"checked {len(docs) + len(errors)} content files — {len(errors)} error(s)")
        return 1 if errors else 0

    os.makedirs(SITE_MECH, exist_ok=True)
    render = [s for s in docs if not wanted or s in wanted]
    for slug in sorted(render):
        doc = docs[slug]
        with open(os.path.join(HERE, slug + ".md"), "w", encoding="utf-8-sig", newline="") as fh:
            fh.write(render_md(doc))
        with open(os.path.join(SITE_MECH, slug + ".html"), "w", encoding="utf-8", newline="") as fh:
            fh.write(render_html(doc))

    touched = refresh_indexes(docs)
    print(f"rendered {len(render)} mechanism(s) → markdown + site HTML")
    for t in touched:
        print("  refreshed", t)
    if errors:
        print(f"  {len(errors)} content file(s) skipped — see errors above")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
