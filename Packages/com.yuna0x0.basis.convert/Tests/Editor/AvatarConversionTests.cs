using System.Collections.Generic;
using Basis.Scripts.BasisSdk.Constraints;
using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// End to end over a real VRChat avatar: read the prefab, map everything, write the rigs onto
    /// an instance, and undo. Skipped when the avatar is absent, since it cannot be committed.
    /// </summary>
    public class AvatarConversionTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
                _instance = null;
            }
        }

        private static void RequireFixture()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }
        }

        [Test]
        public void PlanningReadsEveryPhysBoneAndResolvesItsBones()
        {
            RequireFixture();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            TestContext.WriteLine($"physbones found: {plan.PhysBonesFound}");
            TestContext.WriteLine($"constraints found: {plan.ConstraintsFound}");
            TestContext.WriteLine($"constraints planned: {plan.Constraints.Count}");
            TestContext.WriteLine($"colliders found: {plan.CollidersFound}");
            TestContext.WriteLine($"rigs planned:    {plan.Rigs.Count}");
            TestContext.WriteLine($"unresolved:      {plan.Unresolved}");
            TestContext.WriteLine($"mapped:        {plan.CountOf(DiagnosticSeverity.Mapped)}");
            TestContext.WriteLine($"approximated:  {plan.CountOf(DiagnosticSeverity.Approximated)}");
            TestContext.WriteLine($"dropped:       {plan.CountOf(DiagnosticSeverity.Dropped)}");
            TestContext.WriteLine($"warnings:      {plan.CountOf(DiagnosticSeverity.Warning)}");

            Dictionary<string, int> byCode = new Dictionary<string, int>();
            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                byCode.TryGetValue(diagnostic.Code, out int count);
                byCode[diagnostic.Code] = count + 1;
            }

            foreach (KeyValuePair<string, int> pair in byCode)
            {
                TestContext.WriteLine($"  {pair.Key}: {pair.Value}");
            }

            Assert.That(plan.PhysBonesFound, Is.EqualTo(61));
            Assert.That(plan.CollidersFound, Is.EqualTo(11));
            Assert.That(plan.Unresolved, Is.Zero, "Every PhysBone should resolve to a transform.");
            Assert.That(plan.Rigs.Count, Is.EqualTo(61), "One rig per PhysBone, no splitting.");
            Assert.That(plan.Colliders.Count, Is.EqualTo(plan.CollidersFound),
                "Each collider should be mapped once, however many rigs reference it.");

            // A collider shared between rigs must not report itself once per referencing rig.
            int snapped = 0;
            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                if (diagnostic.Code == "collider.capsuleRotation.snapped")
                {
                    snapped++;
                }
            }

            Assert.That(snapped, Is.LessThanOrEqualTo(plan.CollidersFound),
                "Collider diagnostics are duplicated across the rigs referencing them.");

            Assert.That(plan.ConstraintsFound, Is.EqualTo(28));
            Assert.That(plan.Constraints.Count, Is.EqualTo(28),
                "Every constraint should be planned, not just the PhysBones.");

            foreach (PlannedConstraint constraint in plan.Constraints)
            {
                Assert.That(constraint.SourceHost, Is.Not.Null);
                Assert.That(constraint.Plan.Sources.Count, Is.GreaterThan(0),
                    $"{constraint.Describe()} has no sources, so it would do nothing.");
                foreach (Transform sourceTransform in constraint.SourceTransforms)
                {
                    Assert.That(sourceTransform, Is.Not.Null,
                        $"{constraint.Describe()} has an unresolved source.");
                }
            }

            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                Assert.That(rig.SourceRootBone, Is.Not.Null);
                Assert.That(rig.SourceRootBone.IsChildOf(plan.SourceRoot.transform), Is.True,
                    $"{rig.Describe()} resolved outside the avatar.");
            }
        }

        [Test]
        public void PlanningChangesNothing()
        {
            RequireFixture();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);

            Assert.That(plan.SourceRoot.GetComponentsInChildren<JiggleRig>(true),
                Is.Empty, "A dry run must not write anything.");
            Assert.That(plan.SourceRoot.GetComponentsInChildren<BasisConstraintBase>(true),
                Is.Empty, "A dry run must not write anything.");
        }

        [Test]
        public void ApplyingWritesOneRigPerPhysBoneOntoAnInstance()
        {
            RequireFixture();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath));
            Assert.That(_instance, Is.Not.Null);

            ConversionResult result = AvatarConverter.Apply(plan, _instance);

            TestContext.WriteLine($"written: {result.RigsWritten}, skipped: {result.RigsSkipped}");

            Assert.That(result.TotalSkipped, Is.Zero);
            Assert.That(result.RigsWritten, Is.EqualTo(plan.Rigs.Count));
            Assert.That(result.ConstraintsWritten, Is.EqualTo(plan.Constraints.Count));

            BasisConstraintBase[] constraints =
                _instance.GetComponentsInChildren<BasisConstraintBase>(true);
            Assert.That(constraints.Length, Is.EqualTo(plan.Constraints.Count));

            foreach (BasisConstraintBase constraint in constraints)
            {
                Assert.That(constraint.sourceCount, Is.GreaterThan(0),
                    $"{constraint.name} was written with no sources.");

                foreach (BasisConstraintSourceEntry entry in constraint.Sources)
                {
                    Assert.That(entry.sourceTransform, Is.Not.Null);
                    Assert.That(entry.sourceTransform.IsChildOf(_instance.transform), Is.True,
                        "A constraint source points outside the converted instance.");
                }
            }

            JiggleRig[] rigs = _instance.GetComponentsInChildren<JiggleRig>(true);
            Assert.That(rigs.Length, Is.EqualTo(plan.Rigs.Count));

            int withCurves = 0;
            int withColliders = 0;
            foreach (JiggleRig rig in rigs)
            {
                JiggleRigData data = rig.GetJiggleRigData();

                Assert.That(data.rootBone, Is.Not.Null);
                Assert.That(data.rootBone.IsChildOf(_instance.transform), Is.True,
                    "A rig points at a bone outside the converted instance.");
                Assert.That(data.hasSerializedData, Is.True);

                if (data.jiggleTreeInputParameters.stiffness.curveEnabled)
                {
                    withCurves++;
                }

                if (data.jiggleColliders.Length > 0)
                {
                    withColliders++;
                }
            }

            TestContext.WriteLine($"rigs carrying a stiffness curve: {withCurves}");
            TestContext.WriteLine($"rigs carrying colliders:         {withColliders}");

            Assert.That(withCurves, Is.GreaterThan(0),
                "Every PhysBone in this avatar has falloff curves, so they must survive.");
        }

        [Test]
        public void OneUndoRevertsAWholeAvatarConversion()
        {
            RequireFixture();

            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(FixturePath);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath));

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            AvatarConverter.Apply(plan, _instance);
            Assert.That(_instance.GetComponentsInChildren<JiggleRig>(true), Is.Not.Empty);
            Assert.That(_instance.GetComponentsInChildren<BasisConstraintBase>(true), Is.Not.Empty);

            Undo.RevertAllDownToGroup(group);

            Assert.That(_instance.GetComponentsInChildren<JiggleRig>(true), Is.Empty,
                "One undo should remove every rig the conversion added.");
            Assert.That(_instance.GetComponentsInChildren<BasisConstraintBase>(true), Is.Empty,
                "One undo should remove every constraint the conversion added.");
        }
    }
}
