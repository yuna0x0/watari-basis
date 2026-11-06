using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// The parameters VRChat drives itself, which an avatar's animator can read but nothing
    /// declares.
    /// <para>
    /// They matter here because a layer commonly tests one alongside a menu parameter: a gimmick
    /// that only runs for the wearer tests `IsLocal`, one that stops in a chair tests
    /// `InStation`. Basis has no equivalent for any of them, so a layer guarded this way is still
    /// the menu parameter's layer, and the guard is what gets reported.
    /// </para>
    /// <para>
    /// Names are the SDK's own, taken from what an avatar's controllers actually reference and
    /// from VRChat's animator parameter documentation (checked 2026-11-06). A
    /// name this does not know is treated as the avatar's own parameter, which is the safe way
    /// round: an unknown name makes a layer look steered by two things and leaves it alone.
    /// </para>
    /// </summary>
    public static class VrchatBuiltInParameters
    {
        private static readonly HashSet<string> Names = new HashSet<string>
        {
            "IsLocal",
            "PreviewMode",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "VelocityMagnitude",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "Supine",
            "GroundProximity",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "Earmuffs",
            "IsOnFriendsList",
            "AvatarVersion",
            "IsAnimatorEnabled",
            "UserEyeTracking",
            "UserFaceTracking",
            "ScaleModified",
            "ScaleFactor",
            "ScaleFactorInverse",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent",
        };

        public static bool Contains(string parameter) =>
            !string.IsNullOrEmpty(parameter) && Names.Contains(parameter);
    }
}
