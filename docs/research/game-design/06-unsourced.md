# What does not exist — read before searching

**Eight research passes on 2026-09-01 spent several hundred web searches between them. Every item
below was looked for and NOT found.** Some are genuine absences; some are access blocks. Both are
recorded, because re-running these searches costs the same budget for the same result.

---

## 1. Genuine absences — nobody has published these

### ⛔ No quantified counter-strength target, from anyone

No studio has ever published a statement of the form *"a counter should beat its target by X"* or
*"at Y% cost efficiency."* Blizzard's statements on counter strength are **entirely qualitative** and
always name micro or terrain as the override.

Measured bands exist (SC2 ×1.2–2.5, AoE4 exactly ×2/×3.5, AoE2 ×5–6.3 for the spear line) but they are
**observed from shipped data, not stated design targets.**

**If this project wants a number for how much element matchup should matter, it is deriving it, not
borrowing it.**

### ⛔ No threshold at which a counter matrix becomes unlearnable

No researcher or designer has published a value of *N*. The literature argues a different axis —
**inferrability** — and by that axis Warcraft III's 18 non-neutral cells are harder than Pokémon's 120.

### ⛔ No Game Freak statement capping Pokémon at 18 types

Only evidence of per-addition *cost* (a full rebalance, internal tournaments, first new type in over
ten years). **Do not let "18 is the designed maximum" be reported as a designer statement.**

### ⛔ No industry-standard closed role taxonomy

Blizzard's two official "unit types" pages **both explicitly disclaim completeness** — *"Here are some
of the unit types"* and *"This is not a complete list."* Role is a per-unit editorial judgement used to
detect redundancy, never an enum a unit is assigned from.

### ⛔ Almost no designer commentary on roster or grid design, anywhere

**The most consistent negative finding across every pass.**

**Genshin's official "Developers Discussion" series was enumerated across 18 entries, 2023-09 →
2025-07. Every single one is quality-of-life** — artifact management, resin caps, quest skips, world
levels, auto-lock. **Not one discusses roster growth, duplicate avoidance, element/weapon grid
coverage, 4-star cadence, or power-creep policy.**

Also not found:
- No Hypergryph developer letter on Arknights archetype/branch design
- No Type-Moon / Lasengle statement on FGO class expansion
- No Intelligent Systems statement using the phrase "power creep"
- No Com2uS commentary on Summoners War creep
- No 4Gamer / Famitsu / Automaton Genshin *design* interviews

**Expect to reason from shipped data, not from interviews.**

### ⛔ No Blizzard developer concession that WC3's matrix was hard to learn

The legibility critique is by **Brandon Casteel, a strategy critic writing on Game Developer — not a
developer.** Attribute it correctly.

### ⛔ No Blizzard acknowledgment that the deathball was caused by the damage model

Their framing is consistently **army clumping, micro, and engagement frequency**, and the fixes were
AoE punish units plus economy changes — never the bonus-vs-attribute system.

### ⛔ No designer note on any of SC2's four live attribute swaps

Oracle, Creep Tumor, Sentry and Ghost all had attribute tags added or removed in shipped patches with
**bare one-line notes and no rationale.** Any explanation would have lived in the "Community Update"
forum posts, which are dead links on the decommissioned `us.battle.net` forums.

### ⛔ No Fire Emblem statement on why the weapon triangle is three-sided

Checked: Serenes Forest's translated Kaga interviews (*Fire Emblem: Treasure*, the Genealogy *Official
Guidebook*), the 1996 Genealogy roundtable, Kantopia's Nintendo Dream / N.O.M. interviews for FE7 and
FE9, Iwata Asks for Awakening (all 6 pages) and Fates (all 6 pages). **None contain "triangle",
"rock-paper-scissors", or an equivalent.** Fire Emblem Wiki's own article cites no developer source.

The only primary material is in-fiction: the *Shadow Dragon* tutorial text, and the Japanese name
**武器の３すくみ** (*buki no san-sukumi*, "weapon three-way deadlock").

### ⛔ No explanation of Fire Emblem Fates unifying its triangle

**Iwata Asks: Fire Emblem Fates, all 6 pages: the words "triangle", "weapon", "bow", "shuriken",
"dagger" and "tome" appear zero times.** The revamped triangle was first reported via a **Famitsu
information leak**, not a developer explanation.

### ⛔ No Engage statement on why Break replaced the damage modifier

All three Ask the Developer Vol. 8 chapters were pulled and grepped. **"Break" as a mechanic is never
discussed**; chapters 1 and 2 contain no weapon-triangle content at all.

### ⛔ No Relic commentary framing CoH/DoW damage-vs-armour as rock-paper-scissors

