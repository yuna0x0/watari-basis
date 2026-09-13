using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>One object a control switches, and its state in each choice.</summary>
    public sealed class VixxyActivationPlan
    {
        /// <summary>Transform path relative to the avatar root.</summary>
        public string Path = string.Empty;

        /// <summary>
        /// Index into the control's <see cref="VixxyControlPlan.Motions"/> when this activation
        /// switches an authored motion rather than an object, or -1 when it switches an object.
        /// <para>
        /// A Vixxy activation holds a Component, not a GameObject, and `BasisAuthoredMotion` is
        /// one of the types it is permitted to toggle, so a motion is switched the same way an
        /// object is: by the component it lives on rather than by a path.
        /// </para>
        /// </summary>
        public int MotionIndex = -1;

        /// <summary>
        /// Active state per choice. A toggle has two, off first; a selector has one per value of
        /// its parameter, in the order the menu offers them.
        /// </summary>
        public bool[] Choices = new bool[2];

        /// <summary>
        /// Whether each choice's clip actually animated this object. A choice that did not
        /// leaves it at whatever the avatar was authored with, read from the hierarchy later.
        /// This has to be recorded rather than inferred from the values: a clip that switches an
        /// object off looks exactly like a clip that said nothing about it.
        /// </summary>
        public bool[] Set = {true, true};

        public bool AllChoicesSet
        {
            get
            {
                foreach (bool set in Set)
                {
                    if (!set)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>Motion a control switches on, and which of its choices plays it.</summary>
    public sealed class VixxyMotionPlan
    {
        public AuthoredMotionPlan Motion;

        /// <summary>Index of the choice whose clip this was baked from.</summary>
        public int Choice;
    }

    /// <summary>One blendshape a control sets, and its value in each choice.</summary>
    public sealed class VixxyBlendShapePlan
    {
        public string ShapeName = string.Empty;

        /// <summary>Weight per choice, in the same order as the control's choices.</summary>
        public float[] Choices = new float[2];

        /// <summary>Whether each choice's clip actually set this shape.</summary>
        public bool[] Set = {true, true};

        public bool AllChoicesSet
        {
            get
            {
                foreach (bool set in Set)
                {
                    if (!set)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>How a material property is held, which decides the Vixxy property type.</summary>
    public enum VixxyMaterialPropertyKind
    {
        Float,
        Colour,
        Vector,
    }

    /// <summary>One material property a control sets, and its value in each choice.</summary>
    public sealed class VixxyMaterialPropertyPlan
    {
        /// <summary>Shader property name, as the material declares it.</summary>
        public string PropertyName = string.Empty;

        public VixxyMaterialPropertyKind Kind = VixxyMaterialPropertyKind.Float;

        /// <summary>Value per choice, in the control's choice order. A float uses x only.</summary>
        public Vector4[] Choices = new Vector4[2];

        /// <summary>
        /// Per choice, per channel, whether that clip set it. A clip commonly sets one channel of
        /// a colour, or says nothing in one choice; the rest keep what the material was authored
        /// with, read from the renderer rather than guessed.
        /// </summary>
        public bool[][] Set = {new bool[4], new bool[4]};

        public int Channels => Kind == VixxyMaterialPropertyKind.Float ? 1 : 4;
    }

    /// <summary>What a control sets on one renderer: blendshapes, material properties, or both.</summary>
    public sealed class VixxySubjectPlan
    {
        /// <summary>Transform path of the renderer, relative to the avatar root.</summary>
        public string Path = string.Empty;

        /// <summary>
        /// Full name of the renderer type the clip named. Vixxy resolves the component by this
        /// name, and is lenient between the two renderer types for material properties.
        /// </summary>
        public string RendererTypeName = typeof(SkinnedMeshRenderer).FullName;

        public List<VixxyBlendShapePlan> BlendShapes = new List<VixxyBlendShapePlan>();

        public List<VixxyMaterialPropertyPlan> MaterialProperties =
            new List<VixxyMaterialPropertyPlan>();

        public bool IsEmpty => BlendShapes.Count == 0 && MaterialProperties.Count == 0;
    }

    /// <summary>
    /// One HVR Vixxy control to create, rebuilt from a VRChat menu toggle.
    /// <para>
    /// Vixxy stores object switching as activations holding the object's Transform, with a bool
    /// per choice, rather than as animation. That is why only clips holding a constant can be
    /// rebuilt: there is nowhere for a curve to go.
    /// </para>
    /// </summary>
    public sealed class VixxyControlPlan
    {
        public string MenuName = string.Empty;
        public string Parameter = string.Empty;

        /// <summary>
        /// What each choice is called, in the order the control offers them. A toggle has two,
        /// off first; a selector is named by the menu entry that picks each value.
        /// </summary>
        public List<string> ChoiceNames = new List<string>();

        /// <summary>The parameter value each choice corresponds to.</summary>
        public List<int> ChoiceValues = new List<int>();

        public int ChoiceCount => ChoiceNames.Count > 0 ? ChoiceNames.Count : 2;

        /// <summary>
        /// True when the control is continuous. Vixxy shows those as a slider and interpolates
        /// between the choices rather than snapping between them.
        /// </summary>
        public bool IsSlider;

        /// <summary>
        /// The value the control starts at, in the same space as the choices' own values, which
        /// is what Vixxy compares against. This is the avatar's declared default for the
        /// parameter behind the toggle, so a conversion starts where the avatar was authored.
        /// </summary>
        public float DefaultValue;

        /// <summary>Whether the value persists across sessions. Vixxy's default is to remember.</summary>
        public bool Saved = true;

        /// <summary>Whether the value is sent to other players. Vixxy's default is networked.</summary>
        public bool NetworkSynced = true;

        public List<VixxyActivationPlan> Activations = new List<VixxyActivationPlan>();
        public List<VixxySubjectPlan> Subjects = new List<VixxySubjectPlan>();

        /// <summary>
        /// Motion this control switches on. A toggle whose clip animates over time cannot be
        /// held as a value per choice, so the animation becomes an authored motion and the
        /// control enables and disables it.
        /// </summary>
        public List<VixxyMotionPlan> Motions = new List<VixxyMotionPlan>();

        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
    }
}
