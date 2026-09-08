# AGENTS.md

Orientation for AI agents and for anyone picking this repo up cold.

## What this is

Editor tooling for bringing avatars and the content worn on them into Basis. It installs into a
Basis project as a UPM package.

Working today: VRChat PhysBones and colliders, VRM spring bones in both formats, legacy Dynamic
Bone, all six VRChat constraint types, the avatar descriptor and head chop, menu toggles,
selectors and radial puppets rebuilt as HVR Vixxy controls, animation that plays on its own
rebuilt as authored motion, and a check of the humanoid rig against what Basis's IK needs. A
conversion can be narrowed to some of those, or to individual items, from the window.
`ProductInfo.CheckedAgainst` names the source releases the readers were checked against; the
procedure for checking a new one is in `agent/research/vrchat-serialized-formats.md`.

## Picking this up cold

Read `agent/worklog/` newest entry first; it ends with where the last session stopped and what is
next. Then `agent/decisions/`. The short version of how this works:

- **Source data is read from YAML**, because the VRChat SDK cannot be installed into a Basis
  project and its components arrive as missing scripts. Native Unity types, animator controllers
  and clips, are read through the editor API instead.
- **Three stages**: readers produce plain data, mappers are pure and produce plans, writers touch
  Unity objects. Only writers need a scene, so the rest stays testable headlessly.
- **Anything approximated or dropped produces a diagnostic** with a stable code. Roughly a third
  of the source surface has no Basis equivalent, so silence would misrepresent the result of a
  conversion.

## Lessons this project keeps relearning

- **Read the code, not the docs.** Basis's documentation has been wrong three times: jiggle
  colliders are not sphere-only, chain splitting is unnecessary, and the twist bone lookup lives
  somewhere other than the type named.
- **Decompile rather than infer.** The animation layer ordering quoted everywhere omits a
  deprecated entry and shifts every layer after it. One `ilspycmd` call settled it after the
  wrong version had already shipped in a report.
- **Read the output, not just the test results.** A wide angle limit clamping to a tighter one,
  and duplicate collider diagnostics, were both found by reading a generated report while every
  test passed.
- **Ask what else writes the property.** A converted VRM's Expression selector was right in every
  field and did nothing, because UniVRM's `Vrm10Instance` was still on the avatar rewriting every
  expression blendshape each frame. A plan or component that looks correct is not verified until
  the other runtime writers of the same property are known: the source's own runtime, Basis's
  drivers, anything with an Update.
- **Fix the class, not the instance.** `EditorGUILayout.LabelField` sizes its rect to one line
  whatever style it is handed. Told about four clipped fields, one session fixed those four;
  the other ten came back as a bug report. When a fault is a misused API, grep for every call.
  It happened a third time through the fix itself: `WrappedLabel` was handed a style with
  `wordWrap` off and let it through. Every label in the window goes through `WrappedLabel`,
  and that helper forces word wrap on any style it is given. Never hand `GUILayout.Label` a
  built-in style for text longer than a word or two.

The scope is wider than what is built. Avatars first, then props and worlds; VRChat first, then
whatever else is worth reading, VRM so far. Reading, mapping and writing are separate
stages so that a new source is a new reader and nothing else has to move. Do not narrow the
vocabulary in code or docs to "VRChat avatars", or to social VR platforms: a VRM file is a
format rather than a platform, an avatar carrying nothing but Dynamic Bone belongs to neither,
and all of them are in scope. What decides whether something converts is the components it
carries, not where it came from.

## Read this first

`agent/` is the committed knowledge base:

1. `agent/README.md`, how the folder works
2. `agent/decisions/`, what was decided and why, including what was rejected
3. `agent/worklog/`, newest entry, for where the last session stopped
4. `agent/research/`, API inventories and file format notes
5. `agent/plans/`, the current design

Check `decisions/` before arguing for an approach; it may already have been considered.

Research notes record what was true when written. Verify anything that names a file, field or
flag before relying on it. Basis develops on a `developer` branch, all of its packages are
version `0.0.1`, and several fields this package touches are private or internal and reached
through `SerializedObject`.

## Environment

Setup, test commands and style are in `CONTRIBUTING.md`. Nothing here assumes a particular
checkout location, editor version or user; take the editor version from the Basis project's
`ProjectSettings/ProjectVersion.txt` rather than any version written in prose.

Close the Unity editor before any git operation on the Basis clone, and check that it is
actually closed. Switching branches under a running editor can corrupt its Library, which is
tens of gigabytes and slow to rebuild.

## Constraints that are easy to violate

- **Editor-only.** Basis validates loaded avatars against an allow-list of component types, and
  nothing of ours is on it. Persistent state goes on a GameObject tagged `EditorOnly`, which the
  Basis build pipeline strips. See `agent/decisions/0004`.
- **Report, do not omit.** Anything the converter approximates or cannot carry over produces a
  diagnostic with a stable code.
- **Keep the layers apart.** Readers take text, mappers take plain data, only writers touch
  Unity objects. This is what keeps the bulk of the code testable without an editor.
- **Menus belong to us, not to Basis.** `Tools/<ProductName>/...`, never under Basis's own menu.
  See `agent/decisions/0002`.

## Versions checked

`docs/docs/versions.md` and `ProductInfo.CheckedAgainst` state which release of each source the
readers were checked against. Update them in the same change as the check, every time:

- **The Basis clone is synced to a newer upstream commit** and the suite passes against it:
  put that commit's id, linked to the upstream commit page, and the date in the Basis row.
