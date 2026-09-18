using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Reads what a VRM avatar keeps beside its prefab: expressions, and the look-at and
    /// first-person settings.
    /// <para>
    /// Both formats keep them in assets rather than on the avatar: VRM 0.x has a blend shape
    /// avatar asset listing clip assets, and VRM 1.0 has a VRM10Object asset holding one
    /// expression per preset plus a list of custom ones. So the components on the prefab are
    /// followed off disk, the same way an expression menu's submenus are.
    /// </para>
    /// </summary>
    public static class VrmObjectReader
    {
        /// <summary>
        /// VRM 1.0 weights run from 0 to 1, and Unity's blendshape weights from 0 to 100.
        /// UniVRM's own constant for this is `MorphTargetBinding.VRM_TO_UNITY`.
        /// </summary>
        private const float Vrm10WeightToUnity = 100f;

        /// <summary>
        /// The presets of VRM 0.x, in the order its `BlendShapePreset` enum declares them, which
        /// is what the asset stores.
        /// </summary>
        private static readonly VrmExpressionRole[] Vrm0Roles =
        {
            VrmExpressionRole.Custom,   // Unknown, named by the author instead
            VrmExpressionRole.Neutral,
            VrmExpressionRole.Viseme,   // A
            VrmExpressionRole.Viseme,   // I
            VrmExpressionRole.Viseme,   // U
            VrmExpressionRole.Viseme,   // E
            VrmExpressionRole.Viseme,   // O
            VrmExpressionRole.Blink,
            VrmExpressionRole.Emotion,  // Joy
            VrmExpressionRole.Emotion,  // Angry
            VrmExpressionRole.Emotion,  // Sorrow
            VrmExpressionRole.Emotion,  // Fun
            VrmExpressionRole.LookAt,   // LookUp
            VrmExpressionRole.LookAt,   // LookDown
            VrmExpressionRole.LookAt,   // LookLeft
            VrmExpressionRole.LookAt,   // LookRight
            VrmExpressionRole.Blink,    // Blink_L
            VrmExpressionRole.Blink,    // Blink_R
        };

        /// <summary>
        /// The named expression fields of VRM 1.0, in the order `VRM10ObjectExpression` declares
        /// them, with what each is for.
        /// </summary>
        private static readonly (string Field, VrmExpressionRole Role)[] Vrm10Presets =
        {
            ("Happy", VrmExpressionRole.Emotion),
            ("Angry", VrmExpressionRole.Emotion),
            ("Sad", VrmExpressionRole.Emotion),
            ("Relaxed", VrmExpressionRole.Emotion),
            ("Surprised", VrmExpressionRole.Emotion),
            ("Aa", VrmExpressionRole.Viseme),
            ("Ih", VrmExpressionRole.Viseme),
            ("Ou", VrmExpressionRole.Viseme),
            ("Ee", VrmExpressionRole.Viseme),
            ("Oh", VrmExpressionRole.Viseme),
            ("Blink", VrmExpressionRole.Blink),
            ("BlinkLeft", VrmExpressionRole.Blink),
            ("BlinkRight", VrmExpressionRole.Blink),
            ("LookUp", VrmExpressionRole.LookAt),
            ("LookDown", VrmExpressionRole.LookAt),
            ("LookLeft", VrmExpressionRole.LookAt),
            ("LookRight", VrmExpressionRole.LookAt),
            ("Neutral", VrmExpressionRole.Neutral),
        };

        /// <summary>
        /// The expressions a VRM 0.x avatar declares, followed from its blend shape proxy.
        /// </summary>
        public static List<VrmExpressionData> ReadVrm0(UnityYamlDocument proxy)
        {
            List<VrmExpressionData> expressions = new List<VrmExpressionData>();

            if (proxy == null || !proxy.TryGetTopLevelObjectReference(
                    "BlendShapeAvatar", out string avatarGuid, out long avatarFileId))
            {
                return expressions;
            }

            UnityYamlDocument avatar = Load(avatarGuid, avatarFileId);
            if (avatar == null)
            {
                return expressions;
            }

            foreach ((string guid, long fileId) in ObjectReferences(avatar, "Clips"))
            {
                UnityYamlDocument clip = Load(guid, fileId);
                if (clip != null)
                {
                    expressions.Add(ReadVrm0Clip(clip));
                }
            }

            return expressions;
        }

        /// <summary>
        /// The expressions a VRM 1.0 avatar declares, followed from its instance component. The
        /// preset ones are named fields and the rest are a list, and both are references into
        /// the same object asset.
        /// </summary>
        public static List<VrmExpressionData> ReadVrm10(UnityYamlDocument instance)
        {
            List<VrmExpressionData> expressions = new List<VrmExpressionData>();

            if (instance == null || !instance.TryGetTopLevelObjectReference(
                    "Vrm", out string objectGuid, out long objectFileId))
            {
                return expressions;
            }

            UnityYamlDocument vrm = Load(objectGuid, objectFileId);
            if (vrm == null || !vrm.TryGetTopLevelBlock("Expression", out List<string> block))
            {
                return expressions;
            }

            foreach ((string Field, VrmExpressionRole Role) preset in Vrm10Presets)
            {
                if (!TryReadReference(block, preset.Field, objectGuid,
                        out string guid, out long fileId))
                {
                    continue;
                }

                UnityYamlDocument clip = Load(guid, fileId);
                if (clip == null)
                {
                    continue;
                }

                VrmExpressionData expression = ReadVrm10Clip(clip);
                expression.Name = preset.Field;
                expression.Role = preset.Role;
                expressions.Add(expression);
            }

            foreach ((string guid, long fileId) in CustomClipReferences(block, objectGuid))
            {
                UnityYamlDocument clip = Load(guid, fileId);
                if (clip == null)
                {
                    continue;
                }

                VrmExpressionData expression = ReadVrm10Clip(clip);
                if (string.IsNullOrEmpty(expression.Name))
                {
                    expression.Name = "Expression";
                }

                expressions.Add(expression);
            }

            return expressions;
        }

        /// <summary>
        /// The licence a VRM 0.x avatar carries, followed from its meta component.
        /// </summary>
        public static VrmMetaData ReadVrm0Meta(UnityYamlDocument meta)
        {
            if (meta == null || !meta.TryGetTopLevelObjectReference(
                    "Meta", out string guid, out long fileId))
            {
                return null;
            }

            UnityYamlDocument document = Load(guid, fileId);
            if (document == null)
            {
                return null;
            }

            VrmMetaData data = new VrmMetaData
            {
                Title = document.GetTopLevelValue("Title") ?? string.Empty,
                LicenseUrl = document.GetTopLevelValue("OtherLicenseUrl") ?? string.Empty,
            };

            string author = document.GetTopLevelValue("Author");
            if (!string.IsNullOrEmpty(author))
            {
                data.Authors.Add(author);
            }

            if (document.TryGetInt("AllowedUser", out int allowed))
            {
                data.AvatarPermission = (VrmAvatarPermission)allowed;
            }

            if (document.TryGetInt("LicenseType", out int license))
            {
                data.LicenseName = Vrm0LicenseNames[
                    Mathf.Clamp(license, 0, Vrm0LicenseNames.Length - 1)];
            }

            // VRM 0.x writes each of these as Disallow or Allow, and has no field for the
            // political or antisocial ones that 1.0 added.
            data.ViolentUsage = Allowed(document, "ViolentUssage");
            data.SexualUsage = Allowed(document, "SexualUssage");

            if (Allowed(document, "CommercialUssage") is bool commercial)
            {
                data.CommercialUsage = commercial ? "allowed" : "not allowed";
            }

            return data;
        }

        /// <summary>The licence a VRM 1.0 avatar carries, from its object asset.</summary>
        public static VrmMetaData ReadVrm10Meta(UnityYamlDocument instance)
        {
            if (instance == null || !instance.TryGetTopLevelObjectReference(
                    "Vrm", out string guid, out long fileId))
            {
                return null;
            }

            UnityYamlDocument vrm = Load(guid, fileId);
            if (vrm == null || !vrm.TryGetTopLevelBlock("Meta", out List<string> block))
            {
                return null;
            }

            VrmMetaData data = new VrmMetaData();
            bool inAuthors = false;

            foreach (string line in block)
            {
                string trimmed = line.TrimStart();

                if (inAuthors)
                {
                    if (trimmed.StartsWith("-"))
                    {
                        string name = trimmed.Substring(1).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            data.Authors.Add(name);
                        }

                        continue;
                    }

                    inAuthors = false;
                }

                if (trimmed.StartsWith("Authors:"))
                {
                    inAuthors = true;
                }
                else if (trimmed.StartsWith("Name:"))
                {
                    data.Title = trimmed.Substring("Name:".Length).Trim();
                }
                else if (trimmed.StartsWith("OtherLicenseUrl:"))
                {
                    data.LicenseUrl = trimmed.Substring("OtherLicenseUrl:".Length).Trim();
                }
                else if (trimmed.StartsWith("AvatarPermission:")
                         && UnityYamlValues.TryParseInt(
                             trimmed.Substring("AvatarPermission:".Length).Trim(), out int who))
                {
                    data.AvatarPermission = (VrmAvatarPermission)who;
                }
                else if (trimmed.StartsWith("Modification:")
                         && UnityYamlValues.TryParseInt(
                             trimmed.Substring("Modification:".Length).Trim(), out int change))
                {
                    data.Modification = (VrmModificationPermission)change;
                }
                else if (trimmed.StartsWith("ViolentUsage:"))
                {
                    data.ViolentUsage = Flag(trimmed, "ViolentUsage");
                }
                else if (trimmed.StartsWith("SexualUsage:"))
                {
                    data.SexualUsage = Flag(trimmed, "SexualUsage");
                }
                else if (trimmed.StartsWith("PoliticalOrReligiousUsage:"))
                {
                    data.PoliticalOrReligiousUsage =
                        Flag(trimmed, "PoliticalOrReligiousUsage");
                }
                else if (trimmed.StartsWith("AntisocialOrHateUsage:"))
                {
                    data.AntisocialOrHateUsage = Flag(trimmed, "AntisocialOrHateUsage");
                }
                else if (trimmed.StartsWith("Redistribution:"))
                {
                    data.Redistribution = Flag(trimmed, "Redistribution");
                }
                else if (trimmed.StartsWith("CreditNotation:")
                         && UnityYamlValues.TryParseInt(
                             trimmed.Substring("CreditNotation:".Length).Trim(), out int credit))
                {
                    // required, unnecessary
                    data.CreditRequired = credit == 0;
                }
                else if (trimmed.StartsWith("CommercialUsage:")
                         && UnityYamlValues.TryParseInt(
                             trimmed.Substring("CommercialUsage:".Length).Trim(), out int use))
                {
                    data.CommercialUsage = Vrm10CommercialNames[
                        Mathf.Clamp(use, 0, Vrm10CommercialNames.Length - 1)];
                }
            }

            return data;
        }

        /// <summary>A yes or no field of a VRM 1.0 meta block.</summary>
        private static bool? Flag(string line, string key)
        {
            return UnityYamlValues.TryParseInt(
                line.Substring(key.Length + 1).Trim(), out int value)
                ? value != 0
                : (bool?)null;
        }

        /// <summary>A VRM 0.x usage field, which is Disallow or Allow.</summary>
        private static bool? Allowed(UnityYamlDocument document, string key)
        {
            return document.TryGetInt(key, out int value) ? value != 0 : (bool?)null;
        }

        /// <summary>How far VRM 1.0 lets commercial use go, in its own words.</summary>
        private static readonly string[] Vrm10CommercialNames =
        {
            "personal, not for profit",
            "personal, including for profit",
            "corporate",
        };

        /// <summary>VRM 0.x's licence types, in the order its own enum declares them.</summary>
        private static readonly string[] Vrm0LicenseNames =
        {
            "Redistribution_Prohibited",
            "CC0",
            "CC_BY",
            "CC_BY_NC",
            "CC_BY_SA",
            "CC_BY_NC_SA",
            "CC_BY_ND",
            "CC_BY_NC_ND",
            "Other",
        };

        /// <summary>
        /// A VRM 0.x avatar's first person settings, which is where its eye offset lives.
        /// </summary>
        public static VrmAvatarSettingsData ReadVrm0Settings(UnityYamlDocument firstPerson)
        {
            VrmAvatarSettingsData settings = new VrmAvatarSettingsData();
            if (firstPerson == null)
            {
                return settings;
            }

            firstPerson.TryGetTopLevelFileIdReference("FirstPersonBone",
                out settings.HeadBoneFileId);

            if (firstPerson.TryGetVector3("FirstPersonOffset", out Vector3 offset))
            {
                settings.EyeOffsetFromHead = offset;
                settings.HasEyeOffset = offset != Vector3.zero;
            }

            CountFirstPersonFlags(firstPerson, "Renderers", "FirstPersonFlag", settings);
            return settings;
        }

        /// <summary>
        /// A VRM 1.0 avatar's look at and first person settings, which live in the same object
        /// asset as its expressions.
        /// </summary>
        public static VrmAvatarSettingsData ReadVrm10Settings(UnityYamlDocument instance)
        {
            VrmAvatarSettingsData settings = new VrmAvatarSettingsData();

            if (instance == null || !instance.TryGetTopLevelObjectReference(
                    "Vrm", out string guid, out long fileId))
            {
                return settings;
            }

            UnityYamlDocument vrm = Load(guid, fileId);
            if (vrm == null)
            {
                return settings;
            }

            if (vrm.TryGetTopLevelBlock("LookAt", out List<string> lookAt))
            {
                foreach (string line in lookAt)
                {
                    string trimmed = line.TrimStart();

                    // The four range maps are nested maps; only their output scale matters here.
                    if (trimmed.StartsWith("CurveYRangeDegree:")
                        && UnityYamlValues.TryParseFloat(
                            trimmed.Substring("CurveYRangeDegree:".Length).Trim(), out float scale))
                    {
                        settings.EyeRotationLimitsDegrees.Add(scale);
                        continue;
                    }

                    if (trimmed.StartsWith("LookAtType:"))
                    {
                        settings.LookAtByExpression =
                            trimmed.Substring("LookAtType:".Length).Trim() == "1";
                        continue;
                    }

                    if (!trimmed.StartsWith("OffsetFromHead:"))
                    {
                        continue;
                    }

                    if (UnityYamlValues.TryParseVector3(trimmed, out Vector3 offset))
                    {
                        settings.EyeOffsetFromHead = offset;
                        settings.HasEyeOffset = offset != Vector3.zero;
                    }
                }
            }

            CountFirstPersonFlags(vrm, "FirstPerson", "FirstPersonFlag", settings);
            return settings;
        }

        /// <summary>
        /// Counts the renderers hidden from one view or the other. Both formats write the same
        /// four values in the same order: auto, both, third person only, first person only.
        /// </summary>
        private static void CountFirstPersonFlags(
            UnityYamlDocument document, string key, string flagKey, VrmAvatarSettingsData settings)
        {
            if (!document.TryGetTopLevelBlock(key, out List<string> block))
            {
                return;
            }

            foreach (string line in block)
            {
                string trimmed = line.TrimStart().TrimStart('-').TrimStart();
                if (!trimmed.StartsWith(flagKey + ":"))
                {
                    continue;
                }

                if (!UnityYamlValues.TryParseInt(
                        trimmed.Substring(flagKey.Length + 1).Trim(), out int flag))
                {
                    continue;
                }

                if (flag == 2)
                {
                    settings.ThirdPersonOnlyRenderers++;
                }
                else if (flag == 3)
                {
                    settings.FirstPersonOnlyRenderers++;
                }
            }
        }

        private static VrmExpressionData ReadVrm0Clip(UnityYamlDocument clip)
        {
            VrmExpressionData expression = new VrmExpressionData
            {
                Name = clip.GetTopLevelValue("BlendShapeName") ?? string.Empty,
            };

            if (clip.TryGetInt("Preset", out int preset)
                && preset >= 0 && preset < Vrm0Roles.Length)
            {
                expression.Role = Vrm0Roles[preset];
                expression.PresetName = Vrm0PresetNames.Names[preset];
            }

            // A 0.x binding's weight is already on Unity's scale: UniVRM passes it straight to
            // SetBlendShapeWeight.
            ReadBindings(clip, "Values", 1f, expression);
            ReadVrm0MaterialValues(clip, expression);
            expression.IsBinary = clip.TryGetInt("IsBinary", out int binary) && binary != 0;

            if (string.IsNullOrEmpty(expression.Name))
            {
                expression.Name = clip.GetTopLevelValue("m_Name") ?? "Expression";
            }

            return expression;
        }

        private static VrmExpressionData ReadVrm10Clip(UnityYamlDocument clip)
        {
            VrmExpressionData expression = new VrmExpressionData
            {
                Name = clip.GetTopLevelValue("m_Name") ?? string.Empty,
            };

            ReadBindings(clip, "MorphTargetBindings", Vrm10WeightToUnity, expression);
            ReadVrm10MaterialBindings(clip, expression);

            expression.IsBinary = clip.TryGetInt("IsBinary", out int binary) && binary != 0;
            expression.OverrideBlink = OverrideOf(clip, "OverrideBlink");
            expression.OverrideLookAt = OverrideOf(clip, "OverrideLookAt");
            expression.OverrideMouth = OverrideOf(clip, "OverrideMouth");

            return expression;
        }

        private static VrmExpressionOverride OverrideOf(UnityYamlDocument clip, string key)
        {
            return clip.TryGetInt(key, out int value)
                ? (VrmExpressionOverride)value
                : VrmExpressionOverride.None;
        }

        /// <summary>
        /// The morph target bindings of either format. Both write a sequence of maps holding a
        /// relative path, an index into the mesh's blendshapes, and a weight.
        /// </summary>
        private static void ReadBindings(
            UnityYamlDocument clip, string key, float weightScale, VrmExpressionData expression)
        {
            if (!clip.TryGetTopLevelBlock(key, out List<string> block))
            {
                return;
            }

            VrmMorphBinding current = null;

            foreach (string line in block)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("-"))
                {
                    current = new VrmMorphBinding();
                    expression.Bindings.Add(current);
                    trimmed = trimmed.Substring(1).TrimStart();
                }

                if (current == null)
                {
                    continue;
                }

                if (trimmed.StartsWith("RelativePath:"))
                {
                    current.Path = trimmed.Substring("RelativePath:".Length).Trim();
                }
                else if (trimmed.StartsWith("Index:")
                         && UnityYamlValues.TryParseInt(
                             trimmed.Substring("Index:".Length).Trim(), out int index))
                {
                    current.Index = index;
                }
                else if (trimmed.StartsWith("Weight:")
                         && UnityYamlValues.TryParseFloat(
                             trimmed.Substring("Weight:".Length).Trim(), out float weight))
                {
                    current.Weight = weight * weightScale;
                }
            }
        }

        /// <summary>
        /// The map entries of a top level sequence, as key to value text, one dictionary per
        /// item. Both formats write material bindings as sequences of flat maps.
        /// </summary>
        private static List<Dictionary<string, string>> Entries(UnityYamlDocument clip, string key)
        {
            List<Dictionary<string, string>> items = new List<Dictionary<string, string>>();
            if (!clip.TryGetTopLevelBlock(key, out List<string> block))
            {
                return items;
            }

            Dictionary<string, string> current = null;
            foreach (string line in block)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("-"))
                {
                    current = new Dictionary<string, string>();
                    items.Add(current);
                    trimmed = trimmed.Substring(1).TrimStart();
                }

                int colon = trimmed.IndexOf(':');
                if (current == null || colon <= 0)
                {
                    continue;
                }

                current[trimmed.Substring(0, colon).Trim()] = trimmed.Substring(colon + 1).Trim();
            }

            return items;
        }

        private static readonly System.Text.RegularExpressions.Regex ComponentPattern =
            new System.Text.RegularExpressions.Regex(
                @"(?<key>[xyzwrgba])\s*:\s*(?<value>-?[0-9.eE+-]+)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>`{x: 1, y: 0, z: 0, w: 1}`, or the same with r, g, b, a, as a vector.</summary>
        private static Vector4 ParseVector(string text, Vector4 fallback)
        {
            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }

            Vector4 value = fallback;
            foreach (System.Text.RegularExpressions.Match match in ComponentPattern.Matches(text))
            {
                if (!float.TryParse(match.Groups["value"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float component))
                {
                    continue;
                }

                switch (match.Groups["key"].Value)
                {
                    case "x": case "r": value.x = component; break;
                    case "y": case "g": value.y = component; break;
                    case "z": case "b": value.z = component; break;
                    case "w": case "a": value.w = component; break;
                }
            }

            return value;
        }

        private static void ReadVrm10MaterialBindings(
            UnityYamlDocument clip, VrmExpressionData expression)
        {
            foreach (Dictionary<string, string> item in Entries(clip, "MaterialColorBindings"))
            {
                item.TryGetValue("MaterialName", out string material);
                item.TryGetValue("BindType", out string type);
                item.TryGetValue("TargetValue", out string target);
                expression.MaterialColorBindings.Add(new VrmMaterialColorBinding
                {
                    MaterialName = material ?? string.Empty,
                    PropertyName = VrmMaterialProperties.NameOf(
                        UnityYamlValues.TryParseInt(type ?? string.Empty, out int bindType)
                            ? bindType
                            : -1),
                    TargetValue = ParseVector(target, Vector4.zero),
                });
            }

            foreach (Dictionary<string, string> item in Entries(clip, "MaterialUVBindings"))
            {
                item.TryGetValue("MaterialName", out string material);
                item.TryGetValue("Scaling", out string scaling);
                item.TryGetValue("Offset", out string offset);
                Vector4 scale = ParseVector(scaling, new Vector4(1f, 1f, 0f, 0f));
                Vector4 shift = ParseVector(offset, Vector4.zero);
                expression.MaterialUvBindings.Add(new VrmMaterialUvBinding
                {
                    MaterialName = material ?? string.Empty,
                    Scaling = new Vector2(scale.x, scale.y),
                    Offset = new Vector2(shift.x, shift.y),
                });
            }
        }

        /// <summary>
        /// VRM 0.x names the shader property itself. `_MainTex_ST` is the texture scale and
        /// offset; anything else is a colour.
        /// </summary>
        private static void ReadVrm0MaterialValues(
            UnityYamlDocument clip, VrmExpressionData expression)
        {
            foreach (Dictionary<string, string> item in Entries(clip, "MaterialValues"))
            {
                item.TryGetValue("MaterialName", out string material);
                item.TryGetValue("ValueName", out string property);
                item.TryGetValue("TargetValue", out string target);
                Vector4 value = ParseVector(target, Vector4.zero);

                if (property == VrmMaterialProperties.UvProperty)
                {
                    expression.MaterialUvBindings.Add(new VrmMaterialUvBinding
                    {
                        MaterialName = material ?? string.Empty,
                        Scaling = new Vector2(value.x, value.y),
                        Offset = new Vector2(value.z, value.w),
                    });
                    continue;
                }

                if (string.IsNullOrEmpty(property))
                {
                    continue;
                }

                expression.MaterialColorBindings.Add(new VrmMaterialColorBinding
                {
                    MaterialName = material ?? string.Empty,
                    PropertyName = property,
                    TargetValue = value,
                });
            }
        }

        /// <summary>
        /// Object references in a top level sequence, as the pairs that identify an asset: its
        /// guid, and the fileID of the object within it for a sub-asset.
        /// </summary>
        private static IEnumerable<(string Guid, long FileId)> ObjectReferences(
            UnityYamlDocument document, string key)
        {
            List<(string, long)> references = new List<(string, long)>();
            if (!document.TryGetTopLevelBlock(key, out List<string> block))
            {
                return references;
            }

            foreach (string line in block)
            {
                if (TryParseReference(line, null, out string guid, out long fileId))
                {
                    references.Add((guid, fileId));
                }
            }

            return references;
        }

        private static IEnumerable<(string Guid, long FileId)> CustomClipReferences(
            List<string> block, string ownGuid)
        {
            List<(string, long)> references = new List<(string, long)>();
            bool inCustom = false;

            foreach (string line in block)
            {
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("CustomClips:"))
                {
                    inCustom = true;
                    continue;
                }

                if (!inCustom)
                {
                    continue;
                }

                if (!trimmed.StartsWith("-"))
                {
                    break;
                }

                if (TryParseReference(line, ownGuid, out string guid, out long fileId))
                {
                    references.Add((guid, fileId));
                }
            }

            return references;
        }

        private static bool TryReadReference(
            List<string> block, string key, string ownGuid, out string guid, out long fileId)
        {
            guid = null;
            fileId = 0L;

            foreach (string line in block)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(key + ":"))
                {
                    return TryParseReference(line, ownGuid, out guid, out fileId);
                }
            }

            return false;
        }

        /// <summary>
        /// A reference with no guid of its own points inside the file it was written in, which
        /// is how VRM 1.0 stores its expressions: sub-assets of the object asset.
        /// </summary>
        private static bool TryParseReference(
            string line, string ownGuid, out string guid, out long fileId)
        {
            guid = null;
            fileId = 0L;

            if (!UnityYamlValues.TryParseFileId(line, out fileId))
            {
                return false;
            }

            int at = line.IndexOf("guid:", System.StringComparison.Ordinal);
            if (at >= 0)
            {
                string rest = line.Substring(at + "guid:".Length).Trim();
                int end = rest.IndexOf(',');
                guid = (end >= 0 ? rest.Substring(0, end) : rest).Trim();
            }
            else
            {
                guid = ownGuid;
            }

            return !string.IsNullOrEmpty(guid);
        }

        /// <summary>
        /// True when a VRM object reference points at a file this cannot read, which is what a
        /// `.vrm` imported in place is: its expressions, licence and look at are sub-objects of
        /// a binary file rather than assets in the project.
        /// </summary>
        public static bool IsUnreadableSource(UnityYamlDocument instance)
        {
            if (instance == null || !instance.TryGetTopLevelObjectReference(
                    "Vrm", out string guid, out long fileId))
            {
                return false;
            }

            return Load(guid, fileId) == null;
        }

        /// <summary>One object out of an asset file, by the guid of the file and its fileID.</summary>
        private static UnityYamlDocument Load(string guid, long fileId)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            UnityYamlDocument first = null;
            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(path))
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour)
                {
                    continue;
                }

                if (document.FileId == fileId)
                {
                    return document;
                }

                first ??= document;
            }

            // A reference to the asset's main object names a fileID the file does not use as a
            // document id, so the only MonoBehaviour in it is the one meant.
            return first;
        }
    }
}
