using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Locates a transform by its chain of sibling indices from a root, rather than by name.
    /// <para>
    /// Used between two hierarchies of the same shape, and where a hierarchy is not an instance of
    /// the asset it was read from. An instance that adds or removes children does not have the
    /// asset's shape; <see cref="PrefabCorrespondence"/> handles that case. Names are not used:
    /// avatars routinely repeat bone names, and Basis only renames duplicates at build time.
    /// </para>
    /// </summary>
    public static class TransformIndexPath
    {
        /// <summary>
        /// The path from <paramref name="root"/> down to <paramref name="target"/>, or null when
        /// the target is not under that root. An empty path means the target is the root.
        /// </summary>
        public static int[] Of(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            List<int> reversed = new List<int>();
            Transform cursor = target;

            while (cursor != root)
            {
                Transform parent = cursor.parent;
                if (parent == null)
                {
                    return null;
                }

                reversed.Add(cursor.GetSiblingIndex());
                cursor = parent;
            }

            reversed.Reverse();
            return reversed.ToArray();
        }

        public static bool TryResolve(Transform root, int[] path, out Transform resolved)
        {
            resolved = null;
            if (root == null || path == null)
            {
                return false;
            }

            Transform cursor = root;
            foreach (int index in path)
            {
                if (index < 0 || index >= cursor.childCount)
                {
                    return false;
                }

                cursor = cursor.GetChild(index);
            }

            resolved = cursor;
            return true;
        }
    }
}
