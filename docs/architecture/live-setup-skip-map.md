# Capability map: live-setup-skip

**Status:** Implemented; `quick` LIVE-proven on `pvzrh-3.9` with MelonLoader 2026-09-09. This is a
developer-only live-test initiative. It does not change the player-facing lawn program.

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `setup-skip` | Invoke the host game's existing plant-selection completion path from the injector, expose a server command, and prove the result through an event acknowledgement | existing injector command drain, existing debug endpoint | [spec-setup-skip.md](live-setup-skip/spec-setup-skip.md) |

## Build order

`setup-skip` injector command and event contract → server endpoint and tests → host build → controlled LIVE proof.

## Explicit scope boundary

This map covers only the setup panel used by live testing. It does not authorize general menu
automation, player-facing UI changes, native memory patching, or edits to the PVZ game binary.
