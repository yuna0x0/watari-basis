using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Turns a Dynamic Bone or Dynamic Bone collider document into plain data.
    /// <para>
    /// Dynamic Bone ships as a loose script rather than inside a DLL, so unlike the VRChat
    /// components its fields keep their authored names, prefixed <c>m_</c>.
    /// </para>
    /// </summary>
    public static class DynamicBoneDocumentReader
    {
        public static DynamicBoneData ReadBone(UnityYamlDocument document)
        {
            DynamicBoneData data = new DynamicBoneData
            {
                DocumentFileId = document.FileId,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference("m_Root", out data.RootFileId);

            data.RootFileIds = document.GetFileIdList("m_Roots");
            data.ColliderFileIds = document.GetFileIdList("m_Colliders");
            data.ExclusionFileIds = document.GetFileIdList("m_Exclusions");

            data.Damping = ReadCurved(document, "m_Damping", "m_DampingDistrib", 0.1f);
            data.Elasticity = ReadCurved(document, "m_Elasticity", "m_ElasticityDistrib", 0.1f);
            data.Stiffness = ReadCurved(document, "m_Stiffness", "m_StiffnessDistrib", 0.1f);
            data.Inert = ReadCurved(document, "m_Inert", "m_InertDistrib", 0f);
            data.Friction = ReadCurved(document, "m_Friction", "m_FrictionDistrib", 0f);
            data.Radius = ReadCurved(document, "m_Radius", "m_RadiusDistrib", 0f);

            if (document.TryGetFloat("m_UpdateRate", out float updateRate))
            {
                data.UpdateRate = updateRate;
            }

            if (document.TryGetFloat("m_EndLength", out float endLength))
            {
                data.EndLength = endLength;
            }

            if (document.TryGetFloat("m_BlendWeight", out float blendWeight))
            {
                data.BlendWeight = blendWeight;
            }

            if (document.TryGetVector3("m_EndOffset", out Vector3 endOffset))
            {
                data.EndOffset = endOffset;
            }

            if (document.TryGetVector3("m_Gravity", out Vector3 gravity))
            {
                data.Gravity = gravity;
            }

            if (document.TryGetVector3("m_Force", out Vector3 force))
            {
                data.Force = force;
            }

            if (document.TryGetInt("m_FreezeAxis", out int freezeAxis))
            {
                data.FreezeAxis = (DynamicBoneFreezeAxis)freezeAxis;
            }

            return data;
        }

        public static DynamicBoneColliderData ReadCollider(
            UnityYamlDocument document, bool isPlane)
        {
            DynamicBoneColliderData data = new DynamicBoneColliderData
            {
                DocumentFileId = document.FileId,
                IsPlane = isPlane,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);

            if (document.TryGetInt("m_Direction", out int direction))
            {
                data.Direction = (DynamicBoneColliderDirection)direction;
            }

            if (document.TryGetVector3("m_Center", out Vector3 center))
            {
                data.Center = center;
            }

            if (document.TryGetInt("m_Bound", out int bound))
            {
                data.Bound = (DynamicBoneColliderBound)bound;
            }

            if (document.TryGetFloat("m_Radius", out float radius))
            {
                data.Radius = radius;
            }

            if (document.TryGetFloat("m_Height", out float height))
            {
                data.Height = height;
            }

            if (document.TryGetFloat("m_Radius2", out float radius2))
            {
                data.Radius2 = radius2;
            }

            return data;
        }

        private static PhysBoneCurvedFloat ReadCurved(
            UnityYamlDocument document, string valueKey, string curveKey, float fallback)
        {
            float value = document.TryGetFloat(valueKey, out float parsed) ? parsed : fallback;
            document.TryGetCurve(curveKey, out AnimationCurve curve);
            return new PhysBoneCurvedFloat(value, curve);
        }
    }
}
