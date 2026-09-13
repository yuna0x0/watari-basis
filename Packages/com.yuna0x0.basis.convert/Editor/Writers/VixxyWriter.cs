using System.Collections.Generic;
using HVR.Vixxy;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Writers
{
    public sealed class ResolvedVixxyControl
    {
        public VixxyControlPlan Plan;

        /// <summary>Where the control component goes, normally the avatar root.</summary>
        public GameObject Host;

        /// <summary>
        /// What to switch, in the same order as the plan's activations. An object is switched
        /// through its Transform, as Vixxy asks; a motion through the `BasisAuthoredMotion`
        /// component itself.
        /// </summary>
        public List<Component> Targets = new List<Component>();

        /// <summary>Renderers the subjects act on, in the same order as the plan's subjects.</summary>
        public List<Renderer> Renderers = new List<Renderer>();
    }

    /// <summary>
    /// Creates an HVR Vixxy control and its menu item.
    /// <para>
    /// Vixxy stores object switching as activations, each holding the object's Transform and one
    /// bool per choice. Its own comment explains the shape: "To toggle a GameObject, provide the
    /// Transform instead. It makes things easier as GameObject is not a component."
    /// </para>
    /// <para>
    /// Most of the control's fields are internal to its assembly, so they are written through
    /// SerializedObject. The orchestrator is deliberately left alone: a control finds its own at
    /// runtime through VixxySetup.EnsureInitialized.
    /// </para>
    /// </summary>
    public static class VixxyWriter
    {
        public static HVRVixxyControl Write(
            ResolvedVixxyControl control, string undoName = "Convert menu toggle")
        {
            if (control?.Host == null)
            {
                throw new System.ArgumentException("A host is required", nameof(control));
            }

            VixxyControlPlan plan = control.Plan;

            HVRVixxyControl component = Undo.AddComponent<HVRVixxyControl>(control.Host);
            Undo.SetCurrentGroupName(undoName);

            // choices and defaultValue are public; everything else here is not.
            component.choices = ChoicesOf(plan);
            component.defaultValue = plan.DefaultValue;

            SerializedObject serialized = new SerializedObject(component);

            SerializedProperty networked = serialized.FindProperty("networked");
            if (networked != null)
            {
                networked.boolValue = plan.NetworkSynced;
            }

            SerializedProperty activations = serialized.FindProperty("activations");

            int written = 0;
            activations.arraySize = control.Targets.Count;

            for (int i = 0; i < control.Targets.Count; i++)
            {
                Component target = control.Targets[i];
                if (target == null || i >= plan.Activations.Count)
                {
                    continue;
                }

                SerializedProperty entry = activations.GetArrayElementAtIndex(written);
                entry.FindPropertyRelative("component").objectReferenceValue = target;

                bool[] states = plan.Activations[i].Choices;
                SerializedProperty choices = entry.FindPropertyRelative("choices");
                choices.arraySize = states.Length;
                for (int c = 0; c < states.Length; c++)
                {
                    choices.GetArrayElementAtIndex(c).boolValue = states[c];
                }

                written++;
            }

            activations.arraySize = written;

            WriteSubjects(serialized, control);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Dispose();

            WriteMenuItem(control.Host, component, plan, undoName);

            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>
        /// Blendshapes and material properties are subjects rather than activations: a subject
        /// names the objects and carries a property per thing it sets, each with a value for
        /// each choice. The property list is a SerializeReference list, so each entry is
        /// assigned as a managed reference.
        /// </summary>
        private static void WriteSubjects(
            SerializedObject serialized, ResolvedVixxyControl control)
        {
            SerializedProperty subjects = serialized.FindProperty("subjects");
            List<VixxySubjectPlan> planned = control.Plan.Subjects;

            int written = 0;
            subjects.arraySize = control.Renderers.Count;

            for (int i = 0; i < control.Renderers.Count && i < planned.Count; i++)
            {
                Renderer renderer = control.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                SerializedProperty subject = subjects.GetArrayElementAtIndex(written);
                subject.FindPropertyRelative("selection").enumValueIndex =
                    (int)HVRVixxySelection.Normal;

                SerializedProperty targets = subject.FindPropertyRelative("targets");
                targets.arraySize = 1;
                targets.GetArrayElementAtIndex(0).objectReferenceValue = renderer.gameObject;

                subject.FindPropertyRelative("childrenOf").arraySize = 0;
                subject.FindPropertyRelative("exceptions").arraySize = 0;

                SerializedProperty properties = subject.FindPropertyRelative("properties");
                properties.arraySize = planned[i].BlendShapes.Count
                    + planned[i].MaterialProperties.Count;

                int slot = 0;

                for (int p = 0; p < planned[i].BlendShapes.Count; p++)
                {
                    VixxyBlendShapePlan shape = planned[i].BlendShapes[p];

                    SerializedProperty entry = properties.GetArrayElementAtIndex(slot++);
                    entry.managedReferenceValue = new HVRVixxyPropertyFloat
                    {
                        fullClassName = typeof(SkinnedMeshRenderer).FullName,
                        variant = HVRVixxyPropertyVariant.BlendShape,
                        propertyName = shape.ShapeName,
                    };

                    SerializedProperty choices = entry.FindPropertyRelative("choices");
                    choices.arraySize = shape.Choices.Length;
                    for (int c = 0; c < shape.Choices.Length; c++)
                    {
                        choices.GetArrayElementAtIndex(c).floatValue = shape.Choices[c];
                    }
                }

                foreach (VixxyMaterialPropertyPlan property in planned[i].MaterialProperties)
                {
                    WriteMaterialProperty(properties.GetArrayElementAtIndex(slot++),
                        planned[i].RendererTypeName, property);
                }

                written++;
            }

            subjects.arraySize = written;
        }

        /// <summary>
        /// One material property, as the type Vixxy holds that shape in. Vixxy's own editor
        /// picks between a float, a vector and a colour the same way, and turns the property
        /// name straight into a shader property id, so the name is the shader's own.
        /// </summary>
        private static void WriteMaterialProperty(
            SerializedProperty entry, string rendererTypeName,
            VixxyMaterialPropertyPlan property)
        {
            switch (property.Kind)
            {
                case VixxyMaterialPropertyKind.Colour:
                {
                    entry.managedReferenceValue = new HVRVixxyPropertyColor
                    {
                        fullClassName = rendererTypeName,
                        variant = HVRVixxyPropertyVariant.MaterialProperty,
                        propertyName = property.PropertyName,
                    };

                    SerializedProperty choices = entry.FindPropertyRelative("choices");
                    choices.arraySize = property.Choices.Length;
                    for (int c = 0; c < property.Choices.Length; c++)
                    {
                        choices.GetArrayElementAtIndex(c).colorValue = property.Choices[c];
                    }

                    break;
                }

                case VixxyMaterialPropertyKind.Vector:
                {
                    entry.managedReferenceValue = new HVRVixxyPropertyVector4
                    {
                        fullClassName = rendererTypeName,
                        variant = HVRVixxyPropertyVariant.MaterialProperty,
                        propertyName = property.PropertyName,
                    };

                    SerializedProperty choices = entry.FindPropertyRelative("choices");
                    choices.arraySize = property.Choices.Length;
                    for (int c = 0; c < property.Choices.Length; c++)
                    {
                        choices.GetArrayElementAtIndex(c).vector4Value = property.Choices[c];
                    }

                    break;
                }

                default:
                {
                    entry.managedReferenceValue = new HVRVixxyPropertyFloat
                    {
                        fullClassName = rendererTypeName,
                        variant = HVRVixxyPropertyVariant.MaterialProperty,
                        propertyName = property.PropertyName,
                    };

                    SerializedProperty choices = entry.FindPropertyRelative("choices");
                    choices.arraySize = property.Choices.Length;
                    for (int c = 0; c < property.Choices.Length; c++)
                    {
                        choices.GetArrayElementAtIndex(c).floatValue = property.Choices[c].x;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// The control's choices, titled by the menu entries that select them. A toggle keeps
        /// the OFF and ON titles it always had; a selector is named per value.
        /// </summary>
        private static HVRVixxyChoiceControl[] ChoicesOf(VixxyControlPlan plan)
        {
            if (plan.ChoiceNames.Count == 0)
            {
                return new[]
                {
                    new HVRVixxyChoiceControl {title = "OFF", value = 0f},
                    new HVRVixxyChoiceControl {title = "ON", value = 1f},
                };
            }

            HVRVixxyChoiceControl[] choices =
                new HVRVixxyChoiceControl[plan.ChoiceNames.Count];

            for (int i = 0; i < choices.Length; i++)
            {
                choices[i] = new HVRVixxyChoiceControl
                {
                    title = plan.ChoiceNames[i],
                    value = i < plan.ChoiceValues.Count ? plan.ChoiceValues[i] : i,
                };
            }

            return choices;
        }

        private static void WriteMenuItem(
            GameObject host, HVRVixxyControl control, VixxyControlPlan plan, string undoName)
        {
            HVRVixxyMenuItem item = Undo.AddComponent<HVRVixxyMenuItem>(host);
            Undo.SetCurrentGroupName(undoName);

            SerializedObject serialized = new SerializedObject(item);

            SerializedProperty title = serialized.FindProperty("title");
            if (title != null)
            {
                title.stringValue = plan.MenuName;
            }

            SerializedProperty titleSelection = serialized.FindProperty("titleSelection");
            if (titleSelection != null)
            {
                // Use the title written above rather than the object's name, which is the
                // avatar root here and would label every control identically.
                titleSelection.enumValueIndex =
                    (int)HVRVixxyTitleSelection.UseCustomTitle;
            }

            // A puppet is continuous, so its menu item is shown as a slider and Vixxy
            // interpolates between the control's choices.
            SerializedProperty presentation = serialized.FindProperty("presentation");
            if (presentation != null && plan.IsSlider)
            {
                presentation.enumValueIndex = (int)HVRVixxyControlPresentation.Slider;
            }

            SerializedProperty linked = serialized.FindProperty("control");
            if (linked != null)
            {
                linked.objectReferenceValue = control;
            }

            // A parameter VRChat did not save starts at its default every session.
            SerializedProperty remember = serialized.FindProperty("remember");
            if (remember != null)
            {
                remember.enumValueIndex = (int)(plan.Saved
                    ? HVRVixxyRememberScope.RememberInThisAvatar
                    : HVRVixxyRememberScope.DoNotRemember);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Dispose();
            EditorUtility.SetDirty(item);
        }
    }
}
