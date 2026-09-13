using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>The shapes VRM 1.0 offers a spring bone to collide with.</summary>
    public enum VrmColliderType
    {
        Sphere = 0,
        Capsule = 1,
        Plane = 2,

        /// <summary>Keeps bones inside the shape rather than outside it.</summary>
        SphereInside = 3,

        /// <summary>Keeps bones inside the shape rather than outside it.</summary>
        CapsuleInside = 4,
    }

    /// <summary>
    /// One joint of a VRM spring chain.
    /// <para>
    /// VRM 1.0 carries these per bone, so a chain has a value at each point along it. VRM 0.x
    /// carries one set for the whole group, which is the same thing with every joint equal.
    /// </para>
    /// </summary>
    public sealed class VrmSpringJointData
    {
        /// <summary>The bone this joint sits on.</summary>
        public long OwnerGameObjectFileId;

        public float Stiffness = 1f;
        public float GravityPower;
        public Vector3 GravityDir = Vector3.down;

        /// <summary>Drag, 0 to 1, the same sense jiggle uses.</summary>
        public float DragForce = 0.4f;

        /// <summary>Collision radius in metres.</summary>
        public float Radius = 0.02f;

        /// <summary>
        /// False for a joint that carries no parameters of its own, which is how a chain's tail
        /// is written: it marks where the chain ends rather than how it behaves.
        /// </summary>
        public bool HasParameters = true;

        /// <summary>VRM 1.0 joint angle limit: 0 none, 1 cone, 2 hinge, 3 spherical.</summary>
        public int AngleLimitType;

        /// <summary>Half-angle of a cone limit, in radians. UniVRM's default is pi, no limit.</summary>
        public float Pitch = Mathf.PI;
    }

    /// <summary>One VRM spring chain, from either format.</summary>
    public sealed class VrmSpringChainData
    {
        /// <summary>The spring's own name in VRM 1.0, or the component's comment in 0.x.</summary>
        public string Name = string.Empty;

        public long DocumentFileId;

        /// <summary>The bone the chain hangs from.</summary>
        public long RootTransformFileId;

        /// <summary>The joints, root first. VRM 0.x has one, standing for the whole chain.</summary>
        public List<VrmSpringJointData> Joints = new List<VrmSpringJointData>();

        /// <summary>
        /// The joint components a VRM 1.0 spring names, in order. These are resolved into
        /// <see cref="Joints"/> once every joint document has been read.
        /// </summary>
        public List<long> JointComponentFileIds = new List<long>();

        public List<long> ColliderGroupFileIds = new List<long>();

        /// <summary>
        /// The transform the simulation runs relative to, which is how VRM keeps hair from
        /// lagging as the avatar moves. Jiggle has no equivalent.
        /// </summary>
        public long CenterFileId;

        /// <summary>True when this came from VRM 1.0, where parameters vary along the chain.</summary>
        public bool IsVrm10;
    }

    /// <summary>One collider shape a VRM chain is told about.</summary>
    public sealed class VrmColliderData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;

        public VrmColliderType Type = VrmColliderType.Sphere;
        public Vector3 Offset;
        public float Radius;

        /// <summary>The far end of a capsule, relative to the same object as the offset.</summary>
        public Vector3 Tail;

        public Vector3 Normal = Vector3.up;
    }

    /// <summary>A named group of colliders, which a chain references by name.</summary>
    public sealed class VrmColliderGroupData
    {
        public long DocumentFileId;
        public long OwnerGameObjectFileId;
        public string Name = string.Empty;

        /// <summary>VRM 1.0 references collider components; 0.x holds the shapes inline.</summary>
        public List<long> ColliderFileIds = new List<long>();

        public List<VrmColliderData> InlineColliders = new List<VrmColliderData>();
    }
}
