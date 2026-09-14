using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Rig;

namespace yuna0x0.Basis.Convert.Tests
{
    public class RigReadinessTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        private static bool HasCode(IEnumerable<ConversionDiagnostic> log, string code)
        {
            foreach (ConversionDiagnostic diagnostic in log)
            {
                if (diagnostic.Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void AnAvatarWithNoAnimatorIsReported()
        {
            GameObject bare = new GameObject("NoAnimator");
            _spawned.Add(bare);

            Assert.That(HasCode(RigReadiness.Inspect(bare, true), "rig.noAnimator"), Is.True);
        }

        [Test]
        public void APropWithNoAnimatorSaysNothing()
        {
            // Props and clothing carry physics components too. Demanding a humanoid rig of them
            // would be noise, not a finding.
            GameObject prop = new GameObject("Prop");
            _spawned.Add(prop);

            Assert.That(RigReadiness.Inspect(prop, false), Is.Empty);
        }

        [Test]
        public void TwistBoneLookupMatchesWhatBasisDoes()
        {
            // Basis takes the first direct child whose name contains "twist" or "roll", case
            // insensitively. Mirrored here so the report matches what it will actually find.
            GameObject arm = new GameObject("LowerArm");
            _spawned.Add(arm);

            Assert.That(RigReadiness.FindTwistChild(arm.transform), Is.Null);

            GameObject plain = new GameObject("Hand");
            plain.transform.SetParent(arm.transform, false);
            Assert.That(RigReadiness.FindTwistChild(arm.transform), Is.Null,
                "An unrelated child is not a twist bone.");

            GameObject twist = new GameObject("Forearm_Twist_01");
            twist.transform.SetParent(arm.transform, false);
            Assert.That(RigReadiness.FindTwistChild(arm.transform), Is.Not.Null);
            Assert.That(RigReadiness.FindTwistChild(arm.transform).name,
                Is.EqualTo("Forearm_Twist_01"));
        }

        [Test]
        public void RollIsRecognisedAsWellAsTwist()
        {
            GameObject arm = new GameObject("UpperArm");
            _spawned.Add(arm);

            GameObject roll = new GameObject("arm_ROLL");
            roll.transform.SetParent(arm.transform, false);

            Assert.That(RigReadiness.FindTwistChild(arm.transform), Is.Not.Null,
                "The lookup is case insensitive and accepts roll as well as twist.");
        }

        [Test]
        public void OnlyDirectChildrenCount()
        {
            GameObject arm = new GameObject("LowerArm");
            _spawned.Add(arm);

            GameObject between = new GameObject("Hand");
            between.transform.SetParent(arm.transform, false);

            GameObject deep = new GameObject("Twist");
            deep.transform.SetParent(between.transform, false);

            Assert.That(RigReadiness.FindTwistChild(arm.transform), Is.Null,
                "Basis only looks at direct children, so a deeper twist bone is not found.");
        }

        [Test]
        public void ARealAvatarIsInspectedAsPartOfPlanning()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            Assert.That(plan.RigDiagnostics, Is.Not.Empty,
                "Planning should inspect the rig, not just the components.");

            foreach (ConversionDiagnostic diagnostic in plan.RigDiagnostics)
            {
                TestContext.WriteLine($"[{diagnostic.Severity}] {diagnostic.Code}: "
                    + diagnostic.Message);
            }

            // Rig findings are reported alongside everything else.
            Assert.That(HasCode(plan.AllDiagnostics(), plan.RigDiagnostics[0].Code), Is.True);
        }
    }
}