- **A source package is upgraded** and the suite passes against it: VRChat SDK, UniVRM, Dynamic
  Bone, Modular Avatar, NDMF, or any source added later. Raise its row and `CheckedAgainst`, and
  the changelog entry names the new version.

A green run against the new release is what raises a row; an upgrade that was not tested does
not. Where a release adds components, the procedure in
`agent/research/vrchat-serialized-formats.md` comes first.

## Someone else's UI is not guessable

Steps through another tool's interface are facts to be checked, not prose to be written from a
sense of how such tools work. The ALCOM instructions were VCC's workflow with ALCOM's name on it,
invented rather than read, and stood in two repositories until a reader hit them.

- **Read the tool, not a tool like it.** UI strings live in the source: ALCOM's are
  `vrc-get-gui/locales/en.json5` in `vrc-get/vrc-get`. A CLI's are its own `--help` or README.
  Quote those.
- **A route is not a label.** ALCOM's sidebar entry for `/packages/repositories` reads
  **Resources**; `packages` appears nowhere in the interface. Read the component that renders the
  link, `components/SideBar.tsx`, not the folder it points at.
- **Vendor documentation is second best, and version specific.** Unity renamed "Add package from
  git URL" to "Install package from git URL"; the page for the version in
  `ProjectSettings/ProjectVersion.txt` is the one that counts.
- **Our own menus come from the code.** `ProductInfo.ToolsMenu` and the `MenuItem` attributes,
  not memory.
- **If it cannot be checked, do not write the steps.** Name the screen and stop, or leave it out.
  A vague instruction that is right beats a precise one that is invented.

This is "read the code, not the docs" under Lessons, applied to interfaces.

## Write short

Nobody reads a wall of text. This applies hardest to the changelog and release notes, and to
commit messages, docs, diagnostics and code comments after that.

- **One line per changelog entry.** Say what changed. A second line only if the entry names a
  diagnostic code or a caveat a user would hit. Never a paragraph.
- **Cut the reasoning.** Why a thing was done belongs in `agent/decisions/` or the worklog,
  not in the changelog. A user reading release notes wants the list.
- **No throat-clearing.** Drop "which is what", "it follows that", "rather than", "the same
  point X calls Y", restated context, and sentences that only lead into the next one.
- **Name things directly.** "Converting twice stacked a second control" beats "the rule that
  protects hand-made components elsewhere said nothing there".
- **No filler about the reader.** "in the order most people will want them", "you will probably",
  "simply", "just". Say what the thing is. Four install methods are "Four ways to install it."
- **No rhetorical flourish.** "It does not stay out of the way", "Nothing is lost by leaving it
  out", "is not a harmless extra". Aphorisms and reversals read as padding. State the fact.
- **One sentence per point.** Do not restate a point in different words, and do not add a
  sentence whose only job is to introduce or soften the next one.

Docs pages may be longer than a changelog entry, but the same rule holds inside a paragraph:
state it once and move on. This applies to what an agent writes back in chat too, not only to
what it commits.

Editor UI text is the strictest case. Headings are nouns: Targets, Diagnostics, Warnings,
Dropped, Approximated, Mapped, Rig, Tuning. Status lines are counts. A paragraph appears only
when it tells the user something they cannot see: a setting that acts from a hidden section, an
asset an undo will not remove. Nothing that explains what a button obviously does, nothing that
reassures, no "you can", no "simply", no "check by eye". "Convert writes components you can
tune by hand. One undo reverts all of it" was shipped and had to be removed.

## Public project, not a local notebook

This is an open source package other people install. Nothing that applies only to one
contributor's machine, clone or upgrade path goes into docs, code, comments, changelog, tests or
fixtures.

- **No local deltas.** "1.3.4 differs from 1.3.2 only in when it disables its own
  multithreading" describes one clone's upgrade and shipped in `versions.md`. Docs state what a
  source release adds and what becomes of it on Basis. Which version a contributor upgraded from
  belongs in the worklog, if anywhere.
- **No local paths, clone layouts, remotes, branch names or account details.** Write
  `/path/to/...`. Assume no tool beyond what `CONTRIBUTING.md` names.
- **Facts about a release are checked against its release notes or source**, not against what
  happened to be installed before. See "Someone else's UI is not guessable".

## Professional prose

Every public artifact reads as technical documentation: docs, READMEs, changelog, release notes,
editor UI, report messages, commit messages. No filler, no chat, no reassurance, no commentary on
the reader or on the project's own choices, no rhetorical reversals, no colloquialisms. "Write
short" is the mechanics; this is the bar it serves. Shipped and removed: "Check by eye." in
three report messages, "without one it simply does not apply twist there, which is not a fault",
"a control that looks finished and does half the job is worse than none", "and both are yours to
judge".

## Trademark

Basis, BasisVR and Basis Framework are trademarks of the Basis Project. Their policy permits
descriptive reference and asks third parties not to imply affiliation or endorsement. The product
is named Watari for that reason, with the Basis reference kept descriptive: the display name is
`Watari (Converter for Basis)` and menus are `Tools/Watari/...`. See `agent/decisions/0002`.

The package id and the namespaces still read `com.yuna0x0.basis.convert` and
`yuna0x0.Basis.Convert`, where `basis` is a scope segment rather than a product name. They did
not change with the rename, because an id change means a second OpenUPM entry and orphans what
was published.
