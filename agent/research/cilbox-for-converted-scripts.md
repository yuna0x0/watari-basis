# Cilbox, for converted world scripts

Basis `developer` at `eac8e38cc` (Unity 6000.7.0b2). Paths are relative to `Basis/Basis/Packages/`;
`C:` is `com.cnlohr.cilbox/Cilbox.cs`, `U:` `CilboxUsage.cs`, `P:` `CilboxProxy.cs`, `T:`
`CilboxUtil.cs`, `BC:`/`BS:` the Basis allow-lists `com.basis.shim/Shims/CilboxBasisCommon.cs` and
`CilboxSceneBasis.cs`.

Sources: two full read-throughs of the Cilbox source (2026-10-08), and measurements by basis-udon's
tests (`Tests/Editor/CilboxRunTests.cs`: `Conformance`, `Semantics`, `DataContainersRunUnderCilbox`,
`LockRunsUnderCilbox`), which export a scene through Cilbox and drive it headlessly. **Measured**
marks a result a test pins; everything else is read from the code. When Basis is updated, those
tests are the first thing to rerun.

## How Cilbox works

Cilbox interprets the IL of `[Cilboxable]` classes. The attribute is inherited by subclasses and
covers nested types (`T:1080-1089`).

- **Export** (editor, on scene processing for play mode or a build; `C:2680-3340`): collects every
  MonoBehaviour type in the scene, then every type referenced through a **method or field token**
  in exported IL (`C:2931`, `C:2966`). A type used only as a type token (`newarr`, `castclass`,
  `isinst`, `box`, a catch type) or only in a signature is not added. Every method of a collected
  type is exported, including inherited public/protected instance methods of a base **in the same
  assembly** (copied in); methods without bodies (abstract, interface) are skipped. Tokens become
  name and signature strings, resolved again at load. Each script component is replaced by a
  `CilboxProxy` carrying its serialized fields.
- **Load** (`Cilbox.BoxInitialize`, `C:2268-2527`): resolves every token and checks every native
  type, field and method against the allow-list. Load runs at export too (`P:51`), so most load
  errors appear at build time. **There is no isolation: one failure leaves the whole box broken.**
  Static constructors of every class run eagerly at the end, in export order.
- **Run**: hooks are found by name among the most-derived class's methods (`C:2111`); native calls
  go through reflection and allocate.

## Three kinds of failure

1. **Export**: a method that cannot be decoded (for example `typeof`, an `ldtoken` on a type,
   `C:2883`) is left out with a log line. If it is a hook it never runs; if anything calls it, load
   fails. A generic method parameter makes the **whole export** throw (`C:3186`). **Measured.**
2. **Load**: an unresolvable or disallowed type, field or method anywhere, even in dead code,
   breaks the whole box: abstract and interface calls (`C:2478`, **measured**), `GetType()`,
   anything named `*Invoke*`, `MonoBehaviour` methods other than `.ctor`, non-allow-listed static
   readonly fields, generic types with an interpreted argument, static fields typed as an
   interpreted class (`C:2085`, **measured**).
3. **Run**: catchable by the script (`catch (Exception)`): exceptions thrown inside native methods,
   the interpreter's NullReference, IndexOutOfRange and DivideByZero, user `throw`. Not catchable:
   every interpreter error (missing opcode, failed cast, disallowed field, timeout, reflection
   argument mismatch). An uncaught error disables that script for good; if the entry had no script
   (`.cctor`, a delegate to a static method) the **whole box** is disabled (`C:187-194`).

## Rules for code we generate or keep

**Structure**

- Every interpreted class and its base classes `[Cilboxable]`; no generics; no interfaces; no
  abstract methods (virtual with a body instead).
- **No instance fields on a base class that lives in another assembly.** Field slots are indexed
  by `(declaring type's assembly, token)`, but a subclass's inherited fields are looked up under the
  subclass's assembly (`C:3123`), so a base field's token can collide with an unrelated field token
  of the subclass's assembly and corrupt its index. Not triggered by the current tests (**measured**
  with 24 fields); avoided by construction.
- **No cross-assembly field reads**: such a token is never indexed and fails as "Illegal field
  reference outside of the cilbox" (`C:2351`). **Measured.** Methods and properties across
  assemblies work.
