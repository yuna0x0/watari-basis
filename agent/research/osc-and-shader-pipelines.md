# OSC and render pipeline conversion: what Basis and the shaders already do

Read on 2026-09-18 from the Basis clone at upstream 33fc043c1, the HVR comms package in it,
lilToon 2.3.4, UniVRM 0.131.2, the Poiyomi repository at tag v10.0.21, and the SCSS
repository. Two questions: whether Watari should implement OSC, and whether it should convert
materials from Built-in Render Pipeline shaders to their URP counterparts.

## OSC

### What Basis has

Basis has an OSC server. It is owned by the HVR comms package, not by the framework:

- `OSCAcquisitionServer` listens on UDP 9000, sends to 9001, and runs an OSCQuery
  responder. It sends `/avatar/change` with an `avtr_` id on avatar load, which is what
  VRCFaceTracking waits for. `BasisSettingsDefaults.EnableOSC` is the master switch, default on.
- `BasisOscService.ResolveAddressId` strips `/avatar/parameters/` from an incoming address and
  turns the rest into an `HVRAddress` id. So `/avatar/parameters/Foo` is the address `Foo`.
- `OSCAcquisition` submits every received value into the scene `AcquisitionService`'s
  variable store. A wearer's `HVRVixxyControl` reads that same store (its `_variableStore` is
  the scene store when the avatar is worn), and `HVRVariableNetworking` registers on it, so a
  value that arrives over OSC drives a Vixxy control and replicates.
- Only float arguments are accepted. `TryResolveAddressValue` returns false unless
  `arguments[0] is float`; the parser does produce `bool` and `int` for `T`, `F` and `i`, and
  those messages are dropped. VRChat OSC apps send bool parameters as `T`/`F`.
- `OSCAcquisition` is only created by `AutomaticFaceTracking` (`CreateOSCAcquisitionIfNotExists`)
  or placed by hand. Nothing in the SDK adds either to an avatar. An avatar without one of them
  receives no OSC.
- Haï's docs describe OSC for face tracking only. The Vixxy address path is in code, not in docs.

### Where Watari stands

Watari writes Vixxy controls without an address. `HVRAddressSelector.path` is left empty, so
`CalculateAddress` generates `<path>@<sha1>+<index>` from the control's object path. The VRChat
parameter name is in the plan (`VixxyControlPlan.Parameter`) and is not written anywhere.

Consequence: a VRChat OSC app that sends `/avatar/parameters/<name>` cannot reach a converted
control today, even with face tracking on the avatar, because the address does not match.

### What implementing OSC would mean

There is no OSC to implement in Watari. Basis owns the transport. The conversion-side work is
one field: write the source parameter name into `address.path` on each control. Then:

- VRCFaceTracking already works through Basis's face tracking and is unrelated to Watari.
- Float parameters (radial puppets, sliders) driven by an external app work unchanged.
- Bool and int parameters do not, because Basis drops those argument types. That is a Basis
  limit, and worth reporting upstream before anyone relies on it.
- The avatar still needs `AutomaticFaceTracking` or `OSCAcquisition` on it, which is Basis
  setup, not conversion.

This is the "stable Vixxy address" item from the 2026-09-14 backlog under another name. Two
checks before writing it: address collisions when two menu entries share one parameter (14
controllers in the reference content), and what `HVRAddress.IsSystemAddressName` reserves.

## Render pipelines

Basis is URP. `GraphicsSettings.asset` points at a Universal pipeline asset and the package is
embedded. A material whose shader is not URP goes through `BasisShaderFallback.MaterialCorrection`
at load: `!shader.isSupported` or an `InternalErrorShader` name is replaced by a new material on
`Universal Render Pipeline/Lit` carrying the first albedo, colour, normal, metallic and occlusion
texture it can find under two dozen common property names, with metallic and smoothness set to
0.2. A user blocklist by shader name or keyword forces the same fallback. Bundle builds strip
variants (`BasisBundleShaderStripper`) but do not check pipelines.

