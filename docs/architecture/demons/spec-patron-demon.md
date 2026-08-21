# Spec: patron-demon

Status: **approved anchors 2026-08-21** (owner decisions below); implementation not started. Module id `patron-demon` in the [demon system map](../demon-system-map.md). Depends on `demon-summoning` (demons to designate) and `demon-fusion` (stars scale the aura). First demon module with **injector scope** — the build ends at a LIVE gate that needs the owner's eyes, not just SIM.

## Objective

One designated demon — the patron — stands behind the summoner during live PvZ matches: its element becomes a small typed aura on the player's plants, its stars/rarity/level set the strength, and its presence sweetens Soul earns. This is the first bridge from the web RPG back into the game, deliberately small: one grant, one read path, zero new hot-path work.

Success looks like: a player designates their 3★ fire epic as patron in the web FE, starts a PvZ match, and their plants measurably hit harder with fire-typed force; the debug probe shows exactly one patron grant; frame cost is unchanged; and switching patrons costs Souls after the first.

## Locked decisions (owner, 2026-08-21)

1. **Effect form — element aura:** plants gain per-mille combat bonuses on the patron's element channels. No trait echo in this module (recorded extension).
2. **Designation — soul-priced switch:** first designation free; every change costs **100 Souls** (spend reason `patron`, correlation-idempotent). Takes effect next match, never mid-match.
3. **Scaling — stars + rarity + level** (formula below; integer per-mille).
4. **Economy — small earn bonus:** a set patron adds **+1 bonus Soul per 10 kills** in a PvZ match (the integer form of +10%). The audited earn-v2 invariants are untouched: the per-match kill cap (50) and victory decay stay exactly as reviewed — the bonus only reaches the cap sooner.

## Design

### Aura magnitude (PatronPolicy, Core — integer per-mille, spec-locked; tuning ask-first)

```
auraMilli = RarityBaseMilli(rarity) + 10·star + level   (clamped to 150)
RarityBaseMilli: common 20 · rare 30 · epic 45 · legendary 60
```

The aura lands on the patron's **primary element** as `combat.power.{elem}` and half-strength `combat.defense.{elem}` deltas for plant-side reads — the same 56-channel vocabulary every other combat read uses. A secondary element, if present, gets half magnitudes.

### Delivery — one grant, existing paths only

- The patron is a **match-owner effect grant** (template `patron.aura`, grant id `patron:{playerId}`), overlay carrying `{element, powerMilli, defenseMilli}`. It enters through the Secondary plugin Grant path → Funnel, like every other grant; the overlay combat calculator reads the deltas through the existing derived-channel compose. **No new Unity writes, no per-hit work** — the grant is applied once per match at `board.start` and withdrawn at `board.end` by the normal session lifecycle.
- Injector learns the patron via the existing server↔injector plumbing: the server includes the current patron aura in the match-start state the injector already fetches, and pushes `PatronUpdated` on change (applies from the NEXT match — decision 2's match boundary comes free).
- SIM parity: the SIM effect host applies the same grant, so aura math is provable offline; only the Unity read path needs the LIVE gate.

### Data

- `rpg_patron` (player_id PK, instance_id, set_utc, revision). Set/switch is one transaction: replay-check → validate specimen (owned, demon profile, not Retired) → first-set-free else `TrySpendSouls(100, "patron", correlation)` → upsert + revision.
- **The patron is unconsumable** (the designation has teeth, like the lock): fusion refuses the active patron as a sacrifice or recipe input (`sacrifice.is-patron`). Expedition dispatch of the patron is allowed — the aura is a designation, not a deployment.
- Earn hook: the kill-earn path adds the bonus Soul on every 10th counted kill when the killer's player has a patron set — inside the same fact transaction, under the unchanged cap.

### Server + FE

- `GET /api/patron/{playerId}` (current patron + computed aura), `POST /api/patron/set` (instanceId, correlationId; 409 on insufficient Souls). Hub: `PatronUpdated` to web + injector groups.
- FE: "Make patron" action on roster cards (`#/demons`), patron badge with aura preview (element, ‰, next-switch cost); patron shown on `#/fusion` trays as unconsumable.

### Explicitly not in this module

Trait echo (recorded extension), multiple patrons, patron XP from PvZ, aura effects on hypno-ally demons, any change to Soul kill-cap/victory math.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests      # PatronPolicy magnitudes, earn-bonus math
dotnet test tests\FusionRpg.Data.Tests      # set/switch transaction, fusion guard, earn hook
dotnet test tests\FusionRpg.E2E.Tests       # SIM: set → match → aura grant present, bonus souls exact
$env:FUSIONRPG_GAME_DIR = "<game dir>"; .\scripts\deploy-play.ps1 -NoServer   # LIVE gate (owner)
```

## Structure

```
src/FusionRpg.Core/Demons/Patron/   → PatronPolicy.cs (magnitudes + earn bonus math)
src/FusionRpg.Data/Sqlite/          → RpgStore.Patron.cs (+ rpg_patron schema, earn hook, fusion guard)
src/FusionRpg.Server/               → PatronEndpoints.cs (+ match-start state inclusion)
src/FusionRpg.Injector/             → patron grant application via the existing effect runtime (grant-only)
web/fusion-rpg-web/src/             → roster patron action + badge, bus/patron.ts
tests/                              → Core policy, Data transaction/guard, E2E SIM loop
```

## Code style

Injector-side code is grant-only (Secondary discipline — the no-Unity guard must stay green); Core policy pure and integer; store transactions mirror the summon/fusion pattern.

## Testing strategy

- **Policy:** magnitude table goldens; clamp at 150; earn bonus = counted/10 under the cap (cap boundary cases).
- **Data:** first set free, switch spends exactly 100 with correlation replay; refusals write nothing; fusion refuses the patron in both consuming roles; consuming guard survives patron switches.
- **SIM e2e:** set patron → play SIM match → exactly one `patron.aura` grant in the session, kill earns include the bonus, cap still 50; unset player earns baseline.
- **LIVE gate (owner checklist, overlay-spec style):** deploy → designate patron → real match → (1) debug effects view shows the grant, (2) plant damage vs a fixed target measurably shifts by the aura ‰, (3) `board.end` withdraws it, (4) perf probe window shows no new hot-path cost, (5) switching patrons mid-match changes nothing until the next match.

## Boundaries

- **Always:** grant path only; one grant per match; server-authoritative magnitudes; correlation idempotency on set/switch; earn bonus inside the fact transaction.
- **Ask first:** magnitude/cost tuning; trait echo; anything touching the kill cap or victory math beyond the locked bonus.
- **Never:** Unity stat writes outside EntityStatWriter/Funnel; per-hit server calls; a patron effect that outlives its match; consuming the active patron.

## Success criteria

1. Set/switch loop with pricing works in FE + SIM. 2. SIM match shows the aura grant + exact bonus Souls. 3. All suites + guards green (especially secondary-no-unity). 4. **LIVE checklist passed by the owner** — the aura visibly works in a real match with zero perf regression. 5. Fusion cannot eat the patron.
