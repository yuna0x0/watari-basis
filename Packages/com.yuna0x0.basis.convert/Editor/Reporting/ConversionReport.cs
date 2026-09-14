using System;
using System.Collections.Generic;
using System.Text;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Reporting
{
    public sealed class DiagnosticGroup
    {
        public string Code;
        public DiagnosticSeverity Severity;
        public int Count;

        /// <summary>One representative message. They only differ by the values quoted in them.</summary>
        public string Example;
    }

    /// <summary>
    /// Summarises a plan, or a plan and what applying it did.
    /// <para>
    /// Diagnostics are grouped by code because they repeat per component: a single avatar
    /// produced 61 identical "Is Animated was on" warnings, which is unreadable listed one by
    /// one but useful as a count. Only what the current options would convert is grouped; what
    /// the options leave out is stated as left out rather than reported as a loss.
    /// </para>
    /// </summary>
    public static class ConversionReport
    {
        public static List<DiagnosticGroup> Group(AvatarConversionPlan plan)
        {
            Dictionary<string, DiagnosticGroup> groups = new Dictionary<string, DiagnosticGroup>();

            foreach (ConversionDiagnostic diagnostic in plan.SelectedDiagnostics())
            {
                if (!groups.TryGetValue(diagnostic.Code, out DiagnosticGroup group))
                {
                    group = new DiagnosticGroup
                    {
                        Code = diagnostic.Code,
                        Severity = diagnostic.Severity,
                        Example = diagnostic.Message,
                    };
                    groups[diagnostic.Code] = group;
                }

                group.Count++;
            }

            List<DiagnosticGroup> ordered = new List<DiagnosticGroup>(groups.Values);
            ordered.Sort((left, right) =>
            {
                int bySeverity = right.Severity.CompareTo(left.Severity);
                return bySeverity != 0
                    ? bySeverity
                    : string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            });

            return ordered;
        }

        public static string Write(AvatarConversionPlan plan, ConversionResult result = null)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine($"# {ProductInfo.Name}: avatar to Basis");
            text.AppendLine();
            text.AppendLine($"{ProductInfo.Name} {ProductInfo.Version}. Sources read against {ProductInfo.CheckedAgainst}.");
            text.AppendLine();
            text.AppendLine($"Source: {plan.SourceAssetPath}");
            text.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            text.AppendLine();

            text.AppendLine("## Summary");
            text.AppendLine();
            text.AppendLine($"- Detected: {plan.Profile.Describe()}");

            if (plan.Sources.Count > 1)
            {
                text.AppendLine($"- Read from {plan.Sources.Count} prefabs:");
                foreach (ConversionSource source in plan.Sources)
                {
                    text.AppendLine($"  - {source.Name} ({source.AssetPath})"
                        + Suffix(source.Include));
                }
            }

            text.AppendLine($"- PhysBones found: {plan.PhysBonesFound}");
            text.AppendLine($"- Dynamic Bones found: {plan.DynamicBonesFound}");
            text.AppendLine($"- VRM spring chains found: {plan.VrmChainsFound}");
            text.AppendLine($"- VRM expressions found: {plan.VrmExpressionsFound}");
            text.AppendLine($"- Colliders found: {plan.CollidersFound}");
            text.AppendLine($"- Constraints found: {plan.ConstraintsFound}");
            text.AppendLine($"- Head chops found: {plan.HeadChopsFound}");
            text.AppendLine($"- Jiggle rigs planned: {plan.Rigs.Count}");
            text.AppendLine($"- Basis constraints planned: {plan.Constraints.Count}");
            text.AppendLine($"- Vixxy controls planned: {plan.VixxyControls.Count}");
            text.AppendLine($"- Authored motions planned: {plan.AuthoredMotions.Count}");
            text.AppendLine("- Avatar descriptor: "
                + (plan.Descriptor == null
                    ? "none found"
                    : plan.DescriptorSelected ? "converted" : "found, left out"));

            if (plan.Unresolved > 0)
            {
                text.AppendLine($"- Could not be placed: {plan.Unresolved}");
            }

            WriteExclusions(plan, text);

            if (result != null)
            {
                text.AppendLine($"- Written: {result.TotalWritten}");
                if (result.VrmRuntimeRemoved > 0)
                {
                    text.AppendLine(
                        $"- UniVRM runtime components removed: {result.VrmRuntimeRemoved}");
                }

                if (result.TotalSkipped > 0)
                {
                    text.AppendLine($"- Skipped while writing: {result.TotalSkipped}");
                }

                // The one thing a conversion leaves in the project rather than on the avatar,
                // and the one thing an undo does not take back.
                foreach (string asset in result.MotionAssets)
                {
                    text.AppendLine($"- Motion clip baked: {asset}");
                }
            }

            text.AppendLine();
            text.AppendLine("Diagnostics, grouped by code.");
            text.AppendLine();

            List<DiagnosticGroup> groups = Group(plan);
            foreach (DiagnosticSeverity severity in new[]
                     {
                         DiagnosticSeverity.Warning,
                         DiagnosticSeverity.Dropped,
                         DiagnosticSeverity.Approximated,
                         DiagnosticSeverity.Mapped,
                     })
            {
                List<DiagnosticGroup> section = groups.FindAll(group => group.Severity == severity);
                if (section.Count == 0)
                {
                    continue;
                }

                text.AppendLine($"## {HeadingFor(severity)}");
                text.AppendLine();
                foreach (DiagnosticGroup group in section)
                {
                    text.AppendLine($"- **{group.Code}** ({group.Count}): {group.Example}");
                }

                text.AppendLine();
            }

            if (plan.Constraints.Count > 0)
            {
                text.AppendLine("## Constraints");
                text.AppendLine();
                foreach (PlannedConstraint constraint in plan.Constraints)
                {
                    text.AppendLine($"- {constraint.Describe()}, "
                        + $"{constraint.Plan.Sources.Count} sources"
                        + Suffix(plan.Options.Constraints && constraint.Include
                            && AvatarConversionPlan.IsIncluded(constraint.Source)));
                }

                text.AppendLine();
            }

            if (plan.RigDiagnostics.Count > 0)
            {
                text.AppendLine("## Rig");
                text.AppendLine();
                text.AppendLine("Rig check against Basis IK. These are model import settings; "
                    + "conversion does not change them.");
                text.AppendLine();
                foreach (ConversionDiagnostic diagnostic in plan.RigDiagnostics)
                {
                    text.AppendLine($"- **{diagnostic.Code}**: {diagnostic.Message}");
                }

                text.AppendLine();
            }

            text.AppendLine("## Rigs");
            text.AppendLine();
            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                bool colliders = plan.Options.Physics && plan.Options.Colliders;
                text.AppendLine($"- {rig.Describe()} "
                    + $"[{rig.Plan.Preset}]"
                    + $"{(rig.Plan.ExcludeRoot ? ", motionless root" : string.Empty)}"
                    + $"{(colliders && rig.Colliders.Count > 0 ? $", {rig.Colliders.Count} colliders" : string.Empty)}"
                    + Suffix(plan.Options.Physics && rig.Include
                        && AvatarConversionPlan.IsIncluded(rig.Source)));
            }

            return text.ToString();
        }

        /// <summary>
        /// What the conversion was told not to write. A report of a narrowed conversion has to
        /// say what was narrowed, or it reads as a report of the whole avatar.
        /// </summary>
        private static void WriteExclusions(AvatarConversionPlan plan, StringBuilder text)
        {
            string categories = string.Join(", ", plan.Options.Excluded());
            if (!string.IsNullOrEmpty(categories))
            {
                text.AppendLine($"- Left out by choice: {categories}");
            }

            List<string> excludedSources = new List<string>();
            foreach (ConversionSource source in plan.Sources)
            {
                if (!source.Include)
                {
                    excludedSources.Add(source.Name);
                }
            }

            if (excludedSources.Count > 0)
            {
                text.AppendLine("- Prefabs left out: " + string.Join(", ", excludedSources));
            }

            int individually = 0;
            if (plan.Options.Physics)
            {
                individually += plan.Rigs.Count - plan.SelectedRigCount;
            }

            if (plan.Options.Constraints)
            {
                individually += plan.Constraints.Count - plan.SelectedConstraintCount;
            }

            if (plan.Options.Toggles)
            {
                individually += plan.VixxyControls.Count - plan.SelectedVixxyControlCount;
            }

            if (plan.Options.Motion)
            {
                individually += plan.AuthoredMotions.Count - plan.SelectedAuthoredMotionCount;
            }

            if (plan.Options.Descriptor && plan.Descriptor != null && !plan.Descriptor.Include)
            {
                individually++;
            }

            if (individually > 0)
            {
                text.AppendLine($"- Left out one by one: {individually}");
            }
        }

        private static string Suffix(bool included) => included ? string.Empty : "  (left out)";

        private static string HeadingFor(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Warning => "Warnings",
                DiagnosticSeverity.Dropped => "Dropped",
                DiagnosticSeverity.Approximated => "Approximated",
                _ => "Mapped",
            };
        }
    }
}
