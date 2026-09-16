using System.Collections.Generic;

namespace yuna0x0.Basis.Convert.Sources
{
    public enum SourceComponentKind
    {
        Unknown = 0,
        VrcPhysBone,
        VrcPhysBoneCollider,
        VrcAvatarDescriptor,
        VrcPositionConstraint,
        VrcRotationConstraint,
        VrcAimConstraint,
        VrcParentConstraint,
        VrcScaleConstraint,
        VrcLookAtConstraint,
        VrcExpressionsMenu,
        VrcExpressionParameters,
        VrcPipelineManager,
        VrcContactReceiver,
        VrcContactSender,

        /// <summary>Scales named bones away in first person. Basis has the same component.</summary>
        VrcHeadChop,

        /// <summary>Fires a ray and sets animator parameters from the hit. Nothing in Basis does.</summary>
        VrcRaycast,

        // Instructions to VRChat's uploader: which avatar to build per platform, and impostor
        // generation. They carry no runtime behaviour and mean nothing under Basis.
        VrcPerPlatformOverrides,
        VrcAccessoryPerPlatformOverrides,
        VrcImpostorSettings,
        VrcImpostorEnvironment,
        DynamicBone,
        DynamicBoneCollider,
        DynamicBonePlaneCollider,

        // UniVRM, both formats. VRM 0.x puts a group of chains on one component; VRM 1.0 puts
        // parameters on each bone and lists the chains on the avatar's own component. The guids
        // were read from UniVRM's .meta files and are unchanged across its last several
        // releases. See agent/research/vrm-spring-bones.md.
        VrmSpringBone,
        VrmSpringBoneColliderGroup,
        VrmBlendShapeProxy,
        VrmFirstPerson,
        VrmMeta,

        /// <summary>
        /// Where a VRM 0.x avatar looks, in any of the forms UniVRM writes it. Basis drives gaze
        /// from the eye bones, so there is nothing to convert these into.
        /// </summary>
        VrmLookAt,

        /// <summary>UniVRM's record of the humanoid bone mapping, which Unity's own avatar holds.</summary>
        UniHumanoid,

        // VRM 1.0's node constraints.
        Vrm10RotationConstraint,
        Vrm10AimConstraint,
        Vrm10RollConstraint,
        Vrm10Instance,
        Vrm10SpringBoneJoint,
        Vrm10SpringBoneCollider,
        Vrm10SpringBoneColliderGroup,

        // Modular Avatar, all of it. Naming every component keeps them out of the unknown
        // script report, and lets each be reported for what it does on Basis. The GUIDs were
        // read from Modular Avatar's own .meta files, not derived.
        //
        // Rearranges the hierarchy or the meshes, which is platform-independent work that
        // Modular Avatar does on Basis as well as anywhere else.
        MaMergeArmature,
        MaBoneProxy,
        MaMeshSettings,
        MaBlendshapeSync,
        MaParameters,
        MaMoveTo,
        MaReplaceObject,
        MaScaleAdjuster,
        MaOutfitRoot,
        MaRemoveVertexColor,
        MaMeshCutter,
        MaFloorAdjuster,
        MaWorldScaleObject,
        MaPlatformFilter,
        // Mesh Cutter's vertex filters, and an editor-only helper for moving objects without
        // their children. Read from Modular Avatar 1.18.7's own .meta files. All of this is
        // mesh and hierarchy work Modular Avatar does on Basis as well.
        MaVertexFilterByAxis,
        MaVertexFilterByBone,
        MaVertexFilterByMask,
        MaVertexFilterByShape,
        MaVertexFilterByUVTile,
        MaMoveIndependently,
        MaConvertConstraints,

        // Builds menus or merges animator layers, which target structures only VRChat has.
        MaMenuItem,
        MaMenuInstaller,
        MaMenuGroup,
        MaMenuInstallTarget,
        MaMergeAnimator,
        MaMergeBlendTree,

        // Reacts to a menu item or an object's state by changing something else. Object Toggle
        // is rebuilt; the rest are reported.
        MaObjectToggle,
        MaShapeChanger,
        MaMaterialSetter,
        MaMaterialSwap,

