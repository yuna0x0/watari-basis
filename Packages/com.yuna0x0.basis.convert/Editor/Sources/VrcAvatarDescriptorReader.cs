using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Turns a VRCAvatarDescriptor document into plain data.
    /// <para>
    /// Two shapes need care. The viseme blendshape names are a plain sequence of fifteen
    /// strings, and the eyelid blendshape indices are a hex byte blob rather than a sequence,
    /// which is how Unity serializes some small fixed arrays.
    /// </para>
    /// </summary>
    public static class VrcAvatarDescriptorReader
    {
        private static readonly Regex SequenceItemPattern = new Regex(
            @"^\s*-\s*(?<value>.*?)\s*$", RegexOptions.Compiled);

        private static readonly Regex NestedFieldPattern = new Regex(
            @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)\s*$", RegexOptions.Compiled);

        public static VrcAvatarDescriptorData Read(UnityYamlDocument document)
        {
            VrcAvatarDescriptorData data = new VrcAvatarDescriptorData
            {
                DocumentFileId = document.FileId,
            };

            document.TryGetTopLevelFileIdReference("m_GameObject", out data.OwnerGameObjectFileId);
            document.TryGetTopLevelFileIdReference(
                "VisemeSkinnedMesh", out data.VisemeSkinnedMeshFileId);

            if (document.TryGetVector3("ViewPosition", out Vector3 view))
            {
                data.ViewPosition = view;
            }

            if (document.TryGetInt("lipSync", out int lipSync))
            {
                data.LipSync = (VrcLipSyncStyle)lipSync;
            }

            if (document.TryGetBool("enableEyeLook", out bool eyeLook))
            {
                data.EnableEyeLook = eyeLook;
            }

            data.VisemeBlendShapes = ReadStringSequence(document, "VisemeBlendShapes");
            ReadEyeLookSettings(document, data);

            data.ExpressionsMenuGuid = AssetGuidOf(document, "expressionsMenu");
            data.ExpressionsMenuFileId = AssetFileIdOf(document, "expressionsMenu");
            data.ExpressionParametersGuid = AssetGuidOf(document, "expressionParameters");
            data.ExpressionParametersFileId = AssetFileIdOf(document, "expressionParameters");
            ReadAnimationLayers(document, "baseAnimationLayers", data);
            ReadAnimationLayers(document, "specialAnimationLayers", data);

            return data;
        }

        private static List<string> ReadStringSequence(UnityYamlDocument document, string key)
        {
            List<string> values = new List<string>();
            if (!document.TryGetTopLevelBlock(key, out List<string> block))
            {
                return values;
            }

            foreach (string line in block)
            {
                Match match = SequenceItemPattern.Match(line);
                if (match.Success)
                {
                    values.Add(match.Groups["value"].Value);
                }
            }

            return values;
        }

        /// <summary>
        /// The eye look block is nested, and the fields wanted from it sit at different depths,
        /// so it is scanned for the handful of keys that matter rather than parsed in full.
        /// </summary>
        private static void ReadEyeLookSettings(
            UnityYamlDocument document, VrcAvatarDescriptorData data)
        {
            if (!document.TryGetTopLevelBlock("customEyeLookSettings", out List<string> block))
            {
                return;
            }

            VrcEyeRotations rotations = null;
            foreach (string line in block)
            {
                Match match = NestedFieldPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string value = match.Groups["value"].Value;
                string key = match.Groups["key"].Value;

                // A gaze state is a nested map: its key line opens it and the three lines
                // under it fill it. Any other key at the outer depth closes it.
                if (rotations != null && line.Length - line.TrimStart().Length > 4)
                {
                    ReadEyeRotationField(rotations, key, value);
                    continue;
                }

                rotations = null;
                switch (key)
                {
                    case "eyesLookingStraight":
                        data.EyesLookingStraight = rotations = new VrcEyeRotations();
                        break;

                    case "eyesLookingUp":
                        data.EyesLookingUp = rotations = new VrcEyeRotations();
                        break;

                    case "eyesLookingDown":
                        data.EyesLookingDown = rotations = new VrcEyeRotations();
                        break;

                    case "eyesLookingLeft":
                        data.EyesLookingLeft = rotations = new VrcEyeRotations();
                        break;

                    case "eyesLookingRight":
                        data.EyesLookingRight = rotations = new VrcEyeRotations();
                        break;

                    case "eyelidType":
                        if (UnityYamlValues.TryParseInt(value, out int eyelidType))
                        {
                            data.EyelidType = (VrcEyelidType)eyelidType;
                        }

                        break;

                    case "eyelidsSkinnedMesh":
                        data.EyelidsSkinnedMeshFileId = FileIdIn(value);
                        break;

                    case "eyelidsBlendshapes":
                        data.EyelidsBlendshapes = UnityYamlValues.ParseHexInt32Blob(value);
                        break;

                    case "leftEye":
                        data.LeftEyeFileId = FileIdIn(value);
                        break;

                    case "rightEye":
                        data.RightEyeFileId = FileIdIn(value);
                        break;
                }
            }
        }

        private static void ReadEyeRotationField(VrcEyeRotations rotations, string key, string value)
        {
            switch (key)
            {
                case "linked":
                    rotations.Linked = value.Trim() != "0";
                    break;

                case "left":
                    if (UnityYamlValues.TryParseQuaternion(value, out Quaternion left))
                    {
                        rotations.Left = left;
                    }

                    break;

                case "right":
                    if (UnityYamlValues.TryParseQuaternion(value, out Quaternion right))
                    {
                        rotations.Right = right;
                    }

                    break;
            }
        }

        private static string AssetGuidOf(UnityYamlDocument document, string key)
        {
            string raw = document.GetTopLevelValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            Match match = Regex.Match(raw, @"guid:\s*(?<guid>[0-9a-fA-F]{32})");
            return match.Success ? match.Groups["guid"].Value : null;
        }

        /// <summary>
        /// The file id of a referenced asset. A tool that packs several assets into one file, as
        /// VRCFury does with a built avatar's menus, gives each its own id; the main asset of a
        /// file of its own is 11400000, and that is read as 0 here so a plain reference stays
        /// plain.
        /// </summary>
        private static long AssetFileIdOf(UnityYamlDocument document, string key)
        {
            string raw = document.GetTopLevelValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return 0L;
            }

            Match match = Regex.Match(raw, @"fileID:\s*(?<id>-?\d+)");
            if (!match.Success || !long.TryParse(match.Groups["id"].Value, out long fileId))
            {
                return 0L;
            }

            return fileId == 11400000L ? 0L : fileId;
        }

        /// <summary>
        /// A layer counts as custom when it has a controller assigned and is not left on
        /// VRChat's stock one. Those are the layers someone authored and will have to rebuild.
        /// </summary>
        private static void ReadAnimationLayers(
            UnityYamlDocument document, string key, VrcAvatarDescriptorData data)
        {
            if (!document.TryGetTopLevelBlock(key, out List<string> block))
            {
                return;
            }

            int type = -1;
            string controllerGuid = null;
            bool isDefault = false;
            bool started = false;

            void Flush()
            {
                if (started && type >= 0 && controllerGuid != null && !isDefault)
                {
                    data.AnimationLayers.Add(new VrcAnimationLayerEntry
                    {
                        Layer = (VrcAnimationLayer)type,
                        IsCustom = true,
                        ControllerGuid = controllerGuid,
                    });
                }
            }

            foreach (string line in block)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("-"))
                {
                    Flush();
                    started = true;
                    type = -1;
                    controllerGuid = null;
                    isDefault = false;
                }

                Match match = NestedFieldPattern.Match(trimmed.TrimStart('-', ' '));
                if (!match.Success)
                {
                    continue;
                }

                string value = match.Groups["value"].Value;
                switch (match.Groups["key"].Value)
                {
                    case "type":
                        UnityYamlValues.TryParseInt(value, out type);
                        break;
                    case "animatorController":
                        Match controller = Regex.Match(value, @"guid:\s*(?<guid>[0-9a-fA-F]{32})");
                        controllerGuid = controller.Success
                            ? controller.Groups["guid"].Value
                            : null;
                        break;
                    case "isDefault":
                        isDefault = UnityYamlValues.TryParseInt(value, out int flag) && flag != 0;
                        break;
                }
            }

            Flush();
        }

        private static long FileIdIn(string raw)
        {
            Match match = Regex.Match(raw, @"fileID:\s*(?<id>-?\d+)");
            return match.Success
                && long.TryParse(match.Groups["id"].Value, out long id)
                ? id
                : 0L;
        }
    }
}
