# Spec: `paths-tab`

**Module id:** `paths-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Catalog lists **one** kind: **`paths`**. It wraps / mounts the shipped `PassivesTab` body —
   Passives is not a separate catalog kind.
2. Paths glance is species bloodline vs shared corpus — node track, not a paragraph wall.
3. Full tree may **push once** (GG-10 last push) via tree-surface / PassivesTab. Graph UI uses
   **`@xyflow/react`** (read-only: no drag/connect) — buy before build
   ([tech-stack.md](../../design/tech-stack.md) §3.3). World-map `#/world` stays Phaser (T3), not xyflow.
4. Fat chunk ⇒ split; do not refuse xyflow for GG-38 anxiety.

---

## Objective

Show path identity at a glance and afford opening the real tree surface once.

**Success:** No paragraph wall; glance + one push max; kind id is `paths` only.

---

## Tech Stack / Commands / Structure

Reuse `PassivesTab` / passive-tree FE. Glance may stay compact CSS pips; pushed tree uses
`@xyflow/react`.

```text
web/.../ui/actor/PathsTab.tsx
npm test -- --run src/ui/actor/PathsTab
```

---

## Design

- Two columns or segs: species bloodline | shared corpus.
- Compact node pips (owned / available / locked).
- “Open tree” control pushes existing tree surface once (xyflow read-only); Esc pops back to sheet.

---

## Tunables

Passive-tree numbers stay in `passive-tree.v1.json` etc. Paths tab adds no new balance keys.

---

## Testing / Boundaries / Success

- Unit: glance renders; push increments layer stack by one; catalog kind is `paths`.
- Always: reuse tree-surface; xyflow for the pushed graph; one kind only.
- Never: separate `passives` catalog kind; embedding full lattice as unbounded page scroll inside
  sheet body without GG-61 shell scroll; xyflow on `#/world`.
- Success: Plate Paths pane; no regression to PassivesTab allocate/unlock behaviour.

---

## Open Questions

None — Paths is the kind; PassivesTab mounts underneath.
