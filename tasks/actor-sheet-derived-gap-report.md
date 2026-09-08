# Derived console — visual gap report (2026-09-09)

**Evidence:** `web/fusion-rpg-web/e2e/artifacts/derived/ssot-html.png` · `ssot-spa.png`  
**Viewport:** 1440×900 · **SPA:** rebuilt wwwroot + vite current · live Hub seed `derived-audit`

## Critical

None remaining for structure. `.console > .inspect-split` is a direct child; left|right split applies.

## Major (intentional / data — not defects)

1. **Identity / numbers** — HTML fiction Emberling Lv24 / 2,847 fire power vs live `derived-audit` Lv80 / real sheet values.
2. **Embed height** — sheet console `min(640px, 72vh)` vs standalone HTML `min(820px, 88vh)`.
3. **Dual header** — ActorPanel sheet chrome + `.console-hd` identity (HTML SSOT keeps identity in console).
4. **Glyphs** — CatalogIcon/lucide vs draft emoji in `.glyph` slot.
5. **Contribution richness** — draft multi-bucket donut; audit actor may show fewer buckets (e.g. equip-heavy).

## Minor

- ActorPanel tab chrome surrounds the console (expected embed).

## Verdict

Structure and IA match the draft product surface (cook rails + variant rail + list|inspect + gauges).  
**Owner visual gate required** before calling the fidelity stream done.
