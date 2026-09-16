---
sidebar_position: 5
---

# Reading the report

Every conversion produces a report: in the window while you work, and as Markdown through **Copy
report** or **Save report**. Its first line names the package version and the source releases the
readers were checked against; see [Versions checked](versions.md).

## Severities

| Heading | Meaning |
|---|---|
| Warnings | Converts, but not the way it worked before, or something could not be read. |
| Dropped | No Basis equivalent. |
| Approximated | Fitted onto a Basis setting that does not mean quite the same thing. |
| Mapped | Carried across as it was. Listed so the report is complete. |

Entries with the same code are grouped, with a count and one example.

## Codes

Every code the converter emits, by area. A few codes appear at more than one severity depending
on the case; the usual one is given.

### Source files

| Code | Severity | Meaning |
|---|---|---|
| `avatar.noPrefab` | Warning | Nothing selected is linked to a prefab, so there is no file to read. |
| `avatar.rootNotPrefab` | Warning | The selected object is not linked to a prefab. Only the prefab instances beneath it were read. |
| `source.sceneOnly` | Warning | Components with missing scripts that exist only on the scene object, not in a prefab file. Apply them to the prefab in the project where their scripts are installed. |
| `source.notText` | Warning | The prefab file is stored in binary form and nothing in it can be read. Switch the project where its scripts are installed to Force Text, save the prefab there, and export again. |
| `avatar.missing` | Warning | The prefab file the selection points at does not exist. |
| `avatar.notLoaded` | Warning | The file did not load as a prefab. |
| `source.severalPrefabs` | Mapped | The avatar is built from several prefabs. Each was read from its own file. |
| `source.overridesApplied` | Mapped | Property overrides from prefab variants and nested prefab instances were applied before reading. |
| `source.prefabVariant` | Mapped | The avatar is a prefab variant, so the prefab it inherits from was read as well. |
| `source.inheritedUnreadable` | Warning | A prefab this one inherits from could not be read, so what it carries was not converted. |
| `source.modelRead` | Mapped | The prefab was saved from an imported `.vrm` without unpacking, so its components were read from that file. |
| `source.notUnpacked` | Warning | Nothing was found, and the prefab was saved from an imported model without unpacking. Unpack it completely and save it again. |
| `source.editorOnlyTool` | Mapped | Components of an editor-time authoring tool. They carry no runtime behaviour. |
| `source.unknownScript` | Warning | A component whose script this version does not recognise. Please report it. |

### Writing

| Code | Severity | Meaning |
|---|---|---|
| `apply.noTarget` | Warning | Convert ran without a plan or a target. |
| `apply.sourceUnresolved` | Warning | A prefab is not where it was when scanned, so nothing read from it was written. Rescan. |
| `apply.unresolved` | Warning | A planned rig, constraint or head chop has no object in the target hierarchy and was skipped. |
| `apply.descriptorUnresolved` | Warning | The avatar descriptor has no object in the target hierarchy. |
| `apply.vixxyUnresolved` | Warning | A control switches an object that is not in the target hierarchy. |
| `apply.motionUnresolved` | Warning | A motion came from a prefab that moved since the scan. |
| `vrm.runtimeRemoved` | Mapped | UniVRM's runtime drivers were removed from the converted avatar. They rewrote expressions, spring bones and look-at every frame. Undo restores them. |

### PhysBones

