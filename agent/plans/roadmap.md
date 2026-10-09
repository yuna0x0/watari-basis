# Roadmap

## Scope

Bring avatars and what is worn on them into Basis. What decides whether something converts is
the components it carries rather than where it came from, so the sources read are the axis that
grows. Two axes, both open:

- **Content**: avatars first, then props, then worlds, meaning Basis's own content types
  `BasisAvatar`, `BasisProp` and `BasisScene`. Only the first is written today. Clothing and
  accessories are not a content type: they are objects on an avatar, and they convert with it.
- **Source**: VRChat first, because that is where the content and the demand are, then VRM,
  which is a format rather than a platform and carries VRoid Studio and everything exported for
  it. Nothing in the architecture assumes either. A different source is a new reader.

The pipeline is three stages, kept apart so those axes stay independent:

```
readers  ->  intermediate model  ->  mappers  ->  writers
(text in)    (plain data)            (pure)       (Unity objects out)
```

Only writers touch Unity objects, so the rest stays testable without an editor open.

## Where things stand

**Done.** VRChat PhysBones and their colliders, legacy Dynamic Bone, all six VRChat constraint
types, the avatar descriptor, menu toggles, and animation that plays on its own. Reading
components out of prefabs whose scripts are missing, resolving each to the transform that carries
it, mapping, and writing, driven from an editor window that scans, reports and converts, over as
much or as little of the avatar as is ticked. Re-running replaces its own output rather than
stacking a second set. The humanoid rig is checked against what Basis's IK needs.

See `../research/physbone-to-jiggle-mapping.md` for the mapping table, and the decisions folder
for why chain splitting is unnecessary (0006) and why repeated conversions carry no stored state
(0007).

**The one thing still open on physics** is tuning. Two mappings are fits rather than conversions,
pull to stiffness and spring to drag, and they need a converted avatar watched in motion next to
the original. Everything else maps directly.

## After that

- **Constraints. Done.** All six VRChat types map onto their Basis equivalents. The three
  hazards were real and are handled: a constraint driving another transform is relocated onto
  that transform, only the first `totalLength` of the 16 inline source slots are read, and
  `SolveInLocalSpace` / `FreezeToWorld` are reported as dropped. Basis's own
  `BasisConstraintConversion` still covers Unity and Animation Rigging constraints, which this
  does not duplicate.
- **Avatar descriptor. Done.** View position, the fifteen visemes and blink map onto
  `BasisAvatar`. The viseme ordering is identical between the two, so it is positional. The
  animator, human scale, renderer list and mouth position are deliberately left for Basis's own
  automatic setup, which fills empty values when its inspector is first opened.
- **VRM. Done, and checked against real avatars.** Spring bones in both formats become jiggle
  rigs, with per-joint parameters becoming jiggle's curves over distance from the root and bones
  a spring never named excluded. Expressions become Vixxy controls, the emotions and custom ones
  only, since Basis drives visemes, blinking and gaze itself. The eye offset becomes the Basis
  eye position. The licence is read and shown before converting. VRM 1.0's rotation, aim and roll
  constraints become Basis constraints, none of them exactly. See
  `../research/vrm-spring-bones.md`, which records what two real avatars showed.

  Still unread: what a VRM says about where it looks, its first-person renderer flags, which are
  reported instead, and its metadata beyond the licence.

  **Verifying the 0.x reader against real components is backlogged, not planned.** A current
  UniVRM migrates a 0.x file to 1.0 components on import, so the 0.x path only covers prefabs
  authored in an older project and exported into a Basis one. Checking it would need an old
  UniVRM installed, which is unlikely to compile on the Unity version Basis targets given 0.131.0
  already does not. The reader is built from field names read out of UniVRM's own source and is
  covered by a fixture. A bug report from someone with such a prefab would be better evidence
  than anything synthetic, so wait for one.
- **Legacy physics. Dynamic Bone done.** Its fields map onto jiggle more directly than
  PhysBones do: damping is drag and inert is ignoreRootMotion, both on the same 0 to 1 scale,
  and the distribution curves are jiggle's curves. One component can drive several roots, which
  becomes one rig each. Magica Cloth is still open; it needs a hand-authored fixture, since no
  reference avatar uses it.

- **Rig readiness for full-body IK. Done.** Full-body tracking itself is not convertible: trackers,
  calibration and the IK solve are all client side, and a VRChat avatar carries no data about
  them. What *is* avatar-side, and does belong here, is whether the rig meets what Basis's IK
  needs. That lives in the FBX importer's humanoid description rather than in components, and
  an editor script can read and write it:

  - Animation Type is Humanoid and the bone mapping is complete, which Basis requires.
  - The Jaw mapping is cleared. The Basis setup docs call it out as usually wrong on imported
    avatars, and it is a one-line fix.
  - Twist and roll bones exist and are named so Basis finds them. Its arm twist support keys off
    child bones whose names contain `twist` or `roll`.
  - Eye bones are mapped, since Basis calibrates gaze from them at load.

  Implemented as a validate-and-offer-to-fix pass reported alongside the conversion diagnostics,
  and skipped entirely for props, which carry physics but no humanoid rig.

  The twist behaviour was checked against the code first, as planned, and the documentation was
  half right. It happens exactly as described, first direct child whose name contains `twist` or
  `roll`, case-insensitively, but in `BasisTransformMapping.FindTwistBone` in `com.basis.common`,
  not in the `BasisFullIKConstraintJob` the constraints page names, which does not exist in the
  shipped source. The IK itself lives in `com.basis.eeriemovement`.
