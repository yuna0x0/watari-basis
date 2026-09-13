using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Turns UniVRM's spring bone components into plain data.
    /// <para>
    /// Both VRM formats are read. In 0.x one component carries a group of chains and one set of
    /// parameters for all of them; in 1.0 each bone carries its own joint and the avatar's
    /// `Vrm10Instance` lists which joints make up which chain.
    /// </para>
    /// <para>
    /// UniVRM ships loose scripts rather than a DLL, so the fields keep their authored names.
    /// See <c>agent/research/vrm-spring-bones.md</c> for where each was read from.
    /// </para>
    /// </summary>
    public static class VrmDocumentReader
    {
        /// <summary>
        /// A VRM 0.x spring bone: one set of parameters, and a list of bones to apply it to.
        /// Each root becomes a chain of its own, the same way one Dynamic Bone can drive several.
        /// </summary>
        public static List<VrmSpringChainData> ReadSpringBone0X(UnityYamlDocument document)
        {
            List<VrmSpringChainData> chains = new List<VrmSpringChainData>();

            VrmSpringJointData joint = new VrmSpringJointData();
            document.TryGetTopLevelFileIdReference("m_GameObject", out joint.OwnerGameObjectFileId);

            if (document.TryGetFloat("m_stiffnessForce", out float stiffness))
            {
                joint.Stiffness = stiffness;
            }

            if (document.TryGetFloat("m_gravityPower", out float gravityPower))
            {
                joint.GravityPower = gravityPower;
            }

            if (document.TryGetVector3("m_gravityDir", out Vector3 gravityDir))
            {
                joint.GravityDir = gravityDir;
            }

            if (document.TryGetFloat("m_dragForce", out float drag))
            {
                joint.DragForce = drag;
            }

            if (document.TryGetFloat("m_hitRadius", out float radius))
            {
                joint.Radius = radius;
            }

            string name = document.GetTopLevelValue("m_comment") ?? string.Empty;
            List<long> colliderGroups = document.GetFileIdList("ColliderGroups");
            document.TryGetTopLevelFileIdReference("m_center", out long center);

            foreach (long root in document.GetFileIdList("RootBones"))
            {
                if (root == 0L)
                {
                    continue;
                }

                VrmSpringChainData chain = new VrmSpringChainData
                {
                    Name = name,
                    DocumentFileId = document.FileId,
                    RootTransformFileId = root,
                    CenterFileId = center,
                };

                chain.ColliderGroupFileIds.AddRange(colliderGroups);
                chain.Joints.Add(joint);
                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>One VRM 1.0 joint, which is the parameters for one bone of a chain.</summary>
        public static VrmSpringJointData ReadJoint(UnityYamlDocument document)
        {
            VrmSpringJointData joint = new VrmSpringJointData();
            document.TryGetTopLevelFileIdReference("m_GameObject", out joint.OwnerGameObjectFileId);

            bool any = false;

            if (document.TryGetFloat("m_stiffnessForce", out float stiffness))
            {
                joint.Stiffness = stiffness;
                any = true;
            }

            if (document.TryGetFloat("m_gravityPower", out float gravityPower))
            {
                joint.GravityPower = gravityPower;
                any = true;
            }

            if (document.TryGetVector3("m_gravityDir", out Vector3 gravityDir))
            {
                joint.GravityDir = gravityDir;
            }

            if (document.TryGetFloat("m_dragForce", out float drag))
            {
                joint.DragForce = drag;
                any = true;
            }

            if (document.TryGetFloat("m_jointRadius", out float radius))
            {
                joint.Radius = radius;
                any = true;
            }

            if (document.TryGetInt("m_anglelimitType", out int limitType))
            {
                joint.AngleLimitType = limitType;
            }

            if (document.TryGetFloat("m_pitch", out float pitch))
            {
                joint.Pitch = pitch;
            }

            joint.HasParameters = any;
            return joint;
        }

        /// <summary>
        /// One VRM 1.0 node constraint. All three kinds carry a source and a weight; an aim
        /// constraint adds the axis it points, and a roll constraint the axis it copies.
        /// </summary>
        public static VrmConstraintData ReadConstraint(
            UnityYamlDocument document, VrmConstraintKind kind)
        {
            VrmConstraintData constraint = new VrmConstraintData
            {
                DocumentFileId = document.FileId,
                Kind = kind,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject",
                out constraint.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference("Source",
                out constraint.SourceTransformFileId);

            if (document.TryGetFloat("Weight", out float weight))
            {
                constraint.Weight = weight;
            }

            if (kind == VrmConstraintKind.Aim && document.TryGetInt("AimAxis", out int aim))
            {
                constraint.AimAxis = (VrmAimAxis)aim;
            }

            if (kind == VrmConstraintKind.Roll && document.TryGetInt("RollAxis", out int roll))
            {
                constraint.RollAxis = roll;
            }

            return constraint;
        }

        /// <summary>A VRM 1.0 collider shape.</summary>
        public static VrmColliderData ReadCollider(UnityYamlDocument document)
        {
            VrmColliderData collider = new VrmColliderData
            {
                DocumentFileId = document.FileId,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject",
                out collider.OwnerGameObjectFileId);

            if (document.TryGetInt("ColliderType", out int type))
            {
                collider.Type = (VrmColliderType)type;
            }

            if (document.TryGetVector3("Offset", out Vector3 offset))
            {
                collider.Offset = offset;
            }

            if (document.TryGetFloat("Radius", out float radius))
            {
                collider.Radius = radius;
            }

            if (document.TryGetVector3("Tail", out Vector3 tail))
            {
                collider.Tail = tail;
            }

            if (document.TryGetVector3("Normal", out Vector3 normal))
            {
                collider.Normal = normal;
            }

            return collider;
        }

        /// <summary>
        /// A collider group. VRM 1.0 references collider components; 0.x holds the spheres
        /// inline, as a list of an offset and a radius.
        /// </summary>
        public static VrmColliderGroupData ReadColliderGroup(
            UnityYamlDocument document, bool vrm10)
        {
            VrmColliderGroupData group = new VrmColliderGroupData
            {
                DocumentFileId = document.FileId,
                Name = document.GetTopLevelValue("Name") ?? string.Empty,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject",
                out group.OwnerGameObjectFileId);

            if (vrm10)
            {
                group.ColliderFileIds.AddRange(document.GetFileIdList("Colliders"));
                return group;
            }

            if (!document.TryGetTopLevelBlock("Colliders", out List<string> block))
            {
                return group;
            }

            VrmColliderData current = null;
            foreach (string line in block)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("-"))
                {
                    current = new VrmColliderData
                    {
                        DocumentFileId = document.FileId,
                        OwnerGameObjectFileId = group.OwnerGameObjectFileId,
                    };

                    group.InlineColliders.Add(current);
                    trimmed = trimmed.Substring(1).TrimStart();
                }

                if (current == null)
                {
                    continue;
                }

                if (trimmed.StartsWith("Offset:")
                    && UnityYamlValues.TryParseVector3(trimmed, out Vector3 offset))
                {
                    current.Offset = offset;
                }
                else if (trimmed.StartsWith("Radius:")
                         && UnityYamlValues.TryParseFloat(
                             trimmed.Substring("Radius:".Length).Trim(), out float radius))
                {
                    current.Radius = radius;
                }
            }

            return group;
        }

        /// <summary>
        /// The chains a VRM 1.0 avatar declares, read from `Vrm10Instance`.
        /// <para>
        /// The block names each spring, the joint components that make it up in order, and the
        /// collider groups it collides with. The joints themselves are separate documents.
        /// </para>
        /// </summary>
        public static List<VrmSpringChainData> ReadInstanceSprings(UnityYamlDocument document)
        {
            List<VrmSpringChainData> chains = new List<VrmSpringChainData>();

            if (!document.TryGetTopLevelBlock("SpringBone", out List<string> block))
            {
                return chains;
            }

            VrmSpringChainData current = null;
            string section = null;

            foreach (string line in block)
            {
                string trimmed = line.TrimStart();
                int indent = line.Length - trimmed.Length;

                // A spring starts at the entry indent inside Springs, and its own lists are
                // deeper than that. Nothing else in the block starts an entry.
                if (trimmed.StartsWith("- Name:"))
                {
                    current = new VrmSpringChainData
                    {
                        DocumentFileId = document.FileId,
                        IsVrm10 = true,
                        Name = trimmed.Substring("- Name:".Length).Trim(),
                    };

                    chains.Add(current);
                    section = null;
                    continue;
                }

                if (trimmed.StartsWith("Springs:"))
                {
                    section = null;
                    continue;
                }

                if (trimmed.StartsWith("ColliderGroups:"))
                {
                    // The instance has a list of its own before the springs begin, which is
                    // every group the avatar declares rather than one chain's own.
                    section = current == null ? null : "colliders";
                    continue;
                }

                if (trimmed.StartsWith("Joints:"))
                {
                    section = "joints";
                    continue;
                }

                if (current != null && trimmed.StartsWith("Center:"))
                {
                    if (UnityYamlValues.TryParseFileId(trimmed, out long center))
                    {
                        current.CenterFileId = center;
                    }

                    section = null;
                    continue;
                }

                if (current == null || section == null || !trimmed.StartsWith("-")
                    || indent <= 4)
                {
                    continue;
                }

                if (!UnityYamlValues.TryParseFileId(trimmed, out long fileId) || fileId == 0L)
                {
                    continue;
                }

                if (section == "joints")
                {
                    // The reference is to the joint component, not to the bone. Which bone it
                    // sits on is in the joint's own document, read separately.
                    current.JointComponentFileIds.Add(fileId);
                }
                else
                {
                    current.ColliderGroupFileIds.Add(fileId);
                }
            }

            return chains;
        }
    }
}
