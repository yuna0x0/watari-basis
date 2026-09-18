using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>
    /// What to put on the avatar's `BasisAvatar` component.
    /// <para>
    /// Only the fields VRChat actually carries data for are planned. Everything else, the
    /// animator, the human scale, the renderer list and the mouth position, is left for Basis's
    /// own automatic setup, which fills them when the inspector is opened and which will not
    /// overwrite a value that is already set.
    /// </para>
    /// </summary>
    public sealed class BasisAvatarPlan
    {
        public long SourceDocumentFileId;
        public long AvatarRootFileId;

        /// <summary>
        /// Basis stores the eye position as height above the root and forward offset, which is
        /// VRChat's view position with its sideways component discarded.
        /// </summary>
        public Vector2 EyePosition;

        public long VisemeMeshFileId;

        /// <summary>
        /// Fifteen blendshape names in the shared viseme order. Resolved to indices against the
        /// mesh once it is known; an empty entry means the avatar had none for that viseme.
        /// </summary>
        public List<string> VisemeBlendShapeNames = new List<string>();

        public long BlinkMeshFileId;

        /// <summary>Blendshape index for blink, or -1 when the avatar has none.</summary>
        /// <summary>Blendshape indices on the blink mesh. Basis blinks with all of them.</summary>
        public List<int> BlinkBlendShapeIndices = new List<int>();

        /// <summary>
        /// How far the eye bones may turn from straight ahead, in degrees, already within the
        /// range Basis accepts. Zero leaves the Basis Avatar's own setting alone.
        /// </summary>
        public float EyeMaxLookAngleDegrees;

        public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
    }
}
