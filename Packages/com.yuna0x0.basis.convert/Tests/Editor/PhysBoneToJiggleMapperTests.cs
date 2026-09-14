using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Tests
{
    public class PhysBoneToJiggleMapperTests
    {
        private static PhysBoneData Bone(
            float pull = 0.2f,
            float spring = 0.2f,
            float stiffness = 0.2f,
            PhysBoneIntegrationType integration = PhysBoneIntegrationType.Simplified)
        {
            return new PhysBoneData
            {
                IntegrationType = integration,
                Pull = new PhysBoneCurvedFloat(pull),
                Spring = new PhysBoneCurvedFloat(spring),
                Stiffness = new PhysBoneCurvedFloat(stiffness),
                Gravity = new PhysBoneCurvedFloat(0f),
                GravityFalloff = new PhysBoneCurvedFloat(0f),
                Immobile = new PhysBoneCurvedFloat(0f),
                Radius = new PhysBoneCurvedFloat(0f),
                MaxAngleX = new PhysBoneCurvedFloat(45f),
                MaxAngleZ = new PhysBoneCurvedFloat(45f),
                MaxStretch = new PhysBoneCurvedFloat(0f),
                MaxSquish = new PhysBoneCurvedFloat(0f),
                StretchMotion = new PhysBoneCurvedFloat(0f),
                LimitType = PhysBoneLimitType.Angle,
                AllowCollision = true,
                AllowGrabbing = true,
                Parameter = string.Empty,
            };
        }

        [Test]
        public void ImmobileMapsStraightOntoIgnoreRootMotion()
        {
            PhysBoneData source = Bone();
            source.Immobile = new PhysBoneCurvedFloat(0.35f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.IgnoreRootMotion, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(plan.Diagnostics.HasCode("physbone.immobile.ignoreRootMotion"), Is.True);
        }

        [Test]
        public void AnImmobileBoneWithAlmostNoPullIsReportedAsDrifting()
        {
            // A wind-up key: immobile 1 so the avatar's movement never disturbs it, pull 0.01 so
            // it stays where a hand leaves it. Jiggle cancels the root's translation only, and
            // stiffness 0.01 never brings the chain back once the hips turn, so the key wandered
            // off the doll on a real avatar.
            PhysBoneData source = Bone(pull: 0.01f, spring: 0f);
            source.Immobile = new PhysBoneCurvedFloat(1f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("physbone.immobile.drift"), Is.True);
        }

        [Test]
        public void AnImmobileBoneWithOrdinaryPullIsNotReportedAsDrifting()
        {
            PhysBoneData source = Bone(pull: 0.4f);
            source.Immobile = new PhysBoneCurvedFloat(1f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("physbone.immobile.drift"), Is.False);
        }

        [Test]
        public void FalloffCurvesCarryAcrossUntouched()
        {
            AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            PhysBoneData source = Bone(pull: 0.5f);
            source.Pull = new PhysBoneCurvedFloat(0.5f, curve);
            source.Radius = new PhysBoneCurvedFloat(0.05f, curve);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.Stiffness.Value.CurveEnabled, Is.True);
            Assert.That(plan.Parameters.Stiffness.Value.Curve, Is.SameAs(curve));
            Assert.That(plan.Parameters.CollisionRadius.Value.CurveEnabled, Is.True);
        }

        [Test]
        public void SpringAndDragMoveInOppositeDirections()
        {
            float loose = PhysBoneToJiggleMapper.Map(Bone(spring: 0.9f))
                .Parameters.Drag.Value.Value;
            float tight = PhysBoneToJiggleMapper.Map(Bone(spring: 0.1f))
                .Parameters.Drag.Value.Value;

            Assert.That(loose, Is.LessThan(tight),
                "More spring means more wobble, which must mean less drag.");
            Assert.That(loose, Is.InRange(0f, 1f));
            Assert.That(tight, Is.InRange(0f, 1f));
        }

        [Test]
        public void AdvancedIntegrationLetsStiffnessContributeButSimplifiedDoesNot()
        {
            float simplified = PhysBoneToJiggleMapper
                .Map(Bone(pull: 0.3f, stiffness: 0.8f))
                .Parameters.Stiffness.Value.Value;

            float advanced = PhysBoneToJiggleMapper
                .Map(Bone(pull: 0.3f, stiffness: 0.8f,
                    integration: PhysBoneIntegrationType.Advanced))
                .Parameters.Stiffness.Value.Value;

            Assert.That(simplified, Is.EqualTo(0.3f).Within(1e-6f),
                "Simplified integration has no separate stiffness term.");
            Assert.That(advanced, Is.GreaterThan(simplified));
        }

        [Test]
        public void PolarLimitsTakeTheWiderAngleAndSaySo()
        {
            PhysBoneData source = Bone();
            source.LimitType = PhysBoneLimitType.Polar;
            source.MaxAngleX = new PhysBoneCurvedFloat(10f);
            source.MaxAngleZ = new PhysBoneCurvedFloat(60f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.AngleLimitToggle, Is.True);
            Assert.That(plan.Parameters.AngleLimit.Value.Value,
                Is.EqualTo(60f / 90f).Within(1e-5f));
            Assert.That(plan.Diagnostics.HasCode("physbone.limitType.polar"), Is.True);
        }

        [Test]
        public void ALimitWiderThanJiggleCanExpressIsRemovedRatherThanTightened()
        {
            // Jiggle's angle limit stops at 90 degrees. A PhysBone allowing 180 is effectively
            // unconstrained, so clamping to 90 would hold the bones tighter than the original.
            PhysBoneData source = Bone();
            source.LimitType = PhysBoneLimitType.Angle;
            source.MaxAngleX = new PhysBoneCurvedFloat(180f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.AngleLimitToggle, Is.False);
            Assert.That(plan.Diagnostics.HasCode("physbone.limitType.tooWide"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("mapping.clamped"), Is.False,
                "Reporting this as a clamp would misdescribe what happened.");
        }

        [Test]
        public void NoLimitTypeTurnsTheAngleLimitOff()
        {
            PhysBoneData source = Bone();
            source.LimitType = PhysBoneLimitType.None;

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.AngleLimitToggle, Is.False);
        }

        [Test]
        public void OutOfRangeSourceValuesAreClampedAndReported()
        {
            // Real avatars contain these. The reference fixture has radius -29.73 on two bones.
            PhysBoneData source = Bone();
            source.Radius = new PhysBoneCurvedFloat(-29.73f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.CollisionRadius.Value.Value, Is.Zero);
            Assert.That(plan.Parameters.CollisionToggle, Is.False);
            Assert.That(plan.Diagnostics.HasCode("physbone.radius.negative"), Is.True);
        }

        [Test]
        public void AllowCollisionOffKeepsTheListedCollidersActive()
        {
            // Allow Collision only excludes other players' hands. A skirt with Allow Collision off
            // and two leg colliders listed still collides with those legs in VRChat.
            PhysBoneData source = Bone();
            source.Radius = new PhysBoneCurvedFloat(0.05f);
            source.AllowCollision = false;

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.CollisionToggle, Is.True);
            Assert.That(plan.Diagnostics.HasCode("physbone.allowCollision.off"), Is.True);
        }

        [Test]
        public void AllowCollisionOffWithoutRadiusIsNotReported()
        {
            PhysBoneData source = Bone();
            source.Radius = new PhysBoneCurvedFloat(0f);
            source.AllowCollision = false;

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.CollisionToggle, Is.False);
            Assert.That(plan.Diagnostics.HasCode("physbone.allowCollision.off"), Is.False);
        }

        [Test]
        public void MultiChildIgnoreBecomesMotionlessRoot()
        {
            PhysBoneData source = Bone();
            source.MultiChildType = PhysBoneMultiChildType.Ignore;

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source, rootChildCount: 2);

            Assert.That(plan.ExcludeRoot, Is.True);
            Assert.That(plan.Diagnostics.HasCode("physbone.multiChildType.ignore"), Is.True);
        }

        [Test]
        public void ARootWithOneChildIsSimulatedWhateverMultiChildTypeSays()
        {
            // VRChat: simulatedType = Child when childCount == 1, before Multi Child Type is
            // consulted. Pinning the root would freeze the first joint of every strand.
            foreach (PhysBoneMultiChildType type in new[]
            {
                PhysBoneMultiChildType.Ignore,
                PhysBoneMultiChildType.First,
                PhysBoneMultiChildType.Average,
            })
            {
                PhysBoneData source = Bone();
                source.MultiChildType = type;

                JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source, rootChildCount: 1);

                Assert.That(plan.ExcludeRoot, Is.False, type.ToString());
                Assert.That(plan.Diagnostics.HasCode("physbone.multiChildType.oneChild"), Is.True);
            }
        }

        [Test]
        public void TheSpringCurveIsDroppedRatherThanInvertingTheFalloff()
        {
            // Drag runs opposite to spring, so multiplying drag by spring's curve would fade
            // drag where spring fades.
            PhysBoneData source = Bone();
            source.Spring = new PhysBoneCurvedFloat(0.5f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.Parameters.Drag.Value.CurveEnabled, Is.False);
            Assert.That(plan.Diagnostics.HasCode("physbone.springCurve.dropped"), Is.True);
        }

        [Test]
        public void GrabbingNeedsAPositiveRadius()
        {
            // PhysBoneManager skips a chain with radius <= 0 in both grab paths.
            PhysBoneData zero = Bone();
            zero.AllowGrabbing = true;
            Assert.That(PhysBoneToJiggleMapper.Map(zero).LockFromGrabbing, Is.True);

            PhysBoneData sized = Bone();
            sized.AllowGrabbing = true;
            sized.Radius = new PhysBoneCurvedFloat(0.05f);
            Assert.That(PhysBoneToJiggleMapper.Map(sized).LockFromGrabbing, Is.False);
        }

        [Test]
        public void PerPlayerPermissionsAreReported()
        {
            // AdvancedBool.Other defers to a filter jiggle cannot express.
            PhysBoneData source = Bone();
            source.Radius = new PhysBoneCurvedFloat(0.05f);
            source.AllowGrabbing = true;
            source.AllowGrabbingFiltered = true;
            source.AllowCollisionFiltered = true;

            List<ConversionDiagnostic> log = PhysBoneToJiggleMapper.Map(source).Diagnostics;

            Assert.That(log.HasCode("physbone.allowGrabbing.filtered"), Is.True);
            Assert.That(log.HasCode("physbone.allowCollision.filtered"), Is.True);
        }

        [Test]
        public void HingeIsApproximatedAndCurvesAreReported()
        {
            PhysBoneData source = Bone();
            source.LimitType = PhysBoneLimitType.Hinge;
            source.Pull = new PhysBoneCurvedFloat(0.2f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

            List<ConversionDiagnostic> log = PhysBoneToJiggleMapper.Map(source).Diagnostics;

            Assert.That(log.HasCode("physbone.limitType.hinge"), Is.True);
            Assert.That(log.HasCode("physbone.curves.domain"), Is.True);
        }

        [Test]
        public void BlendedMultiChildIsApproximatedRatherThanSilentlyAccepted()
        {
            PhysBoneData source = Bone();
            source.MultiChildType = PhysBoneMultiChildType.Average;

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source, rootChildCount: 3);

            Assert.That(plan.Diagnostics.HasCode("physbone.multiChildType.blended"), Is.True);
        }

        [Test]
        public void EverythingWithoutAnEquivalentIsReportedRatherThanIgnored()
        {
            PhysBoneData source = Bone();
            source.GravityFalloff = new PhysBoneCurvedFloat(0.5f);
            source.MaxSquish = new PhysBoneCurvedFloat(0.3f);
            source.EndpointPosition = new Vector3(0f, 0.03f, 0f);
            source.LimitRotation = new Vector3(0f, 45f, 0f);
            source.Parameter = "TailWag";
            source.IsAnimated = true;
            source.SnapToHand = true;
            source.AllowPosing = true;

            List<ConversionDiagnostic> log = PhysBoneToJiggleMapper.Map(source).Diagnostics;

            foreach (string code in new[]
            {
                "physbone.gravityFalloff.dropped",
                "physbone.maxSquish.dropped",
                "physbone.endpointPosition.dropped",
                "physbone.limitRotation.dropped",
                "physbone.parameter.dropped",
                "physbone.isAnimated",
                "physbone.snapToHand.dropped",
                "physbone.allowPosing.dropped",
            })
            {
                Assert.That(log.HasCode(code), Is.True, $"missing diagnostic {code}");
            }
        }

        [Test]
        public void GrabbingAndIgnoredTransformsCarryAcross()
        {
            PhysBoneData source = Bone();
            source.AllowGrabbing = false;
            source.IgnoreTransformFileIds = new List<long> { 11L, 22L };
            source.ColliderFileIds = new List<long> { 33L };
            source.MaxStretch = new PhysBoneCurvedFloat(1.5f);

            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(source);

            Assert.That(plan.LockFromGrabbing, Is.True);
            Assert.That(plan.ExcludedTransformFileIds, Is.EquivalentTo(new[] { 11L, 22L }));
            Assert.That(plan.ColliderSourceFileIds, Is.EquivalentTo(new[] { 33L }));
            Assert.That(plan.Diagnostics.HasCode("physbone.maxStretch.dropped"), Is.True,
                "Max Stretch bounds bone length; jiggle's grab stretch bounds grab reach.");
            Assert.That(plan.Parameters.RootStretch, Is.EqualTo(0f),
                "PhysBone never lets the root particle leave its animated position.");
        }

        [Test]
        public void AdvancedToggleIsOnBecauseJiggleIgnoresHalfTheParametersWithoutIt()
        {
            // ToJigglePointParameters gates stretch, collisionRadius, ignoreRootMotion, soften
            // and rootStretch behind advancedToggle. Writing them with it off is a silent no-op.
            JiggleRigPlan plan = PhysBoneToJiggleMapper.Map(Bone());

            Assert.That(plan.Parameters.AdvancedToggle, Is.True);
        }
    }
}
