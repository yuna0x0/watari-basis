using System.Collections.Generic;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Model
{
    /// <summary>
    /// What a VRM avatar says about its own eyes and about what the wearer should see.
    /// <para>
    /// Both formats carry an offset from the head to the point the camera sits at, which is the
    /// same thing VRChat calls the view position and Basis stores as the avatar's eye position.
    /// </para>
    /// </summary>
    public sealed class VrmAvatarSettingsData
    {
        /// <summary>True when the avatar declared an eye offset at all.</summary>
        public bool HasEyeOffset;

        /// <summary>Offset from the head bone to the eyes, in metres.</summary>
        public Vector3 EyeOffsetFromHead;

        /// <summary>
        /// The bone the offset is measured from. VRM 0.x names it; 1.0 always means the head.
        /// </summary>
        public long HeadBoneFileId;

        /// <summary>Renderers the avatar hides from the wearer's own view.</summary>
        /// <summary>
        /// The eyes are aimed with the look up, down, left and right expressions rather than
        /// with eye bones. Basis rotates eye bones, so such eyes do not follow gaze.
        /// </summary>
        public bool LookAtByExpression;

        /// <summary>
        /// The `outputScale` of each VRM 1.0 range map, in degrees: how far an eye bone may turn
        /// in that direction. Empty when the avatar states none.
        /// </summary>
        public List<float> EyeRotationLimitsDegrees = new List<float>();

        /// <summary>The most an eye bone may turn in any direction. Zero when none is stated.</summary>
        public float EyeRotationLimitDegrees =>
            EyeRotationLimitsDegrees.Count == 0 ? 0f : Mathf.Max(EyeRotationLimitsDegrees.ToArray());

        /// <summary>The least an eye bone may turn in some direction. Zero when none is stated.</summary>
        public float EyeRotationLimitMinDegrees =>
            EyeRotationLimitsDegrees.Count == 0 ? 0f : Mathf.Min(EyeRotationLimitsDegrees.ToArray());

        public int ThirdPersonOnlyRenderers;

        /// <summary>Renderers the avatar shows only to the wearer.</summary>
        public int FirstPersonOnlyRenderers;
    }
}