### ⛔ No Dawn of War 1 damage-type × armour-type table with percentages

**Positive evidence it does not exist in that form:** DoW1 has no compact matrix. *"Every weapon in the
game has unique DPS values against each of the thirteen armor types"* / *"every weapon in the game has
15 separate damage values, one for each of the armour classes."* Named damage types with a proper
matrix are a **Dawn of War II** feature.

### ⛔ No CA statement of the Total War armour formula

400 WH3 patch bodies and 3 dev blogs searched. The `random(0.5, 1)` armour roll is community
reverse-engineering **that the wiki itself flags as possibly outdated or incorrect.**

### ⛔ No source stating a Summoners War family count

The **174** figure is computed from distinct `family_id` values over obtainable combat monsters, not
cited.

---

## 2. Access blocks — the material may exist but was unreachable

| Blocked | Effect |
|---|---|
| **Fandom sitewide HTTP 402** | Forced fallback to primary sources for AoE2, DoW, several others. **⚠️ SUPERSEDED 2026-09-02 — this is bypassable, see §2a below.** |
| **Liquipedia rate-limited; wiki.gg 401** | SC2 attribute count and additive-bonus mechanic could not be cited from there (recovered elsewhere) |
| **GameSpot 403 (Cloudflare)** | Kusakihara's Three Houses quote verified only against Nintendo Everything |
| **Jeuxvideo 403** | Yokota's quote likewise republisher-only |
| **`web.archive.org` playback blocked** (CDX index still worked) | Warhound removal post unreachable at source |
| **X / Twitter 402** | Rosewater's "not the best tool for balancing power" quote is via search indexing |
| **GDC Vault members-only** | Browder's GDC 2011 talk not opened; **not cited** |
| **`genshin.hoyoverse.com` JS-rendered** | The two official Zhongli posts confirmed live but not machine-readable; content via Siliconera's verbatim reproduction |
| **Reddit API refused scripted access** | Larian AMA primary source unreachable; quote via PC Gamer |
| **WebSearch 200-call budget exhausted** in several passes | The AoE / C&C designer-interview angle stayed thin |

### Reusable access notes for follow-up

- **Every legacy `us.battle.net/sc2/en/blog/<id>/<slug>` post is live at
  `https://news.blizzard.com/en-us/article/<id>`** with the same numeric ID. The full legacy ID list is
  enumerable from the Wayback **CDX index**, which works even when playback does not:
  `http://web.archive.org/cdx/search/cdx?url=us.battle.net/sc2/en/blog/*&fl=original&collapse=urlkey&filter=statuscode:200`
- **Liquipedia and StarCraft-Fandom block HTML scraping but serve their MediaWiki APIs normally** with
  a descriptive User-Agent, including `list=search&srsearch=insource:/…/` for exact-phrase patch-note
  hunting.
- **`https://r.jina.ai/<url>`** worked as a reader proxy for Cloudflare-blocked sites (this is how the
  Browder GameStar interview was retrieved).

### 2a. Access map re-measured 2026-09-02 — this supersedes the table above

Two further research rounds (**~14 passes, ~16,700 lines**, in [`../genre-mechanics/`](../genre-mechanics/)
and [`../action-taxonomy/`](../action-taxonomy/)) re-tested every block. **The single most important
change: Fandom's 402 is bypassable.**

| Host | State as re-measured | Workaround |
|---|---|---|
| `fandom.com` | 402 direct, confirmed again across all 14 passes | **`r.jina.ai` reader proxy works** — this reopens the FF, DQ, FEH, Megaten and mod-scene wikis the table above treated as lost |
| `poewiki.net` | proof-of-work / Anubis gate | **clears on a second navigation** |
| `wowdev.wiki` | 403 direct, 403 on its MediaWiki API, **and blocked through `r.jina.ai`** | none found — use **TrinityCore source** (`SpellInfo.h` / `SpellInfo.cpp` / `SpellEffects.cpp`) instead, which is first-tier anyway |
| `wiki.gg` | **inconsistent** — some subdomains 401, most serve fine | retry per-subdomain before concluding |
| `liquipedia.net` | article pages 403; API returns stubs only | — |
| `web.archive.org` | 429 / blocked in the later rounds | **the CDX index trick above still works** |
| `scryfall.com` + API, `mtggoldfish`, `kotaku`, `pcgamesn`, `wiki.leagueoflegends.com`, `store.steampowered.com` (DNS) | blocked | — |
| Caves of Qud wiki | — | serves `action=raw` |
| Moegirl, Zhihu, Baidu Baike, Fandom-zh | 403 / 402 | the Chinese-language mod scene is covered second-hand only |
| `megatenwiki.com` | **bot-verification gate, re-measured 2026-09-05** — and it now blocks through the `r.jina.ai` reader proxy too (party-dungeon prior-art review) | none found — Megaten mechanics are covered from the Fandom mirror via the proxy, and from datamines |
| `diablo2.diablowiki.net` | 403, measured 2026-09-05 (party-dungeon prior-art review) | none tried beyond a retry — use the Arreat Summit archive or `d2mods` datamines instead |
| `game8.co` | returns **empty bodies** to the fetcher, measured 2026-09-05 (party-dungeon prior-art review) — a 200 with no content, so it fails silently | none found — treat any game8 citation as unverified until fetched by hand |

