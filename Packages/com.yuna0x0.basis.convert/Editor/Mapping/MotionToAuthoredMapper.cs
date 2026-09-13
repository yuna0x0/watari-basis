using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Plans a `BasisAuthoredMotion` movement from a clip that animates over time.
    /// <para>
    /// Two things arrive here. A layer that plays unprompted is motion the avatar simply has. A
    /// menu toggle whose clip animates rather than switches is motion the wearer turns on, which
    /// Vixxy cannot hold as a value per choice but can enable and disable as a component.
    /// </para>
    /// <para>
    /// Basis bakes rotation only, so a clip that also moves or scales something keeps the turning
    /// and loses the rest. What a clip does besides turning transforms is reported here rather
    /// than at the bake, where the counts are no longer separable.
    /// </para>
    /// </summary>
    public static class MotionToAuthoredMapper
    {
        /// <summary>Motion from a layer with nothing steering it, which plays from load.</summary>
        public static AuthoredMotionPlan MapAmbient(
            string label, bool loop, ClipEffects effects, float speed = 1f)
        {
            AuthoredMotionPlan plan = Map(label, loop, effects);
            if (plan.Paths.Count == 0)
            {
                return plan;
            }

            plan.Speed = speed;

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "motion.baked",
                $"'{plan.Label}' plays without anything switching it on, so it was rebuilt as an "
                + $"authored motion turning {plan.Paths.Count} transforms. Basis replays it from "
                + "a clip baked at conversion time rather than from an animator.");

            // A state with nothing to leave it stays active, but a clip that does not loop holds
            // its last frame in Unity, and Basis's authored motion does the same.
            if (!loop)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "motion.notLooping",
                    $"'{plan.Label}' was not authored to loop. It plays once and holds its last "
                    + "frame, as it did in the animator.");
            }

            return plan;
        }

        /// <summary>
        /// Motion a menu control switches on. The control enables the component rather than
        /// holding a value, which Vixxy permits for this type. A clip authored not to loop plays
        /// once each time it is switched on.
        /// </summary>
        public static AuthoredMotionPlan MapSwitched(
            string label, string choiceName, bool loop, ClipEffects effects)
        {
            AuthoredMotionPlan plan = Map(label, loop, effects);
            if (plan.Paths.Count == 0)
            {
                return plan;
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "motion.switched",
                $"'{label}' animates {plan.Paths.Count} transforms over time on '{choiceName}', "
                + "which a Vixxy control cannot hold as a value. It was rebuilt as an authored "
                + "motion that the control switches on instead.");

            return plan;
        }

        private static AuthoredMotionPlan Map(string label, bool loop, ClipEffects effects)
        {
            AuthoredMotionPlan plan = new AuthoredMotionPlan
            {
                Label = label ?? string.Empty,
                Loop = loop,
            };

            if (effects == null)
            {
                return plan;
            }

            plan.Paths.AddRange(effects.AnimatedRotationPaths);
            if (plan.Paths.Count == 0)
            {
                return plan;
            }

            int other = effects.AnimatedCurves - effects.AnimatedRotationCurves;
            if (other > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "motion.rotationOnly",
                    $"'{plan.Label}' animates {other} curves that are not transform rotation. A "
                    + "baked Basis motion clip holds rotation only, so movement, scaling and "
                    + "anything else the clip animates over time were not carried over.");
            }

            return plan;
        }
    }
}
