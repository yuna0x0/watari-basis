using System.Collections.Generic;
using GatorDragonGames.JigglePhysics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.Tests
{
    public class JiggleRigWriterTests
    {
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

        /// <summary>root -> a1 -> a2, and root -> b1 -> b2. Two branches from a shared root.</summary>
        private Transform BuildBranchingChain()
        {
            GameObject root = new GameObject("Root");
            _spawned.Add(root);

            foreach (string side in new[] { "A", "B" })
            {
                Transform parent = root.transform;
                for (int i = 1; i <= 2; i++)
                {
                    GameObject bone = new GameObject($"{side}{i}");
                    bone.transform.SetParent(parent, false);
                    bone.transform.localPosition = new Vector3(side == "A" ? 0.1f : -0.1f, 0.1f, 0f);
                    parent = bone.transform;
                }
            }

            return root.transform;
        }

        private static ResolvedJiggleRig RigFor(Transform root, JiggleRigPlan plan = null)
        {
            return new ResolvedJiggleRig
            {
                Plan = plan ?? new JiggleRigPlan(),
                Host = root.gameObject,
                RootBone = root,
            };
        }

        [Test]
        public void WritesRootBoneAndParameters()
        {
            Transform root = BuildBranchingChain();

            JiggleRigPlan plan = new JiggleRigPlan
            {
                ExcludeRoot = true,
                LockFromGrabbing = true,
                Parameters =
                {
                    Stiffness = new JiggleCurvedFloatPlan(0.42f),
                    IgnoreRootMotion = 0.35f,
                    CollisionToggle = true,
                    CollisionRadius = new JiggleCurvedFloatPlan(0.07f),
                    AngleLimitToggle = true,
                    AngleLimit = new JiggleCurvedFloatPlan(0.25f),
                },
            };

            JiggleRig written = JiggleRigWriter.Write(RigFor(root, plan));
            JiggleRigData data = written.GetJiggleRigData();

            Assert.That(data.rootBone, Is.SameAs(root));
            Assert.That(data.hasSerializedData, Is.True);
            Assert.That(data.excludeRoot, Is.True);
            Assert.That(data.lockFromGrabbing, Is.True);

            JiggleTreeInputParameters parameters = data.jiggleTreeInputParameters;
            Assert.That(parameters.advancedToggle, Is.True,
                "Without advancedToggle, jiggle ignores stretch, collisionRadius, "
                + "ignoreRootMotion, soften and rootStretch entirely.");
            Assert.That(parameters.stiffness.value, Is.EqualTo(0.42f).Within(1e-6f));
            Assert.That(parameters.ignoreRootMotion, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(parameters.collisionToggle, Is.True);
            Assert.That(parameters.collisionRadius.value, Is.EqualTo(0.07f).Within(1e-6f));
            Assert.That(parameters.angleLimitToggle, Is.True);
            Assert.That(parameters.angleLimit.value, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void CurvesSurviveTheWrite()
        {
            Transform root = BuildBranchingChain();
            AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);

            JiggleRigPlan plan = new JiggleRigPlan();
            plan.Parameters.Stiffness = new JiggleCurvedFloatPlan(0.5f, curve);

            JiggleRig written = JiggleRigWriter.Write(RigFor(root, plan));
            JiggleTreeCurvedFloat stiffness =
                written.GetJiggleRigData().jiggleTreeInputParameters.stiffness;

            Assert.That(stiffness.curveEnabled, Is.True);
            Assert.That(stiffness.curve.length, Is.EqualTo(2));
            Assert.That(stiffness.Evaluate(0f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(stiffness.Evaluate(1f), Is.EqualTo(0.125f).Within(1e-4f));
        }

        [Test]
        public void ParametersWithNoPlannedValueKeepThePresetsTuning()
        {
            Transform root = BuildBranchingChain();

            JiggleRig preset = JigglePresetLibrary.TryLoad(JigglePreset.Hair);
            if (preset == null)
            {
                Assert.Ignore("Jiggle presets are not where they were; check the package layout.");
            }

            float presetSoften = preset.GetJiggleRigData().jiggleTreeInputParameters.soften;

            JiggleRigPlan plan = new JiggleRigPlan { Preset = JigglePreset.Hair };
            plan.Parameters.Stiffness = new JiggleCurvedFloatPlan(0.42f);

            JiggleRig written = JiggleRigWriter.Write(RigFor(root, plan));
            JiggleTreeInputParameters parameters =
                written.GetJiggleRigData().jiggleTreeInputParameters;

            Assert.That(parameters.stiffness.value, Is.EqualTo(0.42f).Within(1e-6f));
            Assert.That(parameters.soften, Is.EqualTo(presetSoften).Within(1e-6f),
                "Soften was not planned, so it should still be the preset's value.");
        }

        [Test]
        public void ExcludedTransformsAreWrittenAndHonoured()
        {
            Transform root = BuildBranchingChain();
            Transform excluded = root.Find("B1");

            JiggleRigPlan plan = new JiggleRigPlan();
            ResolvedJiggleRig rig = RigFor(root, plan);
            rig.ExcludedTransforms.Add(excluded);

            JiggleRigData data = JiggleRigWriter.Write(rig).GetJiggleRigData();

            Assert.That(data.excludedTransforms, Is.EquivalentTo(new[] { excluded }));
            Assert.That(data.GetIsExcluded(excluded), Is.True);
        }

        [Test]
        public void OneRigCoversEveryBranchBelowItsRoot()
        {
            // The Basis docs say paired features need one rig each. The shipped jiggle code walks
            // every valid child from the root, so a single rig covers both branches. This pins
            // that, since the whole no-chain-splitting decision rests on it.
            Transform root = BuildBranchingChain();

            JiggleRigData data = JiggleRigWriter.Write(RigFor(root)).GetJiggleRigData();
            data.BuildNormalizedDistanceFromRootList();

            List<string> covered = new List<string>();
            foreach (JiggleTransformCachedData entry in data.transformCachedData)
            {
                if (entry.bone != null)
                {
                    covered.Add(entry.bone.name);
                }
            }

            Assert.That(covered, Is.SupersetOf(new[] { "A1", "A2", "B1", "B2" }),
                "A single rig should cover both branches, not just the first.");
        }

        [Test]
        public void WritingColliders()
        {
            Transform root = BuildBranchingChain();
            Transform colliderTransform = root.Find("A1");

            ResolvedJiggleRig rig = RigFor(root);
            rig.Colliders.Add(new ResolvedJiggleCollider
            {
                Transform = colliderTransform,
                Plan = new JiggleColliderPlan
                {
                    Shape = JiggleColliderShape.Capsule,
                    Radius = 0.03f,
                    Height = 0.3f,
                    CapsuleAxis = JiggleCapsuleAxis.Z,
                    LocalOffset = new Vector3(0f, 0.12f, 0f),
                },
            });

            JiggleRigData data = JiggleRigWriter.Write(rig).GetJiggleRigData();

            Assert.That(data.jiggleColliders.Length, Is.EqualTo(1));
            JiggleColliderSerializable written = data.jiggleColliders[0];
            Assert.That(written.transform, Is.SameAs(colliderTransform));
            Assert.That(written.collider.type,
                Is.EqualTo(JiggleCollider.JiggleColliderType.Capsule));
            Assert.That(written.collider.radius, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(written.collider.height, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(written.collider.capsuleAxis,
                Is.EqualTo(JiggleCollider.CapsuleAxis.Z));
            Assert.That((float)written.collider.localOffset.y, Is.EqualTo(0.12f).Within(1e-6f));
        }

        [Test]
        public void OneUndoRevertsTheWholeWrite()
        {
            Transform root = BuildBranchingChain();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            JiggleRigWriter.Write(RigFor(root));
            Assert.That(root.GetComponent<JiggleRig>(), Is.Not.Null);

            Undo.RevertAllDownToGroup(group);

            Assert.That(root.GetComponent<JiggleRig>(), Is.Null,
                "Undo should remove the component the writer added.");
        }
    }
}