- No static fields typed as an interpreted class, an array or generic of one, or a type off the
  allow-list; type them `object`. **Measured.** Static strings default to `""` (`U:695`), and
  reading a never-assigned static string in a script crashed in the `Semantics` test (UdonSharp
  scripts have no statics; the runtime's statics are covered by its own tests). Compiler-generated
  `<>c.<>9` (non-capturing lambdas) hits the same rule.
- A type that appears only in a signature is not exported and logs "TYPE FAILED CHECK"; reference it
  from some method body.
- Hooks are found only on the most-derived class; a private hook on a base never runs.

**Calls and delegates**

- `typeof` kills the method at export; `GetComponent(typeof(T))` must become `GetComponent<T>()`.
- `GetComponent<T>()` / `TryGetComponent<T>()` with an **interpreted** `T` work only on a
  **GameObject** receiver (`U:151-178`); through a Component they return null. **Measured.**
  `GetComponents<T>`, `GetComponentInChildren<T>` and similar with an interpreted `T` fail at load
  (**measured**: `GameObject.GetComponentInParent<Script>(bool)` fails the export). basis-udon's
  IL post-processor routes the first through `gameObject` and the rest to static helpers in the
  companion (its decision 0007); Semantics 40 to 46 and Conformance check 2 measure them.
- `ToString`, `Equals`, `GetHashCode` (including string concatenation and `Debug.Log` of an object)
  run natively; an interpreted override is never reached. **Measured** (`ToString`).
- Delegates: only to methods of a MonoBehaviour script (target a `CilboxProxy`) or to static
  methods; at most 4 parameters; never invoked from script; `-=` needs the same stored instance.
  Lambdas capturing locals, and method groups on plain-class objects, crash at creation.
- Virtual dispatch re-binds `callvirt` by signature in the receiver's class, so overrides work
  (**measured**) and `new`-hidden methods act like overrides.

**Values** (the `Semantics` test, all **measured**)

| C# | Under Cilbox |
| --- | --- |
| `Vector3 v = field; v.x = 5;` | **the field changes too** (structs are shared, not copied) |
| `Vector3 b = a; b.x = 5;` | **`a` changes too** |
| `arr[i].x = 7` on a struct array | **write lost** |
| `Vector3 v; v.x = 1; v.y = 2; v.z = 3;` (no initializer) | **crash** |
| `bool[]` element write | **crash** ("not a widening conversion") |
| `%` on float or double | **crash** (no float `rem`) |
| `uint` to `float` | **crash** (`conv.r.un` missing) |
| `(string)null`, any cast of null | **crash** |
| `NaN < 1f` in an `if` | **branch taken** |
| method call on a `char` local | **crash** |
| `finally` when the exception leaves the method | **not run** |
| `catch (NullReferenceException)` | **never matches**; only `catch (Exception)` works |
| `v.Normalize()` on a local, `List<int>` foreach, `Dictionary.TryGetValue(out)`, 8-case string switch, divide by zero caught, enum `ToString`, int arrays, unboxing, int overflow wraps, `string.Format`, float to int, `(int)uint` sign, `long != 0` | same as C# |

Read from the code, not measured: casts or `is` succeed only for the exact type (no base classes or
interfaces); `foreach (Transform c in t)` crashes on a `RectTransform` child (measured in the spike);
`throw;`, `checked`, `lock`, `volatile`, coroutines, iterators and `async` crash or fail to load;
`Nullable<T>` and interpreted structs are unusable; `arr[i] += v` on struct arrays needs `ldobj`,
which is missing.

**Serialized fields** (`P:99-176`)

- Primitives, enums, strings, allowed Unity object references, references to in-scene scripts,
  arrays of those, `Vector3`, `Color` and `Quaternion` (through JSON), and enum arrays survive
  export. **Measured.**
- A serialized field holding a plain interpreted object or a `List<T>` of user types throws at
  export; other JSON types (`AnimationCurve`, `Gradient`, `char`, `decimal`) are unverified.
- `[NonSerialized]` is not honoured: public fields are always written. Serialized values overwrite
  field initializers.

**Lifecycle** (`P:498-520`)

- `Awake` and `Start` both run in Unity's `Start`, back to back; one script's `Start` can precede
  another's `Awake`. The first `OnEnable` is dropped. Nothing runs on an object until it is first
  active, unless another script's reference force-loads it (fields only, no `Awake`/`Start`).
- Forwarded: `Awake`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`, `OnEnable`, `OnDisable`,
  `OnDestroy`, `OnTrigger*(Collider)`, `OnCollision*(Collision)`, `OnRenderObject`,
  `OnWillRenderObject`. Nothing else.

**Cost and budget**

- Every native call allocates and goes through reflection; cache `transform`, hoist native calls.
- One wall-clock budget per frame per box (default 500 ms, at most 1 s), shared by every script;
  overrun disables whichever script is running.
- 1024 stack slots per top-level entry; deep recursion (about 35 to 140 frames) crashes.

## The allow-list

Code, not an asset: `BC:10-318` (types), `BC:320-399` (fields), `BC:401-540` (per-type method
lists), `BS:12-627` (world additions). A type is refused unless listed (one `*` wildcard per name);
fields are refused unless `Type.field` is listed; a listed type allows all its methods unless it has
a method list. Hard refusals (`BC:595-642`): `*Invoke*`, `Application.OpenURL`/`Quit`/`Load*`,
`GameObject.AddComponent`, `SendMessage`, `BroadcastMessage`, `Animator.GetBehaviour(s)`.
`Instantiate` is redirected through a sanitising shim; `Debug` and `VideoPlayer` are swapped for
shims. The world box's `GameObject` allows a listed set (`BS:263-284`); `new GameObject()`,
`GameObject.Find` and `CreatePrimitive` are refused. Only `System.Exception` among exception types.

## Basis APIs reachable from a world script

- **Local player:** `BasisLocalPlayer.Instance`, `GetPositionAndRotation`, `Teleport(pos, rot,
  bypassStand, BasisTeleportMode)` (`WorldRoot`, `WorldFeet`, `FacePoint`, `ToPlayer`), `Respawn`,
  `Immobilize`, movement speed, jump height and gravity accessors, keyed locomotion overrides.
- **Players:** `Basis.Shims.BasisPlayersShim` (`Count`, `All`, `Local`, `GetById`, `GetByName`,
  `Nearest`, `Within`, pose and id extensions). Empty without a server.
- **Network:** `SafeUtil.MakeNetworkable(this)` returns the GameObject's `BasisNetworkShim`, one per
  GameObject and shared by every script on it (**measured**). Events `NetworkReady`,
  `NetworkMessageReceived(ushort, byte[], DeliveryMethod)`, `OwnershipTransfer`, `PlayerJoined`,
  `PlayerLeft`; `SendCustomNetworkEvent(byte[], DeliveryMethod, ushort[] recipients)` (null means
  everyone but the sender), `TakeOwnership`, `IsLocalOwner`, `RequestOwnershipIfNone`; fields
  `CurrentOwnerId`, `IsOwnedLocallyOnServer`, `HasNetworkID`. Ownership is per network component;
  the server gives an unowned object to the first client that asks and moves it to the
  longest-connected player when the owner leaves. Without a server nothing fires, `HasNetworkID` is
  false and sends only log an error.
- **Buttons:** `BasisInteractableButton.ButtonDown`/`ButtonUp` (`+=` a script method). **Pickups:**
  `BasisPickupInteractable.OnInteractStartEvent`/`OnInteractEndEvent`/`OnPickupUse` (`AddListener`);
  its methods are blocked, `enabled` works. **Seats:** `BasisSeat` (poll). **Mirror:**
  `BasisSDKMirror`.
- **Not reachable:** URP `Volume`, any respawn or teleport event, persistent storage, controller or
  mouse input other than `Keyboard`.
- **Triggers:** the local player is one CharacterController on the `LocalPlayer` root, layer 3,
  colliding with Default; identify it with `other.gameObject ==
  BasisLocalPlayer.Instance.gameObject`. Remote players have no colliders.

Statements in Basis's own API catalog found wrong: `LateUpdate` is forwarded; there is no
`BasisTeleportMode.Instant`; `GetComponent<BasisNetworkShim>()` does not add the shim; `UnityEvent`
fields take `AddListener`, not `+=`.

## Existing Udon traces in Basis

No Udon shims or converter exist in Basis or its docs. `com.basis.pooltable/` is an UdonSharp
billiards table ported by hand to native `BasisNetworkBehaviour`. `com.llealloo.audiolink/` wraps
its UdonSharp scripts in `#if UDONSHARP`.
