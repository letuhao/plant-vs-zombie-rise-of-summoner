# World-map-gaps review fixes — task list

**Program:** `world-map-gaps-followup`  
**Plan:** [world-map-gaps-followup-plan.md](world-map-gaps-followup-plan.md)

**Status:** COMPLETE.

| Id | Task | Status |
|---|---|---|
| F1 | Dom latch + force Dom hit + scratch + drop lane emit | done |
| F2 | Edge-scroll CSS ignoreRects + scaled unit test | done |
| F3 | selectedEntityId on world:interaction for force halo | done |
| F4 | Gate/delete `__fusionRpgWorldGen`; stale comments | done |
| F5 | Fit AABB from sectors + dock-aware pads | done |
| F6 | Measure ignoreRects from HUD testids | done |
| F7 | In-place sync upsert; destroy absent only | done |

## Verify

```powershell
cd web\fusion-rpg-web
npx vitest run src/game/world src/stages/world/host src/stages/world/worldIgnoreRects.test.ts
npm run build
npx playwright test e2e/world-map-runtime.spec.ts e2e/world-stage.spec.ts e2e/checkpoint-f.spec.ts
```
