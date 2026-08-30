# Spec: `aura-surface`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Depends on:** `derived-modifier-bucket`, `aura-content`
**Status:** specced 2026-08-30, not built. Last module.

---

## 1. Objective

Make auras real to the player: which are equipped, which is **active**, what enabling one costs, what
switching one off did — and, for the first time in this codebase, **where a derived number came from**.

Today the Actor sheet's Actions tab is four locked placeholder slots
(`web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx`), every slot `LockedGridSlot` with the reason
*"Unlocks once the action system ships (approved, not yet built)."* This module replaces that with the
real thing for auras.

---

## 2. The two things this surface must get right

### 2.1 Eviction has to be visible

`aura-action-shape` makes eviction a typed outcome; this module is where it reaches a person.

**GG-55: never disable without saying why.** The action layer already refuses with typed reasons, and
`spec-action-layer.md` §4 established the shape: *a short typed label under the button (`Cooldown`,
`No qi`, `Too far`, `Unmet`) plus the full typed reason in an adjacent legend* — because a 62px slot
cannot fit a sentence without bleeding into its neighbour (measured, not assumed).

**"Enabling Might switched off Fortitude" is the same class of information.** It is not an error and
not a toast-and-forget — the player made a choice and needs to see its consequence. Requirements:

- The active aura is **unmistakably distinct** from equipped-but-inactive. Not a subtle tint.
- Enabling at the cap **names the aura that switched off**, in the moment, and the message survives
  long enough to read.
- Upkeep is visible **before** committing — which pool, how much per tick — and a projected
  "will run dry" state, the same shape `spec-action-layer.md` §2 specifies for `perTick` costs.

### 2.2 GG-49 becomes satisfiable for the first time

> *"'Why did my attack drop?' is answerable from the interface… **Forbids:** a stat readout with no
> path to its sources."* (`game-gui-principles.md:644-651`)

Today this holds only **vacuously** — no derived value is shown at all. `derived-modifier-bucket` makes
contributions retainable, so a derived channel can finally show *"+40 from Might aura, +12 from
patron"* instead of one opaque number.

**This is the module where the bucket pays off**, and it is why `aura-surface` depends on it directly
rather than only through `aura-content`.

---

## 3. The contract gap that must be closed honestly

`ActorChannelDetail.contributions` exists in the web contract but **has no server producer**.
`web/fusion-rpg-web/src/contract/adapt.ts:37` is unconditional:

```ts
channelSummary: pendingWithReason("The derived-stat snapshot has no server endpoint yet (spec-derived-stat-sheet.md)")
```

Because it never returns `known`, no `ActorChannelDetail` is ever constructed outside two test helpers.
So this module needs a **real server endpoint** exposing derived channels with their contributions —
and until it exists, the surface must render the honest pending state, never a fabricated grid.

⚠️ **Do not bridge to `pvz_stat_contributions`.** That table is real
(`RpgStore.cs:300-313`, served at `/api/pvz-stats/{playerId}/channels/{channel}`) but it is keyed by
**`player_id` with no actor column**, it is a rebuilt-on-every-mutate cache
(*"Never re-apply from finals"*, `pvz-stats.md:40`), and its row shape is different. The contract header
(`contract/types.ts:4-5`) forbids components binding to REST DTOs directly. **Two different things that
share a word.**

---

## 4. Commands

```powershell
cd web\fusion-rpg-web
npm run test
npm run build
npx playwright test
```

Live review: `/review-web` (the `local-web-review` skill) — never an improvised `npm run dev`.

---

## 5. Project structure

| Path | Change |
|---|---|
| `web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx` | edit — real aura slots replacing locked placeholders |
| `web/fusion-rpg-web/src/ui/actor/AuraSlot.tsx` | **new** — one aura: state, upkeep, enable/disable |
| `web/fusion-rpg-web/src/ui/actor/ChannelContributions.tsx` | **new** — the GG-49 readout |
| `web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx` | edit — contributions when known |
| `web/fusion-rpg-web/src/contract/adapt.ts` | edit — return `known` once the endpoint exists |
| `web/fusion-rpg-web/src/lib/bus/*` | edit — the query + the enable/disable mutation |
| `src/FusionRpg.Server/…` | **new** endpoint — derived channels + contributions for an actor |
| `web/fusion-rpg-web/e2e/aura.spec.ts` | **new** |

