using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Finds, for a transform of a prefab asset the plan read, the transform standing for it
    /// in the hierarchy that was scanned.
    /// <para>
    /// Readers resolve references inside the file they read, so what they hand back are
    /// transforms of that prefab's asset. A toggle in one prefab commonly names a renderer in
    /// another, and what a control switches has to be the object in the avatar, with every
    /// override its instance carries. Each source records where it sits in the hierarchy, so an
    /// asset transform is found again by its sibling-index path under that instance.
    /// </para>
    /// </summary>
    public sealed class HierarchyLocator
    {
        /// <summary>The object that was scanned: a scene instance, or the asset itself.</summary>
        public readonly Transform Root;

        private readonly List<ConversionSource> _sources;

        public HierarchyLocator(GameObject hierarchyRoot, List<ConversionSource> sources)
        {
            Root = hierarchyRoot != null ? hierarchyRoot.transform : null;
            _sources = sources ?? new List<ConversionSource>();
        }

        /// <summary>Where this source's instance sits in the hierarchy, or null if it moved.</summary>
        public Transform LiveRootOf(ConversionSource source)
        {
            if (Root == null || source?.Root == null)
            {
                return null;
            }

            if (source.Root.transform == Root)
            {
                return Root;
            }

            return TransformIndexPath.TryResolve(Root, source.PathInHierarchy, out Transform at)
                ? at
                : null;
        }

        /// <summary>
        /// The hierarchy transform for one of this source's asset transforms. A transform that
        /// is already in the hierarchy is returned as it is; one from another source's asset is
        /// located through that source.
        /// </summary>
        public bool TryLive(ConversionSource source, Transform asset, out Transform live)
        {
            live = null;
            if (Root == null || asset == null)
            {
                return false;
            }

            if (asset.IsChildOf(Root))
            {
                live = asset;
                return true;
            }

            Transform sourceRoot = source?.Root != null ? source.Root.transform : null;
            if (sourceRoot == null || !asset.IsChildOf(sourceRoot))
            {
                return TryLive(asset, out live);
            }

            Transform liveRoot = LiveRootOf(source);
            int[] path = TransformIndexPath.Of(sourceRoot, asset);
            return liveRoot != null && path != null
                && TransformIndexPath.TryResolve(liveRoot, path, out live);
        }

        /// <summary>The hierarchy transform for an asset transform of any source read.</summary>
        public bool TryLive(Transform asset, out Transform live)
        {
            live = null;
            if (Root == null || asset == null)
            {
                return false;
            }

            if (asset.IsChildOf(Root))
            {
                live = asset;
                return true;
            }

            foreach (ConversionSource source in _sources)
            {
                if (source?.Root != null && asset.IsChildOf(source.Root.transform))
                {
                    return TryLive(source, asset, out live);
                }
            }

            return false;
        }
    }
}
