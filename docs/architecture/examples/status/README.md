# Status overlay examples

Documentation JSON samples for [status-ssot.md](../../status-ssot.md). Grant bodies match `POST /api/debug/effect/grant`. LIVE packs: `POST /api/debug/scenario/{status-l2-*}`.

Numbers in examples are **illustrative**. Core must not hardcode them. Prove boards pin **actor derived profiles** so apply/resist is not ambient PvzStats.

| File | Demonstrates |
|---|---|
| [wither.overlay.json](wither.overlay.json) | Overlay DoT (`statusId: wither`) |
| [bond.overlay.json](bond.overlay.json) | Hit counter (`statusId: bond`) |
| [blight-row.overlay.json](blight-row.overlay.json) | Contagion spread along row |
| [butter.overlay.json](butter.overlay.json) | UnityCc L2 (`statusId: butter`) → FA2 StatusExecutor |

**Actor derived profiles** (`derivedProfile` on spawn / `POST /api/debug/actor-derived`):

| Profile | Role |
|---|---|
| `neutral` | `progression.power/realm = 1` |
| `glass` | same as neutral; no resist |
| `caster` | `status.power.* = 100` |
| `iron-cc` / `iron-dot` / `iron-contagion` | omni resist 1e6 (category channels are capped at 0.95) |
| `immune-poison` | `status.immune.poison = 1` |

`GET /api/debug/actor-derived?ptr=` dumps the snapshot. `POST /api/debug/status/apply` runs StatusRuntime Apply (not Unity `debug.apply-status`).

**Optional overlay keys (status grants):**

| Key | Notes |
|---|---|
| `chance` | L1 proc gate 0–1; default **1.0**. Combined with L2b: `p_final = chance × p_apply`. |
| `statusId` | Required for StatusRuntime path |
| `immunityTags` | With defender `status.immune.{tag}` → Immunity before roll |

Catalog ids (21 total): [status-ssot.md §9](../../status-ssot.md#9-locked-status-catalog-21-named-ids).

Legacy combat examples: [../combat/](../combat/).
