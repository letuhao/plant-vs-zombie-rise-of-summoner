# gui-lego — plan (design-only)

**Status:** Design pack delivered — **awaiting owner review**. No React implementation in this
wave.  
**Ideal / map:** [docs/architecture/gui-lego-ideal.md](../docs/architecture/gui-lego-ideal.md) ·
[docs/architecture/gui-lego-map.md](../docs/architecture/gui-lego-map.md)  
**Design index:** [docs/design/gui-lego/README.md](../docs/design/gui-lego/README.md)  
**Todo:** [gui-lego-todo.md](gui-lego-todo.md)

---

## Goal

Replace “port the combat console as one page” with a **composable menu Lego kit** (Phaser-analogue
Scene/System/Registry/GameObject + MVVM over DPLP, aligned with ERM). First surface =
`derived-console`.

---

## Delivered this wave

1. Ideal + capability map (pattern lock, bans, bus catalog, theme taxonomy).  
2. Composition grammar + [recipes/derived-console.json](../docs/design/gui-lego/recipes/derived-console.json).  
3. Design README — piece index, dependency graph, reuse matrix.  
4. `foldDerivedSurfaceVm` contract + theme-packs module spec.  
5. Per-piece specs under `docs/architecture/gui-lego/spec-*.md`.  
6. Per-piece HTML drafts + shared `_piece-kit.css`.  
7. Theme demos (fire, ice, status-dot, neutral) + **swap-lab**.  

Stop-gap production code `web/.../ui/actor/derived/*` is **unchanged** on purpose.

---

## Owner review gates

1. Accept / reject piece boundaries and recipe slots.  
2. Accept / reject payload shapes (esp. `channel-row`, gauges, lifecycle).  
3. Confirm theme `css` + `paint` split (open `themes/swap-lab.html`).  
4. Confirm host = ActorPanel tab (no second shell).  
5. Only then: authorize a **separate** React implementation stream.

---

## After acceptance (not this plan)

- Implement piece registry + fold + recipe mount in FE.  
- Replace stop-gap Derived modules by composition.  
- Contract tests for landmark/`>` DOM; side-by-side vs Lego drafts.  
- Do **not** resume CV-only CSS patches as strategy.

---

## How to preview drafts

```powershell
start docs\design\gui-lego\README.md
start docs\design\gui-lego\themes\swap-lab.html
start docs\design\gui-lego\pieces\channel-row.html
```
