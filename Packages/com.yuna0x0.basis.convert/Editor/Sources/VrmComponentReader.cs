using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using Object = UnityEngine.Object;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Reads VRM 1.0's spring bones and node constraints from live components rather than from a
    /// prefab's text.
    /// <para>
    /// A `.vrm` is binary glTF behind a ScriptedImporter, so there is no YAML to scan. UniVRM has
    /// to be installed for the file to import at all, which means its components are real types
    /// on the imported hierarchy and their serialized fields can be read directly. The field
    /// names are the ones the text would have carried, so this produces the same plain data as
    /// <see cref="VrmDocumentReader"/> and everything after it is shared.
    /// </para>
    /// <para>
    /// Identifiers are each object's own local file id inside the imported asset, which is what
    /// <see cref="PrefabObjectResolver"/> indexes, so the two paths address objects the same way.
    /// </para>
    /// <para>
    /// VRM 0.x is not read here, because nothing can present it this way: UniVRM 0.x has no
    /// ScriptedImporter and writes a real prefab beside the `.vrm`, which is text, and UniVRM 1.0
    /// migrates a 0.x file to 1.0 components as it imports. Its components stay in the identity
    /// table, so one on a binary source is still named rather than reported as an unknown script.
    /// See `agent/decisions/0015`.
    /// </para>
    /// </summary>
    public static class VrmComponentReader
    {
        /// <summary>What one hierarchy's VRM components amount to, ready to be assembled.</summary>
        public sealed class Result
        {
            public readonly List<VrmSpringChainData> Chains = new List<VrmSpringChainData>();

            public readonly Dictionary<long, VrmSpringJointData> Joints =
                new Dictionary<long, VrmSpringJointData>();

            public readonly Dictionary<long, VrmColliderData> Colliders =
                new Dictionary<long, VrmColliderData>();

            public readonly Dictionary<long, VrmColliderGroupData> Groups =
                new Dictionary<long, VrmColliderGroupData>();

            public readonly List<VrmConstraintData> Constraints = new List<VrmConstraintData>();

            /// <summary>The avatar's own component, which names its springs.</summary>
            public Component Instance;

            public int ComponentsRead;
            public bool Any => ComponentsRead > 0;
        }

        public static Result Read(GameObject root)
        {
            Result result = new Result();
            if (root == null)
            {
                return result;
            }

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                SourceComponentKind kind = KindOf(component);
                if (kind == SourceComponentKind.Unknown)
                {
                    continue;
                }

                result.ComponentsRead++;
                long id = IdOf(component);
                SerializedObject serialized = new SerializedObject(component);

                switch (kind)
                {
                    case SourceComponentKind.Vrm10SpringBoneJoint:
                        result.Joints[id] = ReadJoint(component, serialized);
                        break;

                    case SourceComponentKind.Vrm10SpringBoneCollider:
                        result.Colliders[id] = ReadCollider(component, serialized, id);
                        break;

                    case SourceComponentKind.Vrm10SpringBoneColliderGroup:
                        result.Groups[id] = ReadColliderGroup10(component, serialized, id);
                        break;

                    case SourceComponentKind.Vrm10Instance:
                        result.Instance = component;
                        result.Chains.AddRange(ReadInstanceSprings(serialized, id));
                        break;

                    default:
                        if (KnownScriptIdentities.IsVrmConstraint(kind))
                        {
                            result.Constraints.Add(ReadConstraint(component, serialized, kind, id));
                        }
                        else
                        {
                            // Recognised, but not ours to read here: counted so a hierarchy that
                            // holds only these is not called empty.
                        }

                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Which component this is, by the guid of the script behind it. The same table the text
        /// path uses, so both recognise exactly the same set.
        /// </summary>
        public static SourceComponentKind KindOf(Component component)
        {
            if (!(component is MonoBehaviour behaviour))
            {
                return SourceComponentKind.Unknown;
            }

            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    script, out string guid, out long fileId))
            {
                return SourceComponentKind.Unknown;
            }

            return KnownScriptIdentities.Resolve(guid, fileId);
        }

        /// <summary>
        /// The object's own identifier inside the asset it belongs to, which is the number the
        /// file would have referred to it by.
        /// </summary>
        public static long IdOf(Object live)
        {
            return live != null
                   && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(live, out string _, out long id)
                ? id
                : 0L;
        }

        private static VrmSpringJointData ReadJoint(Component component, SerializedObject serialized)
        {
            return new VrmSpringJointData
            {
                OwnerGameObjectFileId = IdOf(component.gameObject),
                Stiffness = Float(serialized, "m_stiffnessForce", 1f),
                GravityPower = Float(serialized, "m_gravityPower", 0f),
                GravityDir = Vector(serialized, "m_gravityDir", Vector3.down),
                DragForce = Float(serialized, "m_dragForce", 0.4f),
                Radius = Float(serialized, "m_jointRadius", 0.02f),
                AngleLimitType = Int(serialized, "m_anglelimitType", 0),
                Pitch = Float(serialized, "m_pitch", Mathf.PI),
            };
        }

        private static VrmColliderData ReadCollider(
            Component component, SerializedObject serialized, long id)
        {
            return new VrmColliderData
            {
                DocumentFileId = id,
                OwnerGameObjectFileId = IdOf(component.gameObject),
                Type = (VrmColliderType)Int(serialized, "ColliderType", 0),
                Offset = Vector(serialized, "Offset", Vector3.zero),
                Radius = Float(serialized, "Radius", 0f),
                Tail = Vector(serialized, "Tail", Vector3.zero),
                Normal = Vector(serialized, "Normal", Vector3.up),
            };
        }

        private static VrmColliderGroupData ReadColliderGroup10(
            Component component, SerializedObject serialized, long id)
        {
            VrmColliderGroupData group = new VrmColliderGroupData
            {
                DocumentFileId = id,
                OwnerGameObjectFileId = IdOf(component.gameObject),
                Name = Text(serialized, "Name"),
            };

            group.ColliderFileIds.AddRange(References(serialized, "Colliders"));
            return group;
        }

        /// <summary>
        /// The chains a VRM 1.0 avatar declares. The instance names each spring's joints in
        /// order; the joints themselves are components elsewhere in the hierarchy.
        /// </summary>
        private static List<VrmSpringChainData> ReadInstanceSprings(
            SerializedObject serialized, long id)
        {
            List<VrmSpringChainData> chains = new List<VrmSpringChainData>();
            SerializedProperty springs = serialized.FindProperty("SpringBone.Springs");

            if (springs == null || !springs.isArray)
            {
                return chains;
            }

            for (int i = 0; i < springs.arraySize; i++)
            {
                SerializedProperty spring = springs.GetArrayElementAtIndex(i);
                SerializedProperty name = spring.FindPropertyRelative("Name");
                SerializedProperty center = spring.FindPropertyRelative("Center");

                VrmSpringChainData chain = new VrmSpringChainData
                {
                    DocumentFileId = id,
                    IsVrm10 = true,
                    Name = name == null ? string.Empty : name.stringValue,
                    CenterFileId = center == null ? 0L : IdOf(center.objectReferenceValue),
                };

                chain.JointComponentFileIds.AddRange(
                    References(spring.FindPropertyRelative("Joints")));
                chain.ColliderGroupFileIds.AddRange(
                    References(spring.FindPropertyRelative("ColliderGroups")));

                chains.Add(chain);
            }

            return chains;
        }

        private static VrmConstraintData ReadConstraint(
            Component component, SerializedObject serialized, SourceComponentKind kind, long id)
        {
            VrmConstraintKind constraintKind = kind switch
            {
                SourceComponentKind.Vrm10AimConstraint => VrmConstraintKind.Aim,
                SourceComponentKind.Vrm10RollConstraint => VrmConstraintKind.Roll,
                _ => VrmConstraintKind.Rotation,
            };

            VrmConstraintData constraint = new VrmConstraintData
            {
                DocumentFileId = id,
                Kind = constraintKind,
                OwnerGameObjectFileId = IdOf(component.gameObject),
                SourceTransformFileId = Reference(serialized, "Source"),
                Weight = Float(serialized, "Weight", 1f),
            };

            if (constraintKind == VrmConstraintKind.Aim)
            {
                constraint.AimAxis = (VrmAimAxis)Int(serialized, "AimAxis", 0);
            }

            if (constraintKind == VrmConstraintKind.Roll)
            {
                constraint.RollAxis = Int(serialized, "RollAxis", 0);
            }

            return constraint;
        }


        /// <summary>
        /// A VRM 1.0 avatar's look at and first person settings, held in the object asset its
        /// instance points at. In an imported `.vrm` that asset is a sub-asset of the binary
        /// file, so it is read through the object API rather than followed off disk as text.
        /// </summary>
        public static VrmAvatarSettingsData ReadSettings10(Component instance)
        {
            VrmAvatarSettingsData settings = new VrmAvatarSettingsData();
            SerializedObject vrm = ObjectAsset(instance, "Vrm");

            if (vrm == null)
            {
                return settings;
            }

            SerializedProperty offset = vrm.FindProperty("LookAt.OffsetFromHead");
            if (offset != null)
            {
                settings.EyeOffsetFromHead = offset.vector3Value;
                settings.HasEyeOffset = offset.vector3Value != Vector3.zero;
            }

            SerializedProperty lookAtType = vrm.FindProperty("LookAt.LookAtType");
            settings.LookAtByExpression = lookAtType != null && lookAtType.enumValueIndex == 1;

            foreach (string map in new[]
                     { "HorizontalOuter", "HorizontalInner", "VerticalDown", "VerticalUp" })
            {
                SerializedProperty scale = vrm.FindProperty($"LookAt.{map}.CurveYRangeDegree");
                if (scale != null)
                {
                    settings.EyeRotationLimitDegrees =
                        Mathf.Max(settings.EyeRotationLimitDegrees, scale.floatValue);
                }
            }

            CountFirstPersonFlags(vrm.FindProperty("FirstPerson.Renderers"), settings);
            return settings;
        }

        /// <summary>
        /// Counts the renderers hidden from one view or the other. Both formats write the same
        /// four values in the same order: auto, both, third person only, first person only.
        /// </summary>
        private static void CountFirstPersonFlags(
            SerializedProperty renderers, VrmAvatarSettingsData settings)
        {
            if (renderers == null || !renderers.isArray)
            {
                return;
            }

            for (int i = 0; i < renderers.arraySize; i++)
            {
                SerializedProperty flag = renderers.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("FirstPersonFlag");

                if (flag == null)
                {
                    continue;
                }

                if (flag.intValue == 2)
                {
                    settings.ThirdPersonOnlyRenderers++;
                }
                else if (flag.intValue == 3)
                {
                    settings.FirstPersonOnlyRenderers++;
                }
            }
        }

        /// <summary>The licence a VRM 1.0 avatar carries.</summary>
        public static VrmMetaData ReadMeta10(Component instance)
        {
            SerializedObject vrm = ObjectAsset(instance, "Vrm");
            SerializedProperty meta = vrm?.FindProperty("Meta");

            if (meta == null)
            {
                return null;
            }

            VrmMetaData data = new VrmMetaData
            {
                Title = Relative(meta, "Name")?.stringValue ?? string.Empty,
                LicenseUrl = Relative(meta, "OtherLicenseUrl")?.stringValue ?? string.Empty,
            };

            SerializedProperty authors = Relative(meta, "Authors");
            if (authors != null && authors.isArray)
            {
                for (int i = 0; i < authors.arraySize; i++)
                {
                    string name = authors.GetArrayElementAtIndex(i).stringValue;
                    if (!string.IsNullOrEmpty(name))
                    {
                        data.Authors.Add(name);
                    }
                }
            }

            if (Relative(meta, "AvatarPermission") is SerializedProperty who)
            {
                data.AvatarPermission = (VrmAvatarPermission)who.intValue;
            }

            if (Relative(meta, "Modification") is SerializedProperty change)
            {
                data.Modification = (VrmModificationPermission)change.intValue;
            }

            data.ViolentUsage = Flag(meta, "ViolentUsage");
            data.SexualUsage = Flag(meta, "SexualUsage");
            data.PoliticalOrReligiousUsage = Flag(meta, "PoliticalOrReligiousUsage");
            data.AntisocialOrHateUsage = Flag(meta, "AntisocialOrHateUsage");
            data.Redistribution = Flag(meta, "Redistribution");

            // required, unnecessary
            if (Relative(meta, "CreditNotation") is SerializedProperty credit)
            {
                data.CreditRequired = credit.intValue == 0;
            }

            if (Relative(meta, "CommercialUsage") is SerializedProperty commercial)
            {
                data.CommercialUsage = Vrm10CommercialNames[
                    Mathf.Clamp(commercial.intValue, 0, Vrm10CommercialNames.Length - 1)];
            }

            return data;
        }

        /// <summary>
        /// The expressions a VRM 1.0 avatar declares. The presets are named fields and the rest
        /// are a list, and both reference expression assets beside the object asset.
        /// </summary>
        public static List<VrmExpressionData> ReadExpressions10(Component instance)
        {
            List<VrmExpressionData> expressions = new List<VrmExpressionData>();
            SerializedObject vrm = ObjectAsset(instance, "Vrm");

            if (vrm == null)
            {
                return expressions;
            }

            foreach ((string Field, VrmExpressionRole Role) preset in Vrm10Presets)
            {
                SerializedProperty clip = vrm.FindProperty("Expression." + preset.Field);
                if (clip?.objectReferenceValue == null)
                {
                    continue;
                }

                VrmExpressionData expression = ReadClip10(clip.objectReferenceValue);
                expression.Name = preset.Field;
                expression.Role = preset.Role;
                expressions.Add(expression);
            }

            SerializedProperty custom = vrm.FindProperty("Expression.CustomClips");
            if (custom == null || !custom.isArray)
            {
                return expressions;
            }

            for (int i = 0; i < custom.arraySize; i++)
            {
                Object clip = custom.GetArrayElementAtIndex(i).objectReferenceValue;
                if (clip == null)
                {
                    continue;
                }

                VrmExpressionData expression = ReadClip10(clip);
                if (string.IsNullOrEmpty(expression.Name))
                {
                    expression.Name = "Expression";
                }

                expressions.Add(expression);
            }

            return expressions;
        }

        private static VrmExpressionData ReadClip10(Object clip)
        {
            SerializedObject serialized = new SerializedObject(clip);
            VrmExpressionData expression = new VrmExpressionData { Name = clip.name };

            ReadBindings(serialized.FindProperty("MorphTargetBindings"), Vrm10WeightToUnity,
                expression);

            ReadMaterialBindings(serialized, expression);

            expression.IsBinary = serialized.FindProperty("IsBinary")?.boolValue ?? false;
            expression.OverrideBlink = OverrideOf(serialized, "OverrideBlink");
            expression.OverrideLookAt = OverrideOf(serialized, "OverrideLookAt");
            expression.OverrideMouth = OverrideOf(serialized, "OverrideMouth");

            return expression;
        }

        private static void ReadMaterialBindings(
            SerializedObject serialized, VrmExpressionData expression)
        {
            SerializedProperty colours = serialized.FindProperty("MaterialColorBindings");
            for (int i = 0; colours != null && colours.isArray && i < colours.arraySize; i++)
            {
                SerializedProperty entry = colours.GetArrayElementAtIndex(i);
                expression.MaterialColorBindings.Add(new VrmMaterialColorBinding
                {
                    MaterialName =
                        entry.FindPropertyRelative("MaterialName")?.stringValue ?? string.Empty,
                    PropertyName = VrmMaterialProperties.NameOf(
                        entry.FindPropertyRelative("BindType")?.enumValueIndex ?? -1),
                    TargetValue =
                        entry.FindPropertyRelative("TargetValue")?.vector4Value ?? Vector4.zero,
                });
            }

            SerializedProperty uvs = serialized.FindProperty("MaterialUVBindings");
            for (int i = 0; uvs != null && uvs.isArray && i < uvs.arraySize; i++)
            {
                SerializedProperty entry = uvs.GetArrayElementAtIndex(i);
                expression.MaterialUvBindings.Add(new VrmMaterialUvBinding
                {
                    MaterialName =
                        entry.FindPropertyRelative("MaterialName")?.stringValue ?? string.Empty,
                    Scaling = entry.FindPropertyRelative("Scaling")?.vector2Value ?? Vector2.one,
                    Offset = entry.FindPropertyRelative("Offset")?.vector2Value ?? Vector2.zero,
                });
            }
        }

        private static VrmExpressionOverride OverrideOf(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            return property == null
                ? VrmExpressionOverride.None
                : (VrmExpressionOverride)property.enumValueIndex;
        }

        private static void ReadBindings(
            SerializedProperty bindings, float weightScale, VrmExpressionData expression)
        {
            if (bindings == null || !bindings.isArray)
            {
                return;
            }

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);

                expression.Bindings.Add(new VrmMorphBinding
                {
                    Path = entry.FindPropertyRelative("RelativePath")?.stringValue ?? string.Empty,
                    Index = entry.FindPropertyRelative("Index")?.intValue ?? 0,
                    Weight = (entry.FindPropertyRelative("Weight")?.floatValue ?? 0f) * weightScale,
                });
            }
        }

        /// <summary>The asset a component points at, as something to read fields from.</summary>
        private static SerializedObject ObjectAsset(Component component, string field)
        {
            if (component == null)
            {
                return null;
            }

            Object asset = new SerializedObject(component)
                .FindProperty(field)?.objectReferenceValue;

            return asset == null ? null : new SerializedObject(asset);
        }

        private static SerializedProperty Relative(SerializedProperty parent, string name)
        {
            return parent?.FindPropertyRelative(name);
        }

        /// <summary>A yes or no field of a VRM 1.0 meta block.</summary>
        private static bool? Flag(SerializedProperty meta, string name)
        {
            SerializedProperty property = Relative(meta, name);
            return property == null ? (bool?)null : property.boolValue;
        }

        /// <summary>
        /// VRM 1.0 weights run from 0 to 1, and Unity's blendshape weights from 0 to 100.
        /// UniVRM's own constant for this is `MorphTargetBinding.VRM_TO_UNITY`.
        /// </summary>
        private const float Vrm10WeightToUnity = 100f;

        /// <summary>The presets of VRM 1.0, as the fields the object asset names them by.</summary>
        private static readonly (string Field, VrmExpressionRole Role)[] Vrm10Presets =
        {
            ("Happy", VrmExpressionRole.Emotion),
            ("Angry", VrmExpressionRole.Emotion),
            ("Sad", VrmExpressionRole.Emotion),
            ("Relaxed", VrmExpressionRole.Emotion),
            ("Surprised", VrmExpressionRole.Emotion),
            ("Aa", VrmExpressionRole.Viseme),
            ("Ih", VrmExpressionRole.Viseme),
            ("Ou", VrmExpressionRole.Viseme),
            ("Ee", VrmExpressionRole.Viseme),
            ("Oh", VrmExpressionRole.Viseme),
            ("Blink", VrmExpressionRole.Blink),
            ("BlinkLeft", VrmExpressionRole.Blink),
            ("BlinkRight", VrmExpressionRole.Blink),
            ("LookUp", VrmExpressionRole.LookAt),
            ("LookDown", VrmExpressionRole.LookAt),
            ("LookLeft", VrmExpressionRole.LookAt),
            ("LookRight", VrmExpressionRole.LookAt),
            ("Neutral", VrmExpressionRole.Neutral),
        };

        /// <summary>How far VRM 1.0 lets commercial use go, in its own words.</summary>
        private static readonly string[] Vrm10CommercialNames =
        {
            "personal, not for profit",
            "personal, including for profit",
            "corporate",
        };

        private static float Float(SerializedObject serialized, string name, float fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? fallback : property.floatValue;
        }

        private static int Int(SerializedObject serialized, string name, int fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? fallback : property.intValue;
        }

        private static Vector3 Vector(SerializedObject serialized, string name, Vector3 fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? fallback : property.vector3Value;
        }

        private static string Text(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? string.Empty : property.stringValue ?? string.Empty;
        }

        private static long Reference(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? 0L : IdOf(property.objectReferenceValue);
        }

        private static List<long> References(SerializedObject serialized, string name)
        {
            return References(serialized.FindProperty(name));
        }

        private static List<long> References(SerializedProperty list)
        {
            List<long> ids = new List<long>();
            if (list == null || !list.isArray)
            {
                return ids;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                long id = IdOf(list.GetArrayElementAtIndex(i).objectReferenceValue);
                if (id != 0L)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }
    }
}
