using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Which parts of a plan a conversion writes.
    /// <para>
    /// Reading and mapping always run over the whole avatar, so the counts and the diagnostics
    /// describe all of it whatever is selected here. Narrowing happens between planning and
    /// writing, which makes a narrowed conversion the same as a full one with parts left out,
    /// rather than a different pass over the source.
    /// </para>
    /// </summary>
    public sealed class ConversionOptions
    {
        /// <summary>Jiggle rigs, from PhysBones and from Dynamic Bone.</summary>
        public bool Physics = true;

        /// <summary>
        /// Whether those rigs carry the avatar's colliders. Rigs are still written without
        /// them; their bones pass through the body instead of resting on it.
        /// </summary>
        public bool Colliders = true;

        public bool Constraints = true;

        /// <summary>The Basis Avatar component: view position, visemes and blink.</summary>
        public bool Descriptor = true;

        /// <summary>Menu toggles rebuilt as HVR Vixxy controls.</summary>
        public bool Toggles = true;

        /// <summary>
        /// Animation that plays unprompted, rebuilt as authored motion. This is the one category
        /// that writes an asset to the project, the baked clip, which an undo does not remove.
        /// </summary>
        public bool Motion = true;

        /// <summary>Clothing bones parented under the avatar's, from VRCFury Armature Links.</summary>
        public bool ArmatureLinks = true;

        public bool IsEverything =>
            Physics && Colliders && Constraints && Descriptor && Toggles && Motion && ArmatureLinks;

        /// <summary>Names of the categories switched off, for the report to state plainly.</summary>
        public IEnumerable<string> Excluded()
        {
            if (!Physics)
            {
                yield return "physics";
            }
            else if (!Colliders)
            {
                yield return "colliders";
            }

            if (!Constraints)
            {
                yield return "constraints";
            }

            if (!ArmatureLinks)
            {
                yield return "armature links";
            }

            if (!Descriptor)
            {
                yield return "avatar descriptor";
            }

            if (!Toggles)
            {
                yield return "menu toggles";
            }

            if (!Motion)
            {
                yield return "authored motion";
            }
        }
    }
}
