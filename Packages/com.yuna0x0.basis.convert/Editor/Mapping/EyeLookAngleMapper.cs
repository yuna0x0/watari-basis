using Basis.Scripts.BasisSdk;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a source's eye rotation limits into the one angle a `BasisAvatar` holds.
    /// <para>
    /// VRChat records a rotation per direction and per eye, and VRM 1.0 a range map per
    /// direction. Basis keeps one angle for every direction and both eyes, clamped to its own
    /// range, so the largest limit is written and anything narrower than it is reported.
    /// </para>
    /// </summary>
    public static class EyeLookAngleMapper
    {
        /// <summary>Directions whose limits differ by less than this are treated as one.</summary>
        public const float UnevenToleranceDegrees = 1f;

        public static void Apply(BasisAvatarPlan plan, string code, float minDegrees, float maxDegrees)
        {
            float floor = BasisAvatar.MinEyeMaxLookAngle;
            float ceiling = BasisAvatar.MaxEyeMaxLookAngle;

            if (maxDegrees <= 0f)
            {
                plan.EyeMaxLookAngleDegrees = floor;
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, code + ".none",
                    "Every gaze state is the rest rotation, so the eyes did not turn. "
                    + $"The smallest Basis limit, {floor:0.#} degrees, was written.");
                return;
            }

            float written = Mathf.Clamp(maxDegrees, floor, ceiling);
            plan.EyeMaxLookAngleDegrees = written;

            if (!Mathf.Approximately(written, maxDegrees))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, code + ".clamped",
                    $"The eyes turn up to {maxDegrees:0.#} degrees. Basis allows {floor:0.#} to "
                    + $"{ceiling:0.#}, so {written:0.#} was written.");
                return;
            }

            if (maxDegrees - minDegrees > UnevenToleranceDegrees)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, code + ".uneven",
                    $"The eyes turn {minDegrees:0.#} to {maxDegrees:0.#} degrees depending on "
                    + $"direction. Basis has one limit, so {written:0.#} was written.");
                return;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, code,
                $"The eyes turn up to {written:0.#} degrees. That limit was written to the Basis Avatar.");
        }
    }
}
