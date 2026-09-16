using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>VRChat's expression menu control kinds.</summary>
    public enum VrcExpressionControlType
    {
        Button = 101,
        Toggle = 102,
        SubMenu = 103,
        TwoAxisPuppet = 201,
        FourAxisPuppet = 202,
        RadialPuppet = 203,
    }

    public enum VrcExpressionParameterType
    {
        Int = 0,
        Float = 1,
        Bool = 2,
    }

    public sealed class VrcExpressionControl
    {
        public string Name = string.Empty;
        public VrcExpressionControlType Type = VrcExpressionControlType.Button;

        /// <summary>Parameter this control drives. Empty on a submenu.</summary>
        public string Parameter = string.Empty;

        public float Value = 1f;

        /// <summary>
        /// Parameters a puppet drives. A radial names one float here and leaves
        /// <see cref="Parameter"/> empty; a two axis puppet names two, a four axis four.
        /// </summary>
        public List<string> SubParameters = new List<string>();

        /// <summary>Asset guid of the submenu, for a SubMenu control.</summary>
        public string SubMenuGuid;

        /// <summary>File id of the submenu inside its file; 0 when it is the file's main asset.</summary>
        public long SubMenuFileId;

        public bool HasIcon;
    }

    public sealed class VrcExpressionMenu
    {
        public string Name = string.Empty;
        public string Guid = string.Empty;
        public List<VrcExpressionControl> Controls = new List<VrcExpressionControl>();
    }

    public sealed class VrcExpressionParameter
    {
        public string Name = string.Empty;
        public VrcExpressionParameterType Type = VrcExpressionParameterType.Int;
        public bool Saved;
        public float DefaultValue;
        public bool NetworkSynced = true;
    }

    /// <summary>
    /// An avatar's whole expression menu tree and its parameters, flattened.
    /// <para>
    /// Basis has neither: no menu format and no synced parameter list. This is read so a
    /// conversion can describe what is there and what rebuilding it in HVR Vixxy involves,
    /// rather than reporting only that a menu exists.
    /// </para>
    /// </summary>
    public sealed class VrcExpressionInventory
    {
        public List<VrcExpressionMenu> Menus = new List<VrcExpressionMenu>();
        public List<VrcExpressionParameter> Parameters = new List<VrcExpressionParameter>();

        /// <summary>Menu and parameter assets the descriptor names that could not be read.</summary>
        public List<VrcExpressionAssetProblem> Problems = new List<VrcExpressionAssetProblem>();

        public int ControlCount
        {
            get
            {
                int count = 0;
                foreach (VrcExpressionMenu menu in Menus)
                {
                    count += menu.Controls.Count;
                }

                return count;
            }
        }

        public int CountOf(VrcExpressionControlType type)
        {
            int count = 0;
            foreach (VrcExpressionMenu menu in Menus)
            {
                foreach (VrcExpressionControl control in menu.Controls)
                {
                    if (control.Type == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    public enum VrcExpressionAssetProblemKind
    {
        /// <summary>The guid resolves to nothing in this project.</summary>
        Missing,

        /// <summary>The file exists but holds no text documents: Unity's binary form.</summary>
        NotText,

        /// <summary>The file is text but holds no document for the asset.</summary>
        NoDocument,
    }

    /// <summary>A menu or parameter asset the descriptor names that could not be read.</summary>
    public sealed class VrcExpressionAssetProblem
    {
        /// <summary>"menu", "submenu" or "parameters".</summary>
        public string Role = string.Empty;
        public string Guid = string.Empty;
        public string Path = string.Empty;

        /// <summary>The file id named by the reference; 0 for a file's main asset.</summary>
        public long FileId;
        public VrcExpressionAssetProblemKind Kind;
    }
}
