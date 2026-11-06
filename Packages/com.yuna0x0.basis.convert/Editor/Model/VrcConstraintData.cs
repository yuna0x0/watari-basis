using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    public enum VrcConstraintKind
    {
        Position,
        Rotation,
        Scale,
        Parent,
        Aim,
        LookAt,
    }

    /// <summary>
    /// How a constraint resolves its up direction. Matches Basis's
    /// <c>BasisConstraintWorldUp</c> value for value.
    /// </summary>
    public enum VrcConstraintWorldUp
    {
        SceneUp = 0,
        ObjectUp = 1,
        ObjectRotationUp = 2,
        Vector = 3,
        None = 4,
    }

    public sealed class VrcConstraintSource
    {
        public long SourceTransformFileId;
        public float Weight = 1f;
        public Vector3 ParentPositionOffset;
        public Vector3 ParentRotationOffset;
    }

    /// <summary>
    /// One VRChat constraint, read out of prefab YAML. Covers all six types; the fields a given
    /// type does not use are left at their defaults.
    /// </summary>
    public sealed class VrcConstraintData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;
        public VrcConstraintKind Kind;

        public bool IsActive = true;
        public float GlobalWeight = 1f;
        public bool Locked = true;

        /// <summary>
        /// The transform this constraint drives. VRChat allows this to be something other than
        /// the object the component sits on; 0 means the object itself.
        /// </summary>
        public long TargetTransformFileId;

        /// <summary>VRChat-only. No Unity or Basis equivalent.</summary>
        public bool SolveInLocalSpace;

        /// <summary>VRChat-only. No Unity or Basis equivalent.</summary>
        public bool FreezeToWorld;

        /// <summary>SDK 3.10.6 and later. Zero snaps to the sources. No Basis equivalent.</summary>
        public float SmoothingFactor;

        public List<VrcConstraintSource> Sources = new List<VrcConstraintSource>();

        /// <summary>
        /// How many sources the constraint says it has. VRChat serializes the first sixteen as
        /// numbered slots and the rest in an overflow list, so this can exceed what was read.
        /// </summary>
        public int DeclaredSourceCount;

        public Vector3 PositionAtRest;
        public Vector3 PositionOffset;
        public bool AffectsPositionX = true;
        public bool AffectsPositionY = true;
        public bool AffectsPositionZ = true;

        public Vector3 RotationAtRest;
        public Vector3 RotationOffset;
        public bool AffectsRotationX = true;
        public bool AffectsRotationY = true;
        public bool AffectsRotationZ = true;

        public Vector3 ScaleAtRest = Vector3.one;
        public Vector3 ScaleOffset = Vector3.one;
        public bool AffectsScaleX = true;
        public bool AffectsScaleY = true;
        public bool AffectsScaleZ = true;

        public Vector3 AimAxis = Vector3.forward;
        public Vector3 UpAxis = Vector3.up;
        public VrcConstraintWorldUp WorldUp = VrcConstraintWorldUp.SceneUp;
        public Vector3 WorldUpVector = Vector3.up;
        public long WorldUpTransformFileId;

        public float Roll;
        public bool UseUpTransform;
    }
}