- **Toggles and animation. Partly done.** Menu toggles are rebuilt as HVR Vixxy controls with
  menu items, covering object switching, blendshapes and material properties. Three menu shapes
  are read: a two-state toggle, a selector whose entries share one int parameter and each set a
  different value, and a radial puppet, which becomes a control presented as a slider between the
  two ends of its blend tree. See [decision 0012](../decisions/0012-controls-with-more-than-two-states.md).

  Animation is rebuilt as `BasisAuthoredMotion`, with the clip baked to a `BasisMotionClip`
  beside the animation it came from, both when nothing steers it and when a menu switches it on.
  A switched motion becomes a Vixxy activation on the `BasisAuthoredMotion` component, which
  `HVR_VixxyPermitted` lists among the types an activation may toggle. See
  [decision 0013](../decisions/0013-baked-motion-assets.md). What remains:

  - **Gimmick layers steered by more than one of the avatar's own parameters.** A layer is read
    only when one of them steers it. VRChat's own parameters no longer count towards that: a
    layer guarded by `IsLocal` or `InStation` is still the toggle's, and the guard is reported as
    dropped. A built-in that steers a transition on its own, as a gesture does, still means the
    layer belongs to it.
  - **Two and four axis puppets.** They drive two parameters at once, which no single Vixxy
    control expresses, and the aggregator that would combine two is unbuilt upstream (above).
    Counted and reported as dropped under `expressions.puppets`.

  Material properties are done. A clip sets one channel at a time, `material._Color.r` and its
  siblings, so the channels are gathered back into one property, typed by how they were named:
  r, g, b, a is a colour, x, y, z, w is a vector, no suffix is a float. Channels neither side of
  the toggle sets are read from the material as authored, the same rule blendshapes already
  used. Vixxy applies these through a `MaterialPropertyBlock`, which covers every material on
  the renderer, so a renderer with more than one material gets a diagnostic saying so.

- **Conversion options in the window. Done.** A conversion can be narrowed rather than being all
  or nothing. Basic is a checkbox per kind of thing a conversion produces: physics, the colliders
  those rigs rest on, constraints, the avatar descriptor, menu toggles and authored motion.
  Advanced adds a checkbox per rig, per constraint, per toggle and per motion, with All and None
  on each list, and the two tuning weights.

  The narrowing is a filter over the plan, not a flag threaded through the readers: every scan
  still reads the whole avatar, so the counts and the detected source kind do not change with
  what is ticked. Diagnostics follow the selection, and what was left out is stated as left out
  in the window and in the report. See [decision 0008](../decisions/0008-conversion-options.md).
- **Several source prefabs. Done, variants included.** A conversion reads every prefab the
  hierarchy is built from, not just the avatar's own, because clothing, hair and accessories are
  prefabs of their own carrying their own physics. See
  [decision 0009](../decisions/0009-several-source-prefabs.md).

  **A prefab variant is read from every prefab above it as well.** A variant's own file holds
  nothing but its overrides, so reading it alone found none of what it inherits: a variant of an
  avatar carrying 61 PhysBones converted with zero. Each file in the chain is now read and
  resolved onto the variant's own objects, through the correspondence route
  `PrefabObjectResolver` already used for nested prefabs, so nothing downstream had to change.
  Reported as `source.prefabVariant`. A prefab built from an FBX is a variant of that model in
  Unity's terms and is deliberately not treated as one, since a model carries no components.
- **Modular Avatar. Partly done.** Its components are identified and reported for what they are.
  The hierarchy ones, `MergeArmature`, `BoneProxy`, `MeshSettings`, `BlendshapeSync` and
  `Parameters`, do their job on Basis and are left to it. The ones that target VRChat cannot:
  `MergeAnimator` merges into animator layer slots Basis does not have, and `MenuItem` and
  `MenuInstaller` build an expression menu it does not have.

  A menu item and a merged animator read together describe a toggle completely, so those are
  traced and rebuilt as Vixxy controls, with the merged animator's clip paths rebased onto the
  object it was merged at. `ObjectToggle` needs no animator at all: a menu item and an object
  toggle on the same object say the same thing between them, and are read that way. All 32
  Modular Avatar components are named, so anything not handled is reported as what it is rather
  than as an unknown script. What remains:

  - **Gimmick controllers.** A layer is only read as a toggle when a single parameter steers it,
    which keeps false positives out. Gimmick packs commonly combine conditions, so their toggles
    are reported rather than rebuilt.
  - Menu items are read per prefab. A menu installed into a submenu keeps its label but not its
    place in a menu tree, since Vixxy menu items are flat.
  - Modular Avatar object paths are resolved against the avatar root, as
    `AvatarObjectReference` does. A path naming something outside the prefab being converted is
    reported rather than guessed at.
  - A layer steered by two of the avatar's own parameters is still left alone. Vixxy's
    `HVRVixxyAggregator`, the obvious candidate, cannot carry them: its whole body sits behind
    `HVR_AGGREGATOR_IS_AVAILABLE`, which nothing defines, its `IHVRVixxyAggregator` base is
    commented out, and the `orchestrator.ProvideValue` it calls does not exist on
    `HVRVixxyOrchestrator`, so it would not compile if the define were turned on. It is
    unfinished upstream work rather than a gated feature. Nothing in Vixxy expresses two inputs
    today, so this and the axis puppets below both wait on Vixxy rather than on us. The newest
    worklog lists this and the other deferred work under "Later", with what starts each.
