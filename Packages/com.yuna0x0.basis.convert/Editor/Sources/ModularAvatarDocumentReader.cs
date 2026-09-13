using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>An animator controller Modular Avatar merges into the avatar at build time.</summary>
    public sealed class MaMergeAnimatorData
    {
        public long OwnerGameObjectFileId;

        /// <summary>Asset guid of the controller. Native Unity asset, so it is loaded, not parsed.</summary>
        public string ControllerGuid = string.Empty;

        /// <summary>VRChat's animation layer slot, where 5 is FX. Same numbering as the descriptor.</summary>
        public int LayerType;

        /// <summary>
        /// 0 addresses objects relative to the component's own object, 1 relative to the avatar
        /// root. Read off a real gimmick pack: its clips animate `Melody` and `Key/Armature`,
        /// which are children of the object holding the component, with this set to 0.
        /// </summary>
        public int PathMode;
    }

    /// <summary>A menu entry Modular Avatar installs into the avatar's expression menu.</summary>
    public sealed class MaMenuItemData
    {
        public long OwnerGameObjectFileId;

        /// <summary>Label. Empty means the object's own name is used.</summary>
        public string Name = string.Empty;

        /// <summary>VRChat's control type: 101 button, 102 toggle, 103 submenu, 201 two axis,
        /// 202 four axis and 203 radial puppet.</summary>
        public int ControlType;

        public string Parameter = string.Empty;

        /// <summary>Value of the parameter this entry selects. Several entries commonly share
        /// one parameter and pick different values from it.</summary>
        public float Value = 1f;

        /// <summary>The menu label. Modular Avatar shows this, else the object's name.</summary>
        public string Label = string.Empty;

        /// <summary>Whether the parameter starts at this item's value.</summary>
        public bool IsDefault;

        /// <summary>Whether the value is chosen at build time rather than authored.</summary>
        public bool AutomaticValue = true;

        public bool IsSaved = true;
        public bool IsSynced = true;

        public bool IsToggle => ControlType == ControlTypeToggle;

        public const int ControlTypeToggle = 102;
    }

    /// <summary>One object an Object Toggle switches, and the state it switches it to.</summary>
    public sealed class MaToggledObject
    {
        /// <summary>Transform path, relative to the avatar root Modular Avatar resolves against.</summary>
        public string Path = string.Empty;

        /// <summary>The object itself, which Modular Avatar prefers to the path when it is set.</summary>
        public long TargetObjectFileId;

        public bool Active;
    }

    /// <summary>
    /// A Modular Avatar Object Toggle: the objects it switches when its own object is active.
    /// </summary>
    public sealed class MaObjectToggleData
    {
        public long OwnerGameObjectFileId;

        /// <summary>True when the component acts on its object being inactive instead.</summary>
        public bool Inverted;

        public List<MaToggledObject> Objects = new List<MaToggledObject>();
    }

    /// <summary>
    /// Reads the Modular Avatar components a conversion cares about.
    /// <para>
    /// Modular Avatar ships loose scripts, so its components are identified by guid alone and
    /// their fields are read from YAML like every other source component. That way the package is
    /// never a dependency, and clothing reads the same whether or not Modular Avatar is installed
    /// in the project being converted into.
    /// </para>
    /// </summary>
    public static class ModularAvatarDocumentReader
    {
        public static MaMergeAnimatorData ReadMergeAnimator(UnityYamlDocument document)
        {
            MaMergeAnimatorData data = new MaMergeAnimatorData();

            if (document.TryGetTopLevelFileIdReference("m_GameObject", out long owner))
            {
                data.OwnerGameObjectFileId = owner;
            }

            if (document.TryGetTopLevelObjectReference("animator", out string guid, out long _))
            {
                data.ControllerGuid = guid;
            }

            if (document.TryGetInt("layerType", out int layerType))
            {
                data.LayerType = layerType;
            }

            if (document.TryGetInt("pathMode", out int pathMode))
            {
                data.PathMode = pathMode;
            }

            return data;
        }

        public static MaMenuItemData ReadMenuItem(UnityYamlDocument document)
        {
            MaMenuItemData data = new MaMenuItemData();

            if (document.TryGetTopLevelFileIdReference("m_GameObject", out long owner))
            {
                data.OwnerGameObjectFileId = owner;
            }

            data.Label = document.GetTopLevelValue("label") ?? string.Empty;
            if (document.TryGetBool("isDefault", out bool isDefault))
            {
                data.IsDefault = isDefault;
            }

            if (document.TryGetBool("automaticValue", out bool automatic))
            {
                data.AutomaticValue = automatic;
            }

            if (document.TryGetBool("isSaved", out bool saved))
            {
                data.IsSaved = saved;
            }

            if (document.TryGetBool("isSynced", out bool synced))
            {
                data.IsSynced = synced;
            }

            if (!document.TryGetTopLevelBlock("Control", out List<string> control))
            {
                return data;
            }

            bool inParameter = false;

            foreach (string line in control)
            {
                int indent = IndentOf(line);
                string trimmed = line.Trim();

                if (indent == 4)
                {
                    inParameter = trimmed.StartsWith("parameter:");

                    if (trimmed.StartsWith("name:"))
                    {
                        data.Name = ValueOf(trimmed);
                    }
                    else if (trimmed.StartsWith("type:")
                             && int.TryParse(ValueOf(trimmed), out int type))
                    {
                        data.ControlType = type;
                    }
                    else if (trimmed.StartsWith("value:")
                             && float.TryParse(ValueOf(trimmed),
                                 System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out float value))
                    {
                        data.Value = value;
                    }

                    continue;
                }

                // The control's parameter is a block of its own holding a single name.
                if (inParameter && indent == 6 && trimmed.StartsWith("name:"))
                {
                    data.Parameter = ValueOf(trimmed);
                }
            }

            return data;
        }

        /// <summary>
        /// Reads an Object Toggle's list. Each entry is an object reference by path and the
        /// state it is switched to, which is the same shape a menu toggle's clip produces.
        /// </summary>
        public static MaObjectToggleData ReadObjectToggle(UnityYamlDocument document)
        {
            MaObjectToggleData data = new MaObjectToggleData();

            if (document.TryGetTopLevelFileIdReference("m_GameObject", out long owner))
            {
                data.OwnerGameObjectFileId = owner;
            }

            if (document.TryGetBool("m_inverted", out bool inverted))
            {
                data.Inverted = inverted;
            }

            if (!document.TryGetTopLevelBlock("m_objects", out List<string> block))
            {
                return data;
            }

            MaToggledObject current = null;

            foreach (string line in block)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("- Object:") || trimmed == "-")
                {
                    current = new MaToggledObject();
                    data.Objects.Add(current);
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (trimmed.StartsWith("referencePath:"))
                {
                    current.Path = ValueOf(trimmed);
                }
                else if (trimmed.StartsWith("targetObject:"))
                {
                    int at = trimmed.IndexOf("fileID:");
                    if (at >= 0)
                    {
                        string digits = trimmed.Substring(at + 7).Trim().TrimEnd('}').Trim();
                        long.TryParse(digits, out current.TargetObjectFileId);
                    }
                }
                else if (trimmed.StartsWith("Active:"))
                {
                    current.Active = ValueOf(trimmed) == "1";
                }
            }

            return data;
        }

        private static string ValueOf(string trimmed)
        {
            int colon = trimmed.IndexOf(':');
            return colon < 0 ? string.Empty : trimmed.Substring(colon + 1).Trim();
        }

        private static int IndentOf(string line)
        {
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            return indent;
        }
    }
}
