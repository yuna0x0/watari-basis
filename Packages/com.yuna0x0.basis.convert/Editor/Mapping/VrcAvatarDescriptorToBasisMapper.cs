using System.Collections.Generic;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// Turns a VRCAvatarDescriptor into what a `BasisAvatar` needs.
    /// <para>
    /// The viseme lists line up exactly: both systems keep fifteen blendshapes in the order
    /// sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou, so the mapping is positional
    /// rather than by name.
    /// </para>
    /// <para>
    /// The rest of the descriptor, expression menus, parameters and the playable animation
    /// layers, has no counterpart in Basis at all and is not touched here.
    /// </para>
    /// </summary>
    public static class VrcAvatarDescriptorToBasisMapper
    {
        public const int VisemeCount = 15;

        public static BasisAvatarPlan Map(VrcAvatarDescriptorData source)
        {
            BasisAvatarPlan plan = new BasisAvatarPlan
            {
                SourceDocumentFileId = source.DocumentFileId,
                AvatarRootFileId = source.OwnerGameObjectFileId,

                // Basis keeps height above the root and forward offset; VRChat's sideways
                // component has nowhere to go, and is zero on a symmetric avatar anyway.
                EyePosition = new Vector2(source.ViewPosition.y, source.ViewPosition.z),
            };

            if (!Mathf.Approximately(source.ViewPosition.x, 0f))
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.viewPosition.sideways",
                    $"The view position was offset sideways by {source.ViewPosition.x}. Basis "
                    + "stores only height and forward offset, so that part was dropped.");
            }

            MapVisemes(source, plan);
            MapBlink(source, plan);
            ReportExpressionSystems(source, plan);

            plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.autoSetup",
                "Animator, human scale, renderers and mouth position are filled in by Basis when the Basis Avatar inspector first opens. Values already set are kept.");

            return plan;
        }

        /// <summary>
        /// Basis has no playable animation layers and no expression menu format, so none of this
        /// converts. It is reported anyway: an avatar's toggles and gestures are most of what
        /// its owner notices, and a report that stays silent about them reads as though nothing
        /// was lost.
        /// </summary>
        private static void ReportExpressionSystems(
            VrcAvatarDescriptorData source, BasisAvatarPlan plan)
        {
            if (source.HasExpressionsMenu)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.expressionsMenu",
                    "The avatar has an expression menu. Basis has no equivalent format; its "
                    + "in-app avatar menu is built from HVR Vixxy components, which have to be "
                    + "authored by hand.");
            }

            if (source.HasExpressionParameters)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.expressionParameters",
                    "The avatar has expression parameters. Basis has no synced parameter list; "
                    + "Vixxy controls carry their own state instead.");
            }

            if (source.AnimationLayers.Count == 0)
            {
                return;
            }

            List<string> names = new List<string>();
            foreach (VrcAnimationLayerEntry layer in source.AnimationLayers)
            {
                names.Add(layer.Layer.ToString());
            }

            plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.animationLayers",
                $"{names.Count} custom animation layers (" + string.Join(", ", names) + ") do not carry over; Basis has no playable layers. Toggles become Vixxy controls, looping movement Authored Motion.");
        }

        private static void MapVisemes(VrcAvatarDescriptorData source, BasisAvatarPlan plan)
        {
            if (source.LipSync != VrcLipSyncStyle.VisemeBlendShape)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.lipSync.unsupported",
                    $"Lip sync was set to {source.LipSync}. Basis drives visemes from blendshapes "
                    + "only, so nothing was carried over and the avatar will not lip sync until "
                    + "viseme blendshapes are assigned by hand.");
                return;
            }

            plan.VisemeMeshFileId = source.VisemeSkinnedMeshFileId;

            if (source.VisemeSkinnedMeshFileId == 0L)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "descriptor.visemeMesh.missing",
                    "Lip sync was set to blendshapes but no mesh was assigned to drive them.");
                return;
            }

            for (int i = 0; i < VisemeCount; i++)
            {
                plan.VisemeBlendShapeNames.Add(
                    i < source.VisemeBlendShapes.Count ? source.VisemeBlendShapes[i] : string.Empty);
            }

            if (source.VisemeBlendShapes.Count != VisemeCount)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Warning, "descriptor.visemes.count",
                    $"The avatar listed {source.VisemeBlendShapes.Count} viseme blendshapes "
                    + $"rather than {VisemeCount}. The missing ones were left unset.");
            }
            else
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.visemes",
                    "All fifteen visemes carried across. Both systems keep them in the same "
                    + "order, so they map position for position.");
            }
        }

        private static void MapBlink(VrcAvatarDescriptorData source, BasisAvatarPlan plan)
        {
            // Enable Eye Look is the master switch. The SDK keeps the eyelid settings serialized
            // when it is off, so they are not evidence of a blink.
            if (!source.EnableEyeLook)
            {
                plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.eyeLook.disabled",
                    "Eye Look was disabled, so the avatar did not blink. Blink was left unset.");
                return;
            }

            switch (source.EyelidType)
            {
                case VrcEyelidType.Blendshapes:
                    // The array is blink, looking up, looking down, -1 for unset. Basis has
                    // blink only.
                    int blink = source.EyelidsBlendshapes.Count > 0 ? source.EyelidsBlendshapes[0] : -1;
                    if (blink >= 0)
                    {
                        plan.BlinkMeshFileId = source.EyelidsSkinnedMeshFileId;
                        plan.BlinkBlendShapeIndices.Add(blink);
                    }
                    else
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.eyelids.none",
                            "Eyelids were set to blendshapes but no blink shape was chosen, so "
                            + "blink was left unset.");
                    }

                    bool lookShapes = false;
                    for (int i = 1; i < source.EyelidsBlendshapes.Count; i++)
                    {
                        lookShapes |= source.EyelidsBlendshapes[i] >= 0;
                    }

                    if (lookShapes)
                    {
                        plan.Diagnostics.Add(DiagnosticSeverity.Dropped,
                            "descriptor.eyelids.lookUpDown",
                            "The looking up and looking down eyelid blendshapes were dropped. "
                            + "Basis drives blink only, and moves the eyes with bones.");
                    }

                    break;

                case VrcEyelidType.Bones:
                    plan.Diagnostics.Add(DiagnosticSeverity.Dropped, "descriptor.eyelids.bones",
                        "Eyelids were driven by bones. Basis blinks with a blendshape only, so "
                        + "this was not carried over.");
                    break;

                default:
                    plan.Diagnostics.Add(DiagnosticSeverity.Mapped, "descriptor.eyelids.none",
                        "The avatar had no eyelid setup, so blinking was left unset.");
                    break;
            }
        }
    }
}
