using System.Collections.Generic;
using System.IO;
using Basis.Scripts.BasisSdk.Constraints;
using GatorDragonGames.JigglePhysics;
using HVR.Vixxy;
using UnityEditor;
using UnityEngine;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;
using yuna0x0.Basis.Convert.Reporting;
using yuna0x0.Basis.Convert.Rig;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.UI
{
    /// <summary>
    /// Scan an avatar, show what a conversion would produce, then convert.
    /// <para>
    /// Nothing is written until Convert is pressed, and what it writes is one undo step.
    /// </para>
    /// </summary>
    public sealed class AvatarConversionWindow : EditorWindow
    {
        private GameObject _target;
        private string _sourceAssetPath;
        private string _blocker;

        private AvatarConversionPlan _plan;
        private ConversionResult _result;
        private List<DiagnosticGroup> _groups;

        private Vector2 _scroll;
        private bool _showOptions = true;
        private bool _showSources = true;
        private bool _showRigs = true;
        private bool _showConstraints;
        private bool _showToggles;
        private bool _showMotions;
        private bool _showDiagnostics = true;
        private bool _showTuning;
        private bool _showRig = true;

        /// <summary>
        /// Per item control and the tuning weights, hidden by default. The common case is the
        /// handful of checkboxes above them.
        /// </summary>
        private bool _advanced;

        /// <summary>
        /// Which kinds of thing to write. Held by the window rather than by the plan so a choice
        /// survives rescanning, and remembered between sessions.
        /// </summary>
        private readonly ConversionOptions _options = new ConversionOptions();

        private const string PrefsPrefix = "yuna0x0.basis.convert.options.";

        /// <summary>
        /// The two parts of the mapping that are judgement calls rather than conversions.
        /// Exposed so they can be adjusted and rescanned without editing code.
        /// </summary>
        private readonly JiggleMappingProfile _profile = JiggleMappingProfile.Default;

        [MenuItem(ProductInfo.ToolsMenu + "Convert Avatar to Basis")]
        public static void Open()
        {
            AvatarConversionWindow window = GetWindow<AvatarConversionWindow>();
            window.titleContent = new GUIContent(ProductInfo.Name);
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        [MenuItem(ProductInfo.GameObjectMenu + "Convert Avatar to Basis", false, 30)]
        private static void OpenFromHierarchy(MenuCommand command)
        {
            Open();
            if (command.context is GameObject selected)
            {
                GetWindow<AvatarConversionWindow>().SetTarget(selected);
            }
        }

        private void OnEnable()
        {
            LoadOptions();

            if (_target == null && Selection.activeGameObject != null)
            {
                SetTarget(Selection.activeGameObject);
            }
        }

        private void LoadOptions()
        {
            _advanced = EditorPrefs.GetBool(PrefsPrefix + "advanced", false);
            _options.Physics = EditorPrefs.GetBool(PrefsPrefix + "physics", true);
            _options.Colliders = EditorPrefs.GetBool(PrefsPrefix + "colliders", true);
            _options.Constraints = EditorPrefs.GetBool(PrefsPrefix + "constraints", true);
            _options.Descriptor = EditorPrefs.GetBool(PrefsPrefix + "descriptor", true);
            _options.Toggles = EditorPrefs.GetBool(PrefsPrefix + "toggles", true);
            _options.Motion = EditorPrefs.GetBool(PrefsPrefix + "motion", true);
        }

        private void SaveOptions()
        {
            EditorPrefs.SetBool(PrefsPrefix + "advanced", _advanced);
            EditorPrefs.SetBool(PrefsPrefix + "physics", _options.Physics);
            EditorPrefs.SetBool(PrefsPrefix + "colliders", _options.Colliders);
            EditorPrefs.SetBool(PrefsPrefix + "constraints", _options.Constraints);
            EditorPrefs.SetBool(PrefsPrefix + "descriptor", _options.Descriptor);
            EditorPrefs.SetBool(PrefsPrefix + "toggles", _options.Toggles);
            EditorPrefs.SetBool(PrefsPrefix + "motion", _options.Motion);
        }

        public void SetTarget(GameObject target)
        {
            _target = target;
            Rescan();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Avatar to Basis", EditorStyles.boldLabel);
            WrappedLabel("Reads the source prefab. The source SDK does not need to be installed.");

            EditorGUILayout.Space();

            using (EditorGUI.ChangeCheckScope changed = new EditorGUI.ChangeCheckScope())
            {
                _target = (GameObject)EditorGUILayout.ObjectField(
                    "Avatar", _target, typeof(GameObject), true);

                if (changed.changed)
                {
                    Rescan();
                }
            }

            if (!string.IsNullOrEmpty(_blocker))
            {
                EditorGUILayout.HelpBox(_blocker, MessageType.Info);
                return;
            }

            if (_plan == null)
            {
                return;
            }

            DrawSummary();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Narrowing the conversion narrows what is reported with it, so the diagnostics are
            // regrouped whenever the selection changes rather than only on a rescan.
            using (EditorGUI.ChangeCheckScope changed = new EditorGUI.ChangeCheckScope())
            {
                DrawOptions();
                DrawSources();
                DrawRig();
                DrawDiagnostics();
                DrawRigs();
                DrawConstraints();
                DrawToggles();
                DrawAuthoredMotions();
                DrawTuning();

                if (changed.changed)
                {
                    _groups = ConversionReport.Group(_plan);
                }
            }

            EditorGUILayout.EndScrollView();

            DrawActions();

            EditorGUILayout.Space(2f);
            WrappedLabel(
                $"{ProductInfo.Name} {ProductInfo.Version}. Sources read against "
                + $"{ProductInfo.CheckedAgainst}.", EditorStyles.miniLabel);
        }

        private void DrawSummary()
        {
            EditorGUILayout.Space();

            WrappedField("Detected", _plan.Profile.Kind, boldValue: true);
            WrappedField(" ", string.Join(", ", _plan.Profile.Signals()));

            if (_plan.Sources.Count > 1)
            {
                WrappedField("Read from", $"{_plan.Sources.Count} prefabs: {SourceNames()}");
            }

            // A VRM states who may wear it and what may be done to it. Converting changes the
            // avatar, so the licence goes above the summary rather than in the report alone.
            if (_plan.VrmMeta != null && _plan.VrmMeta.HasAnything)
            {
                bool restricted = _plan.VrmMeta.ForbidsModification
                    || _plan.VrmMeta.AvatarPermission == VrmAvatarPermission.OnlyAuthor;

                EditorGUILayout.HelpBox(
                    _plan.VrmMeta.Describe()
                    + "\n" + string.Join("\n", _plan.VrmMeta.Permissions())
                    + (restricted
                        ? "\n\nConversion modifies the avatar. Check the licence permits it."
                        : string.Empty),
                    restricted ? MessageType.Warning : MessageType.Info);

                EditorGUILayout.Space(2f);
            }

            if (_plan.Profile.LooksInconsistent)
            {
                EditorGUILayout.HelpBox(
                    "Humanoid rig, nothing convertible. An avatar whose physics sits on a child "
                    + "prefab looks like this; check the selected object.", MessageType.Warning);
            }

            EditorGUILayout.Space(2f);

            int warnings = CountOf(DiagnosticSeverity.Warning);
            int dropped = CountOf(DiagnosticSeverity.Dropped);
            int approximated = CountOf(DiagnosticSeverity.Approximated);

            string summary = $"Found: {_plan.PhysBonesFound} PhysBones, "
                + $"{_plan.DynamicBonesFound} Dynamic Bones, "
                + $"{_plan.VrmChainsFound} VRM spring chains, {_plan.CollidersFound} colliders, "
                + $"{_plan.ConstraintsFound} constraints.\n"
                + $"Writes: {_plan.SelectedRigCount} jiggle rigs, "
                + $"{_plan.SelectedConstraintCount} Basis constraints, "
                + $"{_plan.SelectedVixxyControlCount} Vixxy controls, "
                + $"{_plan.SelectedAuthoredMotionCount} authored motions"
                + (_plan.SelectedHeadChopCount > 0
                    ? $", {_plan.SelectedHeadChopCount} head chops"
                    : "")
                + (_plan.DescriptorSelected ? ", Basis Avatar." : ".");

            EditorGUILayout.HelpBox(summary,
                _plan.TotalSelected > 0 ? MessageType.Info : MessageType.Warning);

            if (_plan.Unresolved > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{_plan.Unresolved} PhysBones have no resolvable root bone and are skipped.",
                    MessageType.Warning);
            }

            if (warnings > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{warnings} warnings. See Diagnostics.", MessageType.Warning);
            }

            if (dropped + approximated > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{dropped} settings dropped, {approximated} approximated. See Diagnostics.",
                    MessageType.Info);
            }

        }

        /// <summary>
        /// The prefab names for the summary line, capped. This sits above the scroll view, so an
        /// avatar built from a hundred prefabs would otherwise push the rest of the window out of
        /// reach. The Prefabs list below names every one of them, and the same cap is used in the
        /// report.
        /// </summary>
        private string SourceNames()
        {
            const int limit = 6;
            List<string> names = new List<string>();

            foreach (ConversionSource source in _plan.Sources)
            {
                if (!names.Contains(source.Name))
                {
                    names.Add(source.Name);
                }
            }

            if (names.Count <= limit)
            {
                return string.Join(", ", names);
            }

            return string.Join(", ", names.GetRange(0, limit))
                + $" and {names.Count - limit} more";
        }

        private int CountOf(DiagnosticSeverity severity)
        {
            int count = 0;
            foreach (DiagnosticGroup group in _groups)
            {
                if (group.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }



        /// <summary>
        /// Which parts of the plan get written.
        /// <para>
        /// Basic is the kinds of thing a conversion produces. Advanced adds the per item lists
        /// and the tuning weights, so narrowing a conversion to a single bone or toggle is
        /// possible without that being in the way of the common case.
        /// </para>
        /// </summary>
        private void DrawOptions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showOptions = EditorGUILayout.Foldout(_showOptions, "Targets", true);

                using (EditorGUI.ChangeCheckScope changed = new EditorGUI.ChangeCheckScope())
                {
                    _advanced = GUILayout.Toggle(_advanced, "Advanced", EditorStyles.miniButton,
                        GUILayout.Width(72f));

                    if (changed.changed)
                    {
                        SaveOptions();
                    }
                }
            }

            if (!_showOptions)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (EditorGUI.ChangeCheckScope changed = new EditorGUI.ChangeCheckScope())
            {
                _options.Physics = Category("Physics", _options.Physics,
                    Tally(_plan.SelectedRigCount, _plan.Rigs.Count, "jiggle rigs"),
                    _plan.Rigs.Count > 0);

                if (_advanced)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _options.Colliders = Category("Colliders", _options.Colliders,
                            $"{_plan.Colliders.Count} colliders",
                            _options.Physics && _plan.Colliders.Count > 0);
                    }
                }

                _options.Constraints = Category("Constraints", _options.Constraints,
                    Tally(_plan.SelectedConstraintCount, _plan.Constraints.Count,
                        "Basis constraints"),
                    _plan.Constraints.Count > 0);

                _options.Descriptor = Category("Avatar descriptor", _options.Descriptor,
                    _plan.Descriptor != null
                        ? "view position, visemes, blink"
                        : "none found",
                    _plan.Descriptor != null);

                _options.Toggles = Category("Menu toggles", _options.Toggles,
                    Tally(_plan.SelectedVixxyControlCount, _plan.VixxyControls.Count,
                        "Vixxy controls"),
                    _plan.VixxyControls.Count > 0);

                _options.Motion = Category("Authored motion", _options.Motion,
                    Tally(_plan.SelectedAuthoredMotionCount, _plan.AuthoredMotions.Count,
                        "motions"),
                    _plan.AuthoredMotions.Count > 0);

                if (changed.changed)
                {
                    SaveOptions();
                }
            }

            if (_advanced)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                // Colliders only appear under Advanced, so say so here rather than let the
                // setting act on a conversion from somewhere the reader cannot see it.
                if (!_options.Colliders)
                {
                    WrappedLabel("Colliders are off (Advanced). Rigs are written without them.");
                }
            }
        }

        /// <summary>
        /// The prefabs found under the chosen object, one per row, so a prop parented onto an
        /// avatar can be left out of a conversion of it. Only shown when there is more than one,
        /// since a bare avatar has nothing to choose between.
        /// </summary>
        private void DrawSources()
        {
            if (!_advanced || _plan.Sources.Count < 2)
            {
                return;
            }

            EditorGUILayout.Space();
            _showSources = EditorGUILayout.Foldout(_showSources,
                $"Prefabs ({Tally(IncludedSources(), _plan.Sources.Count, "selected")})", true);
            if (!_showSources)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawSelectAll(include =>
                {
                    foreach (ConversionSource source in _plan.Sources)
                    {
                        source.Include = include;
                    }
                });

                int quiet = 0;

                foreach (ConversionSource source in _plan.Sources)
                {
                    // A gimmick pack nests a dozen prefabs that hold nothing this converts.
                    // Listing them buries the ones worth a decision.
                    string detail = Describe(source);
                    if (detail == null)
                    {
                        quiet++;
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        source.Include = EditorGUILayout.Toggle(source.Include,
                            GUILayout.Width(24f));

                        if (GUILayout.Button(source.Name, EditorStyles.linkLabel,
                                GUILayout.MinWidth(120f)))
                        {
                            EditorGUIUtility.PingObject(source.Root);
                        }

                        GUILayout.Label(detail, EditorStyles.wordWrappedLabel);
                    }
                }

                if (quiet > 0)
                {
                    WrappedLabel($"{quiet} more with nothing to convert.");
                }
            }
        }

        private int IncludedSources()
        {
            int included = 0;
            foreach (ConversionSource source in _plan.Sources)
            {
                if (source.Include)
                {
                    included++;
                }
            }

            return included;
        }

        /// <summary>
        /// What one prefab contributes, so leaving it out is an informed choice, or null when it
        /// contributes nothing.
        /// </summary>
        private string Describe(ConversionSource source)
        {
            int rigs = 0;
            int constraints = 0;

            foreach (PlannedJiggleRig rig in _plan.Rigs)
            {
                if (rig.Source == source)
                {
                    rigs++;
                }
            }

            foreach (PlannedConstraint constraint in _plan.Constraints)
            {
                if (constraint.Source == source)
                {
                    constraints++;
                }
            }

            List<string> parts = new List<string>();
            if (rigs > 0)
            {
                parts.Add($"{rigs} rigs");
            }

            if (constraints > 0)
            {
                parts.Add($"{constraints} constraints");
            }

            if (_plan.Descriptor != null && _plan.Descriptor.Source == source)
            {
                parts.Add("avatar descriptor");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// One category row: what it is on the left, how much of it there is on the right.
        /// Disabled when the avatar has none of that kind, so an unchecked box always means a
        /// choice rather than an absence.
        /// </summary>
        private static bool Category(string label, bool value, string detail, bool available)
        {
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!available))
            {
                bool result = EditorGUILayout.ToggleLeft(label, value && available,
                    GUILayout.Width(170f));
                GUILayout.Label(detail, EditorStyles.wordWrappedLabel);
                return available ? result : value;
            }
        }

        /// <summary>
        /// A prefix and a value that wraps. `EditorGUILayout.LabelField` sizes its rect to one
        /// line whatever the style, so a long value is clipped rather than wrapped;
        /// `GUILayout.Label` sizes to the text at the width it is given.
        /// </summary>
        private static void WrappedField(string label, string value, bool boldValue = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
                GUILayout.Label(value, boldValue ? BoldWrapped : EditorStyles.wordWrappedLabel);
            }
        }

        /// <summary>
        /// A paragraph that wraps. `EditorGUILayout.LabelField` sizes its rect to one line
        /// whatever the style, so anything longer than the window is cut off; `GUILayout.Label`
        /// sizes to the text but ignores the indent level, so the indent is drawn as a space.
        /// </summary>
        private static void WrappedLabel(string text, GUIStyle style = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (EditorGUI.indentLevel > 0)
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                }

                GUILayout.Label(text, Wrapping(style));
            }
        }

        private static readonly Dictionary<GUIStyle, GUIStyle> WrappingStyles =
            new Dictionary<GUIStyle, GUIStyle>();

        /// <summary>
        /// The style with word wrap on. Most built-in styles have it off, and a label handed one
        /// of those is cut at the window edge whatever the helper around it is called, so the
        /// helper never trusts the style it is given.
        /// </summary>
        private static GUIStyle Wrapping(GUIStyle style)
        {
            if (style == null)
            {
                return EditorStyles.wordWrappedLabel;
            }

            if (style.wordWrap)
            {
                return style;
            }

            if (!WrappingStyles.TryGetValue(style, out GUIStyle wrapped))
            {
                wrapped = new GUIStyle(style) { wordWrap = true };
                WrappingStyles[style] = wrapped;
            }

            return wrapped;
        }

        private static GUIStyle _boldWrapped;

        /// <summary>Built on first use: a GUIStyle cannot be made before the GUI is running.</summary>
        private static GUIStyle BoldWrapped =>
            _boldWrapped ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontStyle = FontStyle.Bold,
            };

        private static string Tally(int selected, int total, string noun)
        {
            return selected == total
                ? $"{total} {noun}"
                : $"{selected} of {total} {noun}";
        }

        /// <summary>Select or clear a whole list at once, since avatars carry dozens of each.</summary>
        private static void DrawSelectAll(System.Action<bool> apply)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);

                if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                {
                    apply(true);
                }

                if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(46f)))
                {
                    apply(false);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRig()
        {
            if (_plan.RigDiagnostics.Count == 0)
            {
                return;
            }

            _showRig = EditorGUILayout.Foldout(_showRig, "Rig", true);
            if (!_showRig)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                WrappedLabel(
                    "Rig check against Basis IK. These are model import settings; conversion "
                    + "does not change them.");

                foreach (ConversionDiagnostic diagnostic in _plan.RigDiagnostics)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUI.indentLevel * 15f);
                        GUILayout.Label(IconFor(diagnostic.Severity), GUILayout.Width(20f),
                            GUILayout.Height(18f));
                        GUILayout.Label(diagnostic.Message, EditorStyles.wordWrappedLabel);
                    }
                }

                DrawJawFix();
            }
        }

        /// <summary>
        /// Clearing the Jaw mapping edits the model's import settings rather than the scene, so
        /// it is offered separately from Convert and confirmed on its own.
        /// </summary>
        private void DrawJawFix()
        {
            bool jawMapped = false;
            foreach (ConversionDiagnostic diagnostic in _plan.RigDiagnostics)
            {
                if (diagnostic.Code == "rig.jawMapped")
                {
                    jawMapped = true;
                    break;
                }
            }

            if (!jawMapped)
            {
                return;
            }

            ModelImporter importer = RigReadiness.TryGetModelImporter(_plan.SourceRoot);
            using (new EditorGUI.DisabledScope(importer == null))
            {
                if (!GUILayout.Button("Clear Jaw mapping"))
                {
                    return;
                }

                bool confirmed = EditorUtility.DisplayDialog(
                    "Clear Jaw mapping?",
                    $"Edits the humanoid rig on {importer.assetPath} and reimports it. Every "
                    + "avatar using the model is affected. Not undoable.",
                    "Clear",
                    "Cancel");

                if (confirmed && RigReadiness.ClearJawMapping(importer))
                {
                    Rescan();
                }
            }
        }

        private void DrawTuning()
        {
            if (!_advanced)
            {
                return;
            }

            EditorGUILayout.Space();
            _showTuning = EditorGUILayout.Foldout(_showTuning, "Tuning", true);
            if (!_showTuning)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                WrappedLabel(
                    "Stiffness and drag are fitted, not mapped one to one. Adjust, then rescan.");

                EditorGUILayout.LabelField("Stiffness, from PhysBone pull and stiffness",
                    EditorStyles.boldLabel);
                _profile.PullToStiffness = EditorGUILayout.Slider(
                    "Pull weight", _profile.PullToStiffness, 0f, 2f);
                _profile.StiffnessToStiffness = EditorGUILayout.Slider(
                    "Stiffness weight", _profile.StiffnessToStiffness, 0f, 2f);

                EditorGUILayout.LabelField("Drag, from PhysBone spring",
                    EditorStyles.boldLabel);
                _profile.DragAtNoSpring = EditorGUILayout.Slider(
                    "Drag at spring 0", _profile.DragAtNoSpring, 0f, 1f);
                _profile.DragAtFullSpring = EditorGUILayout.Slider(
                    "Drag at spring 1", _profile.DragAtFullSpring, 0f, 1f);

                WrappedLabel(
                    "Higher stiffness: closer to the animated pose. Higher drag: settles sooner.");
            }
        }

        private void DrawDiagnostics()
        {
            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "Diagnostics", true);
            if (!_showDiagnostics)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawDiagnosticSection(DiagnosticSeverity.Warning, "Warnings");
                DrawDiagnosticSection(DiagnosticSeverity.Dropped, "Dropped");
                DrawDiagnosticSection(DiagnosticSeverity.Approximated, "Approximated");
                DrawDiagnosticSection(DiagnosticSeverity.Mapped, "Mapped");
            }
        }

        private void DrawDiagnosticSection(DiagnosticSeverity severity, string heading)
        {
            List<DiagnosticGroup> section = _groups.FindAll(group => group.Severity == severity);
            if (section.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);

            foreach (DiagnosticGroup group in section)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    GUILayout.Label(IconFor(severity), GUILayout.Width(20f),
                        GUILayout.Height(18f));
                    GUILayout.Label($"{group.Code}  x{group.Count}", BoldWrapped);
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    WrappedLabel(group.Example);
                }
            }
        }

        /// <summary>
        /// Unity's own console icons, so severity reads the same here as everywhere else in the
        /// editor. Warnings and losses share the warning icon because both are things the reader
        /// should notice; the section headings carry the difference between them.
        /// </summary>
        private static GUIContent IconFor(DiagnosticSeverity severity)
        {
            string icon = severity switch
            {
                DiagnosticSeverity.Warning => "console.warnicon.sml",
                DiagnosticSeverity.Dropped => "console.warnicon.sml",
                _ => "console.infoicon.sml",
            };

            return EditorGUIUtility.IconContent(icon);
        }

        private void DrawRigs()
        {
            if (_plan.Rigs.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            _showRigs = EditorGUILayout.Foldout(_showRigs,
                $"Rigs ({Tally(_plan.SelectedRigCount, _plan.Rigs.Count, "selected")})", true);
            if (!_showRigs)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(!_options.Physics))
            {
                if (_advanced)
                {
                    DrawSelectAll(include =>
                    {
                        foreach (PlannedJiggleRig rig in _plan.Rigs)
                        {
                            rig.Include = include;
                        }
                    });
                }

                foreach (PlannedJiggleRig rig in _plan.Rigs)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (_advanced)
                        {
                            rig.Include = EditorGUILayout.Toggle(rig.Include,
                                GUILayout.Width(24f));
                        }

                        if (GUILayout.Button(rig.Describe(), EditorStyles.linkLabel,
                                GUILayout.MinWidth(120f)))
                        {
                            EditorGUIUtility.PingObject(rig.SourceRootBone);
                        }

                        rig.Plan.Preset = (JigglePreset)EditorGUILayout.EnumPopup(
                            rig.Plan.Preset, GUILayout.Width(90f));
                        EditorGUILayout.LabelField(
                            _options.Colliders && rig.Colliders.Count > 0
                                ? $"{rig.Colliders.Count} colliders"
                                : " ",
                            GUILayout.Width(80f));
                    }
                }
            }
        }

        /// <summary>
        /// The constraints, one per row, so a single misbehaving one can be left out without
        /// giving up the rest. Advanced only: with no checkboxes there is nothing to do here
        /// that the report does not already say.
        /// </summary>
        private void DrawConstraints()
        {
            if (!_advanced || _plan.Constraints.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            _showConstraints = EditorGUILayout.Foldout(_showConstraints,
                $"Constraints ({Tally(_plan.SelectedConstraintCount, _plan.Constraints.Count, "selected")})",
                true);
            if (!_showConstraints)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(!_options.Constraints))
            {
                DrawSelectAll(include =>
                {
                    foreach (PlannedConstraint constraint in _plan.Constraints)
                    {
                        constraint.Include = include;
                    }
                });

                foreach (PlannedConstraint constraint in _plan.Constraints)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        constraint.Include = EditorGUILayout.Toggle(constraint.Include,
                            GUILayout.Width(24f));

                        if (GUILayout.Button(constraint.Describe(), EditorStyles.linkLabel,
                                GUILayout.MinWidth(120f)))
                        {
                            EditorGUIUtility.PingObject(constraint.SourceHost);
                        }

                        EditorGUILayout.LabelField(
                            $"{constraint.Plan.Sources.Count} sources", GUILayout.Width(80f));
                    }
                }
            }
        }

        /// <summary>
        /// The animation that plays unprompted, one per row. Advanced only.
        /// <para>
        /// Each of these writes a baked clip into the project beside the animation it came from,
        /// which is the one thing a conversion leaves behind after an undo, so the row says where
        /// it will go.
        /// </para>
        /// </summary>
        private void DrawAuthoredMotions()
        {
            if (!_advanced || _plan.AuthoredMotions.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            _showMotions = EditorGUILayout.Foldout(_showMotions,
                $"Authored motion ({Tally(_plan.SelectedAuthoredMotionCount, _plan.AuthoredMotions.Count, "selected")})",
                true);
            if (!_showMotions)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(!_options.Motion))
            {
                DrawSelectAll(include =>
                {
                    foreach (PlannedAuthoredMotion motion in _plan.AuthoredMotions)
                    {
                        motion.Include = include;
                    }
                });

                foreach (PlannedAuthoredMotion motion in _plan.AuthoredMotions)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        motion.Include = EditorGUILayout.Toggle(motion.Include,
                            GUILayout.Width(24f));

                        EditorGUILayout.LabelField(motion.Describe(), GUILayout.MinWidth(120f));
                        EditorGUILayout.LabelField(
                            $"{motion.Plan.Paths.Count} transforms",
                            EditorStyles.label);
                    }
                }

                EditorGUILayout.HelpBox(
                    "Motion clips are baked as assets beside the source animation. Undo removes "
                    + "the components, not the clips.",
                    MessageType.None);
            }
        }

        /// <summary>The menu toggles that can be rebuilt, one per row. Advanced only.</summary>
        private void DrawToggles()
        {
            if (!_advanced || _plan.VixxyControls.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            _showToggles = EditorGUILayout.Foldout(_showToggles,
                $"Menu toggles ({Tally(_plan.SelectedVixxyControlCount, _plan.VixxyControls.Count, "selected")})",
                true);
            if (!_showToggles)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(!_options.Toggles))
            {
                DrawSelectAll(include =>
                {
                    foreach (PlannedVixxyControl control in _plan.VixxyControls)
                    {
                        control.Include = include;
                    }
                });

                foreach (PlannedVixxyControl control in _plan.VixxyControls)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        control.Include = EditorGUILayout.Toggle(control.Include,
                            GUILayout.Width(24f));

                        // A toggle from a piece of clothing is worth telling apart from the
                        // avatar's own, since it came from Modular Avatar rather than a menu.
                        string label = control.Source != null && !control.Source.IsPrimary
                            ? $"{control.Plan.MenuName}  ({control.Source.Name})"
                            : control.Plan.MenuName;

                        EditorGUILayout.LabelField(label, GUILayout.MinWidth(120f));
                        GUILayout.Label(Describe(control.Plan), EditorStyles.wordWrappedLabel);
                    }
                }
            }
        }

        private static string Describe(VixxyControlPlan plan)
        {
            int shapes = 0;
            int materials = 0;
            foreach (VixxySubjectPlan subject in plan.Subjects)
            {
                shapes += subject.BlendShapes.Count;
                materials += subject.MaterialProperties.Count;
            }

            List<string> parts = new List<string>();

            // The shape of the control comes first, because a slider and a control with more
            // than two choices behave differently in the menu than an on/off toggle does.
            if (plan.IsSlider)
            {
                parts.Add("slider");
            }
            else if (plan.ChoiceCount > 2)
            {
                parts.Add($"{plan.ChoiceCount} choices");
            }

            // Motions are activations too, so objects are counted rather than taken from the
            // list length: a control that only starts a motion switches no objects at all.
            int objects = 0;
            foreach (VixxyActivationPlan activation in plan.Activations)
            {
                if (activation.MotionIndex < 0)
                {
                    objects++;
                }
            }

            if (objects > 0)
            {
                parts.Add($"{objects} objects");
            }

            if (plan.Motions.Count > 0)
            {
                parts.Add(plan.Motions.Count == 1
                    ? "1 motion"
                    : $"{plan.Motions.Count} motions");
            }

            if (shapes > 0)
            {
                parts.Add($"{shapes} blendshapes");
            }

            if (materials > 0)
            {
                parts.Add($"{materials} material properties");
            }

            return string.Join(", ", parts);
        }

        private void DrawActions()
        {
            DrawResult();

            EditorGUILayout.Space(6f);
            Separator();
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rescan"))
                {
                    Rescan();
                }

                using (new EditorGUI.DisabledScope(_plan.TotalSelected == 0))
                {
                    if (GUILayout.Button($"Convert {_plan.TotalSelected} components"))
                    {
                        Convert();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy report"))
                {
                    EditorGUIUtility.systemCopyBuffer = ConversionReport.Write(_plan, _result);
                }

                if (GUILayout.Button("Save report"))
                {
                    SaveReport();
                }
            }
        }

        /// <summary>
        /// The outcome of the last conversion, kept apart from the scan's warnings so the two
        /// are not read as one block.
        /// </summary>
        private void DrawResult()
        {
            if (_result == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            Separator();
            EditorGUILayout.Space(4f);

            bool trouble = _result.TotalSkipped > 0;
            string headline = trouble
                ? $"Converted {_result.TotalWritten} components, skipped {_result.TotalSkipped}"
                : $"Converted {_result.TotalWritten} components";

            WrappedLabel(headline, HeadlineStyle(trouble));
            WrappedLabel(
                $"{_result.RigsWritten} jiggle rigs, {_result.ConstraintsWritten} constraints, "
                + $"{_result.VixxyControlsWritten} Vixxy controls, "
                + $"{_result.AuthoredMotionsWritten} authored motions"
                + (_result.HeadChopsWritten > 0 ? $", {_result.HeadChopsWritten} head chops" : "")
                + (_result.DescriptorWritten ? ", Basis Avatar." : "."));

            if (_result.VrmRuntimeRemoved > 0)
            {
                WrappedLabel(
                    $"Removed {_result.VrmRuntimeRemoved} UniVRM runtime components. They drove "
                    + "the expressions, spring bones and look-at over what was written.");
            }

            if (_result.MotionAssets.Count > 0)
            {
                WrappedLabel(
                    $"Baked {_result.MotionAssets.Count} motion clips into "
                    + $"{System.IO.Path.GetDirectoryName(_result.MotionAssets[0])?.Replace('\\', '/')}. "
                    + "Undo does not remove them.");
            }

            EditorGUILayout.Space(2f);
            WrappedLabel(
                "Jiggle physics runs after Test In Editor on the Basis Avatar component, not in "
                + "plain Play mode.");
        }

        /// <summary>
        /// Green when the conversion came out clean, red when something was skipped. Both tones
        /// are picked per skin: the colours that read on the dark theme are washed out on the
        /// light one.
        /// </summary>
        private static GUIStyle HeadlineStyle(bool trouble)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };

            if (trouble)
            {
                style.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.94f, 0.42f, 0.40f)
                    : new Color(0.65f, 0.10f, 0.10f);
            }
            else
            {
                style.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.40f, 0.83f, 0.45f)
                    : new Color(0.10f, 0.50f, 0.15f);
            }

            return style;
        }

        private static void Separator()
        {
            Rect line = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.25f));
        }

        private void Rescan()
        {
            _plan = null;
            _result = null;
            _groups = null;
            _sourceAssetPath = null;
            _blocker = null;

            if (_target == null)
            {
                _blocker = "Select an avatar.";
                return;
            }

            // The whole hierarchy, not just the avatar's own prefab: clothing and accessories
            // are prefabs of their own and carry their own physics.
            _plan = AvatarConversionPlanner.Plan(_target, _profile);
            _plan.Options = _options;
            _sourceAssetPath = _plan.SourceAssetPath;

            if (_plan.SourceRoot == null)
            {
                _plan = null;
                _blocker = "Not linked to a prefab, so there is no file to read. Use the "
                    + "original prefab, or re-import the avatar and convert it before unpacking.";
                return;
            }

            _groups = ConversionReport.Group(_plan);

            if (_plan.TotalPlanned == 0)
            {
                string model = _plan.ComponentsRead == 0 && _plan.Sources.Count > 0
                    ? _plan.Sources[0].ModelAssetPath()
                    : null;

                _blocker = string.IsNullOrEmpty(model)
                    ? "Nothing convertible in this prefab. Read: VRChat PhysBones, colliders, "
                      + "constraints, head chop, and the avatar descriptor with its menu and "
                      + "animators; VRM spring bones, expressions and constraints; Dynamic Bone."
                    : $"Nothing found: this prefab was saved from {model} without unpacking, "
                      + "and that file is not read. Unpack Completely, save, and convert the "
                      + "result.";
            }
        }

        private void Convert()
        {
            GameObject destination = _target;

            if (PrefabUtility.IsPartOfPrefabAsset(_target))
            {
                destination = (GameObject)PrefabUtility.InstantiatePrefab(_target);
                Undo.RegisterCreatedObjectUndo(destination, "Instantiate avatar");
                Selection.activeGameObject = destination;
            }

            if (!ConfirmReplacement(destination))
            {
                return;
            }

            _result = AvatarConverter.Apply(_plan, destination,
                $"{ProductInfo.Name}: convert avatar");

            _groups = ConversionReport.Group(_plan);
        }

        /// <summary>A count and its noun, pluralised, for the list in the dialog.</summary>
        private static void Describe(List<string> into, int count, string noun)
        {
            if (count > 0)
            {
                into.Add(count == 1 ? $"1 {noun}" : $"{count} {noun}s");
            }
        }

        /// <summary>
        /// Converting twice would otherwise stack a second set of components on top of the
        /// first. Offers to remove the ones sitting where this conversion is about to write, and
        /// leaves the rest of the avatar alone.
        /// </summary>
        private bool ConfirmReplacement(GameObject destination)
        {
            List<Component> replaceable = AvatarConverter.FindReplaceable(_plan, destination);
            if (replaceable.Count == 0)
            {
                return true;
            }

            int rigs = 0;
            int constraints = 0;
            int controls = 0;
            int motions = 0;

            foreach (Component component in replaceable)
            {
                switch (component)
                {
                    case JiggleRig _:
                        rigs++;
                        break;
                    case BasisConstraintBase _:
                        constraints++;
                        break;
                    case BasisAuthoredMotion _:
                        motions++;
                        break;

                    // A control and its menu item are two components making up one control, so
                    // the menu item is what is counted and the control goes with it.
                    case HVRVixxyMenuItem _:
                        controls++;
                        break;
                }
            }

            List<string> kinds = new List<string>();
            Describe(kinds, rigs, "jiggle rig");
            Describe(kinds, constraints, "Basis constraint");
            Describe(kinds, controls, "Vixxy control");
            Describe(kinds, motions, "authored motion");

            bool replace = EditorUtility.DisplayDialog(
                "Convert again?",
                $"{destination.name} already has {string.Join(", ", kinds)} from a previous "
                + "conversion. They will be replaced; nothing else on the avatar is touched. "
                + "Undo restores them"
                + (motions > 0 ? "; baked motion clips stay in the project." : "."),
                "Replace",
                "Cancel");

            if (!replace)
            {
                return false;
            }

            AvatarConverter.RemoveReplaceable(_plan, destination,
                $"{ProductInfo.Name}: replace converted components");
            return true;
        }

        private void SaveReport()
        {
            string suggested = _plan.SourceRoot != null
                ? _plan.SourceRoot.name + "-conversion-report.md"
                : "conversion-report.md";

            string path = EditorUtility.SaveFilePanel(
                "Save conversion report", string.Empty, suggested, "md");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, ConversionReport.Write(_plan, _result));
            }
        }
    }
}
