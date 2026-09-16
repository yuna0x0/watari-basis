using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>One choice a control offers, and what its clip does.</summary>
    public sealed class ResolvedChoice
    {
        /// <summary>Label from the menu control that selects this choice.</summary>
        public string Name = string.Empty;

        /// <summary>Value of the parameter that selects it.</summary>
        public int Value;

        public ClipEffects Effects = new ClipEffects();

        /// <summary>
        /// The clip behind the choice. Kept because animation that has to be baked cannot be
        /// described by what was read out of it.
        /// </summary>
        public AnimationClip Clip;
    }

    /// <summary>A menu control whose animator layer and clips were found.</summary>
    public sealed class ResolvedToggle
    {
        public string MenuName = string.Empty;
        public string Parameter = string.Empty;
        public string LayerName = string.Empty;

        /// <summary>
        /// What the control offers, in the order it offers them. A toggle has two, off first. A
        /// selector has one per value of its parameter. A puppet has the two ends of its range.
        /// </summary>
        public List<ResolvedChoice> Choices = new List<ResolvedChoice>();

        /// <summary>
        /// True for a continuous control rather than a set of states: a radial puppet. Vixxy
        /// shows those as a slider and interpolates between the choices.
        /// </summary>
        public bool IsSlider;

        /// <summary>
        /// Motions a puppet's blend tree held between its two ends. Vixxy interpolates in a
        /// straight line between choices, so anything in between is approximated by that line.
        /// </summary>
        public int MotionsBetweenEnds;

        /// <summary>
        /// VRChat's own parameters the layer also waited on, which Basis does not have. The
        /// control was rebuilt as though each were satisfied.
        /// </summary>
        public List<string> GuardedBy = new List<string>();

        /// <summary>
        /// What the avatar declares its parameter defaults to. A control that starts anywhere
        /// else would switch something the moment the avatar loads.
        /// </summary>
        public float DefaultValue;

        /// <summary>Whether the parameter persisted across sessions. Vixxy remembers by default.</summary>
        public bool Saved = true;

        /// <summary>Whether the parameter was synced to other players. Vixxy networks by default.</summary>
        public bool NetworkSynced = true;

        public bool IsSelector => Choices.Count > 2;

        public ClipEffects WhenOff
        {
            get => EffectsFor(0);
            set => SetEffects(0, value);
        }

        public ClipEffects WhenOn
        {
            get => EffectsFor(1);
            set => SetEffects(1, value);
        }

        private ClipEffects EffectsFor(int index)
        {
            while (Choices.Count <= index)
            {
                Choices.Add(new ResolvedChoice {Value = Choices.Count});
            }

            return Choices[index].Effects;
        }

        private void SetEffects(int index, ClipEffects effects)
        {
            EffectsFor(index);
            Choices[index].Effects = effects ?? new ClipEffects();
        }

        /// <summary>
        /// True when both sides do nothing but switch objects on and off, set blendshapes or set
        /// material properties, which is exactly what a Vixxy control holds.
        /// </summary>
        public bool IsSimple
        {
            get
            {
                bool anything = false;

                foreach (ResolvedChoice choice in Choices)
                {
                    if (choice.Effects.OtherCurves > 0 || choice.Effects.AnimatedCurves > 0)
                    {
                        return false;
                    }

                    anything |= !choice.Effects.IsEmpty;
                }

                return anything;
            }
        }
    }

    /// <summary>
    /// Ties an avatar's menu toggles to the animator layers that implement them, and reduces
    /// each to what it actually does.
    /// <para>
    /// The FX layer is found by its slot in the descriptor. That slot is easy to get wrong: the
    /// layer type ordering has a deprecated entry at 1 that shifts everything after it.
    /// </para>
    /// </summary>
    public static class ToggleResolver
    {
        public static List<ResolvedToggle> Resolve(
            VrcExpressionInventory inventory, string fxControllerGuid)
        {
            return Resolve(inventory, LoadController(fxControllerGuid));
        }

        /// <summary>The same trace against a controller already in hand, as a merged one is.</summary>
        public static List<ResolvedToggle> Resolve(
            VrcExpressionInventory inventory, AnimatorController controller)
        {
            List<ResolvedToggle> resolved = new List<ResolvedToggle>();

            if (controller == null || inventory == null)
            {
                return resolved;
            }

            // Controls are grouped by parameter, because several of them commonly share one and
            // pick different values from it: a hairstyle, an outfit, a facial expression.
            Dictionary<string, List<VrcExpressionControl>> byParameter =
                new Dictionary<string, List<VrcExpressionControl>>();

            foreach (VrcExpressionMenu menu in inventory.Menus)
            {
                foreach (VrcExpressionControl control in menu.Controls)
                {
                    if (control.Type != VrcExpressionControlType.Toggle
                        || string.IsNullOrEmpty(control.Parameter))
                    {
                        continue;
                    }

                    if (!byParameter.TryGetValue(control.Parameter,
                            out List<VrcExpressionControl> controls))
                    {
                        controls = new List<VrcExpressionControl>();
                        byParameter[control.Parameter] = controls;
                    }

                    controls.Add(control);
                }
            }

            ResolvePuppets(inventory, controller, resolved);

            foreach (FxToggleLayer layer in
                     FxControllerReader.FindToggleLayers(controller, byParameter.Keys))
            {
                List<VrcExpressionControl> controls = byParameter[layer.Parameter];

                ResolvedToggle toggle = new ResolvedToggle
                {
                    MenuName = NameFor(controls, layer),
                    Parameter = layer.Parameter,
                    LayerName = layer.LayerName,
                };

                toggle.GuardedBy.AddRange(layer.GuardedBy);
                toggle.DefaultValue = DefaultOf(inventory, layer.Parameter);
                ApplyParameterFlags(inventory, layer.Parameter, toggle);

                foreach (FxParameterState state in layer.States)
                {
                    toggle.Choices.Add(new ResolvedChoice
                    {
                        Name = ChoiceName(controls, state.Value, layer),
                        Value = state.Value,
                        Effects = EffectsOf(state),
                        Clip = state.Clip,
                    });
                }

                resolved.Add(toggle);
            }

            return resolved;
        }

        /// <summary>
        /// What the avatar declares the parameter defaults to, or zero when it declares nothing.
        /// </summary>
        /// <summary>A blend tree state cannot be read as a constant, so it is marked unreadable.</summary>
        public static ClipEffects EffectsOf(FxParameterState state)
        {
            return state.MotionUnreadable
                ? new ClipEffects { OtherCurves = 1 }
                : AnimationClipReader.Read(state.Clip);
        }

        private static void ApplyParameterFlags(
            VrcExpressionInventory inventory, string parameter, ResolvedToggle toggle)
        {
            if (inventory == null)
            {
                return;
            }

            foreach (VrcExpressionParameter declared in inventory.Parameters)
            {
                if (declared.Name == parameter)
                {
                    toggle.Saved = declared.Saved;
                    toggle.NetworkSynced = declared.NetworkSynced;
                    return;
                }
            }
        }

        private static float DefaultOf(VrcExpressionInventory inventory, string parameter)
        {
            if (inventory == null)
            {
                return 0f;
            }

            foreach (VrcExpressionParameter declared in inventory.Parameters)
            {
                if (declared.Name == parameter)
                {
                    return declared.DefaultValue;
                }
            }

            return 0f;
        }

        /// <summary>
        /// Radial puppets, which drive a float rather than switching between states.
        /// <para>
        /// The menu entry names its parameter under subParameters rather than as the control's
        /// own, and the layer holds a blend tree rather than transitions, so neither half is
        /// found by the path a toggle takes.
        /// </para>
        /// </summary>
        private static void ResolvePuppets(
            VrcExpressionInventory inventory, AnimatorController controller,
            List<ResolvedToggle> resolved)
        {
            Dictionary<string, VrcExpressionControl> radials =
                new Dictionary<string, VrcExpressionControl>();

            foreach (VrcExpressionMenu menu in inventory.Menus)
            {
                foreach (VrcExpressionControl control in menu.Controls)
                {
                    if (control.Type == VrcExpressionControlType.RadialPuppet
                        && control.SubParameters.Count > 0
                        && !string.IsNullOrEmpty(control.SubParameters[0])
                        && !radials.ContainsKey(control.SubParameters[0]))
                    {
                        radials[control.SubParameters[0]] = control;
                    }
                }
            }

            if (radials.Count == 0)
            {
                return;
            }

            foreach (FxToggleLayer layer in
                     FxControllerReader.FindBlendTreeLayers(controller, radials.Keys))
            {
                VrcExpressionControl control = radials[layer.Parameter];

                ResolvedToggle toggle = new ResolvedToggle
                {
                    MenuName = control.Name,
                    Parameter = layer.Parameter,
                    LayerName = layer.LayerName,
                    IsSlider = true,
                    DefaultValue = DefaultOf(inventory, layer.Parameter),
                };
                ApplyParameterFlags(inventory, layer.Parameter, toggle);

                // The two ends of the range. Vixxy interpolates between a control's choices, so
                // the ends describe the whole sweep; a tree with motions in between is
                // approximated by the line through them.
                FxParameterState low = layer.States[0];
                FxParameterState high = layer.States[layer.States.Count - 1];

                toggle.MotionsBetweenEnds = layer.States.Count - 2;

                toggle.Choices.Add(new ResolvedChoice
                {
                    Name = "Least",
                    Value = 0,
                    Effects = AnimationClipReader.Read(low.Clip),
                    Clip = low.Clip,
                });

                toggle.Choices.Add(new ResolvedChoice
                {
                    Name = "Most",
                    Value = 1,
                    Effects = AnimationClipReader.Read(high.Clip),
                    Clip = high.Clip,
                });

                resolved.Add(toggle);
            }
        }

        /// <summary>
        /// What to call the control. A toggle is named after the single menu entry that drives
        /// it; a selector has an entry per choice, so the parameter names the control and the
        /// entries name its choices.
        /// </summary>
        private static string NameFor(List<VrcExpressionControl> controls, FxToggleLayer layer)
        {
            if (controls.Count == 1)
            {
                return controls[0].Name;
            }

            return string.IsNullOrEmpty(layer.Parameter) ? layer.LayerName : layer.Parameter;
        }

        private static string ChoiceName(
            List<VrcExpressionControl> controls, int value, FxToggleLayer layer)
        {
            foreach (VrcExpressionControl control in controls)
            {
                if (Mathf.RoundToInt(control.Value) == value)
                {
                    return control.Name;
                }
            }

            // A value the menu never offers still needs a label, and for a plain toggle the
            // menu only ever names the on side.
            if (!layer.IsSelector)
            {
                return value == 0 ? "OFF" : "ON";
            }

            return $"{layer.Parameter} {value}";
        }

        public static AnimatorController LoadController(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
    }
}
