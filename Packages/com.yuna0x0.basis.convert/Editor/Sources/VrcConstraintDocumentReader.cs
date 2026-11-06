using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Turns a VRChat constraint document into plain data.
    /// <para>
    /// The awkward part is the source list. VRChat serialises it as sixteen fixed inline slots,
    /// <c>source0</c> to <c>source15</c>, followed by <c>totalLength</c> and an
    /// <c>overflowList</c>. Only the first <c>totalLength</c> slots hold real data; the rest are
    /// defaults left over from the fixed-size struct, so reading all sixteen would invent
    /// sources that do not exist.
    /// </para>
    /// </summary>
    public static class VrcConstraintDocumentReader
    {
        private static readonly Regex SlotPattern = new Regex(
            @"^\s{4}source(?<index>\d+):\s*$", RegexOptions.Compiled);

        private static readonly Regex FieldPattern = new Regex(
            @"^\s{6}(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)\s*$", RegexOptions.Compiled);

        private static readonly Regex TotalLengthPattern = new Regex(
            @"^\s{4}totalLength\s*:\s*(?<value>-?\d+)\s*$", RegexOptions.Compiled);

        private static readonly Regex FileIdPattern = new Regex(
            @"fileID:\s*(?<id>-?\d+)", RegexOptions.Compiled);

        /// <summary>The constraint kind a recognised script identity denotes, if it is one.</summary>
        public static bool TryGetKind(SourceComponentKind component, out VrcConstraintKind kind)
        {
            switch (component)
            {
                case SourceComponentKind.VrcPositionConstraint:
                    kind = VrcConstraintKind.Position;
                    return true;
                case SourceComponentKind.VrcRotationConstraint:
                    kind = VrcConstraintKind.Rotation;
                    return true;
                case SourceComponentKind.VrcScaleConstraint:
                    kind = VrcConstraintKind.Scale;
                    return true;
                case SourceComponentKind.VrcParentConstraint:
                    kind = VrcConstraintKind.Parent;
                    return true;
                case SourceComponentKind.VrcAimConstraint:
                    kind = VrcConstraintKind.Aim;
                    return true;
                case SourceComponentKind.VrcLookAtConstraint:
                    kind = VrcConstraintKind.LookAt;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        public static VrcConstraintData Read(UnityYamlDocument document, VrcConstraintKind kind)
        {
            VrcConstraintData data = new VrcConstraintData
            {
                DocumentFileId = document.FileId,
                Kind = kind,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference(
                "TargetTransform", out data.TargetTransformFileId);
            document.TryGetTopLevelFileIdReference(
                "WorldUpTransform", out data.WorldUpTransformFileId);

            data.IsActive = ReadBool(document, "IsActive", true);
            data.Locked = ReadBool(document, "Locked", true);
            data.SolveInLocalSpace = ReadBool(document, "SolveInLocalSpace", false);
            data.FreezeToWorld = ReadBool(document, "FreezeToWorld", false);
            if (document.TryGetFloat("SmoothingFactor", out float smoothing))
            {
                data.SmoothingFactor = smoothing;
            }

            data.UseUpTransform = ReadBool(document, "UseUpTransform", false);

            if (document.TryGetFloat("GlobalWeight", out float weight))
            {
                data.GlobalWeight = weight;
            }

            if (document.TryGetFloat("Roll", out float roll))
            {
                data.Roll = roll;
            }

            ReadVector(document, "PositionAtRest", ref data.PositionAtRest);
            ReadVector(document, "PositionOffset", ref data.PositionOffset);
            ReadVector(document, "RotationAtRest", ref data.RotationAtRest);
            ReadVector(document, "RotationOffset", ref data.RotationOffset);
            ReadVector(document, "ScaleAtRest", ref data.ScaleAtRest);
            ReadVector(document, "ScaleOffset", ref data.ScaleOffset);
            ReadVector(document, "AimAxis", ref data.AimAxis);
            ReadVector(document, "UpAxis", ref data.UpAxis);
            ReadVector(document, "WorldUpVector", ref data.WorldUpVector);

            data.AffectsPositionX = ReadBool(document, "AffectsPositionX", true);
            data.AffectsPositionY = ReadBool(document, "AffectsPositionY", true);
            data.AffectsPositionZ = ReadBool(document, "AffectsPositionZ", true);
            data.AffectsRotationX = ReadBool(document, "AffectsRotationX", true);
            data.AffectsRotationY = ReadBool(document, "AffectsRotationY", true);
            data.AffectsRotationZ = ReadBool(document, "AffectsRotationZ", true);
            data.AffectsScaleX = ReadBool(document, "AffectsScaleX", true);
            data.AffectsScaleY = ReadBool(document, "AffectsScaleY", true);
            data.AffectsScaleZ = ReadBool(document, "AffectsScaleZ", true);

            if (document.TryGetInt("WorldUp", out int worldUp))
            {
                data.WorldUp = (VrcConstraintWorldUp)worldUp;
            }

            data.Sources = ReadSources(document, out data.DeclaredSourceCount);
            return data;
        }

        private static List<VrcConstraintSource> ReadSources(
            UnityYamlDocument document, out int declared)
        {
            List<VrcConstraintSource> sources = new List<VrcConstraintSource>();
            declared = 0;

            if (!document.TryGetTopLevelBlock("Sources", out List<string> block))
            {
                return sources;
            }

            Dictionary<int, VrcConstraintSource> slots = new Dictionary<int, VrcConstraintSource>();
            int totalLength = 0;
            int current = -1;

            foreach (string line in block)
            {
                Match total = TotalLengthPattern.Match(line);
                if (total.Success)
                {
                    totalLength = int.Parse(
                        total.Groups["value"].Value, CultureInfo.InvariantCulture);
                    current = -1;
                    continue;
                }

                Match slot = SlotPattern.Match(line);
                if (slot.Success)
                {
                    current = int.Parse(slot.Groups["index"].Value, CultureInfo.InvariantCulture);
                    slots[current] = new VrcConstraintSource();
                    continue;
                }

                if (current < 0)
                {
                    continue;
                }

                Match field = FieldPattern.Match(line);
                if (!field.Success)
                {
                    continue;
                }

                VrcConstraintSource source = slots[current];
                string value = field.Groups["value"].Value;

                switch (field.Groups["key"].Value)
                {
                    case "SourceTransform":
                        Match id = FileIdPattern.Match(value);
                        if (id.Success)
                        {
                            source.SourceTransformFileId = long.Parse(
                                id.Groups["id"].Value, CultureInfo.InvariantCulture);
                        }

                        break;

                    case "Weight":
                        if (UnityYamlValues.TryParseFloat(value, out float weight))
                        {
                            source.Weight = weight;
                        }

                        break;

                    case "ParentPositionOffset":
                        UnityYamlValues.TryParseVector3(value, out source.ParentPositionOffset);
                        break;

                    case "ParentRotationOffset":
                        UnityYamlValues.TryParseVector3(value, out source.ParentRotationOffset);
                        break;
                }
            }

            for (int i = 0; i < totalLength; i++)
            {
                if (slots.TryGetValue(i, out VrcConstraintSource source))
                {
                    sources.Add(source);
                }
            }

            declared = totalLength;
            return sources;
        }

        private static bool ReadBool(UnityYamlDocument document, string key, bool fallback)
        {
            return document.TryGetBool(key, out bool value) ? value : fallback;
        }

        private static void ReadVector(UnityYamlDocument document, string key, ref Vector3 target)
        {
            if (document.TryGetVector3(key, out Vector3 value))
            {
                target = value;
            }
        }
    }
}