**Endpoints confirmed working and worth reusing:**
`overwatch.blizzard.com/en-us/news/patch-notes/live/<YYYY>/<M>/` ·
`dota2.com/datafeed/patchnotes?version=<X.YZ>&language=english` (archive starts at **7.08**, Feb 2018) ·
`warcraft.wiki.gg` · `hearthstone.wiki.gg` · `mtg.wiki` ·
`magic.wizards.com/en/sitemap.xml` (live archive indexes back only to **~2020**) ·
`yugipedia.com/api.php?action=parse&page=…&prop=wikitext&format=json` ·
`raw.githubusercontent.com` for every datamine repo used.

**Newly recorded genuine absences** (full detail in the two folders' own gap sections):

- **No studio publishes a power-vs-cost ratio, or a cooldown-to-power formula.** Consistent with this
  file's existing finding on counter-strength targets — **any such number here is derived, not
  borrowed.**
- **No published break-even mathematics for buffs in turn-based combat, from any developer.** The only
  worked example found anywhere is a hobbyist forum post; the general form was derived in
  [`../action-taxonomy/05-support-healing-actions.md`](../action-taxonomy/05-support-healing-actions.md) §4.1.
- **Every pre-~2007 `magic.wizards.com/en/articles/archive/…` URL now 404s**, taking the whole *Latest
  Developments* column with it — this is why Skullclamp's development story could not be sourced.
- **PVZ Fusion's actual fusion-recipe count is not statically obtainable** — the table is built by native
  IL2CPP code, so it needs a running game and a `PlantMixTreeManager.PrintAllStatistics()` call.

---

## 3. Numbers that are derived, not cited — do not quote as sources

| Figure | Status |
|---|---|
| ~5,700 Monster Hunter hitzone values | Multiplication of two sourced figures (10 rows × 8 cols × 71 monsters) |
| Summoners War **174 families** | Computed from SWARFARM `family_id` |
| Arknights **72 branches** | Computed from `character_table.json`; corroborated by the wiki's own category count |
| All Pokémon matrix statistics | Computed from PokéAPI, cross-validated against reciprocal lists (0 mismatches) |
| SC2 DPS-per-resource spread | Computed from live unit data |
| FGO median ATK by release block | Computed from the Atlas Academy export |
| D&D / PF2e uniqueness percentages | Computed over Open5e (3,207) and Archives of Nethys (4,748) |

---

## 4. Live source conflicts, unresolved or resolved

| Conflict | Status |
|---|---|
| **WC3 Pierce vs Heavy** — Blizzard 100% vs Liquipedia 90% | **RESOLVED**: Reforged Patch 2.0.3, live 2025-07-17, changed it 100% → 90% |
| **AoE2 armour class 33** | Liquipedia's 2020 snapshot calls it "Anti Gunpowder"; the current genie-verified page says **no unit has it** |
| **C&C armour type order** | ModEnc contradicts itself; **EA's GPL source settles it** (`ARMOR_NONE, ARMOR_WOOD, ARMOR_ALUMINUM, ARMOR_STEEL, ARMOR_CONCRETE`) |
| **Epic Seven hero count** | Four sources, four answers: 277 / 299 / 255 / "over 300". **Honest range ~280–300** |
| **HSR roster and Path×Type fill** | ±2 depending on Trailblazer convention; fill count 54–56 of 63 |
| **Genshin roster** | 118 by parse, 120 per the wiki header — same convention issue |

---

## 5. Data that is stale and has no current replacement

**Genshin usage statistics stop at Version 2.7 (2022).** spiralabyss.org is frozen; genshinlab's usage
page 404s. The 319× usage-spread finding is hard data but **four years old.**

**No public Honkai: Star Rail usage dataset exists at all** — no Memory of Chaos, Pure Fiction or
Apocalyptic Shadow percentages were reachable from any source. **No official HoYoverse usage
infographics were found for either game.**

This is the largest data gap in the whole research round.
