using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    public enum PhysBoneIntegrationType
    {
        Simplified = 0,
        Advanced = 1,
    }

    public enum PhysBoneMultiChildType
    {
        Ignore = 0,
        First = 1,
        Average = 2,
    }

    public enum PhysBoneLimitType
    {
        None = 0,
        Angle = 1,
        Hinge = 2,
        Polar = 3,
    }

    public enum PhysBoneImmobileType
    {
        AllMotion = 0,
        WorldMotion = 1,
    }

    public enum PhysBoneColliderShape
    {
        Sphere = 0,
        Capsule = 1,
        Plane = 2,
    }

    /// <summary>
    /// A PhysBone value together with its falloff curve along the bone chain.
    /// <para>
    /// VRChat evaluates the curve over normalized distance from the chain root and scales the
    /// base value by it. An absent or empty curve means no falloff, so the base value applies
    /// along the whole chain. Reading an empty curve as zero instead would silently flatten
    /// every converted rig, so <see cref="HasCurve"/> is explicit.
    /// </para>
    /// </summary>
    public readonly struct PhysBoneCurvedFloat
    {
        public readonly float Value;
        public readonly AnimationCurve Curve;

        public PhysBoneCurvedFloat(float value, AnimationCurve curve = null)
        {
            Value = value;
            Curve = curve != null && curve.length > 0 ? curve : null;
        }

        public bool HasCurve => Curve != null;

        public float Evaluate(float normalizedDistanceFromRoot)
        {
            return HasCurve ? Value * Curve.Evaluate(normalizedDistanceFromRoot) : Value;
        }

        public override string ToString()
        {
            return HasCurve ? $"{Value} (curved, {Curve.length} keys)" : Value.ToString();
        }
    }

    /// <summary>
    /// One VRCPhysBone, read out of prefab YAML. Transform references are kept as the file
    /// identifiers they were, and resolved to live objects separately, so this stays free of
    /// scene state and can be unit tested.
    /// </summary>
    public sealed class PhysBoneData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;

        /// <summary>0 is PhysBone 1.0, 1 is 1.1. Spring behaviour differs between them.</summary>
        public int Version;
        public PhysBoneIntegrationType IntegrationType;

        public long RootTransformFileId;
        public List<long> IgnoreTransformFileIds = new List<long>();
        public Vector3 EndpointPosition;
        public PhysBoneMultiChildType MultiChildType;

        public PhysBoneCurvedFloat Pull;
        public PhysBoneCurvedFloat Spring;
        public PhysBoneCurvedFloat Stiffness;
        public PhysBoneCurvedFloat Gravity;
        public PhysBoneCurvedFloat GravityFalloff;

        public PhysBoneImmobileType ImmobileType;
        public PhysBoneCurvedFloat Immobile;

        public bool AllowCollision;

        /// <summary>Allow Collision was Other: decided per player by a filter.</summary>
        public bool AllowCollisionFiltered;
        public PhysBoneCurvedFloat Radius;
        public List<long> ColliderFileIds = new List<long>();

        public PhysBoneLimitType LimitType;
        public PhysBoneCurvedFloat MaxAngleX;
        public PhysBoneCurvedFloat MaxAngleZ;
        public Vector3 LimitRotation;

        public bool AllowGrabbing;

        /// <summary>Allow Grabbing was Other: decided per player by a filter.</summary>
        public bool AllowGrabbingFiltered;
        public bool AllowPosing;
        public bool SnapToHand;

        public PhysBoneCurvedFloat MaxStretch;
        public PhysBoneCurvedFloat MaxSquish;
        public PhysBoneCurvedFloat StretchMotion;

        public bool IsAnimated;
        public string Parameter;
    }

    /// <summary>One VRCPhysBoneCollider, read out of prefab YAML.</summary>
    public sealed class PhysBoneColliderData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;
        public long RootTransformFileId;

        public PhysBoneColliderShape ShapeType;

        /// <summary>Keeps bones inside the collider rather than outside. No jiggle equivalent.</summary>
        public bool InsideBounds;

        public float Radius;
        public float Height;
        public Vector3 Position;
        public Quaternion Rotation;

        /// <summary>Treats each bone as a sphere rather than a chain. No jiggle equivalent.</summary>
        public bool BonesAsSpheres;

        /// <summary>
        /// Offered to every PhysBone in reach, other avatars' included, rather than only to the
        /// ones that list it. SDK 3.10.4. Basis makes only the body's own colliders global.
        /// </summary>
        public bool GlobalCollision;
    }
}