| Code | Severity | Meaning |
|---|---|---|
| `physbone.unresolved` | Warning | The PhysBone could not be tied to a transform and was skipped. |
| `physbone.disabled` | Mapped | The component was disabled and simulated nothing. No rig was written. |
| `physbone.rootUnresolved` | Warning | The Root Transform could not be resolved. The PhysBone was skipped. |
| `physbone.radius.collisionRadius` | Mapped | Radius became collision radius, with its curve. |
| `physbone.radius.negative` | Warning | A negative radius was clamped to 0 and collision left off. |
| `physbone.gravity` | Approximated | Gravity became the gravity multiplier, with its curve. PhysBone blends toward down; jiggle scales world gravity. |
| `physbone.gravityFalloff.dropped` | Dropped | Gravity Falloff has no equivalent, so gravity applies evenly. |
| `physbone.pull.stiffness` | Approximated | Pull, and Stiffness under Advanced, were fitted onto jiggle stiffness. |
| `physbone.stiffnessCurve.dropped` | Dropped | The stiffness curve was dropped. Jiggle stiffness took the pull curve. |
| `physbone.spring.drag` | Approximated | Spring was fitted onto drag. |
| `physbone.springCurve.dropped` | Dropped | Drag runs opposite to spring, so the curve would have inverted the falloff. |
| `physbone.curves.domain` | Approximated | Curves carried over. PhysBone samples them by bone index, jiggle by distance from the root. |
| `physbone.immobile.ignoreRootMotion` | Approximated | Immobile became ignore root motion, which cancels the root's translation only. A turning parent still swings the chain. |
| `physbone.immobile.drift` | Warning | Immobile 0.5 or more with a stiffness under 0.1. The chain will wander when its parent turns, because nothing brings it back. Raise the rig's stiffness. |
| `physbone.immobileCurve.dropped` | Dropped | The immobile curve was dropped. Ignore root motion is a single value. |
| `physbone.immobileType.world` | Approximated | Immobile Type World damps scene movement only. Ignore root motion also damps animated motion. |
| `physbone.limitType.none` | Mapped | No angle limit on either side. |
| `physbone.limitType.angle` | Mapped | The angle limit became a jiggle angle limit. |
| `physbone.limitType.hinge` | Approximated | The hinge angle became a cone. Nothing keeps the bone on one plane. |
| `physbone.limitType.polar` | Approximated | Separate pitch and yaw limits became one cone, using the wider angle. |
| `physbone.limitType.tooWide` | Approximated | The limit was wider than jiggle physics can express, so no limit was written rather than a tighter one. |
| `physbone.limitRotation.dropped` | Dropped | Limit Rotation was dropped. A jiggle limit is centred on the rest pose. |
| `physbone.stretchMotion.stretch` | Mapped | Stretch Motion became stretch, with its curve. |
| `physbone.maxStretch.dropped` | Dropped | Nothing bounds how far a bone may lengthen. |
| `physbone.maxSquish.dropped` | Dropped | Nothing bounds how far a bone may shorten. |
| `physbone.endpointPosition.dropped` | Dropped | Jiggle derives its own chain endpoint. |
| `physbone.multiChildType.oneChild` | Mapped | The root has one child and is simulated on both sides. Multi Child Type does not apply. |
| `physbone.multiChildType.ignore` | Mapped | Multi Child Type Ignore became a motionless root. |
| `physbone.multiChildType.ignoreBranches` | Approximated | Bones below the root with several children stay still in VRChat under Ignore. Jiggle swings them. |
| `physbone.multiChildType.blended` | Approximated | A shared root moved by its chains cannot be expressed, so the root was left motionless. |
| `physbone.allowCollision.off` | Approximated | Allow Collision off excluded other players' hands. Basis registers every avatar's hands, arms and feet as global colliders and a rig cannot opt out. The listed colliders still apply. |
| `physbone.allowGrabbing.filtered` | Approximated | Allow Grabbing was decided per player. Jiggle grabs for everyone or no one. |
| `physbone.allowCollision.filtered` | Approximated | Allow Collision was decided per player. Basis's global colliders cannot be excluded per rig. |
| `physbone.allowGrabbing` | Mapped | Grabbing kept its setting. Radius 0 is not grabbable in VRChat and locks the rig. |
| `physbone.allowPosing.dropped` | Dropped | Jiggle bones spring back when released. |
| `physbone.snapToHand.dropped` | Dropped | No equivalent. |
| `physbone.isAnimated` | Mapped | The rest pose followed animation. Jiggle always does. |
| `physbone.parameter.dropped` | Dropped | The animator parameter prefix has nothing to feed on Basis. |
| `mapping.clamped` | Warning | A fitted value fell outside its range and was clamped. |

