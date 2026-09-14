using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    public class PhysBoneDocumentReaderTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private static UnityYamlDocument ReadOnly(IEnumerable<string> lines)
        {
            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(1));
            return documents[0];
        }

        [Test]
        public void ReadsScalarsEnumsAndReferences()
        {
            PhysBoneData data = PhysBoneDocumentReader.ReadPhysBone(ReadOnly(new[]
            {
                "--- !u!114 &555",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 777}",
                "  version: 1",
                "  integrationType: 1",
                "  rootTransform: {fileID: 4242}",
                "  ignoreTransforms:",
                "  - {fileID: 111}",
                "  - {fileID: 0}",
                "  - {fileID: 222}",
                "  endpointPosition: {x: 0, y: 0.03, z: 0}",
                "  multiChildType: 2",
                "  pull: 0.4",
                "  immobileType: 1",
                "  immobile: 0.35",
                "  radius: 0.02",
                "  colliders: []",
                "  limitType: 3",
                "  maxAngleX: 10",
                "  maxAngleZ: 6",
                "  allowGrabbing: 0",
                "  grabMovement: 0.25",
                "  isAnimated: 1",
                "  parameter: TailWag",
            }));

            Assert.That(data.OwnerGameObjectFileId, Is.EqualTo(777L));
            Assert.That(data.Version, Is.EqualTo(1));
            Assert.That(data.IntegrationType, Is.EqualTo(PhysBoneIntegrationType.Advanced));
            Assert.That(data.RootTransformFileId, Is.EqualTo(4242L));
            Assert.That(data.MultiChildType, Is.EqualTo(PhysBoneMultiChildType.Average));
            Assert.That(data.ImmobileType, Is.EqualTo(PhysBoneImmobileType.WorldMotion));
            Assert.That(data.LimitType, Is.EqualTo(PhysBoneLimitType.Polar));
            Assert.That(data.EndpointPosition.y, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(data.Pull.Value, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(data.Immobile.Value, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(data.MaxAngleX.Value, Is.EqualTo(10f).Within(1e-6f));
            Assert.That(data.MaxAngleZ.Value, Is.EqualTo(6f).Within(1e-6f));
            Assert.That(data.AllowGrabbing, Is.False);
            Assert.That(data.IsAnimated, Is.True);
            Assert.That(data.Parameter, Is.EqualTo("TailWag"));

            // Null references inside a list are dropped.
            Assert.That(data.IgnoreTransformFileIds, Is.EquivalentTo(new[] { 111L, 222L }));
            Assert.That(data.ColliderFileIds, Is.Empty);
        }

        [Test]
        public void MissingKeysFallBackToVrchatDefaults()
        {
            PhysBoneData data = PhysBoneDocumentReader.ReadPhysBone(ReadOnly(new[]
            {
                "--- !u!114 &1",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 2}",
            }));

            Assert.That(data.Pull.Value, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(data.Spring.Value, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(data.Stiffness.Value, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(data.MaxAngleX.Value, Is.EqualTo(45f).Within(1e-6f));
            Assert.That(data.LimitType, Is.EqualTo(PhysBoneLimitType.None),
                "The SDK field has no initializer, so an absent key is None.");
            Assert.That(data.AllowCollision, Is.True);
            Assert.That(data.AllowGrabbing, Is.True);
        }

        [Test]
        public void AnEmptyCurveMeansNoFalloffRatherThanZero()
        {
            PhysBoneData data = PhysBoneDocumentReader.ReadPhysBone(ReadOnly(new[]
            {
                "--- !u!114 &1",
                "MonoBehaviour:",
                "  pull: 0.6",
                "  pullCurve:",
                "    serializedVersion: 2",
                "    m_Curve: []",
                "    m_PreInfinity: 2",
                "    m_PostInfinity: 2",
                "    m_RotationOrder: 4",
            }));

            Assert.That(data.Pull.HasCurve, Is.False);
            Assert.That(data.Pull.Evaluate(0f), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(data.Pull.Evaluate(1f), Is.EqualTo(0.6f).Within(1e-6f));
        }

        [Test]
        public void ReadsKeyframesAndScalesTheBaseValueAlongTheChain()
        {
            PhysBoneData data = PhysBoneDocumentReader.ReadPhysBone(ReadOnly(new[]
            {
                "--- !u!114 &1",
                "MonoBehaviour:",
                "  pull: 0.5",
                "  pullCurve:",
                "    serializedVersion: 2",
                "    m_Curve:",
                "    - serializedVersion: 3",
                "      time: 0",
                "      value: 1",
                "      inSlope: 0",
                "      outSlope: 0",
                "      tangentMode: 0",
                "      weightedMode: 0",
                "      inWeight: 0.33333334",
                "      outWeight: 0.33333334",
                "    - serializedVersion: 3",
                "      time: 1",
                "      value: 0",
                "      inSlope: 0",
                "      outSlope: 0",
                "      tangentMode: 0",
                "      weightedMode: 0",
                "      inWeight: 0.33333334",
                "      outWeight: 0.33333334",
                "    m_PreInfinity: 2",
                "    m_PostInfinity: 2",
                "    m_RotationOrder: 4",
            }));

            Assert.That(data.Pull.HasCurve, Is.True);
            Assert.That(data.Pull.Curve.length, Is.EqualTo(2));
            Assert.That(data.Pull.Evaluate(0f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(data.Pull.Evaluate(1f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ReadsColliders()
        {
            PhysBoneColliderData data = PhysBoneDocumentReader.ReadCollider(ReadOnly(new[]
            {
                "--- !u!114 &9",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 8}",
                "  rootTransform: {fileID: 0}",
                "  shapeType: 1",
                "  insideBounds: 0",
                "  radius: 0.03",
                "  height: 0.3",
                "  position: {x: 0, y: 0.12, z: 0}",
                "  rotation: {x: 0, y: 0, z: 0, w: 1}",
                "  bonesAsSpheres: 0",
            }));

            Assert.That(data.ShapeType, Is.EqualTo(PhysBoneColliderShape.Capsule));
            Assert.That(data.Radius, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(data.Height, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(data.Position.y, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(data.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(data.InsideBounds, Is.False);
        }

        [Test]
        public void ReadsEveryPhysBoneInARealAvatar()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            int physBones = 0;
            int colliders = 0;
            int curved = 0;
            int withIgnoreTransforms = 0;
            int outOfRange = 0;
            Dictionary<PhysBoneLimitType, int> limitTypes = new Dictionary<PhysBoneLimitType, int>();
            Dictionary<PhysBoneIntegrationType, int> integrations =
                new Dictionary<PhysBoneIntegrationType, int>();

            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(FixturePath))
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                switch (KnownScriptIdentities.Resolve(guid, scriptFileId))
                {
                    case SourceComponentKind.VrcPhysBone:
                    {
                        PhysBoneData data = PhysBoneDocumentReader.ReadPhysBone(document);
                        physBones++;

                        Assert.That(data.OwnerGameObjectFileId, Is.Not.Zero,
                            "A PhysBone was read with no owning GameObject.");

                        // Deliberately no range assertions on the raw values. Real avatars
                        // contain out-of-range settings: this one has radius -29.73 on two
                        // bones. The reader reports what is in the file, and clamping into
                        // jiggle's valid ranges is the mapper's job.
                        if (data.Radius.Value < 0f || data.Pull.Value < 0f
                            || data.Pull.Value > 1f || data.Spring.Value < 0f
                            || data.Spring.Value > 1f)
                        {
                            outOfRange++;
                        }

                        if (data.Pull.HasCurve || data.Radius.HasCurve
                            || data.Stiffness.HasCurve || data.Immobile.HasCurve)
                        {
                            curved++;
                        }

                        if (data.IgnoreTransformFileIds.Count > 0)
                        {
                            withIgnoreTransforms++;
                        }

                        limitTypes.TryGetValue(data.LimitType, out int limitCount);
                        limitTypes[data.LimitType] = limitCount + 1;
                        integrations.TryGetValue(data.IntegrationType, out int integrationCount);
                        integrations[data.IntegrationType] = integrationCount + 1;
                        break;
                    }

                    case SourceComponentKind.VrcPhysBoneCollider:
                    {
                        PhysBoneColliderData data = PhysBoneDocumentReader.ReadCollider(document);
                        colliders++;
                        Assert.That(data.Height, Is.GreaterThanOrEqualTo(0f));
                        break;
                    }
                }
            }

            TestContext.WriteLine($"physbones: {physBones}, colliders: {colliders}");
            TestContext.WriteLine($"with a falloff curve: {curved}");
            TestContext.WriteLine($"with ignoreTransforms: {withIgnoreTransforms}");
            TestContext.WriteLine($"with out-of-range values: {outOfRange}");
            foreach (KeyValuePair<PhysBoneLimitType, int> pair in limitTypes)
            {
                TestContext.WriteLine($"  limitType {pair.Key}: {pair.Value}");
            }

            foreach (KeyValuePair<PhysBoneIntegrationType, int> pair in integrations)
            {
                TestContext.WriteLine($"  integrationType {pair.Key}: {pair.Value}");
            }

            Assert.That(physBones, Is.EqualTo(61));
            Assert.That(colliders, Is.EqualTo(11));
        }
    }
}