        // Specific to VRChat's own systems, and inert on Basis.
        MaGlobalCollider,
        MaPBBlocker,
        MaVisibleHeadAccessory,
        MaWorldFixedObject,
        MaMmdLayerControl,
        MaSyncParameterSequence,
        MaRenameVRChatCollisionTags,
        MaVRChatSettings,

        // Third party authoring tools that carry no runtime behaviour of their own. Named so a
        // report says what they are rather than printing a guid.
        AvatarModifySupport,

        // VRCFury. Its data lives on one component type, as managed references, and is not read
        // by this version. A built copy can carry its debug markers; the original avatar never
        // does.
        VrcFuryComponent,
        VrcFuryBuildMarker,
    }

    /// <summary>
    /// Maps a MonoBehaviour's (script guid, script fileId) pair onto the component type it was,
    /// so components whose script is missing can still be identified.
    /// <para>
    /// Two shapes exist. A loose .cs script always has fileId 11500000 and is identified by its
    /// own guid. A type compiled into a DLL, which is how the VRChat SDK ships, is identified by
    /// the assembly's guid plus a fileId derived from the class name, so one guid covers many
    /// types.
    /// </para>
    /// <para>
    /// Values below were read off real assets written by VRChat SDK 3.10.3. Guids are stable in
    /// practice but are not a contract, which is why an unrecognised identity is reported rather
    /// than skipped: that is how this table is meant to grow.
    /// </para>
    /// </summary>
    public static class KnownScriptIdentities
    {
        public const long LooseScriptFileId = 11500000L;

        public const string GuidVrcPhysBoneAssembly = "2a2c05204084d904aa4945ccff20d8e5";
        public const string GuidVrcConstraintAssembly = "58e2f01a24261a14cb82e6d3399e8b16";
        public const string GuidVrcSdk3AAssembly = "67cc4cb7839cd3741b63733d5adf0442";
        public const string GuidVrcCoreEditorAssembly = "4ecd63eff847044b68db9453ce219299";
        public const string GuidVrcContactAssembly = "80f1b8067b0760e4bb45023bc2e9de66";

