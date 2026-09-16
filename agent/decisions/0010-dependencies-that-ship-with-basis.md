# 0010: Dependencies are declared to VPM, referenced directly, and not gated

**Status:** accepted, 2026-08-30

## Decision

`yuna0x0.Basis.Convert.Editor` references `BasisSDK`, `BasisSDKEditor`, `BasisDebug`,
`com.gator-dragon-games.jigglephysics` and `HVR.Basis.Comms` as ordinary assembly references,
none of them gated behind a `versionDefines` symbol.

Every package those assemblies come from is declared in `vpmDependencies`:

```json
"vpmDependencies": {
  "com.basis.sdk": "0.0.1",
  "com.gator-dragon-games.jigglephysics": "16.0.1",
  "dev.hai-vr.basis.comms": "0.0.1"
}
```

The three Basis assemblies all belong to `com.basis.sdk`. UPM `dependencies` stays empty.

**A bare version is an exact version** (checked 2026-09-16 against vrc-get 29bb9ea, the library
ALCOM uses, and confirmed with a scratch project holding the three packages as embedded folders):
a package already in `Packages/` satisfies the dependency only when its version equals the string.
The string is therefore the version Basis ships, read from the package's own `package.json`, not
a floor. Jiggle Physics has been 16.0.1 in every Basis checkout since 2026-07-09; the 16.0.0 first
declared here made every install show a conflict warning on that package. Weekly-tag projects
carry `-dev.<week>` versions of all three packages and show the warning for each regardless; the
Basis listing rewrites its own packages' dependency strings per tag for the same reason, and a
third-party package cannot.

## Why declare them

They are used, so they are declared. It is also what the surrounding packages do:
`com.basis.framework` lists `com.gator-dragon-games.jigglephysics` in its own `vpmDependencies`,
and `dev.hai-vr.basis.comms` lists `com.basis.sdk`, `com.basis.framework` and `com.basis.server`.
All of those live inside the Basis repository rather than in a listing, and a VPM client treats a
dependency as satisfied by what the project already has, which a Basis project does.

## Why UPM `dependencies` stays empty

That field is resolved against a registry, and the registry was checked rather than assumed
(2026-08-30, `package.openupm.com`):

| Package | On OpenUPM |
|---|---|
| `com.basis.sdk` | no (404) |
| `dev.hai-vr.basis.comms` | no (404) |
| `com.gator-dragon-games.jigglephysics` | yes, up to 16.0.1 |

Two of the three cannot be resolved at all, so naming them would break a git URL or OpenUPM
install rather than describe it. The third could be resolved, but only by a project that has the
OpenUPM scoped registry configured, and a Basis project already embeds that package in its own
`Packages` folder, so declaring it would at best be redundant and at worst fail resolution for
someone without the registry.

`vpmDependencies` is the field VPM clients read, and it is the right place for packages that
arrive with Basis.

This also bounds what publishing to OpenUPM can mean for this package: it can be listed there,
but OpenUPM cannot express its real requirement, since `com.basis.sdk` is not on that registry.
Anyone installing it that way needs a Basis project already, which the README states.

## Why nothing is gated

An asmdef reference is all or nothing: if the assembly is absent the whole package fails to
compile, not only the part that uses it. Making one optional means moving the code that touches
it into a second assembly with `"defineConstraints": ["<symbol>"]`, which Unity skips when the
symbol is absent, and giving the core an interface to call into instead of a direct call.

That is real structure to carry for a situation that does not exist: `git ls-files` on a Basis
checkout finds both `Basis/Packages/dev.hai-vr.basis.comms` and
`Basis/Packages/com.gator-dragon-games.jigglephysics` tracked there, so they arrive with the
framework. This package only means anything inside a Basis project.

If Basis ever unbundles Haï's Comms package, the fix is that split: `VixxyWriter`, the toggle
mapper and the planner's toggle path move into an assembly constrained on a symbol declared
through `versionDefines`, and the rest keeps working without toggles. Nothing else has to move,
because Vixxy is only touched in those places.
