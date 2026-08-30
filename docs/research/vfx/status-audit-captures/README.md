# Status audit captures

Optional screenshot/clips for the [status identity audit](status-identity-audit-2026-08-30.md).

## Capture protocol

For each of the 13 custom statuses, save three files:

| File | Timing | Content |
|---|---|---|
| `{statusId}-apply.png` | T+0.2s after apply | Apply burst + flash peak |
| `{statusId}-sustain.png` | T+2s | Sustained aura/tint/marker |
| `{statusId}-stack.png` | T+2s | Two statuses on one host (stress) |

## How to apply

```powershell
.\scripts\setup-lab-run.ps1
.\scripts\audit-status-vfx-identity.ps1 -Live -Stress -TargetPtr <ZombiePtr>
```

Or per status:

```http
POST /api/debug/status/apply
{ "status": "wither", "ptr": "<TargetPtr>", "duration": 8, "level": 1 }
```

Clear between captures: `POST /api/debug/clear-status` `{ "ptr": "<TargetPtr>" }`.

## LIVE status (2026-08-30)

Automated audit ran **static-only** — server was not running on the audit machine (`127.0.0.1:5088` refused). Re-run with game + server up to populate this folder.