Basis's own shader page (`docs.basisvr.org/en/docs/avatar/shaders`) lists URP/Lit, Poiyomi
(free, "still in beta"), Silent Cel Shading, lilToon, MToon and Sakura. Its Poiyomi instructions
are manual: get the URP release from the Poiyomi Discord, then "select each material in your
avatar's folder and change its shader to use Poiyomi Pro URP".

### Per shader

| shader | URP delivery | material change needed |
|---|---|---|
| lilToon 2.3.4 | same shader assets; `lilShaderContainerImporter` generates the shader for the detected pipeline and `lilStartup` rewrites on a pipeline switch | none |
| MToon10 (UniVRM 0.131.2) | separate `VRM10/Universal Render Pipeline/MToon10`; `Vrm10MaterialDescriptorGeneratorUtility` picks the URP importer from `RenderPipelineUtility.GetRenderPipelineType()` at import | none for a `.vrm` imported in the Basis project; a VRM prefab copied from a BRP project keeps the BRP shader |
| MToon (VRM 0.x) | no URP shader; `UrpVrmMaterialDescriptorGenerator` imports MToon materials as URP unlit ("mtoon URP shader is not ready") | none, but the toon look is lost |
| SCSS | URP `SubShader` in the same file (`"RenderPipeline" = "UniversalPipeline"`) | none |
| Poiyomi 10.0.21 | separate shaders with separate guids: `.poiyomi/Poiyomi Toon URP`, Pro URP and variants; a separate `Poi.Toon.URP` unitypackage "for Universal Render Pipeline in BasisVR"; Unity 6 only | yes, per material |
| Sakura | listed as URP by Basis; subscription, not checked | not checked |

Poiyomi's own editor does part of the switch. `ErrorShaderEditor`, its inspector for a material
whose shader is missing, offers "Switch to latest Toon" and appends `URP` when
`PoiHelpers.IsURP()` is true. The lilToon and Standard translators do the same. Two gaps:

- It is one click per material, with no batch path.
- A locked material (Thry optimizer, `_ShaderOptimizerEnabled`) gets "Unlock Material", which
  switches to the saved original BRP shader name, not the URP one. Pro locks additionally
  require Pro in the project. Whether the `Poi.Toon.URP` package carries the BRP shaders at
  all is not checked; the 10.0.20 changelog says "URP projects now ignore BIRP shaders on
  export".

### What a bulk remap would be

Find materials whose shader name, asset path or retained `POIYOMI` keywords identify Poiyomi,
find an installed Poiyomi shader tagged for URP, and re-point each `.mat` asset to it, keeping
the serialized floats, colours, textures, UV transforms, keywords and render queue. No shader
code is converted. It is the per-material switch Poiyomi's own inspector offers, done for every
material at once. It edits material assets rather than components on the avatar, so Undo covers
it and every object sharing a material sees the change.

### Measured demand

Materials by shader, counted from `.mat` files:

| project | materials | lilToon | Poiyomi | other |
|---|---|---|---|---|
| user's Basis content | 221 | 219 | 0 | 2 built-in |
| reference VRChat project | 977 | 468 | 83 | 426 |

All 83 Poiyomi materials are locked (`Hidden/Locked/.poiyomi/...`), generated per avatar and
shipped with purchased avatars; the Poiyomi package itself is not installed there. Every one of
them needs unlock plus a Pro or Toon URP shader before it renders on Basis.

## Assessment

**OSC:** not a feature to build. Writing the parameter name into the Vixxy address is a small,
in-scope change (data on a component) that makes float parameters reachable from OSC apps. Bool
and int parameters stay unreachable until Basis accepts those types.

**Shader conversion:** lilToon, SCSS and MToon10 need nothing from Watari. Poiyomi needs a
material remap that Poiyomi provides one material at a time and a tool could do in bulk. It is
practical, it is a `.mat` asset edit rather than a component on the avatar, and it depends on a
Unity 6-only package that Basis's docs still call beta. The scope line in AGENTS.md says a
conversion does not touch materials, and `docs/docs/limitations.md` says so to users. Taking it
on is a scope decision, not a technical one; what the measurement says is that the user's own
content does not need it and the reference content needs it for 83 locked materials that
Watari cannot unlock.
