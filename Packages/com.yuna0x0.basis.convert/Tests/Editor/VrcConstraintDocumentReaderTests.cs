using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    public class VrcConstraintDocumentReaderTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        /// <summary>
        /// The real serialized shape: sixteen fixed slots, then totalLength, then an overflow
        /// list. Trimmed to four slots here; the count is what matters, not the padding.
        /// </summary>
        private static string[] RotationConstraintLines(int totalLength, params long[] sourceIds)
        {
            List<string> lines = new List<string>
            {
                "--- !u!114 &900",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 42}",
                "  IsActive: 1",
                "  GlobalWeight: 0.3",
                "  TargetTransform: {fileID: 0}",
                "  SolveInLocalSpace: 0",
                "  FreezeToWorld: 0",
                "  RebakeOffsetsWhenUnfrozen: 0",
                "  Locked: 0",
                "  Sources:",
            };

            for (int i = 0; i < 4; i++)
            {
                long id = i < sourceIds.Length ? sourceIds[i] : 0L;
                lines.Add($"    source{i}:");
                lines.Add($"      SourceTransform: {{fileID: {id}}}");
                lines.Add($"      Weight: {(i == 0 ? "0.75" : "1")}");
                lines.Add("      ParentPositionOffset: {x: 0, y: 0, z: 0}");
                lines.Add("      ParentRotationOffset: {x: 0, y: 0, z: 0}");
                lines.Add("      _defaultsApplied: 1");
            }

            lines.Add($"    totalLength: {totalLength}");
            lines.Add("    overflowList: []");
            lines.Add("  RotationAtRest: {x: 0, y: 90, z: 0}");
            lines.Add("  RotationOffset: {x: 1, y: 2, z: 3}");
            lines.Add("  AffectsRotationX: 1");
            lines.Add("  AffectsRotationY: 0");
            lines.Add("  AffectsRotationZ: 1");
            return lines.ToArray();
        }

        private static VrcConstraintData Read(string[] lines, VrcConstraintKind kind)
        {
            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(1));
            return VrcConstraintDocumentReader.Read(documents[0], kind);
        }

        [Test]
        public void ReadsTheCommonBlockAndTypeSpecificFields()
        {
            VrcConstraintData data = Read(
                RotationConstraintLines(1, 555L), VrcConstraintKind.Rotation);

            Assert.That(data.OwnerGameObjectFileId, Is.EqualTo(42L));
            Assert.That(data.IsActive, Is.True);
            Assert.That(data.GlobalWeight, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(data.Locked, Is.False);
            Assert.That(data.TargetTransformFileId, Is.Zero);
            Assert.That(data.RotationAtRest.y, Is.EqualTo(90f).Within(1e-6f));
            Assert.That(data.RotationOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(data.AffectsRotationX, Is.True);
            Assert.That(data.AffectsRotationY, Is.False);
            Assert.That(data.AffectsRotationZ, Is.True);
        }

        [Test]
        public void OnlyTheFirstTotalLengthSlotsAreRealSources()
        {
            // The sixteen slots always serialize. Reading them all would invent sources that the
            // author never added.
            VrcConstraintData one = Read(
                RotationConstraintLines(1, 111L, 222L, 333L), VrcConstraintKind.Rotation);
            Assert.That(one.Sources.Count, Is.EqualTo(1));
            Assert.That(one.Sources[0].SourceTransformFileId, Is.EqualTo(111L));
            Assert.That(one.Sources[0].Weight, Is.EqualTo(0.75f).Within(1e-6f));

            VrcConstraintData three = Read(
                RotationConstraintLines(3, 111L, 222L, 333L), VrcConstraintKind.Rotation);
            Assert.That(three.Sources.Count, Is.EqualTo(3));
            Assert.That(three.Sources[2].SourceTransformFileId, Is.EqualTo(333L));

            VrcConstraintData none = Read(
                RotationConstraintLines(0, 111L), VrcConstraintKind.Rotation);
            Assert.That(none.Sources, Is.Empty);
        }

        [Test]
        public void ReadsTheFieldsOnlySomeKindsUse()
        {
            VrcConstraintData data = Read(new[]
            {
                "--- !u!114 &901",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 7}",
                "  TargetTransform: {fileID: 8}",
                "  Sources:",
                "    source0:",
                "      SourceTransform: {fileID: 9}",
                "      Weight: 1",
                "    totalLength: 1",
                "    overflowList: []",
                "  AimAxis: {x: 0, y: 0, z: 1}",
                "  UpAxis: {x: 0, y: 1, z: 0}",
                "  WorldUp: 3",
                "  WorldUpVector: {x: 1, y: 0, z: 0}",
                "  WorldUpTransform: {fileID: 10}",
                "  Roll: 12.5",
                "  UseUpTransform: 1",
            }, VrcConstraintKind.Aim);

            Assert.That(data.TargetTransformFileId, Is.EqualTo(8L));
            Assert.That(data.AimAxis, Is.EqualTo(Vector3.forward));
            Assert.That(data.WorldUp, Is.EqualTo(VrcConstraintWorldUp.Vector));
            Assert.That(data.WorldUpVector, Is.EqualTo(Vector3.right));
            Assert.That(data.WorldUpTransformFileId, Is.EqualTo(10L));
            Assert.That(data.Roll, Is.EqualTo(12.5f).Within(1e-6f));
            Assert.That(data.UseUpTransform, Is.True);
        }

        [Test]
        public void EveryConstraintTypeHasAScriptIdentity()
        {
            foreach (SourceComponentKind component in new[]
                     {
                         SourceComponentKind.VrcPositionConstraint,
                         SourceComponentKind.VrcRotationConstraint,
                         SourceComponentKind.VrcScaleConstraint,
                         SourceComponentKind.VrcParentConstraint,
                         SourceComponentKind.VrcAimConstraint,
                         SourceComponentKind.VrcLookAtConstraint,
                     })
            {
                Assert.That(VrcConstraintDocumentReader.TryGetKind(component, out VrcConstraintKind _),
                    Is.True, $"{component} has no constraint kind.");
            }
        }

        [Test]
        public void ReadsTheConstraintsInARealAvatar()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            int found = 0;
            int withSources = 0;
            int retargeted = 0;

            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(FixturePath))
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                SourceComponentKind component = KnownScriptIdentities.Resolve(guid, scriptFileId);
                if (!VrcConstraintDocumentReader.TryGetKind(component, out VrcConstraintKind kind))
                {
                    continue;
                }

                VrcConstraintData data = VrcConstraintDocumentReader.Read(document, kind);
                found++;

                if (data.Sources.Count > 0)
                {
                    withSources++;
                }

                if (data.TargetTransformFileId != 0L)
                {
                    retargeted++;
                }

                Assert.That(data.Sources.Count, Is.LessThanOrEqualTo(16),
                    "More sources were read than the inline slots can hold.");
                Assert.That(data.OwnerGameObjectFileId, Is.Not.Zero);
            }

            TestContext.WriteLine($"constraints: {found}");
            TestContext.WriteLine($"  with at least one source: {withSources}");
            TestContext.WriteLine($"  driving another transform: {retargeted}");

            Assert.That(found, Is.EqualTo(28));
            Assert.That(withSources, Is.EqualTo(found),
                "A constraint with no sources does nothing; none were expected here.");
        }
    }
}