### Dynamic Bone

| Code | Severity | Meaning |
|---|---|---|
| `dynamicbone.unresolved` | Warning | The component could not be tied to a transform and was skipped. |
| `dynamicbone.disabled` | Mapped | The component was disabled and simulated nothing. No rig was written. |
| `dynamicbone.noRoot` | Warning | The component names no root, or has Blend Weight 0, and simulates nothing. No rig was written. |
| `dynamicbone.rootUnresolved` | Warning | A root could not be resolved. That chain was skipped. |
| `dynamicbone.multipleRoots` | Mapped | The component drives several chains. Each became its own rig with the same settings. |
| `dynamicbone.radius.collisionRadius` | Mapped | Radius became collision radius, with its curve. |
| `dynamicbone.radius.zero` | Approximated | Radius 0 with colliders. Dynamic Bone collides points; jiggle needs a radius, so 0.01 was written. |
| `dynamicbone.damping.drag` | Mapped | Damping became drag and air drag, with its curve. |
| `dynamicbone.elasticity.stiffness` | Approximated | Elasticity became stiffness by its square root; both are a per-tick fraction toward the pose. |
| `dynamicbone.stiffness.angleLimit` | Approximated | Stiffness caps deviation at 2·asin(1−s) degrees and became an angle limit of that angle. |
| `dynamicbone.stiffness.tooWide` | Approximated | The cap is wider than 90 degrees. No angle limit was written. |
| `dynamicbone.stiffnessCurve.dropped` | Dropped | The angle limit does not follow the stiffness curve. |
| `dynamicbone.blendWeight` | Approximated | Blend Weight below 1 stiffens the chain toward rigid. Folded into the angle limit. |
| `dynamicbone.updateRate` | Approximated | Update Rate other than 60 scales elasticity. Folded into stiffness. |
| `dynamicbone.inert.ignoreRootMotion` | Mapped | Inert became ignore root motion, measured at the rig root instead of the component. |
| `dynamicbone.inertCurve.dropped` | Dropped | Ignore root motion is one value per rig. |
| `dynamicbone.gravity` | Approximated | Dynamic Bone gravity is per tick without a time step and cancelled at rest. The preset's gravity was kept. |
| `dynamicbone.force.gravity` | Approximated | A force straight down with no gravity became a gravity multiplier, from 60 ticks a second. |
| `dynamicbone.force.dropped` | Dropped | A constant force has no equivalent. |
| `dynamicbone.endpoint.dropped` | Dropped | End length other than 1, or an end offset. Jiggle's own tip matches End Length 1. |
| `dynamicbone.freezeAxis.dropped` | Dropped | Nothing flattens a chain's motion onto a plane. |
| `dynamicbone.friction.dropped` | Dropped | Friction after touching a collider is not modelled apart from drag. |

### VRM spring bones

| Code | Severity | Meaning |
|---|---|---|
| `vrm.unresolved` | Warning | A chain could not be tied to a bone and was skipped. |
| `vrm.noJoints` | Warning | A chain named no joints. |
| `vrm.drag` | Mapped | Drag force became drag, on the same scale. |
| `vrm.radius` | Mapped | Joint radius became collision radius, both in metres. |
| `vrm.stiffness` | Approximated | Stiffness force, which has no upper bound, was fitted onto jiggle stiffness. |
| `vrm.stiffness.clamped` | Approximated | A stiffness force above 1 was written as fully stiff. |
| `vrm.gravity` | Approximated | Gravity power became the gravity multiplier. VRM adds a per-step force; jiggle scales world gravity. |
| `vrm.gravity.direction` | Approximated | Gravity did not point straight down. Only the vertical part was kept. |
| `vrm.angleLimit.cone` | Approximated | A cone limit became a jiggle angle limit, capped at 90 degrees. |
| `vrm.angleLimit.dropped` | Dropped | A hinge or spherical limit has no jiggle shape. |
| `vrm.center` | Approximated | The chain named a centre transform. The rig ignores root motion fully, measured at its root bone. |
| `vrm.springBone.disabled` | Mapped | A VRMSpringBone was disabled and simulated nothing. No rig was written. |
| `vrm.branchesExcluded` | Mapped | Bones under the chain that the spring did not name were excluded, so they stay still. |