        private static readonly Dictionary<(string Guid, long FileId), SourceComponentKind> Table =
            new Dictionary<(string, long), SourceComponentKind>
            {
                { (GuidVrcPhysBoneAssembly, 1661641543L), SourceComponentKind.VrcPhysBone },
                { (GuidVrcPhysBoneAssembly, -1631200402L), SourceComponentKind.VrcPhysBoneCollider },

                { (GuidVrcConstraintAssembly, 1116338486L), SourceComponentKind.VrcPositionConstraint },
                { (GuidVrcConstraintAssembly, 1788371120L), SourceComponentKind.VrcRotationConstraint },
                { (GuidVrcConstraintAssembly, -926596935L), SourceComponentKind.VrcAimConstraint },

                // Computed rather than observed: the reference avatars do not use these three.
                // Unity derives a DLL type's fileID from "s\0\0\0" + namespace + name, hashed
                // with MD4, taking the first four bytes little-endian. Reproducing that for the
                // ten identities above, all of which were read off real assets, gives exactly the
                // values listed, so the same derivation is trusted for these.
                { (GuidVrcConstraintAssembly, 575728033L), SourceComponentKind.VrcParentConstraint },
                { (GuidVrcConstraintAssembly, 41250163L), SourceComponentKind.VrcScaleConstraint },
                { (GuidVrcConstraintAssembly, -372946275L), SourceComponentKind.VrcLookAtConstraint },

                { (GuidVrcSdk3AAssembly, 542108242L), SourceComponentKind.VrcAvatarDescriptor },
                { (GuidVrcSdk3AAssembly, -340790334L), SourceComponentKind.VrcExpressionsMenu },
                { (GuidVrcSdk3AAssembly, -1506855854L), SourceComponentKind.VrcExpressionParameters },

                { (GuidVrcCoreEditorAssembly, -1427037861L), SourceComponentKind.VrcPipelineManager },

                { (GuidVrcContactAssembly, -1450912254L), SourceComponentKind.VrcContactReceiver },
                { (GuidVrcContactAssembly, -802764141L), SourceComponentKind.VrcContactSender },

                // Derived the same way as the constraints above, from the class names in SDK
                // 3.10.5. The per-platform components are loose scripts in the avatars package
                // rather than DLL types, so they carry their own .meta guids.
                { (GuidVrcSdk3AAssembly, -1888410255L), SourceComponentKind.VrcHeadChop },
                { (GuidVrcSdk3AAssembly, 1472509199L), SourceComponentKind.VrcRaycast },
                { (GuidVrcSdk3AAssembly, 798808286L), SourceComponentKind.VrcImpostorSettings },
                { (GuidVrcSdk3AAssembly, 306702890L), SourceComponentKind.VrcImpostorEnvironment },
                { ("45da21a324e147228aaee066e399bff0", LooseScriptFileId), SourceComponentKind.VrcPerPlatformOverrides },
                { ("8a12ddb63afae28468db699a7e0cb228", LooseScriptFileId), SourceComponentKind.VrcAccessoryPerPlatformOverrides },

                { ("f9ac8d30c6a0d9642a11e5be4c440740", LooseScriptFileId), SourceComponentKind.DynamicBone },
                { ("baedd976e12657241bf7ff2d1c685342", LooseScriptFileId), SourceComponentKind.DynamicBoneCollider },
                { ("4e535bdf3689369408cc4d078260ef6a", LooseScriptFileId), SourceComponentKind.DynamicBonePlaneCollider },

                // UniVRM, read from its own .meta files in the package cache.
                { ("00ea06e1753e16f4ca870c39c067c86b", LooseScriptFileId), SourceComponentKind.VrmSpringBone },
                { ("646b65a4a57afd34d8c4ed557efb46a5", LooseScriptFileId), SourceComponentKind.VrmSpringBoneColliderGroup },
                { ("5b678c1df50cfb547990db24a32856da", LooseScriptFileId), SourceComponentKind.VrmBlendShapeProxy },
                { ("dedba1309bdf12b42af2362f52eea134", LooseScriptFileId), SourceComponentKind.VrmFirstPerson },
                { ("690ea0146224b8b4694a1925dddeb352", LooseScriptFileId), SourceComponentKind.VrmMeta },

                // VRM 0.x writes its look at as a head component plus whichever applyer the
                // author chose, bone or blendshape.
                { ("e0a1a470564f16f4f94acb4b9ef56367", LooseScriptFileId), SourceComponentKind.VrmLookAt },
                { ("04a3e59a0190f1647892e3709c075845", LooseScriptFileId), SourceComponentKind.VrmLookAt },
                { ("a8b72334adf6f7948bd98b4f0a873949", LooseScriptFileId), SourceComponentKind.VrmLookAt },
                { ("845471fb50db3cd4aa2f7a3fae3cc3a4", LooseScriptFileId), SourceComponentKind.VrmLookAt },
                { ("97a39af5b64ede64e86b92b5bf94a0e7", LooseScriptFileId), SourceComponentKind.UniHumanoid },

                // VRM 0.x's own record of the same thing.
                { ("3869812175467a143ab9cd865752b4a9", LooseScriptFileId), SourceComponentKind.UniHumanoid },
                { ("7a07fbecedce41b4396f286fd7634e1d", LooseScriptFileId), SourceComponentKind.Vrm10RotationConstraint },
                { ("37b0507e4ae49724898ca17cc3db6f1a", LooseScriptFileId), SourceComponentKind.Vrm10AimConstraint },
                { ("1e864293edac89b40b9f79c23e7aa547", LooseScriptFileId), SourceComponentKind.Vrm10RollConstraint },
                { ("bfba4ccd3f854e64f868ce83553071a9", LooseScriptFileId), SourceComponentKind.Vrm10Instance },
                { ("0a942e03b39600e41a1b161e958048f7", LooseScriptFileId), SourceComponentKind.Vrm10SpringBoneJoint },
                { ("35bfb658269b2af478e501de243deda6", LooseScriptFileId), SourceComponentKind.Vrm10SpringBoneCollider },
                { ("177ea458e237fee41b0902e3006c744b", LooseScriptFileId), SourceComponentKind.Vrm10SpringBoneColliderGroup },

                // Modular Avatar, read off clothing prefabs in the reference library and
                // identified by their serialized fields rather than by a published list.
                { ("2df373bf91cf30b4bbd495e11cb1a2ec", LooseScriptFileId), SourceComponentKind.MaMergeArmature },
                { ("42581d8044b64899834d3d515ab3a144", LooseScriptFileId), SourceComponentKind.MaBoneProxy },
                { ("d9e94e501a2d4c95bff3d5601013d923", LooseScriptFileId), SourceComponentKind.VrcFuryComponent },
                { ("19d6be1140c9472cbc89e515ffd74126", LooseScriptFileId), SourceComponentKind.VrcFuryBuildMarker },
                { ("560fdafd46c74b2db6422fdf0e7f2363", LooseScriptFileId), SourceComponentKind.MaMeshSettings },
                { ("6fd7cab7d93b403280f2f9da978d8a4f", LooseScriptFileId), SourceComponentKind.MaBlendshapeSync },
                { ("71a96d4ea0c344f39e277d82035bf9bd", LooseScriptFileId), SourceComponentKind.MaParameters },
                { ("4e6bb6a99e499d2489ccf296662fa3cd", LooseScriptFileId), SourceComponentKind.MaMoveTo },
                { ("7e949680c0864ee7b441d9b2c93b890b", LooseScriptFileId), SourceComponentKind.MaReplaceObject },
                { ("09a660aa9d4e47d992adcac5a05dd808", LooseScriptFileId), SourceComponentKind.MaScaleAdjuster },
                { ("1895bf16884f4064f8e9550e7493c205", LooseScriptFileId), SourceComponentKind.MaOutfitRoot },
                { ("dc5f8bfae24244aeaedcd6c2bb7264f9", LooseScriptFileId), SourceComponentKind.MaRemoveVertexColor },
                { ("762726b8618cac7419e39bdc2b572b3d", LooseScriptFileId), SourceComponentKind.MaMeshCutter },
                { ("ba18e6eae93342fd8774b3f3f132928a", LooseScriptFileId), SourceComponentKind.MaFloorAdjuster },
                { ("e113c01563a14226b5e863befe6fe769", LooseScriptFileId), SourceComponentKind.MaWorldScaleObject },
                { ("8c8a67d5c01849629fa90c3b2eded93f", LooseScriptFileId), SourceComponentKind.MaPlatformFilter },
                { ("e362b3df8a3d478c82bf5ffe18f622e6", LooseScriptFileId), SourceComponentKind.MaConvertConstraints },
                { ("660848d04d7443b5b6fcfb627e6be5ea", LooseScriptFileId), SourceComponentKind.MaVertexFilterByAxis },
                { ("f8e2c9a1b3d44c6d9a7e5f2c1b8d3e4f", LooseScriptFileId), SourceComponentKind.MaVertexFilterByBone },
                { ("96a7b00b1dae4a02b61b29bf02241063", LooseScriptFileId), SourceComponentKind.MaVertexFilterByMask },
                { ("da7788c69fae9ff4abae088a0dc92c5b", LooseScriptFileId), SourceComponentKind.MaVertexFilterByShape },
                { ("8c38d6a064dbe9b91f24ee30e85c3c4f", LooseScriptFileId), SourceComponentKind.MaVertexFilterByUVTile },
                { ("a8d5b07828ba4eefb9acc305478369d0", LooseScriptFileId), SourceComponentKind.MaMoveIndependently },

                { ("3b29d45007c5493d926d2cd45a489529", LooseScriptFileId), SourceComponentKind.MaMenuItem },
                { ("7ef83cb0c23d4d7c9d41021e544a1978", LooseScriptFileId), SourceComponentKind.MaMenuInstaller },
                { ("97e46a47dd8a425eb4ce9411defe313d", LooseScriptFileId), SourceComponentKind.MaMenuGroup },
                { ("1fad1419b52a42ae89b0df52eb861e47", LooseScriptFileId), SourceComponentKind.MaMenuInstallTarget },
                { ("1bb122659f724ebf85fe095ac02dc339", LooseScriptFileId), SourceComponentKind.MaMergeAnimator },
                { ("229dd561ca024a6588e388160921a70f", LooseScriptFileId), SourceComponentKind.MaMergeBlendTree },

                { ("a162bb8ec7e24a5abcf457887f1df3fa", LooseScriptFileId), SourceComponentKind.MaObjectToggle },
                { ("2db441f589c3407bb6fb5f02ff8ab541", LooseScriptFileId), SourceComponentKind.MaShapeChanger },
                { ("0adf335711644e34b6c635e94ae61fa7", LooseScriptFileId), SourceComponentKind.MaMaterialSetter },
                { ("b259b73280ead4e4fbbdafc5e29175d1", LooseScriptFileId), SourceComponentKind.MaMaterialSwap },

                { ("49bb23f95a7baca4186efa68bc5891b6", LooseScriptFileId), SourceComponentKind.MaGlobalCollider },
                { ("a5bf908a199a4648845ebe2fd3b5a4bd", LooseScriptFileId), SourceComponentKind.MaPBBlocker },
                { ("33dac8cfeaeb4c399ddd90597f849f70", LooseScriptFileId), SourceComponentKind.MaVisibleHeadAccessory },
                { ("0e2d9f1d69e34b92a96e6cc162770fad", LooseScriptFileId), SourceComponentKind.MaWorldFixedObject },
                { ("d1d979d3cedd4ddd969f414e2ea04fb8", LooseScriptFileId), SourceComponentKind.MaMmdLayerControl },
                { ("934543afe4744213b5621aa13a67e3b4", LooseScriptFileId), SourceComponentKind.MaSyncParameterSequence },
                { ("04802bf95b218724a9f4b97003067857", LooseScriptFileId), SourceComponentKind.MaRenameVRChatCollisionTags },
                { ("89c938d7d8a741df99f2eda501b3a6fe", LooseScriptFileId), SourceComponentKind.MaVRChatSettings },

                // Avatar Modify Support. Its behaviour implements VRChat's IEditorOnly and holds
                // nothing but exported blendshape and colour preset data for its own editor
                // window, so it has no runtime behaviour to carry over.
                { ("2efed2fd1a16dec49b4612e16363e973", LooseScriptFileId), SourceComponentKind.AvatarModifySupport },
            };

