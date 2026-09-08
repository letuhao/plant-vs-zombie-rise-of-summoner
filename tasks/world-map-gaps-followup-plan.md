# World-map-gaps review fixes — plan

**Program:** `world-map-gaps-followup`  
**Status:** COMPLETE  
**Closes:** Important + Suggestion findings from world-map-gaps code review (CPG-D follow-up).  
**Tasks:** [world-map-gaps-followup-todo.md](world-map-gaps-followup-todo.md)

See Cursor plan `gaps_review_fixes` for full task text F1–F7. Do not use bare `tasks/plan.md`.

**Verify:**

```powershell
cd web\fusion-rpg-web
npx vitest run src/game/world src/stages/world/host src/stages/world/worldIgnoreRects.test.ts
npm run build
npx playwright test e2e/world-map-runtime.spec.ts e2e/world-stage.spec.ts e2e/checkpoint-f.spec.ts
```
