# Contributing

## Setting up

You need a Basis project to build against: the package references the Basis SDK, Jiggle Physics
and HVR Basis Comms, all of which ship inside the Basis repository. Clone
[BasisVR/Basis](https://github.com/BasisVR/Basis) and open the `Basis/` subfolder as the Unity
project, not the repository root. Use the editor version in that
project's `ProjectSettings/ProjectVersion.txt`; Basis moves between editor versions and the
package follows it rather than pinning one.

Develop this repository separately and link the package into that project, so the two stay
independent:

```sh
ln -s /path/to/watari-basis/Packages/com.yuna0x0.basis.convert \
      /path/to/Basis/Basis/Packages/com.yuna0x0.basis.convert
```

Add that path to the Basis clone's `.git/info/exclude`, not its `.gitignore`, so nothing of
yours lands in a Basis commit. Work on a branch there and never commit to it.

## Tests

Run the EditMode tests from the Test Runner window, or headlessly with the
[Unity CLI](https://docs.unity.com/en-us/unity-cli):

```sh
unity test /path/to/Basis/Basis --mode EditMode --filter "yuna0x0.Basis.Convert*"
```

A passing run is not by itself proof that the code compiled. When a script in the package fails
to compile, the editor keeps the last assemblies that did, and the run reports those tests as
passing. Check the project's `Logs/Editor.log` for `error CS` before believing a pass, and delete
the results file first so a stale one cannot be read as a fresh one.

After syncing the Basis clone to a newer upstream commit, or upgrading a source package (VRChat
SDK, UniVRM, Dynamic Bone, Modular Avatar, NDMF), and running the suite green against it, update
the table in `docs/docs/versions.md`: the upstream commit id and date for Basis, the version for
a source package, and `ProductInfo.CheckedAgainst` for a source package too.

Most of the code is deliberately free of scene and AssetDatabase access so it can be tested
without an editor open. Keep it that way: readers take text, mappers take plain data, and only
the writers touch Unity objects.

Fixtures live in `Tests/Editor/Fixtures`. `SampleAvatar` is an avatar the package ships: a
prefab carrying a descriptor, a head chop, a PhysBone, a constraint, a raycast and a
per-platform override as the missing scripts they arrive as, plus an expression menu,
parameters, an animator and its clips. Between them the menu and the animator cover a plain
toggle, a selector sharing one parameter, a radial puppet, a toggle guarded by one of VRChat's
own parameters, a toggle whose clip animates over time, and a layer with nothing steering it.
`SampleClothing` is the Modular Avatar half. `SampleVrmAvatar` holds one avatar per VRM format,
each with a humanoid rig, a face with the blendshapes its expressions bind to, and its own
expressions, licence, eye offset and spring bones. Their `Avatar` and mesh assets are generated,
since a rig Unity validates and blendshape frames cannot be hand-written. Prefer extending these
over reaching for a real avatar, so the suite runs on a machine that has no purchased assets.
The animator half, and the VRM fixtures' rig and face, are generated through
`Tools/Watari/Development/Regenerate Test Fixtures`, because hand-writing a state machine
produces files that look right and do not load. The generated assets are committed; tests do not
run the generator.

A conversion writes one asset, the baked motion clip, beside the animation it came from. A test
that applies a plan must redirect that: set `OutputFolder` on each planned motion to a folder
under `Assets` and delete it in teardown, or the run leaves files in the package.

Some tests need a real VRChat avatar imported into the Basis project. Those assets cannot be
distributed, so the tests skip themselves when the fixture is absent rather than failing. If you
are working on the readers, point the fixture path at an avatar you own.

## Style

Match the surrounding code, which follows the Basis codebase: four space indent, Allman braces,
PascalCase public members, `_camelCase` private fields, plain public fields over properties.
Log through `BasisDebug` with a tag rather than `Debug.Log`.

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/), as Basis
does. Describe what changed; skip tool or process detail.

## Things worth knowing before you change something

- **Everything this package ships is editor-only.** Basis validates avatars against an
  allow-list of component types at load, and ours are not on it. Persistent state belongs on a
  GameObject tagged `EditorOnly`, which the Basis build pipeline strips.
- **Conversion is destructive and undoable, on purpose.** Some of the mapping is a judgement
  call, so the output has to be editable and has to survive rebuilds. See
  `agent/decisions/0003`. The one exception is the baked motion clip, which is a project asset
  and stays after an undo; see `agent/decisions/0013`.
- **Anything the converter cannot carry over must produce a diagnostic**, not a silent omission.
- Do not commit third party assets: no VRChat SDK, no purchased avatars or plugins, in any form.
  Script GUIDs and field names are facts about a file format and are fine to record.

## Reporting a conversion that came out wrong

Include the source component's settings, what the converted result looked like, and what you
expected. If the source avatar is not yours to share, the settings alone are usually enough.

## Releasing

Releases are immutable: publishing locks the assets and the tag for good. The workflow attaches
everything to a draft and publishes it as a separate step. Keep that shape for prereleases too.

## Documentation

The documentation site is a Docusaurus project in `docs/`, run with pnpm:

```sh
cd docs
pnpm install
pnpm start
```

Pages carry `IMAGE PLACEHOLDER` comments where a screenshot belongs, and
`docs/static/img/PLACEHOLDERS.md` lists what each one should show. Only English is written today;
`docs/README.md` covers adding a language.
