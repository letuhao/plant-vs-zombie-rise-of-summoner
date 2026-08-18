# External patterns (web) — transfer vs non-transfer

Notes from industry client/server authority patterns used as **analogy** for FusionRpg overlay stress.  
**Not** an instruction to move EffectBag to the Server.

## Sources consulted

| Source | Idea |
|---|---|
| [Gabriel Gambetta — Client-Server Game Architecture](https://www.gabrielgambetta.com/client-server-game-architecture.html) | Authoritative server; clients send **inputs/intent**, not trusted results; client is privileged spectator for sim |
| [AccelByte — Server vs Client Authority](https://accelbyte.io/blog/server-authoritative-vs-client-authoritative-architecture) | If a player can gain by lying, that thing runs on the server; match end writes progression from server; prediction + reconcile for feel |
| Roblox / PurrNet-style prediction docs | Local predict → server verify → rollback/replay; presentation vs simulation split |
| Dual-layer / command-pattern sims (e.g. Archon-style docs) | Commands for all state changes; hot sim vs cold presentation; determinism for multiplayer |

## FusionRpg mapping (locked analogy)

| Multiplayer concept | FusionRpg analogue | Transfer? |
|---|---|---|
| Dedicated game sim server | **Unity engine** (closed physics) | N/A — we do not own sim |
| Client prediction | Injector **Hot** EffectBag apply on capture | **Yes** — overlay procs must feel instant |
| Server authority for combat truth | Unity damage/HP resolution | **Yes** — do not shadow HP mid-frame |
| Server authority for economy/progression | **FusionRpg.Data** UniqueActor / RpgProgression | **Yes** — Cold loop |
| Client sends intent (deploy, fire) | **PvzIntent** / unique deploy | **Yes** |
| Client sends results (“I hit for 9999”) | Ban for progression; capture is observation | **Partial** — local single-player trust |
| Lag compensation / rewind hitboxes | Not applicable (single local Unity) | **No** |
| Rollback netcode for procs | No second sim to reconcile against | **No** — use fail-closed skip |
| “Procs must be server RNG” (anti-cheat) | Conflicts with Hot lock; relevant for **multiplayer competitive** | **No** for v1 local overlay |

## What transfers cleanly

1. **Intent vs result** — FE/Server enqueue deploy/spawn Intent; do not accept “specimen already Bound” from FE without Server phase.  
2. **Authority for what can be lied about** — XP, gear ownership, Storage purge → Data/Server. Lawn freeze on this machine is not a ranked PvP claim.  
3. **Hot/cold separation** — combat feel (Hot) vs durable truth (Cold) matches prediction vs backend write.  
4. **Command pattern** — PvzIntent / FA* sink already mirrors “all mutations through commands.”

## What does **not** transfer

1. **Putting on-hit proc RNG on FusionRpg.Server** — would recreate the laggy loop we banned in [overlay-control-loops.md](../../architecture/overlay-control-loops.md). Multiplayer servers own RNG because they own the sim; we do not.  
2. **Full reconcile of Unity HP after Hot heal** — no authoritative overlay HP timeline; LimHealth may fight Writer (**Unknown** LIVE). Fail-closed + research, not rollback netcode.  
3. **Treating Injector as untrusted client** — in this product the injector is our code inside the game process; threat model is local save integrity + bugs, not remote aimbot.  
4. **Dedicated server tick for lawn** — MatchRuntime is RAM projection, not a second Board.

## Stress implications (fed into matrix)

| Pattern pressure | Situation IDs | Outcome |
|---|---|---|
| “Server must roll procs” | S-SRV-PROC-RNG | Reject requirement → Hold; accept requirement → **Break** Hot lock |
| Prediction without reconcile | S-PREDICT-HEAL, S-HIT-PROC-HEAL | Bend/Unknown; fail-closed |
| Don’t trust client results | S-TRUST-KILL-XP, S-CMD-NOT-RESULT | Bend/Hold with Activity dedupe |
| Backend owns end-of-match write | UniqueActor Recovering, type XP | Hold Cold |

## Workshop prompt

When brainstorming enhancements, ask: *Are we trying to become a multiplayer authority server, or stay a local overlay with durable RPG backend?*  
Current locks assume the latter. Changing that is a product ADR, not a silent matrix fix.
