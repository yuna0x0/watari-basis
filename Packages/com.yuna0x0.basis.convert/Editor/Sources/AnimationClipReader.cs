using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Sources
{
    public sealed class BlendShapeEffect
    {
        /// <summary>Transform path of the renderer, relative to the avatar root.</summary>
        public string Path = string.Empty;

        public string ShapeName = string.Empty;
        public float Value;
    }

    /// <summary>One material property a clip sets, on one renderer.</summary>
    public sealed class MaterialPropertyEffect
    {
        /// <summary>Transform path of the renderer, relative to the avatar root.</summary>
        public string Path = string.Empty;

        /// <summary>Full name of the renderer type the binding names.</summary>
        public string RendererTypeName = string.Empty;

        /// <summary>Shader property, without the `material.` prefix or the channel suffix.</summary>
        public string PropertyName = string.Empty;

        /// <summary>
        /// Channel index for a colour or vector property, 0 to 3, or -1 for a plain float.
        /// A clip may set only some of a property's channels; the rest keep what the material
        /// was authored with.
        /// </summary>
        public int Channel = -1;

        /// <summary>True when the channel was named r, g, b or a rather than x, y, z or w.</summary>
        public bool ColourChannel;

        public float Value;
    }

    /// <summary>What a clip does, reduced to the states Vixxy can hold.</summary>
    public sealed class ClipEffects
    {
        /// <summary>Transform paths the clip switches on, by driving m_IsActive to 1.</summary>
        public List<string> Activated = new List<string>();

        /// <summary>Transform paths the clip switches off.</summary>
        public List<string> Deactivated = new List<string>();

        public List<BlendShapeEffect> BlendShapes = new List<BlendShapeEffect>();

        public List<MaterialPropertyEffect> MaterialProperties =
            new List<MaterialPropertyEffect>();

        /// <summary>
        /// Curves driving something other than object activity, a blendshape or a material
        /// property: transforms, component fields, anything else. Counted so a conversion can
        /// say the clip did more than came across.
        /// </summary>
        public int OtherCurves;

        /// <summary>Curves whose value changes over time rather than holding one value.</summary>
        public int AnimatedCurves;

        /// <summary>
        /// Transform paths whose rotation the clip turns over time. This is animation rather than
        /// a state, so it belongs in authored motion; a Vixxy control cannot hold it.
        /// </summary>
        public List<string> AnimatedRotationPaths = new List<string>();

        /// <summary>
        /// How many of <see cref="AnimatedCurves"/> are those rotations. A turning transform is
        /// three or four curves depending on whether the clip was authored with euler or
        /// quaternion keys, so this is not the path count.
        /// </summary>
        public int AnimatedRotationCurves;

        public bool IsEmpty =>
            Activated.Count == 0 && Deactivated.Count == 0 && BlendShapes.Count == 0
            && MaterialProperties.Count == 0;
    }

    /// <summary>
    /// Reduces an AnimationClip to the handful of things a Vixxy control can express: which
    /// objects are on, which are off, and what blendshapes are set to.
    /// <para>
    /// Vixxy holds a value per choice rather than a curve, so only clips that hold a constant
    /// carry across. A clip whose value moves over time is counted separately: that is animation,
    /// and it belongs in Authored Motion if anywhere.
    /// </para>
    /// </summary>
    public static class AnimationClipReader
    {
        private const string BlendShapePrefix = "blendShape.";
        private const string MaterialPrefix = "material.";

        public static ClipEffects Read(AnimationClip clip)
        {
            ClipEffects effects = new ClipEffects();
            if (clip == null)
            {
                return effects;
            }

            // Material swaps and other object references are curves too, of a kind a Vixxy
            // control cannot hold. GetCurveBindings does not list them.
            effects.OtherCurves += AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                if (!TryGetConstant(curve, out float value))
                {
                    effects.AnimatedCurves++;

                    if (IsRotation(binding))
                    {
                        effects.AnimatedRotationCurves++;
                        if (!effects.AnimatedRotationPaths.Contains(binding.path))
                        {
                            effects.AnimatedRotationPaths.Add(binding.path);
                        }
                    }

                    continue;
                }

                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                {
                    if (value >= 0.5f)
                    {
                        effects.Activated.Add(binding.path);
                    }
                    else
                    {
                        effects.Deactivated.Add(binding.path);
                    }

                    continue;
                }

                if (binding.propertyName.StartsWith(BlendShapePrefix))
                {
                    effects.BlendShapes.Add(new BlendShapeEffect
                    {
                        Path = binding.path,
                        ShapeName = binding.propertyName.Substring(BlendShapePrefix.Length),
                        Value = value,
                    });
                    continue;
                }

                if (TryReadMaterialProperty(binding, value, out MaterialPropertyEffect material))
                {
                    effects.MaterialProperties.Add(material);
                    continue;
                }

                effects.OtherCurves++;
            }

            return effects;
        }

        /// <summary>
        /// A binding turning a transform, the one thing authored motion replays.
        /// <para>
        /// Both spellings appear: a clip authored with quaternion keys writes `m_LocalRotation`,
        /// one authored with euler keys writes `localEulerAngles`. Basis bakes either, so both
        /// count here.
        /// </para>
        /// </summary>
        private static bool IsRotation(EditorCurveBinding binding)
        {
            return binding.type == typeof(Transform)
                && (binding.propertyName.StartsWith("m_LocalRotation")
                    || binding.propertyName.StartsWith("localEulerAngles"));
        }

        /// <summary>
        /// A binding of the form `material._Name`, or `material._Name.r` for one channel of a
        /// colour or vector.
        /// <para>
        /// Vixxy applies these through a MaterialPropertyBlock, which only renderers have, so a
        /// binding naming anything else is not one of these however it is spelled.
        /// </para>
        /// </summary>
        private static bool TryReadMaterialProperty(
            EditorCurveBinding binding, float value, out MaterialPropertyEffect effect)
        {
            effect = null;

            if (binding.type == null || !typeof(Renderer).IsAssignableFrom(binding.type)
                || !binding.propertyName.StartsWith(MaterialPrefix))
            {
                return false;
            }

            string name = binding.propertyName.Substring(MaterialPrefix.Length);
            int channel = -1;
            bool colour = false;

            if (name.Length > 2 && name[name.Length - 2] == '.')
            {
                int index = "xyzw".IndexOf(name[name.Length - 1]);
                if (index < 0)
                {
                    index = "rgba".IndexOf(name[name.Length - 1]);
                    colour = index >= 0;
                }

                if (index < 0)
                {
                    return false;
                }

                channel = index;
                name = name.Substring(0, name.Length - 2);
            }

            if (name.Length == 0)
            {
                return false;
            }

            effect = new MaterialPropertyEffect
            {
                Path = binding.path,
                RendererTypeName = binding.type.FullName,
                PropertyName = name,
                Channel = channel,
                ColourChannel = colour,
                Value = value,
            };

            return true;
        }

        /// <summary>
        /// True when every key holds the same value, as a toggle clip does.
        /// </summary>
        private static bool TryGetConstant(AnimationCurve curve, out float value)
        {
            value = curve.keys[0].value;

            for (int i = 1; i < curve.length; i++)
            {
                if (!Mathf.Approximately(curve.keys[i].value, value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
