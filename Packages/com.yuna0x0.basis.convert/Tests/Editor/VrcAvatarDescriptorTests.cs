using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    public class VrcAvatarDescriptorTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        private static readonly string[] VisemeNames =
        {
            "vrc.v_sil", "vrc.v_pp", "vrc.v_ff", "vrc.v_th", "vrc.v_dd",
            "vrc.v_kk", "vrc.v_ch", "vrc.v_ss", "vrc.v_nn", "vrc.v_rr",
            "vrc.v_aa", "vrc.v_e", "vrc.v_ih", "vrc.v_oh", "vrc.v_ou",
        };

        private static VrcAvatarDescriptorData ReadDescriptor(
            string viewPosition = "{x: 0, y: 1.208, z: 0.08}",
            int lipSync = 3,
            int eyelidType = 2,
            string eyelidsBlob = "1d000000ffffffffffffffff",
            int enableEyeLook = 1,
            List<string> gazeStates = null)
        {
            List<string> lines = new List<string>
            {
                "--- !u!114 &700",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 1}",
                $"  ViewPosition: {viewPosition}",
                $"  lipSync: {lipSync}",
                "  VisemeSkinnedMesh: {fileID: 55}",
                "  VisemeBlendShapes:",
            };

            foreach (string name in VisemeNames)
            {
                lines.Add($"  - {name}");
            }

            lines.Add($"  enableEyeLook: {enableEyeLook}");
            lines.Add("  customEyeLookSettings:");
            lines.Add("    eyeMovement:");
            lines.Add("      confidence: 0.5");
            lines.Add("    leftEye: {fileID: 11}");
            lines.Add("    rightEye: {fileID: 12}");
            lines.Add($"    eyelidType: {eyelidType}");
            lines.Add("    eyelidsSkinnedMesh: {fileID: 66}");
            lines.Add($"    eyelidsBlendshapes: {eyelidsBlob}");
            if (gazeStates != null)
            {
                lines.AddRange(gazeStates);
            }

            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(1));
            return VrcAvatarDescriptorReader.Read(documents[0]);
        }

        /// <summary>
        /// The five gaze states as the SDK serializes them: each eye bone's local rotation,
        /// here a turn of the given degrees about X for up and down and about Y for left and
        /// right, composed onto the straight rotation.
        /// </summary>
        private static List<string> GazeStates(
            float up, float down, float left, float right, Quaternion? straight = null,
            float rightEyeScale = 1f)
        {
            Quaternion rest = straight ?? Quaternion.identity;
            List<string> lines = new List<string>();
            void Add(string key, Quaternion turn)
            {
                Quaternion leftEye = rest * turn;
                Quaternion rightEye = rest * Quaternion.Slerp(Quaternion.identity, turn, rightEyeScale);
                lines.Add($"    {key}:");
                lines.Add($"      linked: {(rightEyeScale == 1f ? 1 : 0)}");
                lines.Add($"      left: {Yaml(leftEye)}");
                lines.Add($"      right: {Yaml(rightEye)}");
            }

            Add("eyesLookingStraight", Quaternion.identity);
            Add("eyesLookingUp", Quaternion.Euler(-up, 0f, 0f));
            Add("eyesLookingDown", Quaternion.Euler(down, 0f, 0f));
            Add("eyesLookingLeft", Quaternion.Euler(0f, -left, 0f));
            Add("eyesLookingRight", Quaternion.Euler(0f, right, 0f));
            return lines;
        }

        private static string Yaml(Quaternion q)
        {
            // A "}}" straight after a format item is folded into its format string, so the
            // components are formatted one by one.
            string R(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return "{x: " + R(q.x) + ", y: " + R(q.y) + ", z: " + R(q.z) + ", w: " + R(q.w) + "}";
        }

        [Test]
        public void ComponentsWrittenWithANegativeExponentAreParsed()
        {
            // Unity writes a half turn about Y with a w of about -4e-08, and near-zero
            // components in that form are common in rest rotations.
            bool parsed = UnityYamlValues.TryParseQuaternion(
                "{x: 0, y: 1, z: 0, w: -4.371139e-08}", out Quaternion q);

            Assert.That(parsed, Is.True);
            Assert.That(q.w, Is.EqualTo(-4.371139e-08f).Within(1e-12f));
            Assert.That(q.y, Is.EqualTo(1f));
        }

        [Test]
        public void GazeStatesAreReadAsEyeRotations()
        {
            VrcAvatarDescriptorData data = ReadDescriptor(
                gazeStates: GazeStates(10f, 10f, 15f, 15f, rightEyeScale: 0.5f));

            Assert.That(data.HasEyeRotations, Is.True);
            Assert.That(data.EyesLookingUp.Linked, Is.False);
            Assert.That(Quaternion.Angle(data.EyesLookingStraight.Left, data.EyesLookingUp.Left),
                Is.EqualTo(10f).Within(1e-3f));
            Assert.That(Quaternion.Angle(data.EyesLookingStraight.Right, data.EyesLookingLeft.Right),
                Is.EqualTo(7.5f).Within(1e-3f));
        }

        [Test]
        public void ADescriptorWithoutGazeStatesReadsNone()
        {
            VrcAvatarDescriptorData data = ReadDescriptor();

            Assert.That(data.HasEyeRotations, Is.False);
            Assert.That(VrcAvatarDescriptorToBasisMapper.Map(data).EyeMaxLookAngleDegrees, Is.Zero);
        }

        [Test]
        public void AUniformEyeLimitIsWrittenAsIs()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(gazeStates: GazeStates(15f, 15f, 15f, 15f)));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.EqualTo(15f).Within(1e-3f));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range.uneven"), Is.False);
        }

        [Test]
        public void AnUnevenEyeLimitTakesTheLargestAndIsReported()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(gazeStates: GazeStates(10f, 10f, 15f, 15f)));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.EqualTo(15f).Within(1e-3f));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range.uneven"), Is.True);
        }

        [Test]
        public void TheEyeLimitIsMeasuredFromTheStraightState()
        {
            // An eye bone whose rest rotation is a half turn still turns 10 degrees from it.
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(ReadDescriptor(
                gazeStates: GazeStates(10f, 10f, 10f, 10f, Quaternion.Euler(0f, 180f, 0f))));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range"), Is.True);
        }

        [Test]
        public void GazeStatesLeftAtRestWriteTheSmallestLimit()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(gazeStates: GazeStates(0f, 0f, 0f, 0f)));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.EqualTo(1f));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range.none"), Is.True);
        }

        [Test]
        public void AnEyeLimitPastWhatBasisAllowsIsClamped()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(gazeStates: GazeStates(60f, 60f, 60f, 60f)));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.EqualTo(45f));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range.clamped"), Is.True);
        }

        [Test]
        public void DisabledEyeLookWritesNoEyeLimit()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(enableEyeLook: 0, gazeStates: GazeStates(15f, 15f, 15f, 15f)));

            Assert.That(plan.EyeMaxLookAngleDegrees, Is.Zero);
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.range"), Is.False);
        }

        [Test]
        public void ReadsTheDescriptorIncludingItsNestedEyeSettings()
        {
            VrcAvatarDescriptorData data = ReadDescriptor();

            Assert.That(data.OwnerGameObjectFileId, Is.EqualTo(1L));
            Assert.That(data.ViewPosition.y, Is.EqualTo(1.208f).Within(1e-5f));
            Assert.That(data.LipSync, Is.EqualTo(VrcLipSyncStyle.VisemeBlendShape));
            Assert.That(data.VisemeSkinnedMeshFileId, Is.EqualTo(55L));
            Assert.That(data.VisemeBlendShapes.Count, Is.EqualTo(15));
            Assert.That(data.VisemeBlendShapes[10], Is.EqualTo("vrc.v_aa"));
            Assert.That(data.EnableEyeLook, Is.True);
            Assert.That(data.EyelidType, Is.EqualTo(VrcEyelidType.Blendshapes));
            Assert.That(data.EyelidsSkinnedMeshFileId, Is.EqualTo(66L));
            Assert.That(data.LeftEyeFileId, Is.EqualTo(11L));
            Assert.That(data.RightEyeFileId, Is.EqualTo(12L));
        }

        [Test]
        public void EyelidIndicesAreDecodedFromTheHexBlob()
        {
            // Unity writes this small fixed array as bytes, not as a sequence. Reading it as a
            // string would silently lose the blink blendshape.
            Assert.That(UnityYamlValues.ParseHexInt32Blob("1d000000ffffffffffffffff"),
                Is.EqualTo(new[] { 29, -1, -1 }));
            Assert.That(UnityYamlValues.ParseHexInt32Blob("00000000"), Is.EqualTo(new[] { 0 }));
            Assert.That(UnityYamlValues.ParseHexInt32Blob(string.Empty), Is.Empty);

            VrcAvatarDescriptorData data = ReadDescriptor();
            Assert.That(data.EyelidsBlendshapes, Is.EqualTo(new[] { 29, -1, -1 }));
        }

        [Test]
        public void ViewPositionBecomesHeightAndForwardOffset()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(ReadDescriptor());

            Assert.That(plan.EyePosition.x, Is.EqualTo(1.208f).Within(1e-5f));
            Assert.That(plan.EyePosition.y, Is.EqualTo(0.08f).Within(1e-5f));
        }

        [Test]
        public void ASidewaysViewOffsetIsReportedRatherThanSilentlyLost()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(viewPosition: "{x: 0.05, y: 1.2, z: 0.08}"));

            Assert.That(plan.Diagnostics.HasCode("descriptor.viewPosition.sideways"), Is.True);
        }

        [Test]
        public void VisemesMapPositionForPosition()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(ReadDescriptor());

            Assert.That(plan.VisemeMeshFileId, Is.EqualTo(55L));
            Assert.That(plan.VisemeBlendShapeNames.Count, Is.EqualTo(15));
            Assert.That(plan.VisemeBlendShapeNames[0], Is.EqualTo("vrc.v_sil"));
            Assert.That(plan.VisemeBlendShapeNames[14], Is.EqualTo("vrc.v_ou"));
            Assert.That(plan.Diagnostics.HasCode("descriptor.visemes"), Is.True);
        }

        [Test]
        public void BlinkTakesTheFirstEyelidBlendshapeAndReportsTheRest()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(eyelidsBlob: "1d0000001e000000ffffffff"));

            Assert.That(plan.BlinkMeshFileId, Is.EqualTo(66L));
            Assert.That(plan.BlinkBlendShapeIndices, Is.EqualTo(new[] { 29 }));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyelids.lookUpDown"), Is.True);
        }

        [Test]
        public void UnsetLookShapesAreNotReportedAsDropped()
        {
            // The blob is blink, looking up, looking down; -1 means unset.
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(ReadDescriptor());

            Assert.That(plan.BlinkBlendShapeIndices, Is.EqualTo(new[] { 29 }));
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyelids.lookUpDown"), Is.False);
        }

        [Test]
        public void DisabledEyeLookLeavesBlinkUnset()
        {
            // The SDK keeps eyelid settings serialized when Enable Eye Look is off.
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(enableEyeLook: 0));

            Assert.That(plan.BlinkBlendShapeIndices, Is.Empty);
            Assert.That(plan.BlinkMeshFileId, Is.Zero);
            Assert.That(plan.Diagnostics.HasCode("descriptor.eyeLook.disabled"), Is.True);
        }

        [Test]
        public void LipSyncModesBasisCannotDriveAreReported()
        {
            foreach (int mode in new[] { 0, 1, 2, 4 })
            {
                BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                    ReadDescriptor(lipSync: mode));

                Assert.That(plan.Diagnostics.HasCode("descriptor.lipSync.unsupported"), Is.True,
                    $"lipSync mode {mode} should be reported as unsupported.");
                Assert.That(plan.VisemeMeshFileId, Is.Zero);
            }
        }

        [Test]
        public void BoneDrivenEyelidsAreReported()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(
                ReadDescriptor(eyelidType: 1));

            Assert.That(plan.Diagnostics.HasCode("descriptor.eyelids.bones"), Is.True);
            Assert.That(plan.BlinkBlendShapeIndices, Is.Empty);
        }

        [Test]
        public void TheExpressionSystemsAreReportedRatherThanPassedOverInSilence()
        {
            // These are most of what an avatar's owner notices. A report that says nothing about
            // them reads as though nothing was lost.
            List<string> lines = new List<string>
            {
                "--- !u!114 &701",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 1}",
                "  ViewPosition: {x: 0, y: 1.2, z: 0.08}",
                "  lipSync: 3",
                "  VisemeSkinnedMesh: {fileID: 55}",
                "  customExpressions: 1",
                "  expressionsMenu: {fileID: 11400000, guid: 680d0017fa4a211428aef69c8d5020c2, type: 2}",
                "  expressionParameters: {fileID: 11400000, guid: 01928f26b6522bf4f99368e79ce6cd5a, type: 2}",
                "  customizeAnimationLayers: 1",
                "  baseAnimationLayers:",
                "  - isEnabled: 0",
                "    type: 0",
                "    animatorController: {fileID: 9100000, guid: 205e26ae23607e84d8c162452df418b6, type: 2}",
                "    mask: {fileID: 0}",
                "    isDefault: 0",
                "  - isEnabled: 0",
                "    type: 2",
                "    animatorController: {fileID: 0}",
                "    mask: {fileID: 0}",
                "    isDefault: 1",
                "  - isEnabled: 0",
                "    type: 4",
                "    animatorController: {fileID: 9100000, guid: 214bad824fc67d94a8b40f11ecfcc3c0, type: 2}",
                "    mask: {fileID: 0}",
                "    isDefault: 0",
                "  specialAnimationLayers:",
                "  - isEnabled: 0",
                "    type: 6",
                "    animatorController: {fileID: 0}",
                "    mask: {fileID: 0}",
                "    isDefault: 1",
            };

            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            VrcAvatarDescriptorData data = VrcAvatarDescriptorReader.Read(documents[0]);

            Assert.That(data.HasExpressionsMenu, Is.True);
            Assert.That(data.HasExpressionParameters, Is.True);

            // Only layers with a controller that is not VRChat's stock one count: those are the
            // ones somebody authored and will have to rebuild.
            Assert.That(data.AnimationLayers.Count, Is.EqualTo(2));
            Assert.That(data.AnimationLayers[0].Layer, Is.EqualTo(VrcAnimationLayer.Base));
            Assert.That(data.AnimationLayers[1].Layer, Is.EqualTo(VrcAnimationLayer.Action),
                "Type 4 is Action, not FX: the SDK enum has Deprecated0 at 1.");

            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(data);
            Assert.That(plan.Diagnostics.HasCode("descriptor.expressionsMenu"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("descriptor.expressionParameters"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("descriptor.animationLayers"), Is.True);
        }

        [Test]
        public void TheAnimationLayerOrderingMatchesTheSdk()
        {
            // The ordering usually quoted omits Deprecated0 and shifts everything after it. With
            // that version a real avatar's layers read as Base, Action, FX, Sitting when they are
            // Base, Gesture, Action, FX: wrong, and plausible enough to go unnoticed. Values are
            // from VRCSDK3A.dll.
            Assert.That((int)VrcAnimationLayer.Base, Is.EqualTo(0));
            Assert.That((int)VrcAnimationLayer.Deprecated0, Is.EqualTo(1));
            Assert.That((int)VrcAnimationLayer.Additive, Is.EqualTo(2));
            Assert.That((int)VrcAnimationLayer.Gesture, Is.EqualTo(3));
            Assert.That((int)VrcAnimationLayer.Action, Is.EqualTo(4));
            Assert.That((int)VrcAnimationLayer.FX, Is.EqualTo(5));
            Assert.That((int)VrcAnimationLayer.Sitting, Is.EqualTo(6));
            Assert.That((int)VrcAnimationLayer.TPose, Is.EqualTo(7));
            Assert.That((int)VrcAnimationLayer.IKPose, Is.EqualTo(8));
        }

        [Test]
        public void AnAvatarWithNoExpressionSystemsReportsNothingAboutThem()
        {
            BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(ReadDescriptor());

            Assert.That(plan.Diagnostics.HasCode("descriptor.expressionsMenu"), Is.False);
            Assert.That(plan.Diagnostics.HasCode("descriptor.animationLayers"), Is.False);
        }

        [Test]
        public void ReadsTheDescriptorInARealAvatar()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            int found = 0;
            foreach (UnityYamlDocument document in UnityYamlScanner.ScanFile(FixturePath))
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId)
                    || KnownScriptIdentities.Resolve(guid, scriptFileId)
                        != SourceComponentKind.VrcAvatarDescriptor)
                {
                    continue;
                }

                found++;
                VrcAvatarDescriptorData data = VrcAvatarDescriptorReader.Read(document);
                BasisAvatarPlan plan = VrcAvatarDescriptorToBasisMapper.Map(data);

                TestContext.WriteLine($"view position: {data.ViewPosition}");
                TestContext.WriteLine($"lip sync: {data.LipSync}");
                TestContext.WriteLine($"visemes listed: {data.VisemeBlendShapes.Count}");
                TestContext.WriteLine($"eyelid type: {data.EyelidType}");
                TestContext.WriteLine($"eyelid indices: "
                    + string.Join(", ", data.EyelidsBlendshapes));
                TestContext.WriteLine($"eye position: {plan.EyePosition}");
                TestContext.WriteLine($"blink indices: {string.Join(", ", plan.BlinkBlendShapeIndices)}");

                Assert.That(data.OwnerGameObjectFileId, Is.Not.Zero);
                Assert.That(plan.EyePosition.x, Is.GreaterThan(0f),
                    "An avatar's eyes should sit above its root.");
            }

            Assert.That(found, Is.EqualTo(1), "Expected exactly one avatar descriptor.");
        }
    }
}