### Colliders

| Code | Severity | Meaning |
|---|---|---|
| `collider.limit` | Warning | More colliders were referenced than a jiggle rig holds. The extras were dropped. |
| `physics.excludedRoot` | Approximated | The root bone was in its own ignore list. The rig keeps the root motionless instead. |
| `collider.disabled` | Mapped | The collider component was disabled. Rigs that list it get nothing, as on the source. |
| `collider.transform.unresolved` | Warning | A collider could not be tied to a transform. Rigs referencing it do not collide with it. |
| `physics.collider.unresolved` | Warning | A referenced collider or collider group was not in the file. |
| `physics.excludedTransform.unresolved` | Warning | An excluded transform could not be resolved. |
| `collider.radius.negative` | Warning | A negative radius was clamped to 0. |
| `collider.global.dropped` | Dropped | The collider was marked global, for other avatars' bones. Basis offers only hands, arms and feet to others, so this one stays local. |
| `collider.insideBounds.dropped` | Dropped | The collider kept bones inside it. Jiggle colliders only push bones out. |
| `collider.bonesAsSpheres.dropped` | Dropped | Bones As Spheres has no equivalent. |
| `collider.capsuleRotation.snapped` | Approximated | The capsule was rotated off an axis and was snapped to the nearest one. |
| `collider.taper.dropped` | Approximated | A Dynamic Bone capsule with two radii. One radius was used for the whole length. |
| `collider.planeRotation.dropped` | Dropped | A VRChat plane collider was rotated away from its transform's Y axis. It now faces Y. |
| `collider.planeAxis.dropped` | Dropped | A Dynamic Bone plane collider faced its X or Z axis. It now faces Y. |
| `vrm.collider.capsuleSnapped` | Approximated | A VRM capsule ran off an axis and was snapped to the nearest one. |
| `vrm.collider.inside` | Dropped | A VRM collider held bones inside its shape. It now pushes the opposite way. |
| `vrm.collider.planeNormal` | Dropped | A VRM plane collider's normal pointed away from its transform's Y axis. It now faces Y. |

### Constraints

| Code | Severity | Meaning |
|---|---|---|
| `constraint.unresolved` | Warning | The constraint could not be tied to a transform and was skipped. |
| `constraint.disabled` | Mapped | The component was disabled and drove nothing. None was written. |
| `constraint.locked` | Mapped | Locked was off. VRChat ignores that in play, so the Basis constraint is locked. |
| `constraint.retargeted` | Approximated | The constraint drove another transform. It was written onto the transform it drives. |
| `constraint.noSources` | Warning | The constraint has no sources. It was created anyway. |
| `constraint.source.empty` | Warning | A source slot had no transform and was dropped. |
| `constraint.source.unresolved` | Warning | A source could not be resolved and was dropped. |
| `constraint.source.overflow` | Warning | Sources past the sixteenth sit in an overflow list that is not read yet. |
| `constraint.source.weight.normalized` | Approximated | A lone source below full weight follows fully on Basis, which normalises source weights. |
| `constraint.worldUp.none` | Approximated | World Up Type None leaves roll free in VRChat. Basis uses the scene's up. |
| `constraint.weight.clamped` | Warning | The weight was outside 0 to 1 and was clamped. |
| `constraint.solveInLocalSpace.dropped` | Dropped | Basis constraints solve in world space. |
| `constraint.freezeToWorld.dropped` | Dropped | No equivalent. |
| `vrm.constraint.rotation` | Approximated | A VRM rotation constraint copies a delta from rest. A Basis one follows the rotation itself, offset so the authored pose holds at rest. |
| `vrm.constraint.aim` | Approximated | A VRM aim constraint states no up direction, so the scene's up is used. |
| `vrm.constraint.roll` | Approximated | Nothing in Basis copies rotation about one axis, so this became a rotation constraint limited to it. |
| `vrm.constraint.noSource` | Warning | The constraint names no source. It was created anyway. |

