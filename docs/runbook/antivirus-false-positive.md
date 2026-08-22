# Antivirus false positives on FusionRpg.Server.exe

**Status:** investigation, 2026-08-22. Prompted by Bitdefender flagging `FusionRpg.Server.exe` as a trojan.

**Scope, from the owner's own observation — this is the most useful fact in the document:**

> The development folder has **never** been blocked, with no exclusions configured. The server is
> only flagged after a real release is published to GitHub, downloaded, and run.

Local builds are not affected. Any advice below that assumes otherwise is wrong.

## What was checked

| Fact | Value |
|---|---|
| Authenticode signature | **NotSigned** |
| Publish shape | `--self-contained true`, `PublishSingleFile=false`, `PublishTrimmed=false`, `-r win-x64` |
| `apphost` size | ~152 KB (the .NET launcher stub; the real code is in the sibling DLLs) |
| Version info | Company `FusionRpg (hobby OSS)`, product `FusionRpg`, description `FusionRpg Server` |
| Behaviour at runtime | Binds an HTTP listener on `127.0.0.1`, writes SQLite files, is started as a child process by the launcher |

## Why it gets flagged — and only after download

Nothing here is a compromise. Note what the owner's observation rules **out**: the bytes are
identical locally and after download, and the local copy is never touched. So the trigger is not
the file's content, the build configuration, or anything the compiler did. It is how the file
**arrived**:

1. **Mark of the Web.** A file downloaded from a browser carries a zone identifier marking it as
   internet-sourced. AV engines and SmartScreen apply far more aggressive heuristics to those files
   than to ones produced locally by a compiler the user just ran. This alone explains the whole
   local-vs-downloaded split.
2. **No reputation.** Reputation systems weight how many machines worldwide have seen a file. A
   freshly published release has been seen by almost nobody, so it starts at maximum suspicion.
3. **Unsigned.** No Authenticode signature means no publisher identity to inherit trust from, so
   the file cannot climb out of (2) quickly.
4. **Delivery shape.** An unsigned executable inside a zip from a code-hosting site is the classic
   malware delivery pattern, independent of what the executable does.

The remaining factors below matter, but only in combination with the four above:

1. **`apphost.exe` stub.** Every self-contained .NET app ships the same small launcher stub with the app name patched into it. Malware families abuse the same trick, so the byte pattern is over-represented in detection sets.
2. **Behaviour that reads as suspicious in isolation** — an unknown unsigned binary opening a listening socket and being spawned by another unknown unsigned binary.

**Would a Debug build avoid it?** No. Build configuration is not part of the decision — a Debug
build downloaded from GitHub gets flagged the same way, and arguably harder, since Debug binaries
are rarer in the wild and therefore have even less prevalence. Changing configuration treats
nothing that is actually causing this.

## What can actually be done

**Effective, free:**

- **Report the false positive to Bitdefender.** This is the real remedy and it is free: <https://www.bitdefender.com/submit> (submit the sample as a suspected false positive). Turnaround is typically days, and it fixes it for every user of that engine, not just you.
- **Tell players how to clear Mark of the Web** on the downloaded zip — right-click the **zip**
  → Properties → Unblock, *before* extracting. Doing it on the zip clears it for everything inside;
  doing it after extraction means unblocking each file. This is the cheapest fix that targets the
  actual cause, and it needs no exclusions and no elevation.
- **Add a folder exclusion in Bitdefender** for the install directory if the above is not enough.
  The launcher's "Prepare Windows Security" button is **Defender-only** and does nothing for
  Bitdefender — that has to be done in the Bitdefender UI by hand.
- **Ship checksums** with releases so anyone can verify a download matches what was built.

**Effective, costs something:**

- **Code signing.** This removes the root cause. A commercial OV certificate is a recurring cost; for an open-source project, [SignPath Foundation](https://signpath.io/terms) offers free certificates and CI signing to qualifying OSS projects, which is worth investigating before paying for one. Not done — it needs a project decision, not a code change.

**Considered and rejected:**

| Option | Why not |
|---|---|
| Framework-dependent publish (no self-contained apphost) | Would reduce the flag, but forces every player to install the .NET runtime first. The whole player-pack design is "no SDK, no runtime, no Node" |
| `PublishSingleFile=true` | Makes it *worse* — single-file bundles are flagged more often, not less |
| Obfuscation or packing to dodge signatures | Makes it look more like malware, not less. Never do this |
| Renaming the exe | Cosmetic; reputation follows the hash, not the name |
| Shipping Debug instead of Release | Build configuration is not part of the detection; see above |

## What is already in the product

The launcher does not pretend it can whitelist third-party AV:

- First-run trust dialog states plainly that builds are unsigned and may be quarantined (`AntivirusGuard.ConsentMessage`).
- If the server exe goes missing or fails to start, the launcher shows quarantine-recovery guidance naming the folder and the expected path (`AntivirusGuard.QuarantineHelpMessage`).
- "Prepare Windows Security" adds a Defender exclusion via one elevated `powershell.exe` — **the launcher itself never runs elevated**. As of 2026-08-22 this is no longer offered during first run, because chaining it onto startup made the launcher look like it required administrator rights, and it is useless to anyone not on Defender.

## Recommendation

In order of cost:

1. **Document the zip Unblock step in PLAYERS.txt.** Free, targets the actual trigger, and would
   likely have prevented the report that started this.
2. **Submit the released binary to Bitdefender** as a false positive. Free, and fixes it for every
   user of that engine rather than one machine.
3. **Ship checksums** with the release.
4. **Code signing** if this is ever distributed beyond a handful of people — check SignPath
   Foundation first, since a paid certificate is hard to justify for a hobby overlay.

Do not chase this in the build configuration. Nothing about Debug/Release, file naming, or publish
flags changes a decision that is being made on provenance.