- **Props and worlds. Backlog.** Both are in scope. Neither is blocked on effort.

  - **`BasisProp`** has nothing to read from. VRChat has no prop content type, and the nearest
    thing, an object worn on an avatar, already converts. Setting up a prop is authoring rather
    than conversion, and Basis ships `BasisPropValidator` for it, including a collider layer
    fix-up. A menu item at most, if anyone asks.
  - **`BasisScene`** is blocked by what world scripts are written against, not by their format.
    Geometry, lighting and colliders are plain Unity and need no conversion. Behaviour is Udon,
    and Basis runs sandboxed C# through Cilbox, which interprets CIL. Most world code is
    UdonSharp, so the source is usually already C#, and compiled Udon programs are serialized
    assets that could be lifted. The wall is the API: `UdonSharpBehaviour`, `VRCPlayerApi`,
    `Networking` ownership, `[UdonSynced]`, pickups and stations have no Basis equivalents.
    Converting a world's scripts means implementing VRChat's world API on top of Basis's
    networking and player model first, which is a runtime project rather than a reader, and one
    with its own licensing questions.
  - **Scripts, in progress (2026-10-08, branch `world-scripts`, not released).** Planned as a
    translator here that keeps a world's UdonSharp sources (adding only `partial`), generates
    dispatch and sync code for them, and checks the result against Cilbox's allow-list, plus a
    separate runtime package of `[Cilboxable]` classes that implements the Udon API on Basis.
    Watari never references the runtime package's code; the generated code is the only link. A
    "Scripts" category offers Convert, Remove (the default without the runtime package) and the
    creator's own port. Needs a decision that says how this relates to 0004 and 0017. User docs
    (decided 2026-10-08): Watari's site gets the conversion side and the report codes, linking to
    the runtime package's own reference for the API; drafted after the door milestone, with diagrams
    of the conversion pipeline (asked for by the user). `docs/versions.md` gains rows for
    UdonSharp and for Cilbox (the Basis commit and `Cilbox.cs` hash, since its package version
    stays 0.1.0), matching the runtime package's own "Versions checked" page. VRChat's SDK licence
    forbids using "Content or Distro Assets" (output derived from VRChat's Example Assets, ClientSim
    among them) outside VRChat (§4.3(i)): the converter should detect VRChat example prefabs and
    scripts in a world and report them instead of converting them (basis-udon decision 0002,
    rechecked 2026-10-08). Research:
    `research/udon-world-scripting.md`, `research/cilbox-for-converted-scripts.md`.

  Physics and constraints already convert on any hierarchy the window is pointed at, and the rig
  check skips itself when there is no humanoid.

## Backlog, outside this package

- **A shared documentation domain.** The docs site is built for GitHub Pages at
  `yuna0x0.github.io/watari-basis/`, which needs no DNS and no extra infrastructure. Hosting all
  of yuna0x0's project documentation together instead, as `docs.yuna0x0.com/watari-basis/` on
  Cloudflare Pages, would read better and would be reusable across projects, at the cost of a
  site that has to be maintained on its own. Out of scope here: moving later is a change to
  `url`, `baseUrl` and a deployment workflow, and the pages themselves do not care.

## Constraints that shape all of it

- Everything ships editor-only. See `../decisions/0004-editor-only.md`.
- Conversion is destructive and undoable. See
  `../decisions/0003-destructive-conversion-with-undo.md`.
- Anything approximated or dropped produces a diagnostic. Roughly a third of the PhysBone
  surface has no jiggle equivalent, so silence would be misleading.
- Basis is a moving target: `developer` branch, every package at `0.0.1`, several fields reached
  through `SerializedObject` because they are private. Keep the writer layer thin so breakage
  stays local.

## Open questions

- Whether the package id and namespaces should follow the display name. They did not change with
  the rename to Watari: `basis` is a scope segment rather than a product name, and changing the
  id would mean a second OpenUPM entry and orphaning what was published. See
  `../decisions/0002-menu-placement-and-naming.md`.
- The two heuristic mappings need tuning against avatars compared side by side.
- Whether to offer a non-destructive build-time path in addition, and if so whether through NDMF
  or Basis's own build hooks.
