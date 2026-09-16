using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>What one VRCFury component carries: one feature, or several in the older layout.</summary>
    public sealed class VrcFuryComponentData
    {
        public long OwnerGameObjectFileId;

        public List<VrcFuryFeatureData> Features = new List<VrcFuryFeatureData>();
    }

    public class VrcFuryFeatureData
    {
        /// <summary>The model class name as VRCFury serializes it: Toggle, FullController, ArmatureLink, and so on.</summary>
        public string Class = string.Empty;

        /// <summary>The model's own schema version; -1 when it was never written.</summary>
        public int Version = -1;
    }

    public enum VrcFuryActionKind
    {
        Other = 0,
        ObjectToggle,
        BlendShape,
        MaterialSwap,
        MaterialProperty,
    }

    public enum VrcFuryObjectToggleMode
    {
        TurnOn = 0,
        TurnOff = 1,
        Toggle = 2,
    }

    public enum VrcFuryMaterialPropertyType
    {
        Float = 0,
        Color = 1,
        Vector = 2,
        St = 3,
        LegacyAuto = 4,
    }

    public sealed class VrcFuryActionData
    {
        public VrcFuryActionKind Kind;

        /// <summary>The class name, kept for the report when the kind is Other.</summary>
        public string Class = string.Empty;

        public int Version = -1;

        // Object toggle
        public long ObjectFileId;
        public VrcFuryObjectToggleMode Mode = VrcFuryObjectToggleMode.TurnOn;

        // Blendshape
        public string BlendShape = string.Empty;
        public float BlendShapeValue = 100f;
        public bool AllRenderers = true;
        /// <summary>The renderer component's file id, when one renderer is named.</summary>
        public long RendererFileId;

        // Material swap
        public int MaterialIndex;
        public string MaterialGuid;

        // Material property
        /// <summary>The object carrying the renderer, in the current layout.</summary>
        public long RendererObjectFileId;
        public bool AffectAllMeshes;
        public string PropertyName = string.Empty;
        public VrcFuryMaterialPropertyType PropertyType = VrcFuryMaterialPropertyType.Float;
        public float Value;
        public float[] ValueVector = new float[4];
        public float[] ValueColor = { 1f, 1f, 1f, 1f };
    }

    public sealed class VrcFuryToggleData : VrcFuryFeatureData
    {
        /// <summary>The menu path, folders separated by slashes; empty for a toggle with no menu item.</summary>
        public string Name = string.Empty;

        public bool Saved;
        public bool Slider;
        public bool SliderInactiveAtZero;
        public float DefaultSliderValue;
        public bool DefaultOn;
        public bool HoldButton;
        public bool SecurityEnabled;
        public bool SeparateLocal;
        public bool HasTransition;
        public bool UseGlobalParam;
        public string GlobalParam = string.Empty;
        public bool EnableExclusiveTag;
        public string ExclusiveTag = string.Empty;
        public bool EnableDriveGlobalParam;
        public bool InvertRestLogic;

        public List<VrcFuryActionData> Actions = new List<VrcFuryActionData>();
    }

    public sealed class VrcFuryAssetReference
    {
        public string Guid;
        public long FileId;

        public bool HasGuid => !string.IsNullOrEmpty(Guid);
    }

    public sealed class VrcFuryControllerEntry
    {
        public VrcFuryAssetReference Controller = new VrcFuryAssetReference();

        /// <summary>VRChat's layer type: 5 is FX.</summary>
        public int Type = 5;

        public const int TypeFx = 5;
    }

    public sealed class VrcFuryMenuEntry
    {
        public VrcFuryAssetReference Menu = new VrcFuryAssetReference();
        public string Prefix = string.Empty;
    }

    public sealed class VrcFuryBindingRewrite
    {
        public string From = string.Empty;
        public string To = string.Empty;
        public bool Delete;
    }

    public sealed class VrcFuryFullControllerData : VrcFuryFeatureData
    {
        public List<VrcFuryControllerEntry> Controllers = new List<VrcFuryControllerEntry>();
        public List<VrcFuryMenuEntry> Menus = new List<VrcFuryMenuEntry>();
        public List<VrcFuryAssetReference> Parameters = new List<VrcFuryAssetReference>();
        public List<string> GlobalParams = new List<string>();
        public List<VrcFuryBindingRewrite> RewriteBindings = new List<VrcFuryBindingRewrite>();
        public long RootObjectOverrideFileId;
        public bool RootBindingsApplyToAvatar;
        public string ToggleParam = string.Empty;
        public bool IgnoreSaved;
    }

    public sealed class VrcFuryLinkTarget
    {
        public bool UseBone = true;
        public int Bone;
        public bool UseObject;
        public long ObjectFileId;
        public string Offset = string.Empty;
    }

    public sealed class VrcFuryArmatureLinkData : VrcFuryFeatureData
    {
        public long PropBoneFileId;
        public List<VrcFuryLinkTarget> LinkTo = new List<VrcFuryLinkTarget>();
        public bool Recursive;
        public bool RecursiveKnown;
        public bool AlignPosition;
        public bool AlignRotation;
        public bool AlignScale;
        public bool RemoveParentConstraints = true;
        public string RemoveBoneSuffix = string.Empty;
    }

    /// <summary>
    /// Reads a VRCFury component from its document.
    /// <para>
    /// VRCFury stores each component's feature as a managed reference: <c>content</c> names an
    /// id, and <c>references.RefIds</c> holds every referenced object with its class and data.
    /// Older components hold a list under <c>config.features</c> instead; both are read. Each
    /// class carries its own schema version, and the upgrade rules VRCFury applies on load are
    /// applied here, so an older file reads as VRCFury itself would read it.
    /// </para>
    /// </summary>
    public static class VrcFuryDocumentReader
    {
        private static readonly Regex GuidInId = new Regex(@"^(?<guid>[0-9a-fA-F]{32})", RegexOptions.Compiled);

        public static VrcFuryComponentData Read(UnityYamlDocument document)
        {
            VrcFuryComponentData data = new VrcFuryComponentData();

            if (document.TryGetTopLevelFileIdReference("m_GameObject", out long owner))
            {
                data.OwnerGameObjectFileId = owner;
            }

            Dictionary<long, UnityYamlBlock> references = ReadReferences(document);

            List<long> featureIds = new List<long>();
            if (document.TryGetTopLevelBlock("content", out List<string> contentLines))
            {
                long rid = UnityYamlBlock.Of(contentLines).Long("rid", -2L);
                if (rid >= 0)
                {
                    featureIds.Add(rid);
                }
            }

            if (document.TryGetTopLevelBlock("config", out List<string> configLines))
            {
                foreach (UnityYamlBlock entry in UnityYamlBlock.Of(configLines).Child("features").Entries())
                {
                    long rid = entry.Long("rid", -2L);
                    if (rid >= 0 && !featureIds.Contains(rid))
                    {
                        featureIds.Add(rid);
                    }
                }
            }

            foreach (long rid in featureIds)
            {
                if (!references.TryGetValue(rid, out UnityYamlBlock reference))
                {
                    continue;
                }

                VrcFuryFeatureData feature = ReadFeature(reference, references);
                if (feature != null)
                {
                    data.Features.Add(feature);
                }
            }

            return data;
        }

        /// <summary>Every managed reference in the document, by its id, as the entry's block.</summary>
        private static Dictionary<long, UnityYamlBlock> ReadReferences(UnityYamlDocument document)
        {
            Dictionary<long, UnityYamlBlock> byId = new Dictionary<long, UnityYamlBlock>();
            if (!document.TryGetTopLevelBlock("references", out List<string> lines))
            {
                return byId;
            }

            foreach (UnityYamlBlock entry in UnityYamlBlock.Of(lines).Child("RefIds").Entries())
            {
                string raw = entry.Scalar("rid");
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long rid))
                {
                    byId[rid] = entry;
                }
            }

            return byId;
        }

        private static string ClassOf(UnityYamlBlock entry)
        {
            Dictionary<string, string> type = entry.Flow("type");
            return type != null && type.TryGetValue("class", out string name) ? name : string.Empty;
        }

        private static VrcFuryFeatureData ReadFeature(
            UnityYamlBlock entry, Dictionary<long, UnityYamlBlock> references)
        {
            string className = ClassOf(entry);
            UnityYamlBlock data = entry.Child("data");
            int version = data.Int("version", -1);

            switch (className)
            {
                case "Toggle":
                    return ReadToggle(data, version, references);
                case "FullController":
                    return ReadFullController(data, version);
                case "ArmatureLink":
                    return ReadArmatureLink(data, version);
                case "":
                    return null;
                default:
                    return new VrcFuryFeatureData { Class = className, Version = version };
            }
        }

        private static VrcFuryToggleData ReadToggle(
            UnityYamlBlock data, int version, Dictionary<long, UnityYamlBlock> references)
        {
            VrcFuryToggleData toggle = new VrcFuryToggleData
            {
                Class = "Toggle",
                Version = version,
                Name = data.Text("name"),
                Saved = data.Bool("saved"),
                Slider = data.Bool("slider"),
                SliderInactiveAtZero = data.Bool("sliderInactiveAtZero"),
                DefaultSliderValue = data.Float("defaultSliderValue"),
                DefaultOn = data.Bool("defaultOn"),
                HoldButton = data.Bool("holdButton"),
                SecurityEnabled = data.Bool("securityEnabled"),
                SeparateLocal = data.Bool("separateLocal"),
                HasTransition = data.Bool("hasTransition"),
                UseGlobalParam = data.Bool("useGlobalParam"),
                GlobalParam = data.Text("globalParam"),
                EnableExclusiveTag = data.Bool("enableExclusiveTag"),
                ExclusiveTag = data.Text("exclusiveTag"),
                EnableDriveGlobalParam = data.Bool("enableDriveGlobalParam"),
                InvertRestLogic = data.Bool("invertRestLogic"),
            };

            // VRCFury's own upgrade of a Toggle written before version 2.
            if (version < 2)
            {
                if (!toggle.DefaultOn)
                {
                    toggle.DefaultSliderValue = 0f;
                }

                toggle.SliderInactiveAtZero = true;
            }

            foreach (UnityYamlBlock actionRef in data.Child("state").Child("actions").Entries())
            {
                long rid = actionRef.Long("rid", -2L);
                if (rid < 0 || !references.TryGetValue(rid, out UnityYamlBlock action))
                {
                    continue;
                }

                toggle.Actions.Add(ReadAction(action));
            }

            return toggle;
        }

        private static VrcFuryActionData ReadAction(UnityYamlBlock entry)
        {
            string className = ClassOf(entry);
            UnityYamlBlock data = entry.Child("data");
            VrcFuryActionData action = new VrcFuryActionData
            {
                Class = className,
                Version = data.Int("version", -1),
            };

            switch (className)
            {
                case "ObjectToggleAction":
                    action.Kind = VrcFuryActionKind.ObjectToggle;
                    action.ObjectFileId = data.FileId("obj");
                    action.Mode = (VrcFuryObjectToggleMode)data.Int("mode");
                    // Before version 1 the only mode was to flip the object's state.
                    if (action.Version < 1)
                    {
                        action.Mode = VrcFuryObjectToggleMode.Toggle;
                    }

                    break;

                case "BlendShapeAction":
                    action.Kind = VrcFuryActionKind.BlendShape;
                    action.BlendShape = data.Text("blendShape");
                    action.BlendShapeValue = data.Float("blendShapeValue", 100f);
                    action.AllRenderers = data.Scalar("allRenderers") == null || data.Bool("allRenderers");
                    action.RendererFileId = data.FileId("renderer");
                    break;

                case "MaterialAction":
                    action.Kind = VrcFuryActionKind.MaterialSwap;
                    action.RendererFileId = data.FileId("renderer");
                    action.MaterialIndex = data.Int("materialIndex");
                    action.MaterialGuid = ReadReference(data.Child("mat")).Guid;
                    break;

                case "MaterialPropertyAction":
                    action.Kind = VrcFuryActionKind.MaterialProperty;
                    action.RendererObjectFileId = data.FileId("renderer2");
                    // Before version 1 the renderer component itself was named.
                    if (action.Version < 1)
                    {
                        action.RendererFileId = data.FileId("renderer");
                    }

                    action.AffectAllMeshes = data.Bool("affectAllMeshes");
                    action.PropertyName = data.Text("propertyName");
                    action.PropertyType = action.Version < 2
                        ? VrcFuryMaterialPropertyType.LegacyAuto
                        : (VrcFuryMaterialPropertyType)data.Int("propertyType");
                    action.Value = data.Float("value");
                    action.ValueVector = ReadFour(data, "valueVector", "x", "y", "z", "w", 0f);
                    action.ValueColor = ReadFour(data, "valueColor", "r", "g", "b", "a", 1f);
                    break;

                default:
                    action.Kind = VrcFuryActionKind.Other;
                    break;
            }

            return action;
        }

        private static float[] ReadFour(
            UnityYamlBlock data, string key, string a, string b, string c, string d, float fallback)
        {
            Dictionary<string, string> flow = data.Flow(key);
            float[] values = { fallback, fallback, fallback, fallback };
            if (flow == null)
            {
                return values;
            }

            string[] keys = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                if (flow.TryGetValue(keys[i], out string raw)
                    && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    values[i] = value;
                }
            }

            return values;
        }

        /// <summary>
        /// A VRCFury asset reference: <c>objRef</c> is the live reference and <c>id</c> a string
        /// beginning with the guid, kept so the reference survives a missing asset. The older
        /// layout had <c>guid</c> and <c>fileID</c> fields of their own.
        /// </summary>
        private static VrcFuryAssetReference ReadReference(UnityYamlBlock block)
        {
            VrcFuryAssetReference reference = new VrcFuryAssetReference();
            if (block.IsEmpty)
            {
                return reference;
            }

            reference.Guid = block.Guid("objRef");
            reference.FileId = block.FileId("objRef");

            if (!reference.HasGuid)
            {
                Match match = GuidInId.Match(block.Text("id"));
                if (match.Success)
                {
                    reference.Guid = match.Groups["guid"].Value;
                }
            }

            if (!reference.HasGuid)
            {
                string legacy = block.Text("guid");
                if (GuidInId.IsMatch(legacy))
                {
                    reference.Guid = legacy;
                    reference.FileId = (long)block.Float("fileID");
                }
            }

            return reference;
        }

        private static VrcFuryFullControllerData ReadFullController(UnityYamlBlock data, int version)
        {
            VrcFuryFullControllerData controller = new VrcFuryFullControllerData
            {
                Class = "FullController",
                Version = version,
                RootObjectOverrideFileId = data.FileId("rootObjOverride"),
                RootBindingsApplyToAvatar = data.Bool("rootBindingsApplyToAvatar"),
                ToggleParam = data.Text("toggleParam"),
                IgnoreSaved = data.Bool("ignoreSaved"),
            };

            foreach (UnityYamlBlock entry in data.Child("controllers").Entries())
            {
                controller.Controllers.Add(new VrcFuryControllerEntry
                {
                    Controller = ReadReference(entry.Child("controller")),
                    Type = entry.Int("type", VrcFuryControllerEntry.TypeFx),
                });
            }

            foreach (UnityYamlBlock entry in data.Child("menus").Entries())
            {
                controller.Menus.Add(new VrcFuryMenuEntry
                {
                    Menu = ReadReference(entry.Child("menu")),
                    Prefix = entry.Text("prefix"),
                });
            }

            foreach (UnityYamlBlock entry in data.Child("prms").Entries())
            {
                controller.Parameters.Add(ReadReference(entry.Child("parameters")));
            }

            foreach (UnityYamlBlock entry in data.Child("globalParams").Entries())
            {
                string name = entry.Lines.Count > 0 ? entry.Lines[0].Trim() : string.Empty;
                if (name.Length > 0)
                {
                    controller.GlobalParams.Add(name);
                }
            }

            foreach (UnityYamlBlock entry in data.Child("rewriteBindings").Entries())
            {
                controller.RewriteBindings.Add(new VrcFuryBindingRewrite
                {
                    From = entry.Text("from"),
                    To = entry.Text("to"),
                    Delete = entry.Bool("delete"),
                });
            }

            // Before version 2 a single controller, menu and parameter asset were fields of
            // their own.
            if (version < 2)
            {
                VrcFuryAssetReference single = ReadReference(data.Child("controller"));
                if (single.HasGuid)
                {
                    controller.Controllers.Add(new VrcFuryControllerEntry { Controller = single });
                }

                VrcFuryAssetReference menu = ReadReference(data.Child("menu"));
                if (menu.HasGuid)
                {
                    controller.Menus.Add(new VrcFuryMenuEntry { Menu = menu, Prefix = data.Text("submenu") });
                }

                VrcFuryAssetReference parameters = ReadReference(data.Child("parameters"));
                if (parameters.HasGuid)
                {
                    controller.Parameters.Add(parameters);
                }
            }

            return controller;
        }

        private static VrcFuryArmatureLinkData ReadArmatureLink(UnityYamlBlock data, int version)
        {
            VrcFuryArmatureLinkData link = new VrcFuryArmatureLinkData
            {
                Class = "ArmatureLink",
                Version = version,
                PropBoneFileId = data.FileId("propBone"),
                AlignPosition = data.Bool("alignPosition"),
                AlignRotation = data.Bool("alignRotation"),
                AlignScale = data.Bool("alignScale"),
                RemoveParentConstraints = data.Scalar("removeParentConstraints") == null || data.Bool("removeParentConstraints"),
                RemoveBoneSuffix = data.Text("removeBoneSuffix"),
            };

            foreach (UnityYamlBlock entry in data.Child("linkTo").Entries())
            {
                link.LinkTo.Add(new VrcFuryLinkTarget
                {
                    UseBone = entry.Scalar("useBone") == null || entry.Bool("useBone"),
                    Bone = entry.Int("bone"),
                    UseObject = entry.Bool("useObj"),
                    ObjectFileId = entry.FileId("obj"),
                    Offset = entry.Text("offset"),
                });
            }

            if (version >= 7)
            {
                link.Recursive = data.Bool("recursive");
                link.RecursiveKnown = true;
            }
            else
            {
                // Before version 7 the mode field decided: merging as children or rewriting the
                // skin meant the whole hierarchy, reparenting the root meant that one bone, and
                // Auto depended on the live meshes, which this cannot see.
                int linkMode = data.Int("linkMode", 4);
                link.RecursiveKnown = linkMode != 4;
                link.Recursive = linkMode == 0 || linkMode == 1 || linkMode == 2;
            }

            return link;
        }
    }
}
