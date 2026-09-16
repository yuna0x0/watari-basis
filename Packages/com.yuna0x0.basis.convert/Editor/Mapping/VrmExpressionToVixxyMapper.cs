using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRM avatar's expressions into one Vixxy selector.
    /// <para>
    /// An expression is a named set of blendshape weights. VRM lets an application wear several
    /// at once, each at its own strength, and sums the result; a menu has no strength and no
    /// application driving it, so the wearer picks one. One control with a choice per expression
    /// says that; a toggle per expression let two be on together and filled the menu with one
    /// entry each. Every shape any expression touches is written at every choice, at the
    /// expression's weight or at zero, which is the spec's own rule: all morph targets start at
    /// zero and the worn expressions are added.
    /// </para>
    /// <para>
    /// Only the expressions an author added and the emotions are choices. Visemes, blinking and
    /// looking around are driven by Basis itself. The first choice, Neutral, is every shape at
    /// zero: the spec keeps the `neutral` preset for backwards compatibility and applications do
    /// not apply it, so an avatar's own Neutral expression is not worn either. A material colour
    /// or texture offset an expression sets is written on
    /// every renderer that uses the material it names and nothing else, as a Vixxy material
    /// property; Vixxy sets properties per renderer, so a renderer that also carries other
    /// materials is left alone and reported.
    /// </para>
    /// </summary>
    public static class VrmExpressionToVixxyMapper
    {
        public const string MenuName = "Expression";
        public const string NeutralChoice = "Neutral";

        /// <summary>
        /// Whether this expression is a choice on the selector: an author-added or emotion
        /// expression that moves a blendshape, or changes a material some renderer uses.
        /// </summary>
        public static bool IsMenuWorthy(VrmExpressionData expression,
            IReadOnlyDictionary<string, List<VrmMaterialHost>> hosts = null)
        {
            if (expression == null
                || (expression.Role != VrmExpressionRole.Custom
                    && expression.Role != VrmExpressionRole.Emotion))
            {
                return false;
            }

            if (expression.Bindings.Count > 0)
            {
                return true;
            }

            foreach (VrmMaterialColorBinding colour in expression.MaterialColorBindings)
            {
                if (HasHost(hosts, colour.MaterialName)) return true;
            }

            foreach (VrmMaterialUvBinding uv in expression.MaterialUvBindings)
            {
                if (HasHost(hosts, uv.MaterialName)) return true;
            }

            return false;
        }

        /// <summary>Whether some renderer uses the material and nothing else.</summary>
        private static bool HasHost(
            IReadOnlyDictionary<string, List<VrmMaterialHost>> hosts, string material)
        {
            if (hosts == null || !hosts.TryGetValue(material, out List<VrmMaterialHost> found))
            {
                return false;
            }

            foreach (VrmMaterialHost host in found)
            {
                if (host.OtherMaterials == 0) return true;
            }

            return false;
        }

        /// <summary>
        /// One selector over these expressions, with Neutral as every shape at zero.
        /// </summary>
        public static VixxyControlPlan MapSelector(
            IReadOnlyList<VrmExpressionData> expressions,
            IReadOnlyDictionary<string, List<VrmMaterialHost>> hosts = null)
        {
            VixxyControlPlan plan = new VixxyControlPlan
            {
                MenuName = MenuName,
                Parameter = MenuName,
                DefaultValue = 0f,
            };

            int count = expressions.Count + 1;
            plan.ChoiceNames.Add(NeutralChoice);
            plan.ChoiceValues.Add(0);
            for (int i = 0; i < expressions.Count; i++)
            {
                plan.ChoiceNames.Add(expressions[i].Name);
                plan.ChoiceValues.Add(i + 1);
            }

            // One subject per renderer any expression touches: an expression commonly sets
            // shapes on the face and the eyebrows at once, and Vixxy addresses each renderer.
            Dictionary<string, VixxySubjectPlan> subjects =
                new Dictionary<string, VixxySubjectPlan>();
            Dictionary<(string, string), VixxyBlendShapePlan> shapes =
                new Dictionary<(string, string), VixxyBlendShapePlan>();
            Dictionary<(string, string), VixxyMaterialPropertyPlan> properties =
                new Dictionary<(string, string), VixxyMaterialPropertyPlan>();

            VixxySubjectPlan SubjectFor(string path, string rendererTypeName = null)
            {
                if (!subjects.TryGetValue(path, out VixxySubjectPlan subject))
                {
                    subject = new VixxySubjectPlan { Path = path };
                    subjects[path] = subject;
                    plan.Subjects.Add(subject);
                }

                if (rendererTypeName != null)
                {
                    subject.RendererTypeName = rendererTypeName;
                }

                return subject;
            }

            // A material property on every renderer that uses the material, set at this choice
            // and left to the material as authored at the others. VRM applies a bind as
            // base + (target - base) * weight, so worn fully it is the target and unworn the base.
            int ApplyMaterial(string material, string property, VixxyMaterialPropertyKind kind,
                Vector4 value, int choice, List<string> missing, List<string> shared)
            {
                if (string.IsNullOrEmpty(property))
                {
                    return 0;
                }

                if (hosts == null || !hosts.TryGetValue(material, out List<VrmMaterialHost> found))
                {
                    if (!missing.Contains(material)) missing.Add(material);
                    return 0;
                }

                int written = 0;
                foreach (VrmMaterialHost host in found)
                {
                    if (host.OtherMaterials > 0)
                    {
                        string note =
                            $"'{material}' on {host.Path} ({host.OtherMaterials + 1} materials)";
                        if (!shared.Contains(note)) shared.Add(note);
                        continue;
                    }

                    written++;
                    VixxySubjectPlan subject = SubjectFor(host.Path, host.RendererTypeName);
                    if (!properties.TryGetValue((host.Path, property),
                            out VixxyMaterialPropertyPlan planned))
                    {
                        bool[][] set = new bool[count][];
                        for (int c = 0; c < count; c++) set[c] = new bool[4];
                        planned = new VixxyMaterialPropertyPlan
                        {
                            PropertyName = property,
                            Kind = kind,
                            Choices = new Vector4[count],
                            Set = set,
                        };
                        properties[(host.Path, property)] = planned;
                        subject.MaterialProperties.Add(planned);
                    }

                    planned.Choices[choice] = value;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        planned.Set[choice][channel] = true;
                    }
                }

                return written;
            }

            VixxyBlendShapePlan ShapeFor(VrmMorphBinding binding)
            {
                if (!shapes.TryGetValue((binding.Path, binding.ShapeName),
                        out VixxyBlendShapePlan shape))
                {
                    VixxySubjectPlan subject = SubjectFor(binding.Path);

                    bool[] set = new bool[count];
                    for (int c = 0; c < count; c++)
                    {
                        set[c] = true;
                    }

                    shape = new VixxyBlendShapePlan
                    {
                        ShapeName = binding.ShapeName,
                        Choices = new float[count],
                        Set = set,
                    };
                    shapes[(binding.Path, binding.ShapeName)] = shape;
                    subject.BlendShapes.Add(shape);
                }

                return shape;
            }

            void Apply(VrmExpressionData expression, int choice)
            {
                int unnamed = 0;
                foreach (VrmMorphBinding binding in expression.Bindings)
                {
                    if (string.IsNullOrEmpty(binding.ShapeName))
                    {
                        unnamed++;
                        continue;
                    }

                    ShapeFor(binding).Choices[choice] = binding.Weight;
                }

                if (unnamed > 0)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Warning, "vrm.expression.shapeMissing",
                        $"'{expression.Name}' sets {unnamed} blendshapes the mesh does not have. VRM names shapes by index, so the mesh has likely changed since the expression was authored.");
                }

                List<string> missing = new List<string>();
                List<string> shared = new List<string>();
                int renderers = 0;
                foreach (VrmMaterialColorBinding colour in expression.MaterialColorBindings)
                {
                    renderers += ApplyMaterial(colour.MaterialName, colour.PropertyName,
                        VixxyMaterialPropertyKind.Colour, colour.TargetValue, choice, missing,
                        shared);
                }

                foreach (VrmMaterialUvBinding uv in expression.MaterialUvBindings)
                {
                    renderers += ApplyMaterial(uv.MaterialName, VrmMaterialProperties.UvProperty,
                        VixxyMaterialPropertyKind.Vector, uv.ScaleOffset, choice, missing, shared);
                }

                if (shared.Count > 0)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped,
                        "vrm.expression.materialShared",
                        $"'{expression.Name}' changes {string.Join(", ", shared)}. Vixxy sets a "
                        + "property for the whole renderer, which would change the other materials "
                        + "too, so that part was left out.");
                }

                if (renderers > 0)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "vrm.expression.materialValues",
                        $"'{expression.Name}' also sets material properties, written on the "
                        + $"{renderers} renderers that use the materials it names.");
                }

                if (missing.Count > 0)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.expression.materials",
                        $"'{expression.Name}' changes material '{string.Join("', '", missing)}', "
                        + "which no renderer on this avatar uses, so that part was not written.");
                }
            }

            int continuous = 0;
            for (int i = 0; i < expressions.Count; i++)
            {
                VrmExpressionData expression = expressions[i];
                Apply(expression, i + 1);

                if (!expression.IsBinary)
                {
                    continuous++;
                }

                if (expression.HasOverride)
                {
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "vrm.expression.override",
                        $"'{expression.Name}' {Describe(expression)} while it is worn, which is "
                        + "how VRM keeps a face from fighting its own blink and lip sync. Basis "
                        + "keeps blinking, gaze and lip sync running whatever choice is picked.");
                }
            }

            if (continuous > 0)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Approximated, "vrm.expression.continuous",
                    $"{continuous} expressions can be worn at any strength in VRM. A choice is "
                    + "worn fully or not at all.");
            }

            return plan;
        }

        /// <summary>"blocks blink and lip sync", from the three override fields.</summary>
        private static string Describe(VrmExpressionData expression)
        {
            List<string> blocked = new List<string>();
            List<string> attenuated = new List<string>();

            void Sort(VrmExpressionOverride value, string what)
            {
                if (value == VrmExpressionOverride.Block) blocked.Add(what);
                else if (value == VrmExpressionOverride.Blend) attenuated.Add(what);
            }

            Sort(expression.OverrideBlink, "blink");
            Sort(expression.OverrideLookAt, "gaze");
            Sort(expression.OverrideMouth, "lip sync");

            List<string> parts = new List<string>();
            if (blocked.Count > 0) parts.Add("blocks " + string.Join(" and ", blocked));
            if (attenuated.Count > 0) parts.Add("attenuates " + string.Join(" and ", attenuated));
            return string.Join(" and ", parts);
        }
    }
}
