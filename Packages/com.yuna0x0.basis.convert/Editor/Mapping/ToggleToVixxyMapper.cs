using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a resolved menu toggle into a Vixxy control, where it can be turned into one at all.
    /// <para>
    /// Object switching, blendshapes and material properties are rebuilt. A toggle that does
    /// anything else is left alone rather than half converted: emitting part of it and silently
    /// dropping the rest would produce a control that looks right and does some of the job,
    /// which is worse than not making it.
    /// </para>
    /// </summary>
    public static class ToggleToVixxyMapper
    {
        public static VixxyControlPlan Map(ResolvedToggle toggle)
        {
            VixxyControlPlan plan = new VixxyControlPlan
            {
                MenuName = toggle.MenuName,
                Parameter = toggle.Parameter,
                IsSlider = toggle.IsSlider,
                DefaultValue = toggle.DefaultValue,
                Saved = toggle.Saved,
                NetworkSynced = toggle.NetworkSynced,
            };

            foreach (ResolvedChoice choice in toggle.Choices)
            {
                plan.ChoiceNames.Add(choice.Name);
                plan.ChoiceValues.Add(choice.Value);

                // Turning a transform over time has somewhere to go: an authored motion the
                // control switches on. Anything else animated does not, and a clip driving
                // something a control cannot hold is left alone rather than half converted.
                if (choice.Effects.OtherCurves > 0
                    || choice.Effects.AnimatedCurves > choice.Effects.AnimatedRotationCurves)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vixxy.notSimple",
                        $"'{toggle.MenuName}' animates over time or drives something a Vixxy "
                        + "control cannot hold, such as a transform. Rebuild it by hand.");
                    return plan;
                }
            }

            // Basis's menu treats a two-choice control as a toggle only when its values are 0
            // and 1. An Int parameter toggled to another value is the same control.
            if (!toggle.IsSlider && plan.ChoiceValues.Count == 2
                && (plan.ChoiceValues[0] != 0 || plan.ChoiceValues[1] != 1))
            {
                int onValue = plan.ChoiceValues[1];
                plan.DefaultValue = Mathf.Approximately(toggle.DefaultValue, onValue) ? 1f : 0f;
                plan.ChoiceValues[0] = 0;
                plan.ChoiceValues[1] = 1;
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vixxy.values.normalized",
                    $"'{toggle.MenuName}' switched its parameter to {onValue}. The control uses "
                    + "0 and 1 so Basis presents it as a toggle.");
            }

            MapMotions(toggle, plan);

            int count = toggle.Choices.Count;
            Dictionary<string, bool>[] states = new Dictionary<string, bool>[count];
            HashSet<string> paths = new HashSet<string>();

            for (int choice = 0; choice < count; choice++)
            {
                states[choice] = StatesIn(toggle.Choices[choice].Effects);
                paths.UnionWith(states[choice].Keys);
            }

            foreach (string path in paths)
            {
                // A choice that says nothing about an object leaves it as the avatar was
                // authored, so its value is read from the hierarchy later rather than assumed.
                VixxyActivationPlan activation = new VixxyActivationPlan
                {
                    Path = path,
                    Choices = new bool[count],
                    Set = new bool[count],
                };

                for (int choice = 0; choice < count; choice++)
                {
                    activation.Set[choice] =
                        states[choice].TryGetValue(path, out bool active);
                    activation.Choices[choice] = active;
                }

                plan.Activations.Add(activation);
            }

            MapBlendShapes(toggle, plan);
            MapMaterialProperties(toggle, plan);

            if (toggle.GuardedBy.Count > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vixxy.builtinGuard",
                    $"'{toggle.MenuName}' also waited on VRChat's "
                    + $"{string.Join(", ", toggle.GuardedBy)}, which Basis has no equivalent for. "
                    + "The control switches whenever it is used, as though those were satisfied.");
            }

            if (toggle.MotionsBetweenEnds > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vixxy.puppetEnds",
                    $"'{toggle.MenuName}' blends through {toggle.MotionsBetweenEnds} motions "
                    + "between its ends. A Vixxy slider interpolates in a straight line between "
                    + "its choices, so the ends carry across and the shape of the sweep between "
                    + "them does not.");
            }

            if (plan.Activations.Count == 0 && plan.Subjects.Count == 0
                && plan.Motions.Count == 0 && plan.Diagnostics.Count == 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vixxy.nothingToSwitch",
                    $"'{toggle.MenuName}' did not switch any objects, so there was nothing to "
                    + "rebuild.");
            }

            return plan;
        }

        /// <summary>
        /// Turns a choice that animates transforms over time into a motion the control switches.
        /// <para>
        /// The activation reads like any other: on for the choice whose clip carried the
        /// animation, off for the rest. What it holds is the motion's component rather than an
        /// object's transform, which Vixxy permits for this type specifically.
        /// </para>
        /// </summary>
        private static void MapMotions(ResolvedToggle toggle, VixxyControlPlan plan)
        {
            int count = toggle.Choices.Count;

            for (int choice = 0; choice < count; choice++)
            {
                ResolvedChoice resolved = toggle.Choices[choice];
                if (resolved.Effects.AnimatedRotationPaths.Count == 0)
                {
                    continue;
                }

                AuthoredMotionPlan motion = MotionToAuthoredMapper.MapSwitched(
                    toggle.MenuName, resolved.Name, Looping(resolved), resolved.Effects);

                // The motion's own diagnostics stay on it rather than being folded in here.
                // They say it was rebuilt, which is only true once the control it belongs to
                // resolves against the avatar, and that is known a layer further out.
                VixxyActivationPlan activation = new VixxyActivationPlan
                {
                    MotionIndex = plan.Motions.Count,
                    Choices = new bool[count],
                    Set = new bool[count],
                };

                for (int i = 0; i < count; i++)
                {
                    activation.Choices[i] = i == choice;

                    // Every choice has something to say about a motion: the one that carries it
                    // plays it, and the rest leave it off. There is no authored state to fall
                    // back on, because the component does not exist until this writes it.
                    activation.Set[i] = true;
                }

                plan.Activations.Add(activation);
                plan.Motions.Add(new VixxyMotionPlan {Motion = motion, Choice = choice});
            }
        }

        /// <summary>
        /// Whether a switched motion repeats. A clip authored to loop keeps looping while the
        /// control holds it on; one that was not plays once each time it is switched on.
        /// </summary>
        private static bool Looping(ResolvedChoice choice) =>
            choice.Clip == null || choice.Clip.isLooping;

        /// <summary>
        /// Blendshapes become subjects holding a float property per shape. As with objects, a
        /// shape set on only one side leaves the other at the avatar's authored weight.
        /// </summary>
        private static void MapBlendShapes(ResolvedToggle toggle, VixxyControlPlan plan)
        {
            int count = toggle.Choices.Count;
            Dictionary<(string, string), float>[] shapes =
                new Dictionary<(string, string), float>[count];
            HashSet<(string, string)> keys = new HashSet<(string, string)>();

            for (int choice = 0; choice < count; choice++)
            {
                shapes[choice] = ShapesIn(toggle.Choices[choice].Effects);
                keys.UnionWith(shapes[choice].Keys);
            }

            foreach ((string path, string shape) in keys)
            {
                VixxyBlendShapePlan planned = new VixxyBlendShapePlan
                {
                    ShapeName = shape,
                    Choices = new float[count],
                    Set = new bool[count],
                };

                for (int choice = 0; choice < count; choice++)
                {
                    planned.Set[choice] =
                        shapes[choice].TryGetValue((path, shape), out float weight);
                    planned.Choices[choice] = weight;
                }

                SubjectFor(plan, path).BlendShapes.Add(planned);
            }
        }

        /// <summary>
        /// Material properties become subjects too, holding one property each. Vixxy applies
        /// them through a MaterialPropertyBlock, so the property name is the shader's own.
        /// <para>
        /// A clip sets one channel of a colour at a time, `material._Color.r` and its siblings,
        /// so the channels are gathered back into one property here. Which channels each side of
        /// the toggle set is recorded rather than assumed: what a clip does not set keeps the
        /// value the material was authored with, which is filled in once the renderer is known.
        /// </para>
        /// </summary>
        private static void MapMaterialProperties(ResolvedToggle toggle, VixxyControlPlan plan)
        {
            Dictionary<(string, string), VixxyMaterialPropertyPlan> byProperty =
                new Dictionary<(string, string), VixxyMaterialPropertyPlan>();

            for (int choice = 0; choice < toggle.Choices.Count; choice++)
            {
                Collect(plan, toggle.Choices[choice].Effects, byProperty, choice,
                    toggle.Choices.Count);
            }
        }

        private static void Collect(
            VixxyControlPlan plan, Sources.ClipEffects effects,
            Dictionary<(string, string), VixxyMaterialPropertyPlan> byProperty, int choice,
            int choiceCount)
        {
            foreach (Sources.MaterialPropertyEffect effect in effects.MaterialProperties)
            {
                if (!byProperty.TryGetValue((effect.Path, effect.PropertyName),
                        out VixxyMaterialPropertyPlan property))
                {
                    VixxySubjectPlan subject = SubjectFor(plan, effect.Path);
                    subject.RendererTypeName = effect.RendererTypeName;

                    property = new VixxyMaterialPropertyPlan
                    {
                        PropertyName = effect.PropertyName,
                        Kind = KindOf(effect),
                        Choices = new Vector4[choiceCount],
                        Set = NewSetFlags(choiceCount),
                    };

                    subject.MaterialProperties.Add(property);
                    byProperty[(effect.Path, effect.PropertyName)] = property;
                }

                int channel = effect.Channel < 0 ? 0 : effect.Channel;
                Vector4 value = property.Choices[choice];
                value[channel] = effect.Value;
                property.Choices[choice] = value;

                property.Set[choice][channel] = true;
            }
        }

        private static bool[][] NewSetFlags(int choiceCount)
        {
            bool[][] flags = new bool[choiceCount][];
            for (int choice = 0; choice < choiceCount; choice++)
            {
                flags[choice] = new bool[4];
            }

            return flags;
        }

        private static VixxyMaterialPropertyKind KindOf(Sources.MaterialPropertyEffect effect)
        {
            if (effect.Channel < 0)
            {
                return VixxyMaterialPropertyKind.Float;
            }

            return effect.ColourChannel
                ? VixxyMaterialPropertyKind.Colour
                : VixxyMaterialPropertyKind.Vector;
        }

        /// <summary>
        /// The subject for one path, shared between blendshapes and material properties so a
        /// toggle setting both on the same renderer produces one subject rather than two.
        /// </summary>
        private static VixxySubjectPlan SubjectFor(VixxyControlPlan plan, string path)
        {
            foreach (VixxySubjectPlan existing in plan.Subjects)
            {
                if (existing.Path == path)
                {
                    return existing;
                }
            }

            VixxySubjectPlan subject = new VixxySubjectPlan { Path = path };
            plan.Subjects.Add(subject);
            return subject;
        }

        private static Dictionary<(string, string), float> ShapesIn(
            yuna0x0.Basis.Convert.Sources.ClipEffects effects)
        {
            Dictionary<(string, string), float> shapes =
                new Dictionary<(string, string), float>();

            foreach (yuna0x0.Basis.Convert.Sources.BlendShapeEffect shape in effects.BlendShapes)
            {
                shapes[(shape.Path, shape.ShapeName)] = shape.Value;
            }

            return shapes;
        }

        private static Dictionary<string, bool> StatesIn(yuna0x0.Basis.Convert.Sources.ClipEffects effects)
        {
            Dictionary<string, bool> states = new Dictionary<string, bool>();

            foreach (string path in effects.Deactivated)
            {
                states[path] = false;
            }

            foreach (string path in effects.Activated)
            {
                states[path] = true;
            }

            return states;
        }
    }
}
