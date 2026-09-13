using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// VRM spring bones, in both formats, against hand-written fixtures.
    /// <para>
    /// The fixtures are prefabs, so they are read as text whether or not UniVRM is installed:
    /// with it the components are real types, without it they are missing scripts, and the file
    /// says the same either way. An imported `.vrm` is the other case, and has its own tests.
    /// </para>
    /// </summary>
    public class VrmSpringBoneTests
    {
        private const string Folder =
            "Packages/com.yuna0x0.basis.convert/Tests/Editor/Fixtures/SampleVrmAvatar";

        private const string Vrm10Path = Folder + "/SampleVrm10Avatar.prefab";
        private const string Vrm0Path = Folder + "/SampleVrm0Avatar.prefab";

        private static AvatarConversionPlan Plan(string path)
        {
            AvatarConversionPlan plan = AvatarConversionPlanner.Plan(path);

            foreach (ConversionDiagnostic diagnostic in plan.AllDiagnostics())
            {
                TestContext.WriteLine($"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
            }

            return plan;
        }

        [Test]
        public void TheFixturesLoad()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Vrm10Path), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Vrm0Path), Is.Not.Null);
        }

        [Test]
        public void AVrm10SpringBecomesOneRigOnItsFirstJoint()
        {
            AvatarConversionPlan plan = Plan(Vrm10Path);

            Assert.That(plan.VrmChainsFound, Is.EqualTo(1));
            Assert.That(plan.Rigs.Count, Is.EqualTo(1));

            PlannedJiggleRig rig = plan.Rigs[0];
            Assert.That(rig.SourceRootBone.name, Is.EqualTo("HairRoot"),
                "The chain hangs from the bone its first joint sits on.");
        }

        [Test]
        public void JointsThatDifferAlongTheChainBecomeACurve()
        {
            // Stiffness is 1 at the root and 0.5 in the middle. Jiggle evaluates its parameters
            // over normalized distance from the root, which is the same axis VRM's joints sit
            // on, so the chain keeps its shape rather than being averaged.
            AvatarConversionPlan plan = Plan(Vrm10Path);
            JiggleCurvedFloatPlan? stiffness = plan.Rigs[0].Plan.Parameters.Stiffness;

            Assert.That(stiffness.HasValue, Is.True);
            Assert.That(stiffness.Value.Value, Is.EqualTo(1f).Within(0.001f));
            Assert.That(stiffness.Value.CurveEnabled, Is.True);
            Assert.That(stiffness.Value.Curve.Evaluate(1f), Is.EqualTo(0.5f).Within(0.001f),
                "UniVRM never reads the tail joint's parameters, so the joint before it ends "
                + "the curve whatever the tail carries.");
        }

        private static VrmSpringChainData Chain(params VrmSpringJointData[] joints)
        {
            VrmSpringChainData chain = new VrmSpringChainData { IsVrm10 = true };
            chain.Joints.AddRange(joints);
            return chain;
        }

        [Test]
        public void TheTailJointsParametersAreIgnored()
        {
            // FastSpringBoneBuffer takes joints.Length - 1: the tail only marks where the chain
            // ends, though the importer writes default parameters onto it.
            VrmSpringChainData chain = Chain(
                new VrmSpringJointData { Radius = 0.02f, DragForce = 0.4f },
                new VrmSpringJointData { Radius = 0.02f, DragForce = 0.4f },
                new VrmSpringJointData { Radius = 0f, DragForce = 0.5f });

            JiggleRigPlan plan = Mapping.VrmSpringBoneToJiggleMapper.Map(chain);

            Assert.That(plan.Parameters.CollisionRadius.Value.CurveEnabled, Is.False,
                "Only the two real joints count, and they agree.");
            Assert.That(plan.Parameters.Drag.Value.Value, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void PresetLeaksAreClosedAndGravityIsReported()
        {
            VrmSpringChainData chain = Chain(
                new VrmSpringJointData { GravityPower = 0.2f, GravityDir = Vector3.up },
                new VrmSpringJointData());

            JiggleRigPlan plan = Mapping.VrmSpringBoneToJiggleMapper.Map(chain);

            Assert.That(plan.Parameters.AngleLimitToggle, Is.False);
            Assert.That(plan.Parameters.Stretch.Value.Value, Is.Zero);
            Assert.That(plan.Parameters.RootStretch, Is.Zero);
            Assert.That(plan.Parameters.IgnoreRootMotion, Is.Zero);
            Assert.That(plan.Parameters.Gravity.Value.Value, Is.EqualTo(-0.2f).Within(1e-6f),
                "Upward gravity stays upward; jiggle's multiplier may be negative.");
            Assert.That(plan.Diagnostics.HasCode("vrm.gravity"), Is.True);
        }

        [Test]
        public void AConeLimitBecomesAnAngleLimit()
        {
            VrmSpringChainData chain = Chain(
                new VrmSpringJointData { AngleLimitType = 1, Pitch = Mathf.PI / 4f },
                new VrmSpringJointData());

            JiggleRigPlan plan = Mapping.VrmSpringBoneToJiggleMapper.Map(chain);

            Assert.That(plan.Parameters.AngleLimitToggle, Is.True);
            Assert.That(plan.Parameters.AngleLimit.Value.Value, Is.EqualTo(0.5f).Within(1e-5f),
                "45 degrees is half of jiggle's 90 degree range.");
            Assert.That(plan.Diagnostics.HasCode("vrm.angleLimit.cone"), Is.True);

            VrmSpringChainData hinge = Chain(
                new VrmSpringJointData { AngleLimitType = 2 },
                new VrmSpringJointData());
            Assert.That(Mapping.VrmSpringBoneToJiggleMapper.Map(hinge).Diagnostics
                .HasCode("vrm.angleLimit.dropped"), Is.True);
        }

        [Test]
        public void ACentreTransformBecomesFullIgnoreRootMotion()
        {
            VrmSpringChainData chain = Chain(new VrmSpringJointData(), new VrmSpringJointData());
            chain.CenterFileId = 77L;

            JiggleRigPlan plan = Mapping.VrmSpringBoneToJiggleMapper.Map(chain);

            Assert.That(plan.Parameters.IgnoreRootMotion, Is.EqualTo(1f));
            Assert.That(plan.Diagnostics.HasCode("vrm.center"), Is.True);
        }

        [Test]
        public void AVrm0ClipIsMatchedByItsPresetNotItsName()
        {
            // UniVRM identifies a 0.x clip by its preset; BlendShapeName is whatever the author
            // typed, often Japanese.
            VrmExpressionData vowel = new VrmExpressionData
            {
                Name = "あ", Role = VrmExpressionRole.Viseme, PresetName = "A",
            };
            Assert.That(Mapping.VrmExpressionToVisemeMapper.TryGetSlot(vowel, out int slot), Is.True);
            Assert.That(slot, Is.EqualTo(10));

            VrmExpressionData blink = new VrmExpressionData
            {
                Name = "まばたき", Role = VrmExpressionRole.Blink, PresetName = "Blink",
            };
            Assert.That(Mapping.VrmExpressionToVisemeMapper.IsBlink(blink), Is.True);
        }

        [Test]
        public void ABoneTheSpringNeverNamedIsExcluded()
        {
            // HairAccessory hangs off the chain root but is not one of the spring's joints. VRM
            // leaves it still; a jiggle rig would swing it unless it is excluded.
            AvatarConversionPlan plan = Plan(Vrm10Path);

            List<string> excluded = new List<string>();
            foreach (Transform transform in plan.Rigs[0].SourceExcludedTransforms)
            {
                excluded.Add(transform.name);
            }

            Assert.That(excluded, Does.Contain("HairAccessory"));
            Assert.That(excluded, Does.Not.Contain("HairMiddle"));
            Assert.That(plan.AllDiagnostics().HasCode("vrm.branchesExcluded"), Is.True);
        }

        [Test]
        public void AVrm10ColliderGroupIsAttachedToTheChain()
        {
            AvatarConversionPlan plan = Plan(Vrm10Path);

            Assert.That(plan.Rigs[0].Colliders.Count, Is.EqualTo(1));

            JiggleColliderPlan collider = plan.Rigs[0].Colliders[0].Plan;
            Assert.That(collider.Shape, Is.EqualTo(JiggleColliderShape.Sphere));
            Assert.That(collider.Radius, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(plan.Rigs[0].Colliders[0].SourceTransform.name, Is.EqualTo("Head"));
        }

        [Test]
        public void AVrm0SpringBoneBecomesOneRigPerRootBone()
        {
            // One VRM 0.x component carries a group of chains and one set of parameters for all
            // of them, the same shape one Dynamic Bone with several roots has.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            Assert.That(plan.VrmChainsFound, Is.EqualTo(2));
            Assert.That(plan.Rigs.Count, Is.EqualTo(2));

            List<string> roots = new List<string>();
            foreach (PlannedJiggleRig rig in plan.Rigs)
            {
                roots.Add(rig.SourceRootBone.name);
            }

            Assert.That(roots, Is.EquivalentTo(new[] {"TwintailLeft", "TwintailRight"}));
        }

        [Test]
        public void AVrm0ChainKeepsItsParametersAndColliders()
        {
            AvatarConversionPlan plan = Plan(Vrm0Path);
            PlannedJiggleRig rig = plan.Rigs[0];

            Assert.That(rig.Plan.Parameters.Drag.Value.Value, Is.EqualTo(0.6f).Within(0.001f),
                "Drag force and jiggle drag are the same 0 to 1 scale.");
            Assert.That(rig.Plan.Parameters.CollisionRadius.Value.Value,
                Is.EqualTo(0.03f).Within(0.001f));
            Assert.That(rig.Plan.Parameters.Gravity.Value.Value, Is.EqualTo(0.3f).Within(0.001f));

            // The group holds its spheres inline rather than referencing components.
            Assert.That(rig.Colliders.Count, Is.EqualTo(2));
            Assert.That(rig.Colliders[0].Plan.Shape, Is.EqualTo(JiggleColliderShape.Sphere));
            Assert.That(rig.Colliders[0].Plan.Radius, Is.EqualTo(0.11f).Within(0.0001f));
        }

        [Test]
        public void AVrmChainsParametersAreNotSilentlyExact()
        {
            // VRM measures stiffness as a force with no upper bound and jiggle runs 0 to 1, so
            // the report has to say that one is a fit rather than a conversion.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            Assert.That(plan.AllDiagnostics().HasCode("vrm.stiffness"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.drag"), Is.True);
        }

        [Test]
        public void AVrm10AvatarsExpressionsBecomeOneSelector()
        {
            // An avatar wears one expression at a time, so the expressions are choices on one
            // control rather than a toggle each. Neutral is the first choice.
            AvatarConversionPlan plan = Plan(Vrm10Path);

            List<PlannedVixxyControl> fromVrm = plan.VixxyControls.FindAll(
                control => control.Plan.MenuName == VrmExpressionToVixxyMapper.MenuName);
            Assert.That(fromVrm.Count, Is.EqualTo(1));

            PlannedVixxyControl selector = fromVrm[0];
            Assert.That(selector.Plan.ChoiceNames,
                Is.EqualTo(new[] { "Neutral", "Happy", "Wink" }));
            Assert.That(selector.Plan.ChoiceValues, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(selector.Plan.DefaultValue, Is.EqualTo(0f));
            Assert.That(selector.Plan.IsSlider, Is.False);
            Assert.That(selector.Plan.Subjects.Count, Is.EqualTo(1));
            Assert.That(selector.SourceRenderers[0].name, Is.EqualTo("Face"));

            VixxyBlendShapePlan smile = selector.Plan.Subjects[0].BlendShapes
                .Find(shape => shape.ShapeName == "Smile");
            Assert.That(smile, Is.Not.Null,
                "VRM names a shape by its index in the mesh, so the mesh is what names it.");
            Assert.That(smile.Choices, Is.EqualTo(new[] { 0f, 100f, 0f }).Within(0.01f),
                "A VRM 1.0 weight of 1 is Unity's 100; every other choice sets the shape to 0.");
            Assert.That(smile.Set, Is.All.True, "Every choice writes every shape.");
        }

        [Test]
        public void ACustomExpressionIsAChoiceAndItsMaterialChangesReported()
        {
            AvatarConversionPlan plan = Plan(Vrm10Path);

            PlannedVixxyControl selector = plan.VixxyControls.Find(
                control => control.Plan.MenuName == VrmExpressionToVixxyMapper.MenuName);
            int wink = selector.Plan.ChoiceNames.IndexOf("Wink");

            Assert.That(wink, Is.GreaterThan(0), "Expressions the author added are choices too.");
            VixxyBlendShapePlan shape = selector.Plan.Subjects[0].BlendShapes
                .Find(candidate => candidate.Choices[wink] > 0f);
            Assert.That(shape, Is.Not.Null);
            Assert.That(shape.Choices[wink], Is.EqualTo(75f).Within(0.01f));

            // Wink also turns the Face material's emission red. Vixxy acts on the renderer that
            // uses that material, so the property lands on the Face subject.
            VixxyMaterialPropertyPlan emission = selector.Plan.Subjects[0].MaterialProperties
                .Find(property => property.PropertyName == "_EmissionColor");
            Assert.That(emission, Is.Not.Null,
                "a VRM 1.0 emissionColor bind is MToon's _EmissionColor");
            Assert.That(emission.Kind, Is.EqualTo(VixxyMaterialPropertyKind.Colour));
            Assert.That(emission.Choices[wink], Is.EqualTo(new Vector4(1f, 0f, 0f, 1f)));
            Assert.That(emission.Set[wink], Is.All.True);
            Assert.That(emission.Set[0], Is.All.False,
                "Other choices keep the material as authored, filled in from the material.");
            Assert.That(emission.Choices[0], Is.EqualTo(new Vector4(0f, 0f, 0f, 1f)),
                "The fixture material's emission is black.");
            Assert.That(plan.AllDiagnostics().HasCode("vrm.expression.materialValues"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.expression.materials"), Is.False);
        }

        [Test]
        public void AMaterialOnARendererWithOtherMaterialsIsLeftAlone()
        {
            // Vixxy sets a property for the whole renderer. Shifting one material's texture
            // would shift every material on that renderer, so the bind is reported instead.
            VrmExpressionData happy = new VrmExpressionData
            {
                Name = "Happy",
                Role = VrmExpressionRole.Emotion,
                Bindings =
                {
                    new VrmMorphBinding { Path = "Face", ShapeName = "Smile", Weight = 100f },
                },
                MaterialUvBindings =
                {
                    new VrmMaterialUvBinding
                    {
                        MaterialName = "Eyes", Offset = new Vector2(0.25f, 0f),
                    },
                },
            };
            Dictionary<string, List<VrmMaterialHost>> hosts =
                new Dictionary<string, List<VrmMaterialHost>>
            {
                ["Eyes"] = new List<VrmMaterialHost>
                {
                    new VrmMaterialHost
                    {
                        Path = "Body", RendererTypeName = "R", OtherMaterials = 11,
                    },
                },
            };

            VixxyControlPlan plan = VrmExpressionToVixxyMapper.MapSelector(new[] { happy }, hosts);

            Assert.That(plan.Subjects.Count, Is.EqualTo(1), "only the blendshape subject");
            Assert.That(plan.Subjects[0].MaterialProperties, Is.Empty);
            Assert.That(plan.Diagnostics.HasCode("vrm.expression.materialShared"), Is.True);
            Assert.That(plan.Diagnostics.HasCode("vrm.expression.materialValues"), Is.False);
        }

        [Test]
        public void ConvertingRemovesUniVrmsRuntimeDriver()
        {
            // Vrm10Instance.LateUpdate writes every expression blendshape each frame, zeros
            // included, and runs its own spring bones. Left on the avatar it undoes the
            // conversion every frame, which showed as "nothing changes" on a real avatar.
            AvatarConversionPlan plan = Plan(Vrm10Path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Vrm10Path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Assert.That(HasComponentNamed(instance, "Vrm10Instance"), Is.True,
                    "the fixture carries a live Vrm10Instance while UniVRM is installed");

                ConversionResult result = AvatarConverter.Apply(plan, instance);

                Assert.That(result.VrmRuntimeRemoved, Is.EqualTo(1));
                Assert.That(HasComponentNamed(instance, "Vrm10Instance"), Is.False);
                Assert.That(HasComponentNamed(instance, "VRM10SpringBoneJoint"), Is.True,
                    "data components stay for a later conversion");
                Assert.That(result.Diagnostics.HasCode("vrm.runtimeRemoved"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static bool HasComponentNamed(GameObject root, string typeName)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void ExpressionsBasisDrivesItselfAreLeftToIt()
        {
            // The lip sync shapes, blinking and looking around are driven by Basis. A choice
            // the wearer has to pick would fight it.
            AvatarConversionPlan plan = Plan(Vrm10Path);

            PlannedVixxyControl selector = plan.VixxyControls.Find(
                control => control.Plan.MenuName == VrmExpressionToVixxyMapper.MenuName);
            Assert.That(selector.Plan.ChoiceNames, Does.Not.Contain("Aa"));
            Assert.That(selector.Plan.ChoiceNames, Does.Not.Contain("Blink"));
            Assert.That(plan.AllDiagnostics().HasCode("vrm.expressionsDriven"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.expressionsRebuilt"), Is.True);
        }

        [Test]
        public void AVrm0ClipIsAChoiceWithItsOwnWeightScale()
        {
            // VRM 0.x weights are already on Unity's 0 to 100 scale: UniVRM passes them straight
            // to SetBlendShapeWeight. Only 1.0 needs scaling.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            PlannedVixxyControl selector = plan.VixxyControls.Find(
                control => control.Plan.MenuName == VrmExpressionToVixxyMapper.MenuName);
            Assert.That(selector, Is.Not.Null);

            int joy = selector.Plan.ChoiceNames.IndexOf("Joy");
            Assert.That(joy, Is.GreaterThan(0));
            VixxyBlendShapePlan smile = selector.Plan.Subjects[0].BlendShapes
                .Find(shape => shape.ShapeName == "Smile");
            Assert.That(smile.Choices[joy], Is.EqualTo(100f).Within(0.01f));

            Assert.That(selector.Plan.ChoiceNames, Does.Not.Contain("A"),
                "A is a viseme, whatever the author called the clip.");

            // 0.x names the shader property itself, and its colour is a plain Vector4.
            VixxyMaterialPropertyPlan colour = selector.Plan.Subjects[0].MaterialProperties
                .Find(property => property.PropertyName == "_Color");
            Assert.That(colour, Is.Not.Null);
            Assert.That(colour.Choices[joy], Is.EqualTo(new Vector4(1f, 0.5f, 0.5f, 1f)));
            Assert.That(colour.Choices[0], Is.EqualTo(new Vector4(1f, 1f, 1f, 1f)),
                "Neutral keeps the material's authored white.");
        }

        [Test]
        public void AVrm0EyeOffsetBecomesTheAvatarsEyePosition()
        {
            // VRM measures the camera point from the head bone. Basis stores the height and
            // depth of the same point relative to the avatar root, which is what VRChat's view
            // position holds.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            Assert.That(plan.VrmSettings, Is.Not.Null);
            Assert.That(plan.VrmSettings.HasEyeOffset, Is.True);
            Assert.That(plan.VrmSettings.EyeOffsetFromHead,
                Is.EqualTo(new Vector3(0f, 0.06f, 0.08f)));

            // The fixture's head sits 1.45 above the root, so the eyes land 0.06 above that and
            // 0.08 forward, and that is what goes on the Basis Avatar component.
            Assert.That(plan.AllDiagnostics().HasCode("vrm.eyePosition"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.eyePosition.noRig"), Is.False);

            Assert.That(plan.Descriptor, Is.Not.Null, "descriptor");
            Assert.That(plan.Descriptor.Plan.EyePosition.x, Is.EqualTo(1.51f).Within(0.001f));
            Assert.That(plan.Descriptor.Plan.EyePosition.y, Is.EqualTo(0.08f).Within(0.001f));
        }

        [Test]
        public void AnEyeOffsetIsMeasuredFromTheHeadInTheRootsSpace()
        {
            // The arithmetic on its own: a head 1.4 up, eyes 0.06 above it and 0.08 forward.
            GameObject root = new GameObject("Root");
            GameObject head = new GameObject("Head");

            try
            {
                head.transform.SetParent(root.transform);
                head.transform.localPosition = new Vector3(0f, 1.4f, 0f);

                Vector2 eyes = AvatarConversionPlanner.EyePositionFrom(
                    root.transform, head.transform, new Vector3(0f, 0.06f, 0.08f));

                Assert.That(eyes.x, Is.EqualTo(1.46f).Within(0.001f), "Height above the root.");
                Assert.That(eyes.y, Is.EqualTo(0.08f).Within(0.001f), "Depth in front of it.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FirstPersonRendererFlagsAreReported()
        {
            // Basis hides the head bone and everything under it, which covers the usual case,
            // so the flags are reported rather than turned into head chop targets.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            Assert.That(plan.VrmSettings.ThirdPersonOnlyRenderers, Is.EqualTo(1));
            Assert.That(plan.AllDiagnostics().HasCode("vrm.firstPerson"), Is.True);
        }

        [Test]
        public void TheAvatarsLicenceIsReadAndReported()
        {
            // Every VRM states who may wear it and what may be done to it. Converting changes
            // the avatar, so the licence is shown before anything is written.
            AvatarConversionPlan plan = Plan(Vrm0Path);

            Assert.That(plan.VrmMeta, Is.Not.Null);
            Assert.That(plan.VrmMeta.Title, Is.EqualTo("Sample Twintails"));
            Assert.That(plan.VrmMeta.Authors, Is.EqualTo(new[] {"A Sample Author"}));
            Assert.That(plan.VrmMeta.AvatarPermission,
                Is.EqualTo(VrmAvatarPermission.Everyone));
            Assert.That(plan.VrmMeta.LicenseName, Is.EqualTo("CC_BY"));
            Assert.That(plan.VrmMeta.ForbidsModification, Is.False);

            // Every permission the format states, in its own words.
            Assert.That(plan.VrmMeta.ViolentUsage, Is.False);
            Assert.That(plan.VrmMeta.SexualUsage, Is.False);
            Assert.That(plan.VrmMeta.CommercialUsage, Is.EqualTo("allowed"));
            Assert.That(plan.VrmMeta.PoliticalOrReligiousUsage, Is.Null,
                "VRM 0.x has no field for it, so it is left unstated rather than guessed.");

            List<string> permissions = new List<string>(plan.VrmMeta.Permissions());
            Assert.That(permissions, Does.Contain("Wearing: anyone"));
            Assert.That(permissions, Does.Contain("Violence: not allowed"));
            Assert.That(permissions, Does.Contain("Commercial use: allowed"));
            Assert.That(permissions, Does.Contain("Licence: CC_BY"));

            Assert.That(plan.AllDiagnostics().HasCode("vrm.licence"), Is.True);
            Assert.That(plan.AllDiagnostics().HasCode("vrm.licence.restricted"), Is.False,
                "CC BY allows changing it, and anyone may wear this one.");
        }

        [Test]
        public void ALicenceThatForbidsChangesIsAWarning()
        {
            // Nothing blocks a conversion: the licence is the wearer's to judge. What this has
            // to do is make sure they saw it.
            VrmMetaData meta = new VrmMetaData
            {
                Title = "Someone Else's Avatar",
                LicenseName = "CC_BY_ND",
                AvatarPermission = VrmAvatarPermission.OnlyAuthor,
            };

            Assert.That(meta.ForbidsModification, Is.True);
            Assert.That(new List<string>(meta.Permissions()),
                Does.Contain("Wearing: the author only"));
        }

        [Test]
        public void VrmConstraintsBecomeBasisConstraints()
        {
            // A VRM constraint drives the object it sits on and follows one source, so there is
            // no target to relocate and no source list to flatten.
            AvatarConversionPlan plan = Plan(Vrm10Path);

            Assert.That(plan.VrmConstraintsFound, Is.EqualTo(2));
            Assert.That(plan.Constraints.Count, Is.EqualTo(2));

            PlannedConstraint rotation = plan.Constraints.Find(
                c => c.Plan.Kind == BasisConstraintKind.Rotation);
            Assert.That(rotation, Is.Not.Null);
            Assert.That(rotation.SourceHost.name, Is.EqualTo("Head"));
            Assert.That(rotation.SourceTransforms[0].name, Is.EqualTo("HairRoot"));
            Assert.That(rotation.Plan.Weight, Is.EqualTo(0.5f).Within(0.001f));

            PlannedConstraint aim = plan.Constraints.Find(
                c => c.Plan.Kind == BasisConstraintKind.Aim);
            Assert.That(aim, Is.Not.Null);
            Assert.That(aim.Plan.AimVector, Is.EqualTo(Vector3.forward),
                "AimAxis 4 is positive Z.");

            Assert.That(plan.AllDiagnostics().HasCode("vrm.constraint.rotation"), Is.True,
                "VRM copies a delta from rest, Basis follows the rotation itself.");
            Assert.That(plan.AllDiagnostics().HasCode("vrm.constraint.aim"), Is.True,
                "VRM states no up direction and Basis needs one.");
        }

        [Test]
        public void ARollConstraintBecomesASingleAxisRotation()
        {
            // Nothing in Basis copies rotation about one axis, so this is the closest shape
            // with the difference reported.
            VrmConstraintData roll = new VrmConstraintData
            {
                Kind = VrmConstraintKind.Roll,
                RollAxis = 1,
                SourceTransformFileId = 42L,
                Weight = 1f,
            };

            BasisConstraintPlan plan = Mapping.VrmConstraintToBasisMapper.Map(roll);

            Assert.That(plan.Kind, Is.EqualTo(BasisConstraintKind.Rotation));
            Assert.That(plan.RotationAxis, Is.EqualTo(ConstraintAxes.Y));
            Assert.That(plan.Diagnostics.HasCode("vrm.constraint.roll"), Is.True);
        }

        [Test]
        public void NoVrmComponentIsReportedAsAnUnknownScript()
        {
            foreach (string path in new[] {Vrm10Path, Vrm0Path})
            {
                AvatarConversionPlan plan = AvatarConversionPlanner.Plan(path);
                Assert.That(plan.AllDiagnostics().HasCode("source.unknownScript"), Is.False,
                    $"{path} carries only components this recognises.");
            }
        }
    }
}
