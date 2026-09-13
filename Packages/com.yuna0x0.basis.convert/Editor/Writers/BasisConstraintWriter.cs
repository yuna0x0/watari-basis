using System.Collections.Generic;
using Basis.Scripts.BasisSdk.Constraints;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Writers
{
    /// <summary>A constraint plan with its transform references resolved.</summary>
    public sealed class ResolvedBasisConstraint
    {
        public BasisConstraintPlan Plan;
        public GameObject Host;
        public List<Transform> Sources = new List<Transform>();
        public Transform WorldUpObject;
    }

    /// <summary>
    /// Creates Basis constraint components from a plan.
    /// <para>
    /// Unlike jiggle rigs, every field here is public, so no SerializedObject is needed. Sources
    /// are the exception: the list is private and goes through <c>SetSources</c>.
    /// </para>
    /// <para>
    /// The at-rest pose comes from the plan, which read it from the source component. Basis's
    /// own CaptureRest would replace it with the transform's current local pose.
    /// </para>
    /// </summary>
    public static class BasisConstraintWriter
    {
        public static BasisConstraintBase Write(
            ResolvedBasisConstraint constraint, string undoName = "Convert constraint")
        {
            if (constraint?.Host == null)
            {
                throw new System.ArgumentException(
                    "A host GameObject is required", nameof(constraint));
            }

            BasisConstraintPlan plan = constraint.Plan;
            BasisConstraintBase component = AddComponent(constraint.Host, plan.Kind);
            Undo.SetCurrentGroupName(undoName);

            component.constraintActive = plan.Active;
            component.weight = plan.Weight;
            component.locked = plan.Locked;

            ApplyKindSpecific(component, constraint);

            List<BasisConstraintSourceEntry> sources = new List<BasisConstraintSourceEntry>();
            for (int i = 0; i < constraint.Sources.Count; i++)
            {
                Transform source = constraint.Sources[i];
                if (source == null)
                {
                    continue;
                }

                float weight = i < plan.Sources.Count ? plan.Sources[i].Weight : 1f;
                sources.Add(new BasisConstraintSourceEntry(source, weight));
            }

            component.SetSources(sources);

            // Parent constraints size their per-source offset arrays from the source count, so
            // the offsets can only be written once the sources are in place.
            if (component is BasisParentConstraint parent)
            {
                ApplyParentOffsets(parent, plan);
            }

            EditorUtility.SetDirty(component);

            return component;
        }

        private static BasisConstraintBase AddComponent(GameObject host, BasisConstraintKind kind)
        {
            return kind switch
            {
                BasisConstraintKind.Position => Undo.AddComponent<BasisPositionConstraint>(host),
                BasisConstraintKind.Rotation => Undo.AddComponent<BasisRotationConstraint>(host),
                BasisConstraintKind.Scale => Undo.AddComponent<BasisScaleConstraint>(host),
                BasisConstraintKind.Parent => Undo.AddComponent<BasisParentConstraint>(host),
                BasisConstraintKind.Aim => Undo.AddComponent<BasisAimConstraint>(host),
                _ => Undo.AddComponent<BasisLookAtConstraint>(host),
            };
        }

        private static void ApplyKindSpecific(
            BasisConstraintBase component, ResolvedBasisConstraint constraint)
        {
            BasisConstraintPlan plan = constraint.Plan;

            switch (component)
            {
                case BasisPositionConstraint position:
                    position.translationAtRest = plan.TranslationAtRest;
                    position.translationOffset = plan.TranslationOffset;
                    position.translationAxis = (BasisConstraintAxes)plan.TranslationAxis;
                    break;

                case BasisRotationConstraint rotation:
                    rotation.rotationAtRest = plan.RotationAtRest;
                    rotation.rotationOffset = plan.RotationOffset;
                    rotation.rotationAxis = (BasisConstraintAxes)plan.RotationAxis;
                    break;

                case BasisScaleConstraint scale:
                    scale.scaleAtRest = plan.ScaleAtRest;
                    scale.scaleOffset = plan.ScaleOffset;
                    scale.scalingAxis = (BasisConstraintAxes)plan.ScaleAxis;
                    break;

                case BasisParentConstraint parent:
                    parent.translationAtRest = plan.TranslationAtRest;
                    parent.rotationAtRest = plan.RotationAtRest;
                    parent.translationAxis = (BasisConstraintAxes)plan.TranslationAxis;
                    parent.rotationAxis = (BasisConstraintAxes)plan.RotationAxis;
                    break;

                case BasisAimConstraint aim:
                    aim.aimVector = plan.AimVector;
                    aim.upVector = plan.UpVector;
                    aim.worldUpType = (BasisConstraintWorldUp)plan.WorldUpType;
                    aim.worldUpObject = constraint.WorldUpObject;
                    aim.worldUpVector = plan.WorldUpVector;
                    aim.rotationAtRest = plan.RotationAtRest;
                    aim.rotationOffset = plan.RotationOffset;
                    aim.rotationAxis = (BasisConstraintAxes)plan.RotationAxis;
                    break;

                case BasisLookAtConstraint lookAt:
                    lookAt.roll = plan.Roll;
                    lookAt.useUpObject = plan.UseUpObject;
                    lookAt.worldUpObject = constraint.WorldUpObject;
                    lookAt.rotationAtRest = plan.RotationAtRest;
                    lookAt.rotationOffset = plan.RotationOffset;
                    break;
            }
        }

        private static void ApplyParentOffsets(
            BasisParentConstraint parent, BasisConstraintPlan plan)
        {
            int count = parent.sourceCount;
            Vector3[] translations = new Vector3[count];
            Vector3[] rotations = new Vector3[count];

            for (int i = 0; i < count && i < plan.Sources.Count; i++)
            {
                translations[i] = plan.Sources[i].PositionOffset;
                rotations[i] = plan.Sources[i].RotationOffset;
            }

            parent.translationOffsets = translations;
            parent.rotationOffsets = rotations;
        }
    }
}
