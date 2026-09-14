using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Turns a VRCPhysBone or VRCPhysBoneCollider YAML document into plain data.
    /// <para>
    /// Every field is optional on read. VRChat has added fields over time and will add more, so
    /// a missing key leaves the model at the default VRChat itself uses rather than failing the
    /// whole component.
    /// </para>
    /// </summary>
    public static class PhysBoneDocumentReader
    {
        public static PhysBoneData ReadPhysBone(UnityYamlDocument document)
        {
            PhysBoneData data = new PhysBoneData
            {
                DocumentFileId = document.FileId,
                Parameter = document.GetTopLevelValue("parameter") ?? string.Empty,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference("rootTransform", out data.RootTransformFileId);
            data.IgnoreTransformFileIds = document.GetFileIdList("ignoreTransforms");
            data.ColliderFileIds = document.GetFileIdList("colliders");

            if (document.TryGetInt("version", out int version))
            {
                data.Version = version;
            }

            data.IntegrationType = (PhysBoneIntegrationType)ReadEnum(
                document, "integrationType", (int)PhysBoneIntegrationType.Simplified);
            data.MultiChildType = (PhysBoneMultiChildType)ReadEnum(
                document, "multiChildType", (int)PhysBoneMultiChildType.Ignore);
            data.ImmobileType = (PhysBoneImmobileType)ReadEnum(
                document, "immobileType", (int)PhysBoneImmobileType.AllMotion);
            data.LimitType = (PhysBoneLimitType)ReadEnum(
                document, "limitType", (int)PhysBoneLimitType.None);

            if (document.TryGetVector3("endpointPosition", out Vector3 endpoint))
            {
                data.EndpointPosition = endpoint;
            }

            if (document.TryGetVector3("limitRotation", out Vector3 limitRotation))
            {
                data.LimitRotation = limitRotation;
            }

            data.Pull = ReadCurved(document, "pull", "pullCurve", 0.2f);
            data.Spring = ReadCurved(document, "spring", "springCurve", 0.2f);
            data.Stiffness = ReadCurved(document, "stiffness", "stiffnessCurve", 0.2f);
            data.Gravity = ReadCurved(document, "gravity", "gravityCurve", 0f);
            data.GravityFalloff = ReadCurved(document, "gravityFalloff", "gravityFalloffCurve", 0f);
            data.Immobile = ReadCurved(document, "immobile", "immobileCurve", 0f);
            data.Radius = ReadCurved(document, "radius", "radiusCurve", 0f);
            data.MaxAngleX = ReadCurved(document, "maxAngleX", "maxAngleXCurve", 45f);
            data.MaxAngleZ = ReadCurved(document, "maxAngleZ", "maxAngleZCurve", 45f);
            data.MaxStretch = ReadCurved(document, "maxStretch", "maxStretchCurve", 0f);
            data.MaxSquish = ReadCurved(document, "maxSquish", "maxSquishCurve", 0f);
            data.StretchMotion = ReadCurved(document, "stretchMotion", "stretchMotionCurve", 0f);

            // AdvancedBool: 0 False, 1 True, 2 Other, where Other defers to a per-player filter.
            data.AllowCollision = ReadBool(document, "allowCollision", true);
            data.AllowCollisionFiltered = document.TryGetInt("allowCollision", out int collisionRaw)
                && collisionRaw == 2;
            data.AllowGrabbingFiltered = document.TryGetInt("allowGrabbing", out int grabRaw)
                && grabRaw == 2;
            // isGrabbable and isPoseable are the pre-rename keys ([FormerlySerializedAs]).
            data.AllowGrabbing = ReadBool(document, "allowGrabbing",
                ReadBool(document, "isGrabbable", true));
            data.AllowPosing = ReadBool(document, "allowPosing",
                ReadBool(document, "isPoseable", true));
            data.SnapToHand = ReadBool(document, "snapToHand", false);
            data.IsAnimated = ReadBool(document, "isAnimated", false);


            return data;
        }

        public static PhysBoneColliderData ReadCollider(UnityYamlDocument document)
        {
            PhysBoneColliderData data = new PhysBoneColliderData
            {
                DocumentFileId = document.FileId,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference("rootTransform", out data.RootTransformFileId);

            data.ShapeType = (PhysBoneColliderShape)ReadEnum(
                document, "shapeType", (int)PhysBoneColliderShape.Sphere);
            data.InsideBounds = ReadBool(document, "insideBounds", false);
            data.BonesAsSpheres = ReadBool(document, "bonesAsSpheres", false);

            // An AdvancedBool: 0 off, 1 on, 2 on with a filter. Anything but off is global.
            data.GlobalCollision = ReadEnum(document, "globalCollision", 0) != 0;

            data.Radius = document.TryGetFloat("radius", out float radius) ? radius : 0.5f;
            data.Height = document.TryGetFloat("height", out float height) ? height : 2f;

            if (document.TryGetVector3("position", out Vector3 position))
            {
                data.Position = position;
            }

            data.Rotation = document.TryGetQuaternion("rotation", out Quaternion rotation)
                ? rotation
                : Quaternion.identity;

            return data;
        }

        private static PhysBoneCurvedFloat ReadCurved(
            UnityYamlDocument document, string valueKey, string curveKey, float fallback)
        {
            float value = document.TryGetFloat(valueKey, out float parsed) ? parsed : fallback;
            document.TryGetCurve(curveKey, out AnimationCurve curve);
            return new PhysBoneCurvedFloat(value, curve);
        }

        private static bool ReadBool(UnityYamlDocument document, string key, bool fallback)
        {
            return document.TryGetBool(key, out bool value) ? value : fallback;
        }

        private static int ReadEnum(UnityYamlDocument document, string key, int fallback)
        {
            return document.TryGetInt(key, out int value) ? value : fallback;
        }
    }
}
