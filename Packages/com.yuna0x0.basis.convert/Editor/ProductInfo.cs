namespace yuna0x0.Basis.Convert
{
    /// <summary>
    /// Names shown in the editor, in one place.
    /// <para>
    /// Menus live under our own product name rather than Basis's menu. Basis's trademark policy
    /// asks third parties not to imply affiliation, and the surrounding ecosystem does the same:
    /// NDMF uses Tools/NDM Framework, Modular Avatar uses Tools/Modular Avatar, and Unity's own
    /// guidance is to nest under an existing menu or under Tools rather than add a top-level one.
    /// </para>
    /// </summary>
    public static class ProductInfo
    {
        public const string Name = "Watari";

        public const string ToolsMenu = "Tools/" + Name + "/";
        public const string GameObjectMenu = "GameObject/" + Name + "/";

        /// <summary>
        /// The releases of each source the readers were checked against. A component or field a
        /// later release adds is not read until this is raised, so the report and the docs say
        /// which ones were.
        /// </summary>
        public const string CheckedAgainst =
            "VRChat SDK 3.10.5, UniVRM 0.131.2, Dynamic Bone 1.3.4, Modular Avatar 1.18.7, VRCFury 1.1429.0";

        /// <summary>The package version, from its manifest.</summary>
        public static string Version =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ProductInfo).Assembly)
                ?.version ?? "unknown";
    }
}
