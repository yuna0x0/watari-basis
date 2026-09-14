using System.Collections.Generic;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Reporting;

namespace yuna0x0.Basis.Convert.Tests
{
    public class ConversionReportTests
    {
        private static AvatarConversionPlan PlanWith(params ConversionDiagnostic[] diagnostics)
        {
            AvatarConversionPlan plan = new AvatarConversionPlan { SourceAssetPath = "Assets/Test.prefab" };
            plan.Diagnostics.AddRange(diagnostics);
            return plan;
        }

        [Test]
        public void RepeatedDiagnosticsAreGroupedByCode()
        {
            // A single avatar produced 61 identical "Is Animated" warnings. Listed one by one
            // that is unreadable; as a count it is useful.
            AvatarConversionPlan plan = PlanWith(
                new ConversionDiagnostic(DiagnosticSeverity.Warning, "a.code", "first"),
                new ConversionDiagnostic(DiagnosticSeverity.Warning, "a.code", "second"),
                new ConversionDiagnostic(DiagnosticSeverity.Dropped, "b.code", "third"));

            List<DiagnosticGroup> groups = ConversionReport.Group(plan);

            Assert.That(groups.Count, Is.EqualTo(2));

            DiagnosticGroup first = groups.Find(group => group.Code == "a.code");
            Assert.That(first.Count, Is.EqualTo(2));
            Assert.That(first.Example, Is.EqualTo("first"));
            Assert.That(first.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void GroupsAreOrderedMostSeriousFirst()
        {
            AvatarConversionPlan plan = PlanWith(
                new ConversionDiagnostic(DiagnosticSeverity.Mapped, "d.mapped", string.Empty),
                new ConversionDiagnostic(DiagnosticSeverity.Warning, "a.warning", string.Empty),
                new ConversionDiagnostic(DiagnosticSeverity.Approximated, "c.approx", string.Empty),
                new ConversionDiagnostic(DiagnosticSeverity.Dropped, "b.dropped", string.Empty));

            List<DiagnosticGroup> groups = ConversionReport.Group(plan);

            Assert.That(groups[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(groups[1].Severity, Is.EqualTo(DiagnosticSeverity.Dropped));
            Assert.That(groups[2].Severity, Is.EqualTo(DiagnosticSeverity.Approximated));
            Assert.That(groups[3].Severity, Is.EqualTo(DiagnosticSeverity.Mapped));
        }

        [Test]
        public void TheReportStatesWhatWasFoundAndWhatWasNotCarriedOver()
        {
            AvatarConversionPlan plan = PlanWith(
                new ConversionDiagnostic(
                    DiagnosticSeverity.Dropped, "physbone.maxSquish.dropped", "Squish went."),
                new ConversionDiagnostic(
                    DiagnosticSeverity.Warning, "physbone.isAnimated", "Was animated."));
            plan.PhysBonesFound = 7;
            plan.CollidersFound = 2;

            string report = ConversionReport.Write(plan);

            Assert.That(report, Does.Contain("PhysBones found: 7"));
            Assert.That(report, Does.Contain("Colliders found: 2"));
            Assert.That(report, Does.Contain("physbone.maxSquish.dropped"));
            Assert.That(report, Does.Contain("physbone.isAnimated"));
            Assert.That(report, Does.Contain("Diagnostics, grouped by code"));
        }

        [Test]
        public void TheReportCoversARealAvatarEndToEnd()
        {
            string fixturePath = LocalFixtures.Find(LocalFixtures.ShinanoPrefab);
            if (fixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(fixturePath);
            string report = ConversionReport.Write(plan);

            TestContext.WriteLine(report.Length > 4000 ? report.Substring(0, 4000) : report);

            Assert.That(report, Does.Contain($"PhysBones found: {plan.PhysBonesFound}"));
            Assert.That(report, Does.Contain("## Rigs"));
            Assert.That(ConversionReport.Group(plan), Is.Not.Empty);
        }
    }
}
