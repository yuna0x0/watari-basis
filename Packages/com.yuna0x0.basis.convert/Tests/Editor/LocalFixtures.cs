using System.Collections.Generic;
using System.IO;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Locates avatars the package does not ship.
    /// <para>
    /// A purchased avatar cannot be committed, so the tests that read one look for it under
    /// <c>Assets/_UserContent</c>, the folder Basis keeps out of version control, and under
    /// any folder directly inside <c>Assets</c>. A test whose fixture is absent skips.
    /// </para>
    /// </summary>
    internal static class LocalFixtures
    {
        public const string ShinanoPrefab = "Avatars/Shinano/Prefab/Shinano.prefab";
        public const string ShinanoModel = "Avatars/Shinano/FBX/Shinano.fbx";

        /// <summary>The project-relative path of the fixture, or null when no root holds it.</summary>
        public static string Find(string relativePath)
        {
            foreach (string root in Roots())
            {
                string candidate = root + "/" + relativePath;
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> Roots()
        {
            foreach (string parent in new[] { "Assets/_UserContent", "Assets" })
            {
                if (!Directory.Exists(parent))
                {
                    continue;
                }

                foreach (string folder in Directory.GetDirectories(parent))
                {
                    yield return folder.Replace('\\', '/');
                }
            }
        }
    }
}