### Avatar descriptor

| Code | Severity | Meaning |
|---|---|---|
| `descriptor.unresolved` | Warning | The descriptor could not be tied to a transform and was skipped. |
| `descriptor.noSource` | Mapped | A humanoid rig without a descriptor. An empty Basis Avatar was added. |
| `descriptor.autoSetup` | Mapped | The animator, human scale, renderer list and mouth position are left for Basis to fill in when its inspector is first opened. |
| `descriptor.visemes` | Mapped | All fifteen visemes carried across, position for position. |
| `descriptor.visemes.count` | Warning | Fewer than fifteen viseme blendshapes were listed. The missing ones are unset. |
| `descriptor.visemeMesh.missing` | Warning | Lip sync was set to blendshapes with no mesh assigned. |
| `descriptor.visemesUnset` | Warning | Nothing named the visemes or blink. Assign them on the Basis Avatar by hand. |
| `descriptor.lipSync.unsupported` | Dropped | Lip sync was not blendshape based. Basis drives visemes from blendshapes only. |
| `descriptor.eyeLook.disabled` | Mapped | Eye Look was disabled, so blink is unset. |
| `descriptor.eyelids.none` | Mapped | No eyelid setup or no blink shape chosen, so blink is unset. |
| `descriptor.eyelids.bones` | Dropped | Eyelids were driven by bones. Basis blinks with a blendshape. |
| `descriptor.eyelids.lookUpDown` | Dropped | The looking up and down eyelid shapes have no equivalent. |
| `descriptor.viewPosition.sideways` | Dropped | The sideways part of the view position. Basis stores height and forward offset. |
| `descriptor.animationLayers` | Dropped | Custom animation layers were assigned. Basis has no playable layers. |
| `descriptor.expressionsMenu` | Dropped | The expression menu as a structure. Its toggles are rebuilt as Vixxy controls; see the menu codes. |
| `descriptor.expressionParameters` | Dropped | Expression parameters as a list. Vixxy controls carry their own state. |
| `headChop.disabled` | Mapped | The component was disabled and hid nothing. None was written. |
| `headChop.head.ignored` | Dropped | An entry named the humanoid Head. Basis scales the Head itself and ignores such entries. |
| `headChop.condition.dropped` | Approximated | A head chop bone was scaled away only in VR or only on desktop. Basis scales it away in both. |
| `headChop.target.unresolved` | Warning | A head chop bone could not be resolved and was dropped. |
| `contacts.dropped` | Dropped | VRChat contacts were found. Basis has no contact system. |
| `raycast.dropped` | Dropped | VRChat raycast components were found. Basis has nothing that fires a ray into animator parameters. |
| `vrchat.buildSettings` | Mapped | Per-platform overrides or impostor settings. They instruct VRChat's uploader and carry no behaviour. |

### Menu toggles

