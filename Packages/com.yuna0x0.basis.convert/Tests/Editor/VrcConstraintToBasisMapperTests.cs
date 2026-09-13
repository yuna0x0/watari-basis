using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Tests
{
    public class VrcConstraintToBasisMapperTests
    {
        private static VrcConstraintData Constraint(VrcConstraintKind kind)
        {
            VrcConstraintData data = new VrcConstraintData
            {
                Kind = kind,
                OwnerGameObjectFileId = 100L,
            };
            data.Sources.Add(new VrcConstraintSource { SourceTransformFileId = 200L, Weight = 1f });
            return data;
        }

        [Test]
        public void EachVrchatKindBecomesTheMatchingBasisKind()
        {
            foreach ((VrcConstraintKind from, BasisConstraintKind to) in new[]
                     {
                         (VrcConstraintKind.Position, BasisConstraintKind.Position),
                         (VrcConstraintKind.Rotation, BasisConstraintKind.Rotation),
                         (VrcConstraintKind.Scale, BasisConstraintKind.Scale),
                         (VrcConstraintKind.Parent, BasisConstraintKind.Parent),
                         (VrcConstraintKind.Aim, BasisConstraintKind.Aim),
                         (VrcConstraintKind.LookAt, BasisConstraintKind.LookAt),
                     })
            {
                Assert.That(VrcConstraintToBasisMapper.Map(Constraint(from)).Kind, Is.EqualTo(to));
            }
        }

        [Test]
        public void AxisFlagsBecomeAxisMasks()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.AffectsRotationX = true;
            source.AffectsRotationY = false;
            source.AffectsRotationZ = true;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.RotationAxis, Is.EqualTo(ConstraintAxes.X | ConstraintAxes.Z));
        }

        [Test]
        public void AllAxesOffIsNotSilentlyTurnedIntoAll()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Position);
            source.AffectsPositionX = false;
            source.AffectsPositionY = false;
            source.AffectsPositionZ = false;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.TranslationAxis, Is.EqualTo(ConstraintAxes.None));
        }

        [Test]
        public void AConstraintDrivingAnotherTransformMovesToThatTransform()
        {
            // The one real structural difference: VRChat constraints can drive something other
            // than the object they sit on, Basis constraints cannot.
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.TargetTransformFileId = 999L;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.HostFileId, Is.EqualTo(999L));
            Assert.That(plan.Diagnostics.HasCode("constraint.retargeted"), Is.True);
        }

        [Test]
        public void ATargetPointingAtItsOwnObjectIsNotTreatedAsARetarget()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.TargetTransformFileId = source.OwnerGameObjectFileId;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.HostFileId, Is.EqualTo(source.OwnerGameObjectFileId));
            Assert.That(plan.Diagnostics.HasCode("constraint.retargeted"), Is.False);
        }

        [Test]
        public void LockedIsAlwaysWrittenBecauseVrchatForcesItInPlay()
        {
            // VRC.Dynamics: jobData.Locked = Locked || Application.isPlaying. Basis unlocked
            // snaps undriven axes to the captured rest pose, which VRChat never does.
            VrcConstraintData source = Constraint(VrcConstraintKind.Position);
            source.Locked = false;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Locked, Is.True);
            Assert.That(plan.Diagnostics.HasCode("constraint.locked"), Is.True);
        }

        [Test]
        public void LookAtRollIsNegatedForBasis()
        {
            // VRChat rotates by -Roll about the look axis, Basis by +Roll about the same axis.
            VrcConstraintData source = Constraint(VrcConstraintKind.LookAt);
            source.Roll = 30f;

            Assert.That(VrcConstraintToBasisMapper.Map(source).Roll, Is.EqualTo(-30f).Within(1e-6f));
        }

        [Test]
        public void ALoneSourceBelowFullWeightIsReported()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.Sources[0].Weight = 0.3f;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("constraint.source.weight.normalized"), Is.True);
        }

        [Test]
        public void AimWithNoWorldUpIsReported()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Aim);
            source.WorldUp = VrcConstraintWorldUp.None;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("constraint.worldUp.none"), Is.True);
        }

        [Test]
        public void EmptySourceSlotsAreDroppedAndReported()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.Sources.Add(new VrcConstraintSource { SourceTransformFileId = 0L });

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Sources.Count, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.HasCode("constraint.source.empty"), Is.True);
        }

        [Test]
        public void AConstraintWithNoSourcesIsFlagged()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.Sources.Clear();

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("constraint.noSources"), Is.True);
        }

        [Test]
        public void VrchatOnlySettingsAreReportedRatherThanIgnored()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.SolveInLocalSpace = true;
            source.FreezeToWorld = true;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("constraint.solveInLocalSpace.dropped"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("constraint.freezeToWorld.dropped"), Is.True);
        }

        [Test]
        public void AimSettingsCarryAcross()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Aim);
            source.AimAxis = Vector3.right;
            source.UpAxis = Vector3.forward;
            source.WorldUp = VrcConstraintWorldUp.ObjectRotationUp;
            source.WorldUpVector = new Vector3(0f, 0f, 1f);
            source.WorldUpTransformFileId = 321L;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.AimVector, Is.EqualTo(Vector3.right));
            Assert.That(plan.UpVector, Is.EqualTo(Vector3.forward));
            Assert.That(plan.WorldUpType, Is.EqualTo(ConstraintWorldUp.ObjectRotationUp));
            Assert.That(plan.WorldUpTransformFileId, Is.EqualTo(321L));
        }

        [Test]
        public void ParentConstraintsKeepTheirPerSourceOffsets()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Parent);
            source.Sources[0].ParentPositionOffset = new Vector3(1f, 2f, 3f);
            source.Sources[0].ParentRotationOffset = new Vector3(0f, 90f, 0f);

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Sources[0].PositionOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(plan.Sources[0].RotationOffset, Is.EqualTo(new Vector3(0f, 90f, 0f)));
        }

        [Test]
        public void SourcesPastTheSixteenthAreReportedAsUnread()
        {
            // VRChat serializes the first sixteen sources as numbered slots and the rest in an
            // overflow list, which is not read. Losing them silently would be worse than saying
            // so.
            VrcConstraintData source = Constraint(VrcConstraintKind.Parent);
            source.DeclaredSourceCount = 18;
            source.Sources.Clear();

            for (int i = 0; i < 16; i++)
            {
                source.Sources.Add(new VrcConstraintSource {SourceTransformFileId = 100 + i});
            }

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Sources.Count, Is.EqualTo(16));
            Assert.That(plan.Diagnostics.HasCode("constraint.source.overflow"), Is.True);
        }

        [Test]
        public void EverySourceReadIsNotReportedAsOverflow()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.DeclaredSourceCount = source.Sources.Count;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Diagnostics.HasCode("constraint.source.overflow"), Is.False);
        }

        [Test]
        public void WeightOutsideTheAcceptedRangeIsClampedAndReported()
        {
            VrcConstraintData source = Constraint(VrcConstraintKind.Rotation);
            source.GlobalWeight = 2.5f;

            BasisConstraintPlan plan = VrcConstraintToBasisMapper.Map(source);

            Assert.That(plan.Weight, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(plan.Diagnostics.HasCode("constraint.weight.clamped"), Is.True);
        }
    }
}