        /// <summary>VRChat's own build settings, which have nothing to convert and lose nothing.</summary>
        public static bool IsVrcBuildSetting(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.VrcPerPlatformOverrides
                && kind <= SourceComponentKind.VrcImpostorEnvironment;
        }

        /// <summary>VRM 1.0's node constraints, which are read for counting but not converted.</summary>
        public static bool IsVrmConstraint(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.Vrm10RotationConstraint
                && kind <= SourceComponentKind.Vrm10RollConstraint;
        }

        /// <summary>
        /// True for the Modular Avatar components that do their job on Basis, which are the ones
        /// that only rearrange the hierarchy. Nothing needs converting for these.
        /// </summary>
        public static bool IsHandledByModularAvatar(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.MaMergeArmature
                && kind <= SourceComponentKind.MaConvertConstraints;
        }

        /// <summary>
        /// Modular Avatar components that build menus or merge animator layers. Both target
        /// structures only VRChat has, so what they add does nothing on Basis unless it is
        /// rebuilt.
        /// </summary>
        public static bool IsModularAvatarMenuOrAnimator(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.MaMenuItem
                && kind <= SourceComponentKind.MaMergeBlendTree;
        }

        /// <summary>
        /// Modular Avatar components that react to a menu item or an object's state. Object
        /// Toggle is rebuilt as a Vixxy control; the others are reported.
        /// </summary>
        public static bool IsModularAvatarReactive(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.MaObjectToggle
                && kind <= SourceComponentKind.MaMaterialSwap;
        }

        /// <summary>
        /// Modular Avatar components tied to VRChat's own systems, which have nothing to act on
        /// under Basis.
        /// </summary>
        public static bool IsModularAvatarVrchatOnly(SourceComponentKind kind)
        {
            return kind >= SourceComponentKind.MaGlobalCollider
                && kind <= SourceComponentKind.MaVRChatSettings;
        }

        /// <summary>
        /// Third party editor-time tools whose components carry no runtime behaviour. Nothing is
        /// converted for these, and nothing is lost by not converting them; they are named so a
        /// report can say which tool a component belongs to.
        /// </summary>
        public static bool IsEditorOnlyAuthoringTool(SourceComponentKind kind)
        {
            return kind == SourceComponentKind.AvatarModifySupport;
        }

        public static SourceComponentKind Resolve(string guid, long fileId)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return SourceComponentKind.Unknown;
            }

            return Table.TryGetValue((guid, fileId), out SourceComponentKind kind)
                ? kind
                : SourceComponentKind.Unknown;
        }
    }
}