| Code | Severity | Meaning |
|---|---|---|
| `expressions.menu` | Dropped | The menu's contents, counted by kind. Toggles and radials are rebuilt; the rest is not. |
| `expressions.parameters` | Dropped | Expression parameters were declared. There is no parameter list to recreate. |
| `expressions.puppets` | Dropped | Two-axis and four-axis puppets. Each drives two parameters at once. |
| `expressions.togglesResolved` | Mapped | How many animator layers were traced from the menu and how many became controls. |
| `vixxy.rebuilt` | Mapped | The count of menu toggles rebuilt as Vixxy controls, each with a menu item. |
| `vixxy.notSimple` | Dropped | The toggle animates over time or drives something a Vixxy control cannot hold. |
| `vixxy.nothingToSwitch` | Dropped | The toggle switched nothing that exists on this avatar. |
| `vixxy.values.normalized` | Mapped | A two-state control used a value other than 1. It was written as 0 and 1 so Basis presents a toggle. |
| `vixxy.commsMissing` | Warning | No HVR Avatar Comms on the avatar. Vixxy controls initialise through it; add the HVR.Networking prefab. |
| `vixxy.puppetEnds` | Approximated | A radial puppet blended through motions between its ends. A slider interpolates in a straight line. |
| `vixxy.builtinGuard` | Approximated | The layer also waited on a VRChat parameter such as `IsLocal`. The control switches whenever it is used. |
| `vixxy.materialBlock` | Approximated | The control sets material properties on a renderer with several materials. Vixxy sets them per renderer, so all are affected. |
| `vixxy.targetMissing` | Warning | The control switches an object that is not in this avatar. That object was left out. |
| `vixxy.componentSwitch` | Mapped | The toggle switches PhysBones or renderers on or off. The control drives their jiggle rigs and renderers. |
| `vixxy.componentSwitch.dropped` | Dropped | The toggle switches a component that is not a converted PhysBone or a renderer. That switch was left out. |
| `vixxy.rootActivation` | Warning | The control switches the avatar root, which Vixxy refuses. That object was left out. |
| `vixxy.overlap` | Warning | Two controls set the same object, blendshape or property. The control used last wins on Basis. |
| `fx.layersUnread` | Dropped | FX layers nothing read, by name. Only menu-steered layers and layers that play on their own are read. |
| `vixxy.rendererMissing` | Warning | The control sets a renderer or blendshape that is not in this avatar. |
| `modularAvatar.hierarchy` | Mapped | Modular Avatar components that rearrange the hierarchy or meshes. Left to Modular Avatar, which applies them at Basis build time when installed with the Basis NDMF platform. |
| `modularAvatar.menus` | Dropped | Modular Avatar menu and animator components. See [Modular Avatar](what-converts/modular-avatar.md). |
| `modularAvatar.togglesRebuilt` | Mapped | How many Modular Avatar menu toggles became Vixxy controls. |
| `modularAvatar.vrchatOnly` | Dropped | Modular Avatar components that act on VRChat's own systems. |

### Authored motion

| Code | Severity | Meaning |
|---|---|---|
| `motion.baked` | Mapped | An animator layer that plays on its own was rebuilt as authored motion. |
| `motion.switched` | Mapped | A menu toggle animated over time, so it was rebuilt as a motion the control switches on. |
| `motion.notLooping` | Mapped | The clip was not authored to loop. It plays once and holds its last frame. |
| `motion.motionTime` | Dropped | A state scrubbed by a parameter through motion time. Rebuild it as a slider by hand. |
| `motion.notRotation` | Dropped | A layer playing on its own animates something other than rotation, which a baked motion cannot hold. |
| `motion.rotationOnly` | Dropped | The layer also animates something other than rotation, which a baked motion clip cannot hold. |
| `motion.noFolder` | Warning | There was nowhere inside the project to write the baked clip, so no motion was written. |
| `motion.nothingToBake` | Warning | The clip turns nothing that exists on this avatar. |
| `motion.pathMissing` | Warning | The clip turns an object that is not under the avatar root. That part was not baked. |

### VRM expressions, face and metadata