---

## 6. Design notes

**Reuse, do not invent.** The Actor sheet ladder (Token/Chip/Row/Card/Panel), `ActorRungState`,
`Pending<T>`, `PendingNote`, `EmptyState`, `LockedGridSlot`, `TabList` all exist and are tested. An
aura slot is a **Card-rung** control inside the existing Actions tab, not a new surface.

**Locked states keep saying what unlocks them** (GG-17). An aura gated by W3/W4/R1 renders locked with
its *real* reason — *"needs the overlay combat calculator"*, not a generic "coming soon". `LockedGridSlot`
already takes a `reason` and renders it as `title`.

**Mutations get toast feedback for free** — `meta.entity` on the mutation and the global
`MutationCache` listener in `app/providers.tsx` produce a band-4 failure toast without per-call-site
wiring (`mutations.ts:18-24`). Use it; do not hand-roll error UI.

**No fabricated data, ever.** Every not-yet-real field renders its honest pending/empty/locked state.
This is the rule the actor-sheet program already enforced and it applies unchanged.

---

## 7. Testing strategy

**Unit (vitest + Testing Library)** — mirroring the existing `ui/actor` tests:

| # | Test | Asserts |
|---|---|---|
| 1 | Active vs equipped-inactive | visually and semantically distinct; `aria` state correct |
| 2 | Enable at cap | the evicted aura is **named in the UI**, not just in the result object |
| 3 | Upkeep before commit | pool and per-tick amount shown pre-enable |
| 4 | Unaffordable | typed refusal naming **which** pool; button disabled with reason (GG-55) |
| 5 | Gated aura | locked with its real reason, never a generic string |
| 6 | Contributions known | each source and magnitude rendered |
| 7 | Contributions pending | `PendingNote` with the real reason; **no fabricated grid** |
| 8 | Non-ready rung states | loading/empty/error still short-circuit before any aura renders |

**E2E (Playwright)** — enable an aura, assert active state; enable a second at cap 1, assert the first
is named as switched off; assert a locked aura states its reason. **Screenshots at desktop and mobile,
actually inspected** — the actor-sheet program's own standard, and the tab bar plus aura slots are
exactly the kind of dense content that breaks at 375px.

> Known trap from that program, worth repeating: Tailwind's `transition-colors` means a screenshot
> taken immediately after a click can catch a mid-transition paint. Wait for `aria-selected` plus a
> short settle before capturing — fix the test, not the product.

---

## 8. Boundaries

**Always**
- Render honest pending/empty/locked states with real reasons.
- Name the evicted aura in the UI.
- Reuse the existing Actor ladder components.
- Bind to `@/contract`, never a REST DTO directly.

**Ask first**
- Any new top-level route or rail entry — GG-1 says menus open over where the player already is.
- Changing the Actor sheet's tab set.

**Never**
- Fabricate a contributions grid.
- Bridge `ActorChannelDetail.contributions` to `pvz_stat_contributions`.
- Show engine vocabulary (`typeId`, `grantId`, channel ids) raw on a player surface.
- Disable a control without saying why.

---

## 9. Success criteria

- [ ] Active vs equipped-inactive is unmistakable.
- [ ] Enabling at the cap names the aura that switched off.
- [ ] Upkeep pool and rate are visible before committing.
- [ ] Every locked aura states its real reason.
- [ ] A derived channel shows its contributions — **GG-49 satisfied non-vacuously for the first time**.
- [ ] Pending states are honest; nothing fabricated.
- [ ] `npm run test`, `npm run build`, `npx playwright test` green; mobile screenshots inspected.

## 10. Open questions

1. **Does the aura control belong in the Actions tab, or does it earn its own tab?** Actions is the
   natural home and the tab set is already six wide. Leaning: Actions, with auras as a distinct group
   above the (still locked) action slots.
2. **How much of the derived-stat sheet does this module build?** `spec-derived-stat-sheet.md` designs a
   full surface with six render states; this module needs only the contributions readout. Recommendation:
   build the narrow readout, leave the full sheet to its own spec, and have `DerivedStatsTab`'s
   existing disabled "Open full sheet" doorway keep saying so.
