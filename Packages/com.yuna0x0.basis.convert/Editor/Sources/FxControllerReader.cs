using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>One state a layer holds, and the parameter value that selects it.</summary>
    public sealed class FxParameterState
    {
        /// <summary>Value of the layer's parameter that selects this state.</summary>
        public int Value;

        /// <summary>
        /// Where this sits on a blend tree's axis. Only meaningful for a puppet, where the
        /// parameter is continuous rather than a set of values.
        /// </summary>
        public float Threshold;

        public AnimationClip Clip;

        /// <summary>
        /// The state held a motion that is not a clip, a blend tree. Nothing constant can be read
        /// from it, so the choice is not rebuildable.
        /// </summary>
        public bool MotionUnreadable;
    }

    /// <summary>
    /// One animator layer steered by a single parameter.
    /// <para>
    /// A bool parameter gives the two states of an on/off toggle. An int parameter gives one
    /// state per value, the shape a menu with several controls sharing a parameter produces: a
    /// hairstyle picker, an outfit set, a facial expression.
    /// </para>
    /// </summary>
    public sealed class FxToggleLayer
    {
        public string LayerName = string.Empty;
        public string Parameter = string.Empty;

        /// <summary>The states, ordered by the value that selects them.</summary>
        public List<FxParameterState> States = new List<FxParameterState>();

        public bool IsSelector => States.Count > 2;

        /// <summary>
        /// True when the layer blends continuously between its states rather than switching
        /// between them, as a puppet does.
        /// </summary>
        public bool IsBlendTree;

        /// <summary>
        /// VRChat's own parameters the layer also tests, which Basis has no equivalent for. The
        /// layer was read as though each were satisfied, so these are what a rebuilt control no
        /// longer waits for.
        /// </summary>
        public List<string> GuardedBy = new List<string>();

        /// <summary>The clip for a parameter value of 0, which a toggle calls off.</summary>
        public AnimationClip WhenOff => ClipFor(0);

        /// <summary>The clip for a parameter value of 1, which a toggle calls on.</summary>
        public AnimationClip WhenOn => ClipFor(1);

        public AnimationClip ClipFor(int value)
        {
            foreach (FxParameterState state in States)
            {
                if (state.Value == value)
                {
                    return state.Clip;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// A layer that plays on its own, with no parameter to switch it.
    /// <para>
    /// This is where ambient motion lives: a tail that sways, ears that twitch, an accessory
    /// that turns. Basis replays that from a <c>BasisAuthoredMotion</c> rather than an animator.
    /// </para>
    /// </summary>
    public sealed class AmbientMotionLayer
    {
        public string LayerName = string.Empty;
        public string StateName = string.Empty;
        public AnimationClip Clip;

        /// <summary>The state's playback speed. Basis replays the baked clip at this rate.</summary>
        public float Speed = 1f;

        /// <summary>Whether the clip was authored to loop, which most ambient motion is.</summary>
        public bool Loop;
    }

    /// <summary>
    /// Finds the animator layers behind an avatar's menu toggles.
    /// <para>
    /// Unlike the rest of the source data, an AnimatorController is a native Unity type, so it is
    /// read through the editor API rather than out of YAML. Its scripts are not missing because
    /// there are none: nothing here belongs to the VRChat SDK.
    /// </para>
    /// </summary>
    public static class FxControllerReader
    {
        public static List<FxToggleLayer> FindToggleLayers(
            AnimatorController controller, ICollection<string> parameters)
        {
            List<FxToggleLayer> found = new List<FxToggleLayer>();
            if (controller == null || parameters == null || parameters.Count == 0)
            {
                return found;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorStateMachine machine = layer.stateMachine;
                if (machine == null)
                {
                    continue;
                }

                // A layer is a toggle for a parameter only when that parameter is the only one
                // of the avatar's own steering it. Gesture layers commonly carry a second
                // condition on an unrelated parameter, and treating those as that parameter's
                // own layer picks up the wrong clips entirely.
                HashSet<string> steering = SteeringParameters(machine);
                List<string> guards = new List<string>();
                string parameter = null;
                bool ambiguous = false;

                foreach (string steered in steering)
                {
                    if (VrchatBuiltInParameters.Contains(steered))
                    {
                        guards.Add(steered);
                    }
                    else if (parameter == null)
                    {
                        parameter = steered;
                    }
                    else
                    {
                        ambiguous = true;
                    }
                }

                // VRChat's own parameters are a different matter: nothing on Basis drives them,
                // so a layer guarded by one is still the menu parameter's layer. It counts as a
                // guard only when no transition tests it on its own, since a layer with states
                // of its own per gesture belongs to the gesture rather than to the toggle.
                if (ambiguous || parameter == null || !parameters.Contains(parameter)
                    || (guards.Count > 0 && !GuardsOnly(machine, parameter)))
                {
                    continue;
                }

                FxToggleLayer toggle = ReadLayer(layer, machine, parameter);
                if (toggle != null)
                {
                    toggle.GuardedBy.AddRange(guards);
                    found.Add(toggle);
                }
            }

            return found;
        }

        /// <summary>
        /// Finds the layers a puppet drives, which hold a blend tree rather than transitions.
        /// <para>
        /// A radial puppet is a float from 0 to 1 blending between motions, so the layer has one
        /// state whose motion is a blend tree keyed on that parameter. There are no conditions to
        /// read, which is why these are looked for separately.
        /// </para>
        /// </summary>
        public static List<FxToggleLayer> FindBlendTreeLayers(
            AnimatorController controller, ICollection<string> parameters)
        {
            List<FxToggleLayer> found = new List<FxToggleLayer>();
            if (controller == null || parameters == null || parameters.Count == 0)
            {
                return found;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine == null)
                {
                    continue;
                }

                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (!(child.state.motion is BlendTree tree)
                        || !parameters.Contains(tree.blendParameter))
                    {
                        continue;
                    }

                    FxToggleLayer read = ReadBlendTree(layer, tree);
                    if (read != null)
                    {
                        found.Add(read);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// The tree's children in threshold order. Vixxy interpolates between the values of its
        /// choices, so what a slider needs is the motions at each end; anything between them is
        /// on the line those two describe.
        /// </summary>
        private static FxToggleLayer ReadBlendTree(AnimatorControllerLayer layer, BlendTree tree)
        {
            List<ChildMotion> children = new List<ChildMotion>(tree.children);
            if (children.Count < 2)
            {
                return null;
            }

            children.Sort((left, right) => left.threshold.CompareTo(right.threshold));

            FxToggleLayer read = new FxToggleLayer
            {
                LayerName = layer.name,
                Parameter = tree.blendParameter,
                IsBlendTree = true,
            };

            foreach (ChildMotion child in children)
            {
                read.States.Add(new FxParameterState
                {
                    Value = Mathf.RoundToInt(child.threshold),
                    Threshold = child.threshold,
                    Clip = child.motion as AnimationClip,
                });
            }

            return read;
        }

        /// <summary>
        /// Finds the layers that play unconditionally, the shape ambient motion is authored in:
        /// no parameter steers them, and their state runs from the moment the avatar loads.
        /// <para>
        /// A layer with a single state and no transitions is the usual shape. One with several
        /// states but nothing steering them plays only its default, so that is the state read.
        /// </para>
        /// </summary>
        public static List<AmbientMotionLayer> FindAmbientLayers(AnimatorController controller)
        {
            List<AmbientMotionLayer> found = new List<AmbientMotionLayer>();
            if (controller == null)
            {
                return found;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorStateMachine machine = layer.stateMachine;
                if (machine == null || SteeringParameters(machine).Count > 0)
                {
                    continue;
                }

                AnimatorState state = machine.defaultState;
                if (state == null || !(state.motion is AnimationClip clip))
                {
                    continue;
                }

                found.Add(new AmbientMotionLayer
                {
                    LayerName = layer.name,
                    StateName = state.name,
                    Clip = clip,
                    Loop = clip.isLooping,
                    Speed = state.speed,
                });
            }

            return found;
        }

        /// <summary>
        /// Whether every transition testing one of VRChat's own parameters also tests the
        /// avatar's. When one does not, that transition is steered by the built-in alone, and
        /// the layer belongs to it rather than to the menu.
        /// </summary>
        private static bool GuardsOnly(AnimatorStateMachine machine, string parameter)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    if (!IsGuarded(transition.conditions, parameter))
                    {
                        return false;
                    }
                }
            }

            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (!IsGuarded(transition.conditions, parameter))
                {
                    return false;
                }
            }

            foreach (AnimatorTransition transition in machine.entryTransitions)
            {
                if (!IsGuarded(transition.conditions, parameter))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsGuarded(AnimatorCondition[] conditions, string parameter)
        {
            if (conditions == null)
            {
                return true;
            }

            bool builtIn = false;
            bool own = false;

            foreach (AnimatorCondition condition in conditions)
            {
                if (condition.parameter == parameter)
                {
                    own = true;
                }
                else if (VrchatBuiltInParameters.Contains(condition.parameter))
                {
                    builtIn = true;
                }
            }

            return !builtIn || own;
        }

        /// <summary>Every parameter any transition in the layer tests.</summary>
        private static HashSet<string> SteeringParameters(AnimatorStateMachine machine)
        {
            HashSet<string> parameters = new HashSet<string>();

            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    Collect(transition.conditions, parameters);
                }
            }

            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                Collect(transition.conditions, parameters);
            }

            foreach (AnimatorTransition transition in machine.entryTransitions)
            {
                Collect(transition.conditions, parameters);
            }

            return parameters;
        }

        private static void Collect(AnimatorCondition[] conditions, HashSet<string> into)
        {
            if (conditions == null)
            {
                return;
            }

            foreach (AnimatorCondition condition in conditions)
            {
                if (!string.IsNullOrEmpty(condition.parameter))
                {
                    into.Add(condition.parameter);
                }
            }
        }

        /// <summary>
        /// Reads a layer as one state per value of its parameter.
        /// <para>
        /// A bool parameter yields two: whatever an <c>If</c> transition points at is value 1,
        /// and the state the layer starts in is value 0. An int parameter yields one state per
        /// <c>Equals</c> value, which is how a menu offers several controls that share it.
        /// </para>
        /// </summary>
        private static FxToggleLayer ReadLayer(
            AnimatorControllerLayer layer, AnimatorStateMachine machine, string parameter)
        {
            Dictionary<int, AnimatorState> byValue = new Dictionary<int, AnimatorState>();
            AnimatorState whenOff = null;
            bool mentionsParameter = false;
            bool ambiguous = false;

            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    Classify(transition.conditions, transition.destinationState, parameter,
                        ref mentionsParameter, byValue, ref whenOff, ref ambiguous);
                }
            }

            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                Classify(transition.conditions, transition.destinationState, parameter,
                    ref mentionsParameter, byValue, ref whenOff, ref ambiguous);
            }

            foreach (AnimatorTransition transition in machine.entryTransitions)
            {
                Classify(transition.conditions, transition.destinationState, parameter,
                    ref mentionsParameter, byValue, ref whenOff, ref ambiguous);
            }

            // One value reaching two different states means something else decides between them,
            // which is the shape of a layer combining a toggle with a gesture. Reading it would
            // pick whichever transition came first, so it is left alone instead.
            if (!mentionsParameter || ambiguous)
            {
                return null;
            }

            // Whatever the layer starts in covers the value no transition named, which for a
            // toggle is the off side and for a selector is usually its first entry.
            AnimatorState fallback = whenOff ?? machine.defaultState;
            if (fallback != null && !byValue.ContainsValue(fallback))
            {
                byValue[byValue.ContainsKey(0) ? LowestUnusedValue(byValue) : 0] = fallback;
            }

            if (byValue.Count < 2)
            {
                return null;
            }

            List<int> values = new List<int>(byValue.Keys);
            values.Sort();

            FxToggleLayer read = new FxToggleLayer
            {
                LayerName = layer.name,
                Parameter = parameter,
            };

            foreach (int value in values)
            {
                Motion motion = byValue[value].motion;
                read.States.Add(new FxParameterState
                {
                    Value = value,
                    Clip = motion as AnimationClip,
                    MotionUnreadable = motion != null && !(motion is AnimationClip),
                });
            }

            return read;
        }

        /// <summary>
        /// Records the state a value selects. Two transitions into the same state are ordinary;
        /// two into different states mean the value alone does not decide, so the layer cannot
        /// be read as this parameter's own.
        /// </summary>
        private static void Claim(
            Dictionary<int, AnimatorState> byValue, int value, AnimatorState destination,
            ref bool ambiguous)
        {
            if (byValue.TryGetValue(value, out AnimatorState claimed))
            {
                if (claimed != destination)
                {
                    ambiguous = true;
                }

                return;
            }

            byValue[value] = destination;
        }

        private static int LowestUnusedValue(Dictionary<int, AnimatorState> byValue)
        {
            int value = 0;
            while (byValue.ContainsKey(value))
            {
                value++;
            }

            return value;
        }

        /// <summary>
        /// Which value of the parameter each transition selects. <c>Equals</c> names the value
        /// outright, which is how a selector is written; <c>If</c> means 1 and <c>IfNot</c> means
        /// 0, which is how a bool toggle is written.
        /// </summary>
        private static void Classify(
            AnimatorCondition[] conditions, AnimatorState destination, string parameter,
            ref bool mentionsParameter, Dictionary<int, AnimatorState> byValue,
            ref AnimatorState whenOff, ref bool ambiguous)
        {
            if (destination == null || conditions == null)
            {
                return;
            }

            foreach (AnimatorCondition condition in conditions)
            {
                if (condition.parameter != parameter)
                {
                    continue;
                }

                mentionsParameter = true;

                switch (condition.mode)
                {
                    case AnimatorConditionMode.Equals:
                        Claim(byValue, Mathf.RoundToInt(condition.threshold), destination,
                            ref ambiguous);
                        break;

                    case AnimatorConditionMode.If:
                    case AnimatorConditionMode.Greater:
                        Claim(byValue, 1, destination, ref ambiguous);
                        break;

                    case AnimatorConditionMode.IfNot:
                    case AnimatorConditionMode.NotEqual:
                    case AnimatorConditionMode.Less:
                        whenOff ??= destination;
                        break;
                }
            }
        }
    }
}
