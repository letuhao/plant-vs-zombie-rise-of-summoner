# Actor sheet UI references — 2026-09-07

**Status:** research note for plate 13. Not a genre pitch. Layout patterns only.

**What was asked:** other games’ character sheets, so the ActorSheet stops being a wall of ids.

**Method:** public screenshots that returned HTTP 200 image bytes. Marketing shots that were combat-only were discarded.

## Steal

| Pattern | From | Used on plate 13 as |
|---|---|---|
| Left-docked sheet over live play | Path of Exile 2 character panel | ActorSheet is a layer, not a route |
| Pool tokens first | PoE2 Life / ES / Mana | Condition: HP radial + six coloured meters |
| Coloured resist / element chips | Last Epoch, Monster Hunter | Element hex + StatusGlyph |
| One open collapse group | Diablo IV S5 | Derived tab categories |
| Inspector, not a wiki table | BG3 Examine, Genshin Details | InspectSplit (GG-63) |
| Unspent + plus + confirm | Grim Dawn / Titan Quest | LeftoverBar |
| Shield as colour on HP | Slay the Spire block, Hades 2 armour, Last Epoch ward | Shield radials + HP overlay |
| Icon grid for the book | HoMM3 spellbook | Kit loadout / lawn combat book (already plate 12) |

## Do not steal

- PoE2 / Last Epoch **page-length derived lists** as the first paint.
- Diablo IV **auto-grant** as if it were leftover points (it is not).
- Last Epoch **+ on attributes** (that game has no leftover pool).
- Genshin / HSR **constellation graphs** on the lawn sheet.
- HoMM3 **full-screen book** covering the board.
- Six circular gauges that imply six **caps** on uncapped magnitudes.

## Screenshot URLs (verified 2026-09-07)

1. PoE2 character panel — https://assetsio.gnwcdn.com/character-stats.jpg?width=2048&fit=bounds&quality=85&format=jpg&auto=webp
2. Last Epoch stats — https://static.wikia.nocookie.net/lastepoch_gamepedia_en/images/f/f4/CharacterStats.png/revision/latest?cb=20180826220854
3. HoMM3 spellbook — https://upload.wikimedia.org/wikipedia/en/b/bb/Heroes_of_Might_and_Magic_III_Spellbook.jpg
4. Slay the Spire HUD — https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/646570/ss_01aa3e7759e457bfbf2422f31c325d7b3ba8a6eb.1920x1080.jpg
5. Hades HUD — https://upload.wikimedia.org/wikipedia/en/1/1a/Hades_video_game_screenshot.jpg

## What I could not find

- A current Diablo IV **character-stats** screenshot that was not a combat or map marketing still (Steam/GameFAQs 403).
- A PoE2 Wikipedia screenshot (429). The RPS asset above is the substitute.
- First-party designer notes on leftover-point confirm UX (studios ship the UI; they do not write the rationale).
- Any published rule that a derived-stat spark must fill against a global cap — that absence is why GG-64 had to be written here instead of copied.

## What this is not

Not a claim that Rise of Summoner should play like PoE. The lawn stays the first core loop. These are chrome patterns for a 1280×720 overlay that must not grow a page scrollbar.
