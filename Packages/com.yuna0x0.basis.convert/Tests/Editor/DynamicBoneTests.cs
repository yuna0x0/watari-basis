using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    public class DynamicBoneTests
    {
        private static DynamicBoneData ReadBone(params string[] extra)
        {
            List<string> lines = new List<string>
            {
                "--- !u!114 &800",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 10}",
                "  m_Script: {fileID: 11500000, guid: f9ac8d30c6a0d9642a11e5be4c440740, type: 3}",
                "  m_Root: {fileID: 20}",
                "  m_Roots: []",
                "  m_UpdateRate: 60",
                "  m_UpdateMode: 3",
                "  m_Damping: 0.35",
                "  m_Elasticity: 0.4",
                "  m_Stiffness: 0.2",
                "  m_Inert: 0.15",
                "  m_Friction: 0",
                "  m_Radius: 0.03",
                "  m_EndLength: 0",
                "  m_EndOffset: {x: 0, y: 0, z: 0}",
                "  m_Gravity: {x: 0, y: -0.5, z: 0}",
                "  m_Force: {x: 0, y: 0, z: 0}",
                "  m_BlendWeight: 1",
                "  m_Colliders: []",
                "  m_Exclusions: []",
                "  m_FreezeAxis: 0",
            };
            lines.AddRange(extra);

            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(1));
            return DynamicBoneDocumentReader.ReadBone(documents[0]);
        }

        [Test]
        public void TheScriptIdentityIsRecognised()
        {
            Assert.That(
                KnownScriptIdentities.Resolve("f9ac8d30c6a0d9642a11e5be4c440740", 11500000L),
                Is.EqualTo(SourceComponentKind.DynamicBone));
        }

        [Test]
        public void ReadsTheSettings()
        {
            DynamicBoneData data = ReadBone();

            Assert.That(data.OwnerGameObjectFileId, Is.EqualTo(10L));
            Assert.That(data.RootFileId, Is.EqualTo(20L));
            Assert.That(data.Damping.Value, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(data.Elasticity.Value, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(data.Stiffness.Value, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(data.Inert.Value, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(data.Radius.Value, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(data.Gravity.y, Is.EqualTo(-0.5f).Within(1e-6f));
        }

        [Test]
        public void DampingAndInertMapDirectly()
        {
            // Both systems use the same 0 to 1 scale for these, so they are not approximations.
            List<JiggleRigPlan> plans = DynamicBoneToJiggleMapper.Map(ReadBone());
            Assert.That(plans.Count, Is.EqualTo(1));

            JiggleRigPlan plan = plans[0];
            Assert.That(plan.Parameters.Drag.Value.Value, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(plan.Parameters.IgnoreRootMotion, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.damping.drag"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.inert.ignoreRootMotion"), Is.True);
        }

        [Test]
        public void EachRootBecomesItsOwnRig()
        {
            DynamicBoneData data = ReadBone();
            data.RootFileIds = new List<long> { 21L, 22L };

            List<JiggleRigPlan> plans = DynamicBoneToJiggleMapper.Map(data);

            Assert.That(plans.Count, Is.EqualTo(3));
            Assert.That(plans[0].RootBoneFileId, Is.EqualTo(20L));
            Assert.That(plans[1].RootBoneFileId, Is.EqualTo(21L));
            Assert.That(plans[2].RootBoneFileId, Is.EqualTo(22L));
            Assert.That(plans[0].Diagnostics.HasCode("dynamicbone.multipleRoots"), Is.True);

            foreach (JiggleRigPlan plan in plans)
            {
                Assert.That(plan.Parameters.Drag.Value.Value, Is.EqualTo(0.35f).Within(1e-6f),
                    "Chains from one component share its settings.");
            }
        }

        [Test]
        public void ABoneWithNoRootProducesNoRig()
        {
            // Dynamic Bone builds no particle tree without a root, so nothing simulates.
            DynamicBoneData data = ReadBone();
            data.RootFileId = 0L;

            Assert.That(DynamicBoneToJiggleMapper.Map(data), Is.Empty);
        }

        [Test]
        public void BlendWeightZeroProducesNoRig()
        {
            DynamicBoneData data = ReadBone();
            data.BlendWeight = 0f;

            Assert.That(DynamicBoneToJiggleMapper.Map(data), Is.Empty);
        }

        [Test]
        public void ElasticityBecomesStiffnessByItsSquareRoot()
        {
            // Dynamic Bone lerps elasticity of the way to the pose per tick; jiggle lerps
            // stiffness squared, so sqrt(0.4) gives the same fraction.
            JiggleRigPlan plan = DynamicBoneToJiggleMapper.Map(ReadBone())[0];
            Assert.That(plan.Parameters.Stiffness.Value.Value,
                Is.EqualTo(Mathf.Sqrt(0.4f)).Within(1e-5f));
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.elasticity.stiffness"), Is.True);

            DynamicBoneData fast = ReadBone();
            fast.UpdateRate = 120f;
            JiggleRigPlan scaled = DynamicBoneToJiggleMapper.Map(fast)[0];
            Assert.That(scaled.Parameters.Stiffness.Value.Value,
                Is.EqualTo(Mathf.Sqrt(0.8f)).Within(1e-5f),
                "Update Rate 120 doubles the per-tick fraction.");
            Assert.That(scaled.Diagnostics.HasCode("dynamicbone.updateRate"), Is.True);
        }

        [Test]
        public void StiffnessBecomesAnAngleLimit()
        {
            // stiffness s caps displacement at 2·length·(1−s), an angle of 2·asin(1−s).
            DynamicBoneData loose = ReadBone();
            loose.Stiffness = new PhysBoneCurvedFloat(0.2f);
            JiggleRigPlan wide = DynamicBoneToJiggleMapper.Map(loose)[0];
            Assert.That(wide.Parameters.AngleLimitToggle, Is.False,
                "2·asin(0.8) is 106 degrees, wider than jiggle's 90.");
            Assert.That(wide.Diagnostics.HasCode("dynamicbone.stiffness.tooWide"), Is.True);

            DynamicBoneData firm = ReadBone();
            firm.Stiffness = new PhysBoneCurvedFloat(0.6f);
            JiggleRigPlan limited = DynamicBoneToJiggleMapper.Map(firm)[0];
            float degrees = 2f * Mathf.Asin(0.4f) * Mathf.Rad2Deg;
            Assert.That(limited.Parameters.AngleLimitToggle, Is.True);
            Assert.That(limited.Parameters.AngleLimit.Value.Value,
                Is.EqualTo(degrees / 90f).Within(1e-5f));
            Assert.That(limited.Diagnostics.HasCode("dynamicbone.stiffness.angleLimit"), Is.True);
        }

        [Test]
        public void BlendWeightTightensTheAngleLimit()
        {
            // stiffness = Lerp(1, s, weight) in Dynamic Bone, so weight 0.5 with s 0.2 is 0.6.
            DynamicBoneData data = ReadBone();
            data.Stiffness = new PhysBoneCurvedFloat(0.2f);
            data.BlendWeight = 0.5f;

            JiggleRigPlan plan = DynamicBoneToJiggleMapper.Map(data)[0];

            Assert.That(plan.Parameters.AngleLimitToggle, Is.True);
            Assert.That(plan.Parameters.AngleLimit.Value.Value,
                Is.EqualTo(2f * Mathf.Asin(0.4f) * Mathf.Rad2Deg / 90f).Within(1e-5f));
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.blendWeight"), Is.True);
        }

        [Test]
        public void DampingBecomesDragAndAirDrag()
        {
            // Dynamic Bone damps the whole velocity; jiggle splits it into drag and air drag.
            JiggleRigPlan plan = DynamicBoneToJiggleMapper.Map(ReadBone())[0];
            Assert.That(plan.Parameters.Drag.Value.Value, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(plan.Parameters.AirDrag.Value.Value, Is.EqualTo(0.35f).Within(1e-6f));
        }

        [Test]
        public void ZeroRadiusWithCollidersStillCollides()
        {
            // Dynamic Bone collides a radius 0 bone as a point; jiggle skips radius 0 entirely.
            DynamicBoneData data = ReadBone();
            data.Radius = new PhysBoneCurvedFloat(0f);
            data.ColliderFileIds = new List<long> { 30L };

            JiggleRigPlan plan = DynamicBoneToJiggleMapper.Map(data)[0];

            Assert.That(plan.Parameters.CollisionToggle, Is.True);
            Assert.That(plan.Parameters.CollisionRadius.Value.Value,
                Is.EqualTo(DynamicBoneToJiggleMapper.PointCollisionRadius).Within(1e-6f));
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.radius.zero"), Is.True);

            DynamicBoneData bare = ReadBone();
            bare.Radius = new PhysBoneCurvedFloat(0f);
            Assert.That(DynamicBoneToJiggleMapper.Map(bare)[0].Parameters.CollisionToggle,
                Is.False, "Without colliders nothing collides on either side.");
        }

        [Test]
        public void GravityIsReportedAndLeftToThePreset()
        {
            // Dynamic Bone adds gravity per tick without a time step and cancels it at rest, so
            // no multiplier reproduces it. The plan leaves the preset's gravity in place.
            JiggleRigPlan plan = DynamicBoneToJiggleMapper.Map(ReadBone())[0];
            Assert.That(plan.Parameters.Gravity.HasValue, Is.False);
            Assert.That(plan.Diagnostics.HasCode("dynamicbone.gravity"), Is.True);

            DynamicBoneData none = ReadBone();
            none.Gravity = Vector3.zero;
            JiggleRigPlan still = DynamicBoneToJiggleMapper.Map(none)[0];
            Assert.That(still.Parameters.Gravity.Value.Value, Is.Zero);
            Assert.That(still.Diagnostics.HasCode("dynamicbone.gravity"), Is.False);
        }

        [Test]
        public void DroppedDistributionCurvesAreReported()
        {
            DynamicBoneData data = ReadBone(
                "  m_StiffnessDistrib:",
                "    serializedVersion: 2",
                "    m_Curve:",
                "    - serializedVersion: 3",
                "      time: 0",
                "      value: 1",
                "      inSlope: 0",
                "      outSlope: 0",
                "      tangentMode: 0",
                "      weightedMode: 0",
                "      inWeight: 0",
                "      outWeight: 0",
                "  m_InertDistrib:",
                "    serializedVersion: 2",
                "    m_Curve:",
                "    - serializedVersion: 3",
                "      time: 0",
                "      value: 1",
                "      inSlope: 0",
                "      outSlope: 0",
                "      tangentMode: 0",
                "      weightedMode: 0",
                "      inWeight: 0",
                "      outWeight: 0");

            List<ConversionDiagnostic> log = DynamicBoneToJiggleMapper.Map(data)[0].Diagnostics;

            Assert.That(log.HasCode("dynamicbone.stiffnessCurve.dropped"), Is.True);
            Assert.That(log.HasCode("dynamicbone.inertCurve.dropped"), Is.True);
        }

        [Test]
        public void EndLengthOneMatchesJigglesOwnTip()
        {
            DynamicBoneData exact = ReadBone();
            exact.EndLength = 1f;
            Assert.That(DynamicBoneToJiggleMapper.Map(exact)[0].Diagnostics
                .HasCode("dynamicbone.endpoint.dropped"), Is.False);

            DynamicBoneData other = ReadBone();
            other.EndLength = 0.5f;
            Assert.That(DynamicBoneToJiggleMapper.Map(other)[0].Diagnostics
                .HasCode("dynamicbone.endpoint.dropped"), Is.True);
        }

        [Test]
        public void SettingsWithNoJiggleEquivalentAreReported()
        {
            DynamicBoneData data = ReadBone();
            data.Force = new Vector3(0f, 0f, 1f);
            data.FreezeAxis = DynamicBoneFreezeAxis.Y;
            data.Friction = new PhysBoneCurvedFloat(0.5f);
            data.EndOffset = new Vector3(0f, 0.1f, 0f);

            List<ConversionDiagnostic> log = DynamicBoneToJiggleMapper.Map(data)[0].Diagnostics;

            foreach (string code in new[]
                     {
                         "dynamicbone.force.dropped",
                         "dynamicbone.freezeAxis.dropped",
                         "dynamicbone.friction.dropped",
                         "dynamicbone.endpoint.dropped",
                     })
            {
                Assert.That(log.HasCode(code), Is.True, $"missing diagnostic {code}");
            }
        }

        [Test]
        public void ColliderShapeComesFromItsHeight()
        {
            DynamicBoneColliderData sphere = new DynamicBoneColliderData
            {
                Radius = 0.1f,
                Height = 0f,
            };
            Assert.That(DynamicBoneColliderToJiggleMapper.Map(sphere).Shape,
                Is.EqualTo(JiggleColliderShape.Sphere));

            DynamicBoneColliderData capsule = new DynamicBoneColliderData
            {
                Radius = 0.1f,
                Height = 0.5f,
                Direction = DynamicBoneColliderDirection.Z,
            };
            JiggleColliderPlan mapped = DynamicBoneColliderToJiggleMapper.Map(capsule);
            Assert.That(mapped.Shape, Is.EqualTo(JiggleColliderShape.Capsule));
            Assert.That(mapped.CapsuleAxis, Is.EqualTo(JiggleCapsuleAxis.Z));

            DynamicBoneColliderData plane = new DynamicBoneColliderData { IsPlane = true };
            Assert.That(DynamicBoneColliderToJiggleMapper.Map(plane).Shape,
                Is.EqualTo(JiggleColliderShape.Plane));
        }

        [Test]
        public void ATaperedCapsuleIsReported()
        {
            DynamicBoneColliderData tapered = new DynamicBoneColliderData
            {
                Radius = 0.1f,
                Height = 0.5f,
                Radius2 = 0.02f,
            };

            JiggleColliderPlan plan = DynamicBoneColliderToJiggleMapper.Map(tapered);

            Assert.That(plan.Diagnostics.HasCode("collider.taper.dropped"), Is.True);
            Assert.That(plan.Radius, Is.EqualTo(0.1f).Within(1e-6f));

            // Dynamic Bone ignores a second radius within 0.01 of the first.
            DynamicBoneColliderData near = new DynamicBoneColliderData
            {
                Radius = 0.1f,
                Height = 0.5f,
                Radius2 = 0.105f,
            };
            Assert.That(DynamicBoneColliderToJiggleMapper.Map(near).Diagnostics
                .HasCode("collider.taper.dropped"), Is.False);
        }
    }
}
