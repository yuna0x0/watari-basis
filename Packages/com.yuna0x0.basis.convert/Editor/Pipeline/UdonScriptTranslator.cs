using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;
using Assembly = System.Reflection.Assembly;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// One translation of a folder of UdonSharp sources into a folder of scripts that run on the
    /// runtime package through Cilbox.
    /// </summary>
    public sealed class UdonScriptTranslation
    {
        /// <summary>Folder holding the original sources with their <c>.meta</c> files. Usually ends in <c>~</c> so Unity skips it.</summary>
        public string SourceFolder;

        /// <summary>Folder under <c>Assets/</c> that receives the converted scripts, their companions and an asmdef.</summary>
        public string OutputFolder;

        /// <summary>Name of the asmdef written to <see cref="OutputFolder"/>.</summary>
        public string AssemblyName;

        /// <summary>
        /// Classes a previous load check found Cilbox cannot load (<see cref="UdonScriptTranslator.CheckLoad"/>).
        /// The sources declaring them are set aside, and with them every source that needs them.
        /// </summary>
        public readonly HashSet<string> RefusedClasses = new HashSet<string>();

        public readonly List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
        public readonly List<string> Translated = new List<string>();
        public readonly List<string> Excluded = new List<string>();
        public bool Succeeded;
    }

    /// <summary>
    /// Converts UdonSharp sources without ever putting a file that fails to compile into the project:
    /// each source is rewritten (<see cref="UdonSharpSourceRewriter"/>), compiled on the side with
    /// <see cref="SideCompiler"/>, and set aside with a diagnostic when it fails, until the rest
    /// compiles. The compiled types are then read (<see cref="UdonScriptTypeReader"/>), companion code
    /// is generated (<see cref="UdonCompanionGenerator"/>) and compiled once more with the sources,
    /// and only then is anything written under <c>Assets/</c>.
    /// <para>
    /// Compilation runs on the editor's update loop, so the result arrives through a callback. After
    /// Unity has compiled the written scripts (a domain reload, so a later editor step or a second
    /// batch run): <see cref="CheckLoad"/>, and if it reports classes, another <see cref="Run"/> with
    /// them in <see cref="UdonScriptTranslation.RefusedClasses"/>; then <see cref="ReadRewrites"/> and,
    /// with the converted scene open, <see cref="SetUpScene"/>.
    /// </para>
    /// </summary>
    public static class UdonScriptTranslator
    {
        public const string RuntimeAssembly = "yuna0x0.Basis.Udon";
        public const string DataAssembly = "yuna0x0.Basis.Udon.Data";
        public const string RuntimePackage = "com.yuna0x0.basis.udon";
        const string WorkFolder = "Temp/WatariUdon";
        const int MaxPasses = 64;

        sealed class Job
        {
            public UdonScriptTranslation Translation;
            public Action<UdonScriptTranslation> Done;
            public Dictionary<string, string> Rewritten = new Dictionary<string, string>();
            public Dictionary<string, string> SourceOf = new Dictionary<string, string>();
            public Dictionary<string, string> DeclaredIn = new Dictionary<string, string>();
            public Dictionary<string, string> EnumIn = new Dictionary<string, string>();
            public HashSet<string> Active = new HashSet<string>();
            public Dictionary<string, string> Companions = new Dictionary<string, string>();
            public string[] References;
            public string[] Defines;
            public int Pass;
        }

        public static void Run(UdonScriptTranslation translation, Action<UdonScriptTranslation> done)
        {
            Job job = new Job { Translation = translation, Done = done };
            if (!PrepareReferences(job))
            {
                Finish(job, false);
                return;
            }

            if (Directory.Exists(WorkFolder))
            {
                Directory.Delete(WorkFolder, true);
            }

            Directory.CreateDirectory(Path.Combine(WorkFolder, "src"));
            foreach (string path in Directory.GetFiles(translation.SourceFolder, "*.cs", SearchOption.AllDirectories))
            {
                string relative = path.Substring(translation.SourceFolder.Length).TrimStart('/', '\\').Replace('\\', '/');
                if (IsEditorPath(relative))
                {
                    translation.Diagnostics.Add(DiagnosticSeverity.Dropped, "script.editorScript.notCopied",
                        $"{relative} is an editor script; editor scripts are not converted yet.");
                    continue;
                }

                UdonSharpSourceRewriter.Result rewritten = UdonSharpSourceRewriter.Rewrite(File.ReadAllText(path), relative);
                translation.Diagnostics.AddRange(rewritten.Diagnostics);
                string work = Path.Combine(WorkFolder, "src", relative.Replace('/', '_'));
                File.WriteAllText(work, rewritten.Text);
                job.Rewritten[relative] = work;
                foreach (string name in rewritten.ClassNames)
                {
                    job.DeclaredIn[name] = relative;
                }

                foreach (string name in rewritten.EnumNames)
                {
                    job.EnumIn[name] = relative;
                }

                job.SourceOf[relative] = path;
                job.Active.Add(relative);
            }

            foreach (string refused in translation.RefusedClasses)
            {
                string name = refused.Substring(refused.LastIndexOf('.') + 1);
                if (job.DeclaredIn.TryGetValue(name, out string relative) && job.Active.Contains(relative))
                {
                    Exclude(job, relative, "script.cilbox.excluded",
                        $"{relative} declares {name}, which Cilbox cannot load (found by the load check); set aside so the rest of the scene loads.");
                }
            }

            if (job.Active.Count == 0)
            {
                Finish(job, true);
                return;
            }

            CompileSources(job);
        }

        public const string RuntimeEditorAssembly = "yuna0x0.Basis.Udon.Editor";

        /// <summary>
        /// Sets up a scene holding translated scripts so they run (the runtime package's scene setup:
        /// its driver and lists, the Cilbox host, buttons for Interact, seats for stations). What the
        /// setup reports is added to the translation's diagnostics; a missing partner component is a
        /// warning.
        /// </summary>
        public static void SetUpScene(UnityEngine.SceneManagement.Scene scene, UdonScriptTranslation translation)
        {
            MethodInfo apply = RuntimeEditorType("yuna0x0.Basis.Udon.Editor.UdonSceneSetup")
                ?.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(UnityEngine.SceneManagement.Scene) }, null);
            if (apply == null)
            {
                translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.scene.setupUnavailable",
                    $"The runtime package {RuntimePackage} has no scene setup, so the scene was not set up to run scripts.");
                return;
            }

            foreach (string line in (string[])apply.Invoke(null, new object[] { scene }))
            {
                string[] parts = line.Split(new[] { '\t' }, 2);
                bool problem = parts[0].EndsWith(".missing", StringComparison.Ordinal);
                translation.Diagnostics.Add(problem ? DiagnosticSeverity.Warning : DiagnosticSeverity.Mapped,
                    "script.scene." + parts[0].Substring("setup.".Length), parts.Length > 1 ? parts[1] : "");
            }
        }

        const string RewritesResource = "yuna0x0.Basis.Udon.Rewrites";

        /// <summary>
        /// Reports what the runtime package's IL post-processor changed in each script so it behaves
        /// under Cilbox as it did on Udon, read from the list it embeds in the compiled assembly. Runs
        /// after Unity has compiled the written scripts; one diagnostic per class.
        /// </summary>
        public static void ReadRewrites(UdonScriptTranslation translation)
        {
            Assembly assembly = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .FirstOrDefault(a => a.GetName().Name == translation.AssemblyName);
            Stream stream = assembly?.GetManifestResourceStream(RewritesResource);
            if (stream == null)
            {
                return;
            }

            string text;
            using (StreamReader reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            IEnumerable<string[]> changes = text.Split('\n').Select(l => l.Split('\t')).Where(p => p.Length >= 2);
            foreach (IGrouping<string, string[]> byClass in changes.GroupBy(p => p[1]).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                string patterns = string.Join(", ", byClass.GroupBy(p => p[0]).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => g.Count() == 1 ? g.Key : $"{g.Key} x{g.Count()}"));
                translation.Diagnostics.Add(DiagnosticSeverity.Mapped, "script.rewritten",
                    $"{byClass.Key}: compiled code adjusted to run under Cilbox as on Udon ({patterns}).");
            }
        }

        static Type RuntimeEditorType(string name)
        {
            return UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .FirstOrDefault(a => a.GetName().Name == RuntimeEditorAssembly)
                ?.GetType(name);
        }

        /// <summary>
        /// Checks that the written scripts load in a scene's Cilbox, after Unity has compiled them. One
        /// member Cilbox refuses stops every script in a scene, so each failing class is reported and
        /// returned; translating again with them in <see cref="UdonScriptTranslation.RefusedClasses"/>
        /// sets them aside. The check itself is the runtime package's (it exports and loads scratch
        /// scenes; open scenes must be saved, and are restored afterwards).
        /// </summary>
        public static List<string> CheckLoad(UdonScriptTranslation translation)
        {
            List<string> refused = new List<string>();
            MethodInfo run = RuntimeEditorType("yuna0x0.Basis.Udon.Editor.UdonLoadCheck")
                ?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (run == null)
            {
                translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.cilbox.checkUnavailable",
                    $"The runtime package {RuntimePackage} has no load check, so the scripts were not checked against Cilbox.");
                return refused;
            }

            foreach (string line in (string[])run.Invoke(null, new object[] { translation.AssemblyName }))
            {
                string[] parts = line.Split(new[] { '\t' }, 2);
                string reason = parts.Length > 1 ? parts[1] : "";
                if (parts[0] == "!")
                {
                    translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.cilbox.checkUnavailable",
                        $"The scripts were not checked against Cilbox: {reason}");
                    continue;
                }

                string[] names = parts[0].Split(',');
                refused.AddRange(names);
                translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.cilbox.refused",
                    names.Length == 1
                        ? $"{names[0]} does not load in Cilbox, which stops every script in the scene: {reason}"
                        : $"{string.Join(", ", names)} do not load together in Cilbox, which stops every script in the scene: {reason}");
            }

            return refused;
        }

        static bool PrepareReferences(Job job)
        {
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies();
            UnityEditor.Compilation.Assembly runtime = assemblies.FirstOrDefault(a => a.name == RuntimeAssembly);
            UnityEditor.Compilation.Assembly data = assemblies.FirstOrDefault(a => a.name == DataAssembly);
            if (runtime == null)
            {
                job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.runtime.missing",
                    $"The runtime package {RuntimePackage} is not installed, so scripts cannot be converted.");
                return false;
            }

            int contract = ReadContractVersion();
            if (contract != UdonCompanionGenerator.ContractVersion)
            {
                job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.runtime.unsupportedVersion",
                    $"The runtime package declares contract {contract}; this converter generates contract {UdonCompanionGenerator.ContractVersion}.");
                return false;
            }

            SideCompiler.ReferencesOf(RuntimeAssembly, out List<string> references, out List<string> defines);
            references.AddRange(SideCompiler.PlayerAssemblies());
            if (data != null)
            {
                references.Add(Path.GetFullPath(data.outputPath));
            }

            defines.AddRange(UploadDefines);
            job.Defines = defines.Distinct().ToArray();
            job.References = references.Distinct().ToArray();
            return true;
        }

        static int ReadContractVersion()
        {
            foreach (Assembly assembly in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies())
            {
                if (assembly.GetName().Name != RuntimeAssembly)
                {
                    continue;
                }

                FieldInfo field = assembly.GetType("yuna0x0.Basis.Udon.UdonContract")?.GetField("Version");
                return field != null ? (int)field.GetRawConstantValue() : -1;
            }

            return -1;
        }

        // Pass 1: the rewritten sources alone, until every remaining file compiles.
        static void CompileSources(Job job)
        {
            // Every source was set aside: nothing is left to compile or convert.
            if (job.Active.Count == 0)
            {
                Finish(job, true);
                return;
            }

            Build(job, job.Active.Select(r => job.Rewritten[r]), (path, messages) =>
            {
                HashSet<string> failing = FailingSources(job, messages, out Dictionary<string, string> firstError);
                if (failing.Count > 0)
                {
                    foreach (string relative in failing)
                    {
                        string dependency = ExcludedDependency(job, firstError[relative]);
                        if (dependency != null)
                        {
                            Exclude(job, relative, "script.compile.dependsOnExcluded",
                                $"{relative} uses {dependency}, which was left out.");
                        }
                        else
                        {
                            Exclude(job, relative, "script.compile.excluded",
                                $"{relative} does not compile against the runtime package: {firstError[relative]}");
                        }
                    }

                    Continue(job, CompileSources);
                    return;
                }

                if (messages.Any(m => m.type == CompilerMessageType.Error))
                {
                    job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.compile.unattributed",
                        "Compilation failed with errors that name no source file; nothing was converted.");
                    Finish(job, false);
                    return;
                }

                Continue(job, j => GenerateCompanions(j, path));
            });
        }

        static void GenerateCompanions(Job job, string assemblyPath)
        {
            Assembly assembly = UnityEngine.Assemblies.CurrentAssemblies.LoadFromBytes(File.ReadAllBytes(assemblyPath));
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            bool excludedAny = false;
            job.Companions.Clear();
            foreach (Type type in types)
            {
                // Only classes the sources declare: the compiler adds its own (module initialisers).
                if (type.IsNested || !type.IsClass || !job.DeclaredIn.TryGetValue(type.Name, out string relative)
                    || !job.Active.Contains(relative))
                {
                    continue;
                }

                string generic = UdonScriptTypeReader.FindGenericMethod(type);
                if (generic != null)
                {
                    Exclude(job, relative, "script.genericMethod.excluded",
                        $"{type.Name}.{generic} is a generic method, which makes Cilbox fail the whole scene.");
                    excludedAny = true;

                    continue;
                }

                UdonScriptData data = UdonScriptTypeReader.Read(type);
                string key = (string.IsNullOrEmpty(type.Namespace) ? "" : type.Namespace + ".") + type.Name;
                job.Companions[key] = UdonCompanionGenerator.Generate(data, ProductInfo.Version, job.Translation.Diagnostics);
            }

            if (excludedAny)
            {
                Continue(job, CompileSources);
                return;
            }

            string companionFolder = Path.Combine(WorkFolder, "gen" + job.Pass);
            Directory.CreateDirectory(companionFolder);
            List<string> files = job.Active.Select(r => job.Rewritten[r]).ToList();
            foreach (KeyValuePair<string, string> companion in job.Companions)
            {
                string file = Path.Combine(companionFolder, companion.Key + ".udon.g.cs");
                File.WriteAllText(file, companion.Value);
                files.Add(file);
            }

            // Pass 2: sources and companions together. A failure here is the generator's, not the
            // script's, and is reported as such.
            Build(job, files, (path, messages) =>
            {
                CompilerMessage[] errors = messages.Where(m => m.type == CompilerMessageType.Error).ToArray();
                // CS0260 marks each source declaration that lacks the partial its companion needs, at
                // the type's name; the modifier is inserted there and the pass runs again. The other
                // errors of such a pass follow from the missing modifier (the companion is then a
                // separate class without the script's members) and are not reported.
                CompilerMessage[] missingPartial = errors.Where(e => e.message.Contains("error CS0260")).ToArray();
                if (missingPartial.Length > 0 && AddPartial(job, missingPartial))
                {
                    Continue(job, j => GenerateCompanions(j, assemblyPath));
                    return;
                }

                if (errors.Length > 0)
                {
                    foreach (CompilerMessage error in errors.Take(5))
                    {
                        job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.companion.failed",
                            $"Generated code failed to compile: {error.message}");
                    }

                    Finish(job, false);
                    return;
                }

                Write(job, ProjectAssemblies(path));
                Finish(job, true);
            });
        }

        static bool AddPartial(Job job, CompilerMessage[] errors)
        {
            bool changed = false;
            foreach (IGrouping<string, CompilerMessage> file in errors.GroupBy(e => Path.GetFullPath(e.file)))
            {
                string relative = job.Rewritten.FirstOrDefault(r => Path.GetFullPath(r.Value) == file.Key).Key;
                if (relative == null)
                {
                    continue;
                }

                string text = File.ReadAllText(job.Rewritten[relative]);
                // Later positions first, so earlier ones stay valid.
                foreach (CompilerMessage error in file.OrderByDescending(e => e.line).ThenByDescending(e => e.column))
                {
                    string edited = UdonSharpSourceRewriter.InsertPartial(text, error.line, error.column);
                    if (edited != null)
                    {
                        text = edited;
                        changed = true;
                    }
                }

                File.WriteAllText(job.Rewritten[relative], text);
            }

            return changed;
        }

        // The asmdef must list every project assembly the scripts use (UI, TextMeshPro, ...); read
        // them off the compiled result rather than guessing.
        static List<string> ProjectAssemblies(string compiledPath)
        {
            HashSet<string> project = new HashSet<string>(CompilationPipeline.GetAssemblies().Select(a => a.name));
            List<string> used = new List<string> { RuntimeAssembly, DataAssembly, "Cilbox" };
            Assembly compiled = UnityEngine.Assemblies.CurrentAssemblies.LoadFromBytes(File.ReadAllBytes(compiledPath));
            foreach (AssemblyName reference in compiled.GetReferencedAssemblies())
            {
                if (project.Contains(reference.Name) && !used.Contains(reference.Name))
                {
                    used.Add(reference.Name);
                }
            }

            return used;
        }

        static void Write(Job job, List<string> references)
        {
            UdonScriptTranslation t = job.Translation;
            ClearPrevious(t.OutputFolder);
            Directory.CreateDirectory(t.OutputFolder);
            foreach (string relative in job.Active)
            {
                string target = Path.Combine(t.OutputFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(job.Rewritten[relative], target, true);
                string meta = job.SourceOf[relative] + ".meta";
                if (File.Exists(meta))
                {
                    // The original guid keeps every scene and prefab reference to the script valid.
                    File.Copy(meta, target + ".meta", true);
                }

                t.Translated.Add(relative);
            }

            foreach (KeyValuePair<string, string> companion in job.Companions)
            {
                File.WriteAllText(Path.Combine(t.OutputFolder, companion.Key + ".udon.g.cs"), companion.Value);
            }

            File.WriteAllText(Path.Combine(t.OutputFolder, t.AssemblyName + ".asmdef"), Asmdef(t.AssemblyName, references));
            t.Diagnostics.Add(DiagnosticSeverity.Mapped, "script.translated",
                $"{t.Translated.Count} script(s) converted; {t.Excluded.Count} left out.");
        }

        // A script a previous run wrote and this run leaves out must not stay behind and compile.
        // Only what a translation writes is removed (scripts, companions, the asmdef and their .meta
        // files, then folders left empty); anything else in the folder stays.
        static void ClearPrevious(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".cs.meta", StringComparison.Ordinal)
                    || file.EndsWith(".asmdef", StringComparison.Ordinal) || file.EndsWith(".asmdef.meta", StringComparison.Ordinal))
                {
                    File.Delete(file);
                }
            }

            foreach (string dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    if (File.Exists(dir + ".meta"))
                    {
                        File.Delete(dir + ".meta");
                    }
                }
            }
        }

        static string Asmdef(string name, List<string> references)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("{");
            b.AppendLine($"    \"name\": \"{name}\",");
            b.AppendLine("    \"rootNamespace\": \"\",");
            b.AppendLine("    \"references\": [ " + string.Join(", ", references.Select(r => "\"" + r + "\"")) + " ],");
            b.AppendLine("    \"autoReferenced\": false,");
            b.AppendLine("    \"versionDefines\": [");
            for (int k = 0; k < UploadDefines.Length; k++)
            {
                string comma = k < UploadDefines.Length - 1 ? "," : "";
                b.AppendLine($"        {{ \"name\": \"{RuntimePackage}\", \"expression\": \"0.0.0\", \"define\": \"{UploadDefines[k]}\" }}{comma}");
            }
            b.AppendLine("    ]");
            b.AppendLine("}");
            return b.ToString();
        }

        // Defined for converted scripts, in the side compile and through the written asmdef:
        // COMPILER_UDONSHARP as UdonSharp's compiler defines it, and the project-wide symbols a VRChat
        // project has (UdonSharp adds UDONSHARP to the scripting defines; the SDK adds VRC_SDK_VRCSDK3).
        public static readonly string[] UploadDefines = { "COMPILER_UDONSHARP", "UDONSHARP", "VRC_SDK_VRCSDK3" };

        static void Build(Job job, IEnumerable<string> files, Action<string, CompilerMessage[]> finished)
        {
            job.Pass++;
            if (job.Pass > MaxPasses)
            {
                job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.compile.gaveUp",
                    $"Compilation did not settle after {MaxPasses} passes; nothing was converted.");
                Finish(job, false);
                return;
            }

            // A unique assembly name per build: the compiled result is loaded to read its types, and a
            // second assembly with a name already loaded in this editor session resolves to the first.
            string output = Path.Combine(WorkFolder, $"pass{job.Pass}_{Guid.NewGuid():N}.dll");
            SideCompiler compiler = new SideCompiler
            {
                OutputPath = output,
                Sources = files.ToArray(),
                References = job.References,
                Defines = job.Defines,
            };
            if (!compiler.Start(finished, out string problem))
            {
                job.Translation.Diagnostics.Add(DiagnosticSeverity.Warning, "script.compile.notStarted",
                    $"The side compilation could not start; nothing was converted. {problem}");
                Finish(job, false);
            }
        }

        // The next pass starts one editor tick after the previous one reported, outside its callback.
        static void Continue(Job job, Action<Job> next)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                EditorApplication.update -= step;
                next(job);
            };
            EditorApplication.update += step;
        }

        static HashSet<string> FailingSources(Job job, CompilerMessage[] messages, out Dictionary<string, string> firstError)
        {
            firstError = new Dictionary<string, string>();
            HashSet<string> failing = new HashSet<string>();
            foreach (CompilerMessage m in messages)
            {
                if (m.type != CompilerMessageType.Error || string.IsNullOrEmpty(m.file))
                {
                    continue;
                }

                string full = Path.GetFullPath(m.file);
                foreach (string relative in job.Active)
                {
                    if (Path.GetFullPath(job.Rewritten[relative]) != full)
                    {
                        continue;
                    }

                    failing.Add(relative);
                    if (!firstError.ContainsKey(relative))
                    {
                        int at = m.message.IndexOf("error ", StringComparison.Ordinal);
                        firstError[relative] = at >= 0 ? m.message.Substring(at) : m.message;
                    }
                }
            }

            return failing;
        }

        // A compiler error naming a class that an excluded source declares means this file failed
        // only because of that exclusion.
        static string ExcludedDependency(Job job, string error)
        {
            foreach (KeyValuePair<string, string> declared in job.DeclaredIn.Concat(job.EnumIn))
            {
                if (!job.Active.Contains(declared.Value) && error.Contains("'" + declared.Key + "'"))
                {
                    return declared.Key;
                }
            }

            return null;
        }

        static void Exclude(Job job, string relative, string code, string message)
        {
            job.Active.Remove(relative);
            job.Translation.Excluded.Add(relative);
            job.Translation.Diagnostics.Add(DiagnosticSeverity.Dropped, code, message);
        }

        static bool IsEditorPath(string relative)
        {
            return relative.StartsWith("Editor/", StringComparison.Ordinal) || relative.Contains("/Editor/");
        }

        static void Finish(Job job, bool succeeded)
        {
            job.Translation.Succeeded = succeeded;
            job.Done?.Invoke(job.Translation);
        }
    }
}
