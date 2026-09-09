# gui-lego-condition — todo

**Plan:** [gui-lego-condition-plan.md](gui-lego-condition-plan.md)

- [x] Align queue/spec docs for Condition glance and shell identity ownership
- [x] Extend `/api/actors/{id}/sheet` for Standing + ResourcePools (+ identity/progression)
- [x] Author Condition piece HTML drafts and `condition-console` recipe (2×2 grid)
- [x] Add assembled `docs/design/gui-lego/surfaces/condition-console.html` as CSS grid
- [x] Implement fold/bus + piece registry + thin `ConditionTab` (consume standing/pools)
- [x] Slim `ActorSummarize` — name / Lv · role only (species on Condition)
- [x] Audit fixes — pending honesty, sheet retry, empty vs unwired statuses
- [x] Amend shell/condition/surface-vm + menu queue for rail ownership + grid + Standing
- [x] Unit + server tests green; `deploy-play.ps1 -NoServer -NoGame`
- [x] UniqueActor Hub `ResourceBaselineSubsystem` seeds `resource.max/regen` (SSOT)
- [x] Live curl `GET /sheet` proves six pools + standing + `rpg.resource.base` contributions
- [x] FE kit radial + plate meter chrome (no icon list / no pending under filled pools)
- [x] Live SPA Condition shot with real `/sheet` numbers (`e2e/artifacts/condition-fidelity/`)
