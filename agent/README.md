# agent/

Committed notes so that work can be picked up between sessions without re-deriving what is
already known. Aimed at AI agents, useful to anyone.

## Layout

| folder | holds |
|---|---|
| `research/` | API inventories, file format notes, findings about other projects |
| `plans/` | design documents for current and upcoming work |
| `decisions/` | what was decided, why, and what was rejected |
| `worklog/` | `YYYY-MM-DD.md` per session: what happened, what broke, where it stopped |

`decisions/` is append-only. Reversing a decision means adding a new one that supersedes it, and
saying so in both. Research notes are corrected in place when they turn out to be wrong, rather
than accumulating contradictions.

## Accuracy

These notes record what was true when written, against a framework that moves. Verify before
relying on anything specific, and fix what you find stale.

Numbers measured against a particular avatar are calibration, not guarantees. Say which avatar,
or at least say that a number came from one sample.

## What does not belong here

- **Personal information.** Contributors commit under their own name and address, and nothing
  else needs identifying. Do not record who did what, machine-specific absolute paths, or
  anything about a contributor's setup that is not needed to reproduce a result. Write paths as
  `/path/to/...` or relative to the repository. The same holds for anyone who reports a bug:
  no handle, no avatar name, no file name, no store, no screenshot text. Record the shape of
  the report, never the report.
- **Third party assets**, in any form, including inside a `.unitypackage`: no VRChat SDK, no
  purchased avatars or plugins. Script GUIDs, fileIDs and field names are facts about a file
  format and are fine to record; the files are not ours to redistribute.
- **Secrets.** No tokens, credentials, asset bundle passwords or server configuration.

Test fixtures are hand-authored minimal YAML in the package's test folder. Findings from real
avatars are recorded here as prose and numbers, never as committed assets.
