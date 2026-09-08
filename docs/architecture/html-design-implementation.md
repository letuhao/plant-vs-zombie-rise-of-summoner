# HTML design implementation — repo architecture

**Status: binding for FE ports of approved `docs/design/*.html` drafts.**  
**Procedure skill (local):** `.agents/skills/html-design-implementation/SKILL.md` and
`.claude/skills/html-design-implementation/SKILL.md` (both gitignored skill trees — this doc is
the committed half).

This document marks **where drafts live, how plans are filed, what reuse is allowed, and what
"done" means** in Rise of Summoner. The skill owns the phase workflow; this file owns the
repo-specific invariants so they survive skill-folder wipes.

---

## 1. When this applies

Any task that ports an approved HTML/CSS draft into `web/fusion-rpg-web` (ActorSheet tabs, HUD
consoles, modern-stat-hud bars, design-kit screens). Trigger phrases: "match the draft," "HTML
fidelity," "combat console," "don't Tailwind-rewrite," side-by-side screenshots.

Does **not** replace [DESIGN-GATE.md](../DESIGN-GATE.md) or [game-gui-principles.md](game-gui-principles.md).

---

## 2. Visual SSOT locations

| Kind | Path | Rule |
|---|---|---|
| Approved drafts | [`docs/design/*.html`](../design/) + [`docs/design/_kit/`](../design/_kit/) | Read-only during implement; edit draft first if look must change |
| Module visual pointer | e.g. [`actor-sheet/spec-derived-tab.md`](actor-sheet/spec-derived-tab.md) → `derived-combat-console.html` | Spec-named SSOT **wins** over older plate HTML |
| Plate / history | e.g. `13-actor-sheet.html` | Shell/history unless the active spec still points here |
| Production CSS port | e.g. `web/fusion-rpg-web/src/ui/actor/DerivedCombatConsole.css` | Scoped under a root class; sync from draft `<style>` |
| Tokens | `web/fusion-rpg-web/src/theme/tokens.css` (+ design kit tokens) | Prefer draft/`--*` tokens; do not invent a parallel theme |

**Direction of truth:** implementation moves toward the draft — never edit the draft to match a
cheap production pass.

---

## 3. Planning and program prefixes

HTML fidelity work is still a program stream:

- Plan / todo: `tasks/<program>-plan.md`, `tasks/<program>-todo.md`
- Spec / map: `docs/architecture/<program>-map.md`, `docs/architecture/<program>/spec-*.md`

Example: Derived console → `actor-sheet` program. Never write bare `tasks/plan.md`.

---

## 4. Structure breakdown is mandatory

Before production code, deliver a **landmark tree** (class names from the draft) and a
**component contract** (draft region → file).

Especially call out CSS **child combinators** (`.console > .inspect-split`). An intervening wrapper
(even `display: contents`) breaks `>` matching and silently destroys left|right layouts. Contract
tests must assert DOM shape (`:scope > .inspect-split`), not only "class exists somewhere."

Cutting a god TSX into contract modules **is** the fidelity structure work. Unrelated refactors
are not.

---

## 5. Reuse vs fidelity (kit policy)

Locked presentation libs: [tech-stack.md](../design/tech-stack.md) (lucide, recharts, xyflow,
motion, sparklines). Buy-before-build still applies when the draft needs charts/icons/motion.

**Do not** force a draft into plate-13 React kit when the SSOT grammar differs:

| Plate-13 kit (other tabs) | Console / modern-stat-hud drafts |
|---|---|
| `InspectSplit.tsx`, `StatRow.tsx`, Tailwind utility chrome | `.console`, `.cat-bar`, `.inspect-split`, draft CSS classes |
| Shared OK: `CatalogIcon`, `PanelShell`, bus hooks | Draft-only CSS + landmark components |

Reuse when visually compatible. Reuse does not override fidelity.

---

## 6. Deploy and verification proof

| Proof | Invalid without |
|---|---|
| Unit / contract | Landmark + cook IA asserts; **direct-child** layout contracts where CSS uses `>` |
| Live / Playwright | Real Server Hub when the surface is sheet-backed; not a fake 269-channel paint unless the task says so |
| SPA currency | Rebuilt `wwwroot` **or** vite against current source |
| Visual gate | Side-by-side screenshots same viewport (e.g. `web/fusion-rpg-web/e2e/artifacts/.../ssot-html.png` + `ssot-spa.png`) + owner confirm when SSOT-locked |

**Behavior green ≠ visual green.** Join/expand tests do not close an HTML fidelity task.

---

## 7. Anti-patterns already paid for in this repo

1. Mood-board Tailwind rewrite of a draft console.
2. sheetGroup Offense/Pools as primary Derived rail when the draft primary rail is cook tabs.
3. Stale Server `wwwroot` demoed as "the new UI."
4. Landmark class present but parent wrapper breaks `>` layout CSS.
5. Claiming done without side-by-side owner gate.

---

## 8. Completion checklist (repo)

- [ ] DESIGN-GATE docs for the surface read in-session
- [ ] Draft HTML read in full; structure + component contract written
- [ ] Plan under `tasks/<program>-*`
- [ ] Landmark DOM matches draft (no illicit wrappers under `>` parents)
- [ ] CSS synced or intentionally scoped deltas listed (e.g. embed height)
- [ ] SPA proven current (wwwroot or vite)
- [ ] Side-by-side screenshots captured
- [ ] Owner visual gate (when surface is SSOT-locked)

---

## Related

- Skill procedure: `.agents/skills/html-design-implementation/SKILL.md`
- GUI principles: [game-gui-principles.md](game-gui-principles.md)
- FE foundation: [fe-game-foundation.md](fe-game-foundation.md)
- Design index: [../design/README.md](../design/README.md)
- Composable menu kit (design-only): [gui-lego-ideal.md](gui-lego-ideal.md) ·
  [../design/gui-lego/README.md](../design/gui-lego/README.md)