| Code | Severity | Meaning |
|---|---|---|
| `vrm.objectUnreadable` | Warning | The expressions, licence and eye offset are inside the `.vrm` and could not be read. Press "Extract Meta And Expressions" in the file's import settings. |
| `vrm.licence` | Mapped | What the licence says: title, author and who may wear the avatar. |
| `vrm.licence.restricted` | Warning | The licence forbids changing the avatar, or limits who may wear it. |
| `vrm.expressionsRebuilt` | Mapped | The expressions became one Vixxy selector named Expression: Neutral, then one choice each. |
| `vrm.expressionsDriven` | Dropped | Viseme, blink and look-at expressions were left to Basis, which drives those itself. |
| `vrm.expression.materialValues` | Mapped | An expression's material changes were written on the renderers that use the materials it names. |
| `vrm.expression.materials` | Dropped | An expression changes a material no renderer on the avatar uses. |
| `vrm.expression.materialShared` | Dropped | The material shares its renderer with others. Vixxy sets a property per renderer, so that part was left out. |
| `vrm.expression.continuous` | Approximated | Expressions VRM lets the wearer apply at any strength. A choice is all or nothing. |
| `vrm.expression.override` | Dropped | The expression blocks or attenuates blink, gaze or lip sync while worn. Basis keeps those running. |
| `vrm.expression.shapeMissing` | Warning | The expression names blendshapes that are not on the mesh. The mesh changed after it was authored. |
| `vrm.visemes` | Approximated | The vowel expressions filled five of the fifteen visemes. VRM names no consonants. |
| `vrm.visemeCompound` | Dropped | A vowel expression moves several blendshapes. A viseme names one, so it was left unset. |
| `vrm.visemeMeshSplit` | Dropped | Vowels sit on more than one renderer. Basis reads all fifteen from one. |
| `vrm.blink` | Mapped | Blink was taken from the avatar's own blink expression. |
| `vrm.eyePosition` | Mapped | The eye offset became the Basis eye position. |
| `vrm.eyePosition.noRig` | Warning | The avatar states an eye offset, but the rig is not humanoid with a head mapped. |
| `vrm.firstPerson` | Dropped | Renderers marked to hide from the wearer. Basis hides the head bone instead. |
| `vrm.lookAt` | Dropped | VRM 0.x look-at components. Basis drives gaze from the eye bones itself. |
| `vrm.lookAt.expression` | Dropped | The avatar aims its eyes with expressions, not eye bones, so its eyes do not follow gaze. |
| `vrm.lookAt.range` | Dropped | The avatar limits how far its eye bones turn. Basis turns eyes up to 25 degrees for every avatar. |

### Rig

| Code | Severity | Meaning |
|---|---|---|
| `rig.noAnimator` | Warning | No Animator with an avatar. Set the model's Animation Type to Humanoid. |
| `rig.notHumanoid` | Warning | The rig is not a valid humanoid. |
| `rig.missingBones` | Warning | Bones the Basis validator requires are missing from the humanoid mapping, Chest and Neck included. |
| `rig.bonesComplete` | Mapped | Every bone the Basis validator requires is mapped. |
| `rig.recommendedBones` | Warning | A shoulder is unmapped. Basis places it from a fallback table and its validator warns. |
| `rig.eyesMapped` | Mapped | Both eye bones are mapped. |
| `rig.eyesMissing` | Warning | One or both eye bones are unmapped. Basis calibrates gaze from both. |
| `rig.jawMapped` | Warning | A Jaw bone is mapped. Basis does not drive it, and the window offers to clear it. |
| `rig.twistBones` | Mapped | How many arm bones have a twist child Basis will pick up. |
| `rig.twistBonesAbsent` | Mapped | Arm bones without a twist child. Basis applies no twist there. |

## The rig section

Separate from the conversion, the report describes what Basis's full-body IK will make of the
humanoid rig: whether the bone mapping is complete, whether the eye bones are mapped, whether
twist bones are named so Basis finds them, and whether a Jaw bone is mapped that the Basis setup
guide asks to be cleared. These are settings on the model, not things a conversion changes, and
the window offers to clear the Jaw mapping. See [Rig check](what-converts/rig-check.md).
