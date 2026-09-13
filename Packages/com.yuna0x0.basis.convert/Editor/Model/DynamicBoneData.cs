using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    public enum DynamicBoneFreezeAxis
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 3,
    }

    public enum DynamicBoneColliderDirection
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    public enum DynamicBoneColliderBound
    {
        Outside = 0,
        Inside = 1,
    }

    /// <summary>
    /// One Dynamic Bone, read out of prefab YAML.
    /// <para>
    /// Its distribution curves are evaluated along the chain the same way VRChat's and jiggle's
    /// are, so they are kept alongside their values rather than flattened.
    /// </para>
    /// </summary>
    public sealed class DynamicBoneData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;

        /// <summary>
        /// Single chain root. Dynamic Bone simulates nothing when neither this nor
        /// <see cref="RootFileIds"/> is set.
        /// </summary>
        public long RootFileId;

        /// <summary>
        /// Extra chain roots. One component can drive several independent chains, which becomes
        /// one jiggle rig each.
        /// </summary>
        public List<long> RootFileIds = new List<long>();

        public PhysBoneCurvedFloat Damping = new PhysBoneCurvedFloat(0.1f);
        public PhysBoneCurvedFloat Elasticity = new PhysBoneCurvedFloat(0.1f);
        public PhysBoneCurvedFloat Stiffness = new PhysBoneCurvedFloat(0.1f);
        public PhysBoneCurvedFloat Inert = new PhysBoneCurvedFloat(0f);
        public PhysBoneCurvedFloat Friction = new PhysBoneCurvedFloat(0f);
        public PhysBoneCurvedFloat Radius = new PhysBoneCurvedFloat(0f);

        /// <summary>
        /// Ticks per second. Dynamic Bone scales elasticity and force by this over 60, so a
        /// component at 120 returns to its pose twice as fast as one at 60.
        /// </summary>
        public float UpdateRate = 60f;

        public float EndLength;
        public Vector3 EndOffset;
        public Vector3 Gravity;
        public Vector3 Force;
        public float BlendWeight = 1f;

        public List<long> ColliderFileIds = new List<long>();
        public List<long> ExclusionFileIds = new List<long>();

        public DynamicBoneFreezeAxis FreezeAxis = DynamicBoneFreezeAxis.None;
    }

    /// <summary>One DynamicBoneCollider or DynamicBonePlaneCollider.</summary>
    public sealed class DynamicBoneColliderData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;

        public DynamicBoneColliderDirection Direction = DynamicBoneColliderDirection.Y;
        public Vector3 Center;
        public DynamicBoneColliderBound Bound = DynamicBoneColliderBound.Outside;

        public float Radius = 0.5f;
        public float Height;

        /// <summary>Second radius of a tapered capsule. Jiggle capsules do not taper.</summary>
        public float Radius2;

        /// <summary>True for DynamicBonePlaneCollider, which has no radius or height.</summary>
        public bool IsPlane;
    }
}
