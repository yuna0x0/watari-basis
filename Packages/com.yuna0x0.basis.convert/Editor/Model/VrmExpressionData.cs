using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>
    /// What an expression is for. VRM names a handful of roles the runtime drives itself, and
    /// Basis drives the same ones from its own avatar component, so those are not rebuilt as
    /// menu controls: a viseme belongs to lip sync rather than to a menu.
    /// </summary>
    public enum VrmExpressionRole
    {
        /// <summary>An expression the author added, and the reason a menu control exists.</summary>
        Custom = 0,

        /// <summary>Happy, angry, sad, relaxed, surprised, and 0.x's joy, sorrow and fun.</summary>
        Emotion = 1,

        /// <summary>The five lip sync shapes, which Basis drives from its viseme list.</summary>
        Viseme = 2,

        Blink = 3,
        LookAt = 4,

        /// <summary>The resting face, which is how the avatar already looks.</summary>
        Neutral = 5,
    }

    /// <summary>One blendshape an expression sets, and how far.</summary>
    /// <summary>
    /// Names UniVRM gives the VRM 0.x presets, in <c>BlendShapePreset</c> order. A 0.x clip is
    /// identified by its preset, and its free-text name may be anything.
    /// </summary>
    public static class Vrm0PresetNames
    {
        public static readonly string[] Names =
        {
            string.Empty, "Neutral", "A", "I", "U", "E", "O", "Blink", "Joy", "Angry", "Sorrow",
            "Fun", "LookUp", "LookDown", "LookLeft", "LookRight", "Blink_L", "Blink_R",
        };
    }

    public sealed class VrmMorphBinding
    {
        /// <summary>Transform path of the renderer, relative to the avatar root.</summary>
        public string Path = string.Empty;

        /// <summary>Index of the blendshape in the mesh, which is how VRM names it.</summary>
        public int Index;

        /// <summary>Weight on Unity's 0 to 100 scale.</summary>
        public float Weight;

        /// <summary>
        /// The blendshape's name, looked up on the mesh. VRM refers to a shape by index and
        /// Vixxy by name, so this is filled in once the renderer is known.
        /// </summary>
        public string ShapeName = string.Empty;
    }

    /// <summary>
    /// What an expression does to blink, gaze or lip sync while it is worn. VRM 1.0 only; the
    /// spec calls these overrideBlink, overrideLookAt and overrideMouth.
    /// </summary>
    public enum VrmExpressionOverride
    {
        None = 0,
        Block = 1,
        Blend = 2,
    }

    /// <summary>
    /// A material colour an expression sets. VRM 1.0 names one of six colour kinds and UniVRM
    /// maps each to an MToon property; VRM 0.x names the shader property itself. Both end up as
    /// a property name here.
    /// </summary>
    public sealed class VrmMaterialColorBinding
    {
        public string MaterialName = string.Empty;

        /// <summary>
        /// Shader property, as MToon declares it: _Color, _ShadeColor, _EmissionColor and so on.
        /// </summary>
        public string PropertyName = string.Empty;

        public Vector4 TargetValue;
    }

    /// <summary>
    /// A texture scale and offset an expression sets, on every UV texture of a material.
    /// </summary>
    public sealed class VrmMaterialUvBinding
    {
        public string MaterialName = string.Empty;
        public Vector2 Scaling = Vector2.one;
        public Vector2 Offset;

        /// <summary>The value Unity's _MainTex_ST holds: scale in xy, offset in zw.</summary>
        public Vector4 ScaleOffset => new Vector4(Scaling.x, Scaling.y, Offset.x, Offset.y);
    }

    /// <summary>
    /// A renderer that uses a material an expression names, by path under the avatar root.
    /// </summary>
    public sealed class VrmMaterialHost
    {
        public string Path = string.Empty;
        public string RendererTypeName = string.Empty;

        /// <summary>
        /// Materials on the same renderer other than this one. Vixxy sets a property for the
        /// whole renderer, so a host with any is left alone rather than changing them too.
        /// </summary>
        public int OtherMaterials;
    }

    /// <summary>VRM 1.0's material colour kinds, in the order UniVRM serializes them.</summary>
    public static class VrmMaterialProperties
    {
        public const string UvProperty = "_MainTex_ST";

        /// <summary>
        /// The MToon property each kind names, from UniVRM's MToon10Properties table.
        /// </summary>
        public static string NameOf(int bindType)
        {
            switch (bindType)
            {
                case 0: return "_Color";
                case 1: return "_EmissionColor";
                case 2: return "_ShadeColor";
                case 3: return "_MatcapColor";
                case 4: return "_RimColor";
                case 5: return "_OutlineColor";
                default: return string.Empty;
            }
        }
    }

    /// <summary>One VRM expression, from either format.</summary>
    public sealed class VrmExpressionData
    {
        /// <summary>
        /// The preset this expression fills, by UniVRM's name for it, or empty for a custom one.
        /// VRM 1.0 preset fields are named this way already; a 0.x clip's <c>Name</c> is free text.
        /// </summary>
        public string PresetName = string.Empty;

        public string Name = string.Empty;
        public VrmExpressionRole Role = VrmExpressionRole.Custom;

        /// <summary>Worn fully or not at all. Otherwise VRM lets it be worn at any strength.</summary>
        public bool IsBinary;

        public VrmExpressionOverride OverrideBlink = VrmExpressionOverride.None;
        public VrmExpressionOverride OverrideLookAt = VrmExpressionOverride.None;
        public VrmExpressionOverride OverrideMouth = VrmExpressionOverride.None;

        public bool HasOverride =>
            OverrideBlink != VrmExpressionOverride.None
            || OverrideLookAt != VrmExpressionOverride.None
            || OverrideMouth != VrmExpressionOverride.None;

        public List<VrmMorphBinding> Bindings = new List<VrmMorphBinding>();

        /// <summary>
        /// Material changes the expression also carries. VRM names the material; Vixxy acts on a
        /// renderer, so the planner finds the renderers that use each material.
        /// </summary>
        public List<VrmMaterialColorBinding> MaterialColorBindings =
            new List<VrmMaterialColorBinding>();

        public List<VrmMaterialUvBinding> MaterialUvBindings = new List<VrmMaterialUvBinding>();

        public int MaterialBindingCount => MaterialColorBindings.Count + MaterialUvBindings.Count;
    }
}
