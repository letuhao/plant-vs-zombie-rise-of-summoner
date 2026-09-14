# Session boundary records

One file per automated session, named `<session>.json`. Each is a
[session boundary](../docs/contributing/session-boundary.md) record: which problem the session owns,
which branch/mode it works in, and — the load-bearing field — the exact `paths` it may edit and
commit.

**One file per session, never a shared registry file**, so two sessions that start at the same time
cannot conflict on the record itself. The directory listing IS the index; add a file, don't edit a
central one.

Read every record here at session start (`/session-start`) and check that no other *active* record
claims the paths or branch you are about to touch. Validate with:

```powershell
.\scripts\session-boundary-check.ps1
```

Template: [`_template.json`](_template.json). Policy:
[docs/contributing/session-boundary.md](../../docs/contributing/session-boundary.md).
