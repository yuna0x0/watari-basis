# Udon and UdonSharp world scripting, for conversion

Researched 2026-10-08 against VRChat Worlds SDK 3.10.5 (released 2026-09-04) and the UdonSharp
repository at 1.1.9. Written for the planned script conversion (see
`cilbox-for-converted-scripts.md` and the roadmap's "Props and worlds" entry), which keeps a
world's UdonSharp sources and runs them on Basis through Cilbox and a runtime package.

## Sources and what may be used

Every fact here comes from one of two sources, tagged where it matters:

- **[Docs]** VRChat's public creator docs (creators.vrchat.com, mirrored as Markdown in
  `vrchat-community/creator-docs`, whose git history dates pages) and the UdonSharp docs site
  (udonsharp.docs.vrchat.com).
- **[MIT]** The UdonSharp repository, `vrchat-community/UdonSharp`, MIT licensed ("Copyright (c)
  2020-2021 Merlin and UdonSharp Contributors; Copyright (c) 2022-Present VRChat Inc."). Its
  behaviour may be described; its code is not copied.

Not used, and not to be used for anything in this repository or the runtime package:

- **The VRChat SDK packages**, including the copy of UdonSharp bundled in them and their DLLs.
  The SDK licence (https://hello.vrchat.com/legal/sdk) forbids derivative works and reverse
  engineering.
- **ClientSim** (`vrchat-community/ClientSim`). Its `License.md` is VRChat's distro licence,
  which defers to the same SDK agreement, not an open licence.

Where the docs leave a question open, this note says so. It does not fill the gap from those
sources.

Two cautions about the docs themselves:

- The UdonSharp docs site still says "Open Beta" and lists only two network event targets. The
  current UdonSharp pages are under https://creators.vrchat.com/worlds/udon/udonsharp/.
- Both UdonSharp FAQs list inheritance, method overloads, properties and user enums as
  unsupported. That is 0.x text; 1.x supports all four (interfaces remain unsupported).

## How UdonSharp ships

- The last standalone release is 1.1.9 (2023-08-01). Since SDK 3.4.0 (2023-10-04) UdonSharp and
  ClientSim are merged into the Worlds SDK under `Integrations/` and versioned with it
  (https://creators.vrchat.com/releases/release-3-4-0). [Docs]
- The public repository has had no code change since 1.1.9. Features added later (parameterized
  network events, new event timings) are documented in the SDK release notes, not in that code.
- MerlinVR published an unofficial "UdonSharp 1.2-beta1" (2024-11-23) with non-behaviour classes,
  generic types and collection stand-ins. Scripts written for it can use features that 1.x does
  not have. Rare; such scripts fail to compile and are reported.

## The C# a script can contain

UdonSharp 1.x supports [Docs, MIT tests]:

- control flow: `if`, `while`, `for`, `do`, `foreach` over arrays, strings and `Transform`
  children, `switch` on constants and enums, `break`, `continue`, `return`, `?:`, `??`
- built-in operators, implicit and explicit conversions, `nameof`, `default`, string
  interpolation, `const` fields, field initializers
- arrays and jagged arrays (no multidimensional arrays)
- methods with `out`/`ref`, `params`, default and named arguments, overloads
- properties and auto-properties, expression-bodied members
- inheritance between UdonSharpBehaviours, `abstract`, `virtual`/`override`, `base.` calls
- static methods; extension methods and generic static methods in static non-behaviour classes
- partial classes; user enums (stored as their underlying integer type)
- recursion only with `[RecursiveMethod]`
- `typeof` on built-in types only

Not supported, and so never present in a 1.x script [MIT compiler errors, Docs]: `try`/`catch`/
`finally`/`throw`, local functions, constructors on behaviours, generic methods on behaviours,
generic behaviours, nested types, interfaces, non-const static fields, `new` member hiding,
object and collection initializers, `List<T>` and other generic collections (arrays and the
VRChat data containers instead), user structs. The compiler has no handler for lambdas,
delegates, LINQ, `is`/`as` and pattern matching, `?.`, `using`, `lock`, iterators, `async`,
tuples or `goto`, so those fail too.

Numeric casts are overflow-checked in Udon. Calling a mutating method on a struct copy (for
example `Vector3.Normalize()`) does not change it. An exception halts that UdonBehaviour for the
rest of the session (https://creators.vrchat.com/worlds/udon/debugging-udon-projects).

## Preprocessor symbols

- `COMPILER_UDONSHARP` is defined only while UdonSharp compiles a script. Editor code inside a
  behaviour's file sits behind `#if !COMPILER_UDONSHARP && UNITY_EDITOR`. [MIT, Docs]
- `UNITY_EDITOR` is absent from upload builds. The project also gets a `UDONSHARP` define, and
  SDK 3.10.5 added version defines such as `SDK_310_OR_NEWER`
  (https://creators.vrchat.com/releases/release-3-10-5).
- A converter compiling the sources outside UdonSharp should define `COMPILER_UDONSHARP` for that
  assembly only. Defining `UDONSHARP` project-wide is wrong in Basis: Basis's AudioLink copy uses
  `#if UDONSHARP` to compile its own UdonSharp scripts out.

## How behaviours are stored (1.x)

[MIT: test prefabs and scenes, `UdonSharpEditorUtility.cs`, `UdonSharpEditorManager.cs`; Docs:
https://udonsharp.docs.vrchat.com/migration]

- Each behaviour is two components on one GameObject:
  1. The UdonSharpBehaviour itself, `m_Script` pointing at the user's `.cs`. It holds the user's
     fields as normal Unity fields, an Odin `serializationData` block for what Unity cannot
     serialize (jagged arrays, for example), and `_udonSharpBackingUdonBehaviour: {fileID: ...}`.
  2. A hidden backing `UdonBehaviour` (loose script guid `45115577ef41a5b4ca741ed302693907`,
     `m_ObjectHideFlags: 2`), after the first. It holds `programSource`,
     `serializedProgramAsset`, `_syncMethod`, the interact settings, and Odin public-variable
     data.
- In 1.x the UdonSharpBehaviour holds the authoritative field values; the backing UdonBehaviour's
  public variables are empty until runtime ("Data is owned by a C# proxy ... the corresponding
  UdonBehaviour is empty until runtime"). In 0.x it was the reverse, and the 1.x upgrader
  converts 0.x data. A converter reading the UdonSharpBehaviour's fields is correct for 1.x
  scenes only; a 0.x scene is recognisable by the missing UdonSharpBehaviour.
- A field typed as another UdonSharp class references that class's UdonSharpBehaviour component.
  A field typed `UdonBehaviour` or `Component` references the backing UdonBehaviour. Null
  references stay null.
- On a player build UdonSharp copies the data into the UdonBehaviours and destroys the
  UdonSharpBehaviours, so an uploaded world contains only UdonBehaviours.
- The program asset (`.asset` beside the `.cs`) is an `UdonSharpProgramAsset` with
  `sourceCsScript`, `behaviourSyncMode`, `hasInteractEvent`, `scriptID` and Odin field
  definitions. One program asset per class; class and file names must match.
- Prefab overrides on UdonSharp fields are inferred, not checked on a sample, to be ordinary
  `m_Modifications` on the UdonSharpBehaviour (`propertyPath: field` or
  `field.Array.data[i]`). Fields kept only in Odin data would use Odin's prefab modifications.
- The editor-scripting pages on both doc sites still describe 0.x proxies ("flagged to not be
  saved"). That is out of date for 1.x.

## UdonSharpBehaviour's API

[MIT `Runtime/UdonSharpBehaviour.cs`; Docs for the later additions]

| Member | Notes |
| --- | --- |
| `SendCustomEvent(string)` | Calls a method by name (see below) |
| `SendCustomNetworkEvent(NetworkEventTarget, string)` | Network event, see Networking |
| `SendCustomNetworkEvent(target, name, p0 ... p7)` | Since SDK 3.8.1, up to 8 parameters (https://creators.vrchat.com/worlds/udon/networking/events) |
| `SendCustomEventDelayedSeconds(string, float, EventTiming = Update)` | |
| `SendCustomEventDelayedFrames(string, int, EventTiming = Update)` | A delay of 0 and 1 frames behave the same (source comment) |
| `GetProgramVariable(string)`, `SetProgramVariable(string, object)` | By field name |
| `RequestSerialization()` | Manual sync |
| `DisableInteractive`, `InteractionText` | Properties |
| `GetUdonTypeID()`, `GetUdonTypeID<T>()`, `GetUdonTypeName()` | ID is the first 8 bytes of SHA-256 over UTF-8 `Type.FullName`, read as little-endian `long`; name is `Type.Name` (`UdonSharpRuntimeUtils.cs`) |
| `VRCInstantiate(GameObject)` | Obsolete; `Object.Instantiate` compiles to the same local-only instantiate |

- `EventTiming` (`VRC.Udon.Common.Enums`): `Update`, `LateUpdate`, plus `FixedUpdate` and
  `PostLateUpdate` since SDK 3.10.2 (https://creators.vrchat.com/releases/release-3-10-2).
- `NetworkEventTarget` (`VRC.Udon.Common.Interfaces`): `All`, `Owner`, plus `Others` and `Self`
  since 3.8.1.
- `NetworkCalling` (`VRC.SDK3.UdonNetworkCalling`): `SendCustomNetworkEvent(behaviour, ...)`,
  `CallingPlayer`, `InNetworkCall`, `GetQueuedEvents`, `GetAllQueuedEvents` [Docs, events page].

## Attributes

[MIT `Runtime/UdonSharpAttributes.cs`; Docs https://udonsharp.docs.vrchat.com/udonsharp and
https://creators.vrchat.com/worlds/udon/udonsharp/attributes]

- `[UdonSynced]` / `[UdonSynced(UdonSyncMode)]`: `NotSynced`, `None` (default, no
  interpolation), `Linear`, `Smooth`. The compiler rejects synced fields in `None` and
  `NoVariableSync` behaviours, arrays in Continuous, and `Linear`/`Smooth` in Manual.
- `[UdonBehaviourSyncMode(BehaviourSyncMode)]`, inherited by subclasses:

  | Value | Serialized as | Meaning |
  | --- | --- | --- |
  | `Any` | 0 | Default; set in the inspector |
  | `None` | 1 | No synced variables, no network events |
  | `NoVariableSync` | 2 | No synced variables; network events work |
  | `Continuous` | 3 | Automatic, about 200 bytes per serialization |
  | `Manual` | 4 | On `RequestSerialization`, about 280,496 bytes per serialization |

- `[FieldChangeCallback(nameof(Property))]`: on a change by network sync or `SetProgramVariable`,
  the property's setter runs instead of a plain write. **Inside the setter the field still holds
  the old value**; the new value is the setter's `value` (MIT: the compiler moves the incoming
  value into `value` and restores the old one before calling the setter). Direct writes inside
  the class do not fire it; direct writes from another behaviour are a compile error. It does
  not fire for in-place array element changes [Docs].
- `[RecursiveMethod]`: required for recursion, because Udon locals live on the heap.
- `[NetworkCallable]` / `[NetworkCallable(maxEventsPerSecond)]` (`VRC.SDK3.UdonNetworkCalling`,
  SDK 3.8.1): see Networking.
- `[DefaultExecutionOrder]` orders `Update`, `LateUpdate` and `FixedUpdate` among behaviours.
- Unity attributes honoured: `Header`, `HideInInspector`, `NonSerialized`, `SerializeField`,
  `Space`, `Tooltip`, `ColorUsage`, `GradientUsage`, `TextArea`.

## Names: events, methods and variables

[MIT `EmitContext`, `CompilationContext`, `CompilerUdonInterface`; Docs events page]

- `SendCustomEvent(name)` reaches public, parameterless instance methods by exact, case-sensitive
  name. Methods with parameters are exported under mangled names and are not reachable by their
  C# name. Static methods are never exported. Return values are discarded.
- Built-in events are exported as `_` plus lower camel case: `_start`, `_update`, `_interact`,
  `_onPlayerJoined`. Their arguments travel in heap variables named after the event and the
  parameter (`onPlayerJoinedPlayer`). Inferred: `SendCustomEvent("_interact")` reaches
  `Interact`, and `SendCustomEvent("Interact")` does not.
- `Get/SetProgramVariable` resolve any instance field by name. `SetProgramVariable` goes through
  a `[FieldChangeCallback]` setter when one exists. Whether VRChat restricts lookups to public or
  serialized fields is not documented.

## Events

Signatures as UdonSharp overrides. The full table is on
https://creators.vrchat.com/worlds/udon/graph/event-nodes and the UdonSharp events page
https://udonsharp.docs.vrchat.com/events. Year of addition from the release notes.

- **Lifecycle:** `Start`, `Update`, `LateUpdate`, `FixedUpdate`, `PostLateUpdate` (after
  LateUpdate), `OnEnable`, `OnDisable`, `OnDestroy`. No `Awake` is listed; initialisation is by
  field initializers and `Start`. `OnEnable` and `Start` run before any other event; events
  before `Start` are rejected.
- **Interaction:** `Interact`, `OnPickup`, `OnDrop`, `OnPickupUseDown`, `OnPickupUseUp` (the
  pickup events fire for the local player only), `OnStationEntered(VRCPlayerApi)`,
  `OnStationExited(VRCPlayerApi)` (the parameterless forms are compile errors since 1.0.0b9).
- **Players:** `OnPlayerJoined`, `OnPlayerLeft`, `OnPlayerRespawn` (local player only),
  `OnPlayerTriggerEnter/Stay/Exit`, `OnPlayerCollisionEnter/Stay/Exit` (moving colliders only),
  `OnPlayerParticleCollision`, all taking `VRCPlayerApi`. Players do not raise plain
  `OnTriggerEnter` (https://creators.vrchat.com/worlds/udon/players/player-collisions).
  Added 2024 or later: `OnAvatarChanged`, `OnAvatarEyeHeightChanged` (3.5.2),
  `OnPlayerSuspendChanged`, `OnMasterTransferred` (3.7.0), `OnPlayerRestored`,
  `OnPlayerDataUpdated` (3.7.4).
- **Networking:** `OnPreSerialization`, `OnPostSerialization(SerializationResult)`,
  `OnDeserialization()` and `OnDeserialization(DeserializationResult)`,
  `bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)`,
  `OnOwnershipTransferred(VRCPlayerApi)`.
- **Input** (local player): `InputJump`, `InputUse`, `InputGrab`, `InputDrop(bool,
  UdonInputEventArgs)`, `InputMoveHorizontal/Vertical`, `InputLookHorizontal/Vertical(float,
  UdonInputEventArgs)`, `OnInputMethodChanged` (3.5.2)
  (https://creators.vrchat.com/worlds/udon/input-events).
- **Unity messages** listed as supported: `OnTrigger*`, `OnCollision*` (3D and 2D),
  `OnControllerColliderHit`, `OnParticleCollision`, `OnAnimatorIK`, `OnAnimatorMove`,
  `OnBecameVisible/Invisible`, render callbacks, `OnTransformChildren/ParentChanged`,
  `OnMouse*`, plus editor-only `OnValidate`, `Reset`, `OnDrawGizmos`.
- **Out of the conversion's first scope** (reported when a script uses them): video player
  events, string and image loading callbacks, MIDI, `OnLanguageChanged`, camera and quality
  settings events, PhysBone and Contact events (3.10.0), drone events (3.8.0), persistence
  usage events, Creator Economy events.

## Players: VRCPlayerApi

[Docs: https://creators.vrchat.com/worlds/udon/players/ and its subpages]

- **Identity:** `displayName`, `playerId`, `isLocal`, `isMaster`, `isInstanceOwner`,
  `isSuspended` (3.7.0), `IsValid()`, `IsUserInVR()`, `IsOwner(GameObject)`;
  `VRCPlayerApi.GetPlayerById`, `GetPlayerCount`, `GetPlayers(VRCPlayerApi[])` (fills the array
  passed), `GetPlayers()` (3.10.2).
- **Position:** `GetPosition`, `GetRotation`, `GetBonePosition/Rotation(HumanBodyBones)` (zero
  or identity when the bone is missing), `GetTrackingData(TrackingDataType)` with `Head`,
  `LeftHand`, `RightHand`, `Origin`, `AvatarRoot`.
- **Movement (local player):** `TeleportTo(Vector3, Quaternion[, SpawnOrientation[, bool
  lerpOnRemote]])` with `Default`, `AlignPlayerWithSpawnPoint`, `AlignRoomWithSpawnPoint`;
  `Respawn()`; `SetVelocity`/`GetVelocity`; `IsPlayerGrounded`; `Immobilize`;
  `UseLegacyLocomotion`; walk (default 2), run (4), strafe (2), jump impulse (0; the VRChat world
  prefab sets 3) and gravity strength (1).
- **Avatar scaling** (3.5.2), **voice and avatar audio**, **haptics**
  (`PlayHapticEventInHand`), **pickups in hand**, **player tags**, **language**
  (`GetCurrentLanguage`).

## Networking

[Docs: https://creators.vrchat.com/worlds/udon/networking/ and subpages, dated by
`creator-docs` history: ownership 2025-05, variables 2025-11, events 2026-06, network details
2025-08, late joiners 2025-05]

### Ownership

- The unit is the GameObject: every networked GameObject has an owner, and all UdonBehaviours on
  it share that owner. Children do not inherit ownership (exception: a PlayerObject's contents).
- The first player in an instance becomes owner of every networked object. The instance master
  "is the player that owns any object that never had its ownership manually set or transferred".
  When an owner leaves, "VRChat automatically assigns a new owner"; the pre-2025 networking page
  said the new master takes over the previous master's objects. Who receives a non-master's
  objects is not stated in the current docs.
- Master guarantees, which the docs call the only ones: there is always a valid master; the
  first player into an empty instance becomes master; the master changes when the master leaves
  (or a mobile client is backgrounded too long); the choice is made by the server and is
  unpredictable. `OnMasterTransferred` (3.7.0) fires for everyone, and for the first player after
  `OnPlayerJoined`. A new master is chosen before `OnPlayerLeft` runs.
- `Networking.SetOwner(player, obj)` applies locally at once (release 2021.2.2: "now works
  immediately"). Owners can give ownership to anyone; non-owners can only take it for
  themselves, unless the behaviour has no `OnOwnershipRequest`.
- Request flow (pre-2025 page and its diagram): the requester runs `OnOwnershipRequest` locally
  and cancels on false; it then runs `OnOwnershipTransferred(newOwner)` at once, before
  confirmation; the current owner runs `OnOwnershipRequest`, and false (or no return) rejects,
  sending the requester `OnOwnershipTransferred(originalOwner)`; on acceptance everyone gets
  `OnOwnershipTransferred(newOwner)`. The logic runs on both sides and disagreements desync.
- `Networking.IsOwner([player,] obj)`, `GetOwner(obj)`, `IsMaster`, `Master`, `IsInstanceOwner`
  (true only for the creator of Invite, Invite+, Friends and Friends+ instances),
  `InstanceOwner` (null while absent).

### Synced variables

- Sync modes as in the attribute table. With `None`, network events do not work either.
- Syncable types: `bool`, `char`, all integer types, `float`, `double`, `string`, `Vector2/3/4`,
  `Quaternion`, `Color`, `Color32`, `VRCUrl`, and one-dimensional arrays of these (Manual only);
  user enums as their underlying type. Scene object references cannot be synced. An unset synced
  array stops the behaviour syncing.
- Interpolation (`Linear`, `Smooth`) is Continuous only, and only for numeric, vector,
  quaternion and colour types.
- `RequestSerialization()` flags the behaviour for the next network tick, which is not every
  frame; repeated calls merge into one send, after `OnPreSerialization` and followed by
  `OnPostSerialization(result)`. Not needed in Continuous.
- `OnDeserialization` runs after all synced variables are written; it does not say which
  changed. `DeserializationResult` carries `sendTime`, `receiveTime` (both on the receiver's
  `Time.realtimeSinceStartup` clock) and `isFromStorage`.
- Late joiners receive the latest state in both modes through `OnDeserialization`, without the
  owner calling anything; `Networking.IsNetworkSettled` becomes true once that is applied.
- Limits: Continuous about 200 bytes per serialization, Manual about 280,496 (64,690 before
  about April 2025); Udon output about 11 KB/s overall (the events page says a hard cap of about
  18 KB/s including overhead). When clogged, Continuous drops and logs, Manual retries.
- Not documented: what a non-owner's `RequestSerialization` or local write does, the Continuous
  send rate, the Manual rate formula, and ordering between events and variable sync.

### Network events

- Targets: `All` (everyone; the sender runs it locally and immediately), `Others`, `Owner`,
  `Self` (local only, never sent, no rate limit). There is no per-player target.
- **Legacy events** (no `[NetworkCallable]`): the method must be public and parameterless, and a
  name starting with `_` cannot be called over the network. They behave like
  `GameObject.SendMessage`: every UdonBehaviour on the target GameObject receives them.
- **`[NetworkCallable]` events** (3.8.1): public, not static, virtual, override or overloaded, up
  to 8 syncable parameters, no `ref`/`out`/`params`/defaults; a leading `_` is allowed. They reach
  only the specific component, identified by its index on the GameObject.
- The receiving behaviour can be disabled, but must not use sync mode `None`.
- Rate limits: 5 per second per event by default, 1 to 100 with the attribute; about 100 per
  second overall; sends are queued locally, never dropped; local execution is never limited.
  Parameter data up to 16 KB per event; `null` arrives as `default(T)`.
- Ordering: events from one sender arrive in send order, unless a rate-limited queue lets others
  pass. Events are never replayed to late joiners.
- `NetworkCalling.CallingPlayer` names the sender inside a network call.

### Networked components and instantiation

- **VRCObjectSync** syncs position, rotation, and kinematic and gravity state;
  `SetKinematic`, `SetGravity`, `FlagDiscontinuity`, `TeleportTo(Transform)`, `Respawn`;
  options Allow Collision Ownership Transfer and Force Kinematic On Remote (3.8.0).
- **VRC Pickup**: grabbing an object with ObjectSync makes the grabber its owner (pre-2025
  page). `DisallowTheft` blocks taking it from another hand.
- **VRCObjectPool**: the owner's `TryToSpawn()` returns an object or null; `Return(obj)`
  disables it; active state syncs, including to late joiners.
- **Network IDs** are assigned per networked GameObject in the editor, tied to the hierarchy path
  (https://creators.vrchat.com/worlds/udon/networking/network-id-utility). Builds that differ in
  component types or order, or in synced variables, are incompatible.
- **Instantiate** creates a local, unsynced copy, and UdonBehaviours on it are unsynced
  (3.10.0 notes).

### Persistence (out of the first scope)

`PlayerData` (per-player key-value store, local writes only, any change resends all of the
player's data), `VRCPlayerObject` and `VRCEnablePersistence`, `OnPlayerRestored`, 100 KB of each
per player per world (https://creators.vrchat.com/worlds/udon/persistence/).

## World components (docs pages under https://creators.vrchat.com/worlds/components/)

Only what the docs pages state; serialized field names come from reading real files, as in
`vrchat-serialized-formats.md`, not from SDK binaries.

- **VRCPickup**: needs a Rigidbody and a Collider; proximity, orientation (Any, Grip, Gun), auto
  hold, exact gun and grip transforms, `pickupable`, theft, throw settings; scripting via the
  pickup events above. The docs page does not list the scripting API.
- **VRCStation**: player mobility, entry and exit locations, `disableStationExit`, `seated`;
  `UseStation(player)`, `ExitStation(player)`.
- **VRCMirrorReflection**, **VRC_SceneDescriptor** (spawns, spawn order and orientation,
  respawn height, reference camera, interact passthrough), **VRCAvatarPedestal**,
  **VRCPortalMarker**, **VRCSpatialAudioSource**, **VRCUiShape** (the canvas needs a BoxCollider
  and must not be on the UI layer), video players (`VRCUnityVideoPlayer`, `VRCAVProVideoPlayer`),
  MIDI, camera dolly (3.9.0), and PhysBones/Contacts in worlds (3.10.0).

## Program formats, for worlds without C# source

[Docs: https://creators.vrchat.com/worlds/udon/vm-and-assembly/, community-written, last
updated March 2024]

- A behaviour runs a compiled program: bytecode, a typed heap, exported entry points (the event
  symbols above) and a symbol table. Udon Graph programs and Udon Assembly compile to the same
  form; UdonSharp generates Udon Assembly.
- Instructions: NOP, PUSH, POP, JUMP_IF_FALSE, JUMP, EXTERN, ANNOTATION, JUMP_INDIRECT, COPY.
  No locals, no call or return instruction; EXTERN calls a whitelisted .NET or VRChat method by a
  signature string such as `Type.__Method__ArgTypes__Ret`. The docs say there is no complete
  reference of externs.
- Converting a Graph-only world means decompiling programs to structured code and mapping each
  extern. Not in scope; such behaviours are reported.

## Release history relevant to scripts

| SDK | Date | Change |
| --- | --- | --- |
| 3.4.0 | 2023-10 | UdonSharp and ClientSim merged into the Worlds SDK |
| 3.5.2 | 2024-03 | `OnInputMethodChanged`, avatar scaling, byte and bit operations |
| 3.7.0 | 2024-08 | `OnMasterTransferred`, `Networking.Master`, `InstanceOwner`, `isSuspended`, `OnLanguageChanged` |
| 3.7.4 | 2024-11 | Persistence |
| 3.8.0 | 2025-04 | Drones, ObjectSync Force Kinematic On Remote |
| 3.8.1 | 2025-05 | Network events with parameters, `[NetworkCallable]`, `Others` and `Self` |
| 3.10.0 | 2025-10 | PhysBones, Contacts and Constraints in worlds |
| 3.10.2 | 2026-02 | `GetPlayers()`, `FixedUpdate` and `PostLateUpdate` event timing |
| 3.10.4 | 2026-06 | VRCTween, data container capacity |
| 3.10.5 | 2026-09 | Assembly version defines |

Release notes: https://creators.vrchat.com/releases/

## The Udon VM, and where it differs from C#

Researched 2026-10-08 from the VM and Assembly page (https://creators.vrchat.com/worlds/udon/vm-and-assembly/,
community-written, last edited 2024-03-22, "the VRChat team can't guarantee the validity"), the
debugging and graph pages, and the MIT UdonSharp 1.1.9 compiler. The compiler bundled in today's
SDK is newer, so lowering details are 1.1.9's.

### Model

- A bytecode interpreter with a typed heap and no locals; an integer stack carries heap indexes.
  Opcodes: NOP 0, PUSH 1, POP 2, JUMP_IF_FALSE 4, JUMP 5 (`JUMP 0xFFFFFFFC` ends), EXTERN 6,
  ANNOTATION 7, JUMP_INDIRECT 8, COPY 9. No call or return: UdonSharp pushes a return address,
  copies arguments into the callee's fixed heap slots and jumps; a return is `JUMP_INDIRECT`.
- Externs are named `UdonTypeName.__Method__Param1_Param2__Return` (dots and `+` removed, `[]` as
  `Array`, `&` as `Ref`); there is no complete public list.
- Events are exported as `_` plus lower camel case with parameters in heap variables
  (`onPlayerJoinedPlayer`); methods with parameters are renamed `__N_Name`, so name-based calls
  reach only parameterless methods. Field change callbacks are `_onVarChange_<field>` with the old
  value in `_old_<field>`.
- `_onEnable` then `_start` run first; events before them are ignored. Any exception halts that
  behaviour (`[UdonBehaviour] An exception occurred during Udon execution, this UdonBehaviour will be
  halted.`). No execution timeout is documented.
- `[DefaultExecutionOrder]` becomes `.update_order` and orders Update, LateUpdate and FixedUpdate.
- Field initializers run once in the editor at compile time and are baked into the program, so a
  random or time-based initializer is the same for every instance and session.

### Differences that matter for a C# re-host, against Cilbox (measured in basis-udon's tests)

Fixed by basis-udon's IL post-processor (its decision 0007) unless stated; each row is a case in
basis-udon's `Tests/Editor/CilboxRunTests` (Semantics 0 to 39, the two lifecycle tests).

| Behaviour | Udon (what scripts were tested on) | Cilbox alone | Now |
| --- | --- | --- | --- |
| Struct copies (`Vector3 v = field; v.x = 5`, `b = a`, struct parameters) | copy (C#) | shared | copy (0, 1, 31) |
| `arr[i].x = 5`, `arr[i].Normalize()` on a struct array | written back (C#) | lost or crash | written back (2, 36) |
| Mutating method on another behaviour's struct field (`other.v.Normalize()`) | no effect (a `GetProgramVariable` copy) | changes it | no effect (35) |
| Field set on another behaviour's struct field (`other.v.x = 9`) | written back (`SetProgramVariable`) | written | written (39) |
| Numeric casts | `System.Convert`, floats truncated first; overflow halts | wrap | same as Udon (16, 25 to 29, 37) |
| `int`/`uint` and `long`/`ulong` casts | `System.Convert`, overflow halts | reinterpret | same as Udon when the value is stored; a cast value used without being stored is not detected |
| Widening to `ulong` from a negative `int` or `long` | halts | wraps | wraps (not detected: the opcode does not say the target is unsigned) |
| `%` on float or double, `uint`/`ulong` to floating point, `bool[]` write, method on a `char` | work | crash | work (3, 4, 5, 9, 32, 33, 34) |
| Cast of null, casts between script types | unchecked | crash, exact type only | unchecked (6, 38) |
| NaN in `if (a < b)` | not taken | taken | not taken (8) |
| User enum `ToString()` and interpolation | the number ("1") | the name | the number (17, 30) |
| Exception | halts the behaviour | the event stops; interpreted `catch` handles every exception, native ones included, but not interpreter faults (`cilbox-for-converted-scripts.md`; basis-udon `research/cilbox-execution-model.md`) | the event stops; whether later events still run is in the re-audit |
| First `OnEnable` | runs, before `Start` | dropped | runs once before `Start` (companion `Awake`, flag set by the post-processor) |
| Order of first `OnEnable` across behaviours | not documented | | each behaviour runs its own `OnEnable` then `Start`; unverified against Udon |
| Re-entrant calls with heap locals | locals overwritten | C# locals | keep C# (Udon's is a bug) |
| `GetUdonTypeID`, `GetUdonTypeName` | SHA-256 of the full name, first 8 bytes; `Type.Name` | n/a | constants in the companion (contract 3) |
| `try`/`catch`/`finally`, static fields | not in UdonSharp | `finally` skipped on propagation, only `catch (Exception)`, static fields typed as scripts fail | hand-ported code only; reported in the porting guide |

### Worlds without C# source

A Graph or Assembly world ships compiled programs (Odin-serialized; the binary layout is not
publicly documented). Converting one would mean loading the program, recovering calls and returns
from the UdonSharp calling convention, mapping each extern back to an API, and emitting IL with one
method per export (structuring can be skipped, since the target is IL). UdonSharp programs carry
rich symbol names (`__refl_typename`, `__lcl_name_Type_N`), so method boundaries and names are
recoverable. No allowed source names a public decompiler. Not planned.
