# Capability map: actor-sheet

Source: [docs/design/08-actor-sheet.html](../design/08-actor-sheet.html) (draft plate, reviewed
2026-08-29 — reference sweep against Diablo IV / Path of Exile / Baldur's Gate 3 character-menu
conventions, then grounded against this repo's own current code and locked design docs before
drawing anything). **Status: proposed, pending owner approval.**

## What this program is

One centralized Actor Panel — six tabs — replacing today's scattered surfaces: Primary Stats lives
in its own standalone rail layer, derived stats has a locked spec with zero render path, actions and
passives have no menu outside live battle, and the existing Panel's own Overview tab is real while
its other three (Effects/Gear/History) are undrawn button stubs.

**Promote is explicitly out of scope** (owner: "ignore it for now, we will come back later if I have
an idea") — nothing in this program should reference it, block on it, or leave a placeholder for it.

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `actor-sheet-shell` | The six-tab container itself. Wires Overview as the landing tab, keeping today's real `ActorPanel.tsx` content exactly as-is behind the new tab bar: the identity header (real), and the four `PendingNote` placeholders for Standing/Element typing/Shield/Equipment (all unconditionally pending today — no server endpoint, confirmed by reading `adaptActor`). **Correction from this map's own first draft**: the resource meters and five-axis "Standing" power vector shown in `00-foundation.html`'s mockup were never built as real React code — only the identity header and the pending-note stubs exist in `ActorPanel.tsx` today. This module does not invent meters/vectors that have no backing data; it only adds the tab bar around what's real. Also wires the two dead footer buttons (`Release`/`Deploy` — currently no `onClick` at all) to at least the panel-close action if nothing more specific is decided. | — (foundational, first) |
| `progression-tab` | Per-actor level/XP (typed on `ActorView` today, never rendered — `xp`/`xpToNext` sit unused) shown as a real progress readout, plus primary-stat (aptitude) distribution embedded here, fronting the *same* `AptitudeEndpoints`/save flow the standalone Primary Stats rail layer already uses — not a second allocation system. | `actor-sheet-shell` |
| `derived-stats-tab` | A small, new key-value grid (`statgrid` is a plate-only CSS class today, not existing React — this module writes a first, minimal React equivalent, not a reuse) showing a handful of headline channels, plus a button that opens the *already fully specified* `spec-derived-stat-sheet.md` panel. **Does not build that full sheet** — this module's own boundary is the summary + doorway only; the sheet behind the door is that spec's own scope, unchanged. | `actor-sheet-shell` |
| `locked-preview-tabs` | Actions and Passives, combined — both are static, locked-grid previews sharing one visual pattern (`.actionslot` reused verbatim from the live battle bar; `.passnode`, new but trivial) and one acceptance shape ("shows what exists, locked, states the real reason why"). Neither has a backing system to wire — the action program is approved-but-unbuilt, passives are the owner's own explicitly deferred sub-feature. Splitting these into two modules would be ceremony over two near-identical static components. | `actor-sheet-shell` |
| `gear-tab` | An honest empty state — `equipSlots` is typed and already unconditionally "pending" (no server endpoint yet, same reason as derived stats). Not a lock (nothing gates it — there's just nothing to equip yet), so it gets `EmptyState`, not the locked-grid treatment. | `actor-sheet-shell` |

## Build order

`actor-sheet-shell` first — it's the container every other tab mounts into, and it's also the module
with the least new work (reusing existing Overview content almost verbatim). After that,
`progression-tab`, `derived-stats-tab`, `locked-preview-tabs`, and `gear-tab` are independent of each
other and can build in any order.

## Explicitly not in this program

- **Building real resource meters or a "Standing" power-vector for Overview.** `00-foundation.html`'s
  mockup shows both, but neither was ever built as real React code (`ActorPanel.tsx` confirmed —
  identity header plus four unconditionally-pending placeholders, nothing else) and `ActorView` has
  no HP/stamina fields to bind a meter to regardless. `actor-sheet-shell` wraps what's real in a tab
  bar; it does not close this older, separate gap.
- **Promote** — no definition exists; out of scope per the owner's own instruction.
- **The full derived-stat sheet** (the six-state, 12-column grid behind `derived-stats-tab`'s doorway
  button) — that's `spec-derived-stat-sheet.md`'s own scope, a separate, already-locked design. This
  program does not re-spec or rebuild it.
- **The action system itself** (live resolution, costs, cooldowns) and **a passive-skill system** (any
  node-graph, any effect resolution) — both are `locked-preview-tabs`' whole point: preview what's
  coming, build none of the system behind it.
- **The other three aptitude-allocation scopes** (demon-type / aspect / unique-demon) — commander
  scope only, matching what's already built; the other three need "a specimen-picker design fork
  nothing has decided yet" (`spec-aptitude-allocation-surface.md` §1), unchanged by this program.
- **Whether the standalone "Primary Stats" rail entry retires** once `progression-tab` ships, or stays
  as a shortcut — an open question from the plate's own §H, owner call, not resolved here.
