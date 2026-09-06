# Spec: `host-data-lifecycle`

**Module id:** `host-data-lifecycle` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1 cutover gate.  
**Depends on:** `island-host` · **Blocks:** freeze gate / `lawn-plane` “done” claims; base-defense unpause

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) §Locked answers follow-up.  
**POC gap:** scene-switch POC measured paint only — not model revision survival.

---

## Objective

Prove that **production** lawn and world hosts keep data correct across Game destroy/create and
buffer-until-ready — so stage travel does not greenwash on ScenePOC alone.

**Success:** a named test suite fails if foreign generation is applied, if pre-ready model is lost,
or if only the POC harness passes while facades regress.

---

## What already exists (verified)

**Built:** host buffer-until-ready patterns in LawnGameHost / WorldGameHost; generation on bus
payloads; ScenePOC Track B destroy/create timings.

**Gap:** no automated assert that latest revision wins across remount; emit policies
(`lawnHostBuffer` / world projection) not covered as a cutover gate.

---

## Contract

### Cutover gate (binding)

`lawn-plane` and the map **Freeze** must not claim Wave 1 island complete until this module’s
**production-host** suite is green.

ScenePOC / `?dev=phaser-scene-poc` may be **extended** as a harness helper, but:

| Sufficient for cutover? | Path |
|---|---|
| **Yes** | Tests that drive `LawnGameHost` / `WorldGameHost` (or their extracted buffer helpers) |
| **No** | ScenePOC-only timings without facade involvement |

### Required assertions

1. **Buffer until ready:** model emitted before `:ready` is applied once after ready (same generation).
2. **Foreign generation drop:** events with other `generation` never apply.
3. **Track-B remount:** destroy → create new generation; latest model after remount applies; old
   generation handlers do not fire into the new Game.
4. **Latest wins:** if multiple buffered models exist, the newest revision/`modelSeq` is what applies
   after ready (match existing host emit policy — document the chosen rule in the test name).
5. **Active-only (if multi-Scene harness):** sleeping scene does not apply model (optional until a
   multi-Scene production stage exists; required for any ScenePOC multi-Scene extension).

### Suggested paths

```text
web/fusion-rpg-web/src/game-host/hostDataLifecycle.test.ts
# and/or extend:
web/fusion-rpg-web/src/features/lawn/LawnGameHost.test.tsx
web/fusion-rpg-web/src/stages/world/host/WorldGameHost.test.tsx
web/fusion-rpg-web/e2e/phaser-scene-poc.spec.ts  # optional helper only
```

---

## Dual-track

N/A — acceptance of hosts after `island-host` lands. If facade buffer helpers are extracted, 
switch-and-delete applies to those helpers under `island-host` / this suite.

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game-host/hostDataLifecycle.test.ts
npm test -- src/features/lawn/LawnGameHost.test.tsx src/stages/world/host/WorldGameHost.test.tsx
```

---

## Testing strategy

Vitest with mocked `Phaser.Game` / bus. Prefer deterministic fake ready events over real WebGL.
If an e2e helper is added, it records Pass/Fail into research notes — it does **not** replace unit
cutover.

---

## Tunables

None.

---

## Boundaries

- **Always:** production hosts in the suite; foreign generation drop; buffer-until-ready.
- **Ask first:** changing host emit policy semantics (revision vs modelSeq).
- **Never:** mark freeze complete on ScenePOC-only green; client prediction of living set.

---

## Success criteria

1. Suite includes Lawn **and** World production host paths (or shared helper both use).
2. Assertions 1–4 above have failing tests if broken (mutation-style confidence).
3. Map Freeze / `lawn-plane` completion explicitly depends on this suite green.
4. Document in research or test README: ScenePOC alone is insufficient.

---

## Out of module

Implementing `destroyGame`; paint golden; siege scene; Decision 40.
