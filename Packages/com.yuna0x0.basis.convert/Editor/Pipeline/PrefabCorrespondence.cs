using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Finds the object standing for a prefab asset's transform in an instance of that prefab
    /// through Unity's prefab links.
    /// <para>
    /// A sibling-index path taken in the asset does not hold in an instance that adds or removes
    /// children: an object added ahead of the prefab's own, or one removed before them, shifts
    /// every index after it. Following the links level by level skips added objects and finds
    /// nothing for removed ones.
    /// </para>
    /// </summary>
    public static class PrefabCorrespondence
    {
        public enum Outcome
        {
            /// <summary>The instance's object was found.</summary>
            Found,

            /// <summary>The instance is linked to the asset but has no object for this one: it was removed on the instance.</summary>
            Removed,

            /// <summary>The hierarchy is not an instance of the asset, so links say nothing.</summary>
            NoLink,
        }

        /// <param name="liveRoot">The instance's root, the object standing for <paramref name="assetRoot"/>.</param>
        /// <param name="assetRoot">The prefab asset's root.</param>
        /// <param name="asset">A transform under <paramref name="assetRoot"/>.</param>
        public static Outcome Find(
            Transform liveRoot, Transform assetRoot, Transform asset, out Transform live)
        {
            live = null;
            if (liveRoot == null || assetRoot == null || asset == null
                || !Corresponds(liveRoot, assetRoot))
            {
                return Outcome.NoLink;
            }

            List<Transform> steps = new List<Transform>();
            for (Transform cursor = asset; cursor != assetRoot; cursor = cursor.parent)
            {
                if (cursor == null)
                {
                    return Outcome.NoLink;
                }

                steps.Add(cursor);
            }

            steps.Reverse();
            Transform at = liveRoot;
            foreach (Transform step in steps)
            {
                Transform match = null;
                for (int i = 0; i < at.childCount && match == null; i++)
                {
                    Transform child = at.GetChild(i);
                    if (Corresponds(child, step))
                    {
                        match = child;
                    }
                }

                if (match == null)
                {
                    return Outcome.Removed;
                }

                at = match;
            }

            live = at;
            return Outcome.Found;
        }

        /// <summary>
        /// Whether <paramref name="asset"/> is <paramref name="live"/> or one of the objects it
        /// comes from: the prefab it is an instance of, the base of a variant, or a nested
        /// prefab's own asset.
        /// </summary>
        private static bool Corresponds(Transform live, Transform asset)
        {
            Object cursor = live;
            for (int depth = 0; cursor != null && depth < 64; depth++)
            {
                if (cursor == asset)
                {
                    return true;
                }

                cursor = PrefabUtility.GetCorrespondingObjectFromSource(cursor);
            }

            return false;
        }
    }
}
