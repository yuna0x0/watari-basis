using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>
    /// A jiggle parameter together with its falloff curve. Jiggle evaluates these as
    /// <c>value * curve.Evaluate(t)</c> over normalized distance from the chain root, which is
    /// exactly what VRChat does with its PhysBone curves, so a curve carries across unchanged.
    /// </summary>
    public readonly struct JiggleCurvedFloatPlan
    {
        public readonly float Value;
        public readonly AnimationCurve Curve;

        public JiggleCurvedFloatPlan(float value, AnimationCurve curve = null)
        {
            Value = value;
            Curve = curve != null && curve.length > 0 ? curve : null;
        }

        public bool CurveEnabled => Curve != null;
    }

    /// <summary>
    /// The jiggle parameters a conversion determined. A null field means "leave whatever the
    /// chosen preset already has", so the emitter only overwrites what the source data actually
    /// determines and everything else keeps values tuned by the jiggle author.
    /// </summary>
    public sealed class JiggleParameterPlan
    {
        public JiggleCurvedFloatPlan? Stiffness;
        public JiggleCurvedFloatPlan? AngleLimit;
        public JiggleCurvedFloatPlan? Stretch;
        public JiggleCurvedFloatPlan? Drag;
        public JiggleCurvedFloatPlan? AirDrag;
        public JiggleCurvedFloatPlan? Gravity;
        public JiggleCurvedFloatPlan? CollisionRadius;

        public float? Soften;
        public float? AngleLimitSoften;
        public float? RootStretch;
        public float? IgnoreRootMotion;

        public bool? CollisionToggle;
        public bool? AngleLimitToggle;

        /// <summary>
        /// Jiggle ignores stretch, collisionRadius, ignoreRootMotion, soften and rootStretch
        /// entirely unless this is on, so anything writing those must also set it.
        /// </summary>
        public bool AdvancedToggle = true;
    }

    public enum JiggleColliderShape
    {
        Sphere = 0,
        Capsule = 1,
        Plane = 2,
    }

    public enum JiggleCapsuleAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    /// <summary>
    /// One collider a rig should be affected by. Mirrors jiggle's serialized collider rather
    /// than referencing its types, so the model stays independent of the jiggle package.
    /// </summary>
    public sealed class JiggleColliderPlan
    {
        public long SourceDocumentFileId;

        /// <summary>Transform the collider is placed on. 0 means the source component's own.</summary>
        public long TransformFileId;

        public JiggleColliderShape Shape;
        public float Radius;
        public float Height;
        public JiggleCapsuleAxis CapsuleAxis = JiggleCapsuleAxis.Y;
        public Vector3 LocalOffset;

        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
    }

    public enum JigglePreset
    {
        Hair = 0,
        Tail = 1,
        Breasts = 2,
        Rope = 3,
    }

    /// <summary>
    /// Everything needed to produce one JiggleRig, still expressed in file identifiers rather
    /// than live objects so it can be produced and tested without a scene.
    /// </summary>
    public sealed class JiggleRigPlan
    {
        public long SourcePhysBoneDocumentFileId;

        /// <summary>Resolved separately; 0 means "the PhysBone's own GameObject".</summary>
        public long RootBoneFileId;

        public List<long> ExcludedTransformFileIds = new List<long>();
        public List<long> ColliderSourceFileIds = new List<long>();
        public List<JiggleColliderPlan> Colliders = new List<JiggleColliderPlan>();

        public bool ExcludeRoot;
        public bool LockFromGrabbing;

        public JigglePreset Preset = JigglePreset.Hair;
        public JiggleParameterPlan Parameters = new JiggleParameterPlan();
        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
    }
}
