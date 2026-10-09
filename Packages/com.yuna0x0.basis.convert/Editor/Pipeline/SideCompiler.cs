using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Compiles C# files to an assembly outside the project, with the Roslyn compiler and .NET host
    /// that ship inside the editor (the ones Unity's own script compilation runs). References and
    /// defines are taken from an assembly Unity already compiles, through
    /// <see cref="CompilationPipeline"/>, so the result compiles as that assembly's sources would.
    /// The compiler runs as a separate process; <see cref="Finished"/> is polled from the editor
    /// loop, so callbacks arrive on the main thread.
    /// </summary>
    public sealed class SideCompiler
    {
        public string OutputPath;
        public string[] Sources;
        public string[] References;
        public string[] Defines;

        Process _process;
        readonly StringBuilder _output = new StringBuilder();
        Action<string, CompilerMessage[]> _done;

        /// <summary>References and defines of an assembly Unity compiles, plus that assembly itself.</summary>
        public static bool ReferencesOf(string assemblyName, out List<string> references, out List<string> defines)
        {
            references = new List<string>();
            defines = new List<string>();
            UnityEditor.Compilation.Assembly assembly = CompilationPipeline.GetAssemblies().FirstOrDefault(a => a.name == assemblyName);
            if (assembly == null)
            {
                return false;
            }

            references.Add(Path.GetFullPath(assembly.outputPath));
            references.AddRange(assembly.allReferences.Select(Path.GetFullPath));
            references.AddRange(assembly.compiledAssemblyReferences.Select(Path.GetFullPath));
            foreach (string directory in CompilationPipeline.GetSystemAssemblyDirectories(assembly.compilerOptions.ApiCompatibilityLevel))
            {
                if (Directory.Exists(directory))
                {
                    references.AddRange(Directory.GetFiles(directory, "*.dll"));
                }
            }

            defines.AddRange(assembly.defines);
            return true;
        }

        /// <summary>
        /// Every project assembly that goes into a player build (TextMeshPro, UI, other packages and
        /// the project's own scripts), as compiled for the editor. Scripts may use any of them.
        /// </summary>
        public static IEnumerable<string> PlayerAssemblies()
        {
            HashSet<string> player = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name));
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (player.Contains(assembly.name) && File.Exists(assembly.outputPath))
                {
                    yield return Path.GetFullPath(assembly.outputPath);
                }
            }
        }

        public bool Start(Action<string, CompilerMessage[]> done, out string problem)
        {
            problem = null;
            if (!FindCompiler(out string dotnet, out string csc))
            {
                problem = "The C# compiler that ships with the editor was not found under " + EditorApplication.applicationContentsPath + ".";
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
            string rsp = Path.GetFullPath(OutputPath) + ".rsp";
            StringBuilder args = new StringBuilder();
            args.AppendLine("-target:library");
            args.AppendLine("-nostdlib");
            args.AppendLine("-noconfig");
            args.AppendLine("-langversion:9.0");
            args.AppendLine("-nologo");
            args.AppendLine("-utf8output");
            args.AppendLine("-preferreduilang:en-US");
            args.AppendLine("-debug:portable");
            args.AppendLine("-nowarn:0169,0649,0282,1701,1702");
            args.AppendLine("-out:" + Quote(Path.GetFullPath(OutputPath)));
            // A file name can appear in more than one reference set; the compiler rejects duplicates.
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string reference in References)
            {
                if (File.Exists(reference) && seen.Add(Path.GetFileName(reference)))
                {
                    args.AppendLine("-r:" + Quote(reference));
                }
            }

            foreach (string define in Defines.Distinct())
            {
                args.AppendLine("-define:" + define);
            }

            foreach (string source in Sources)
            {
                args.AppendLine(Quote(Path.GetFullPath(source)));
            }

            File.WriteAllText(rsp, args.ToString());

            _done = done;
            _process = new Process
            {
                StartInfo = new ProcessStartInfo(dotnet, "exec " + Quote(csc) + " /shared " + Quote("@" + rsp))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
            };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_output) _output.AppendLine(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_output) _output.AppendLine(e.Data); };
            try
            {
                _process.Start();
            }
            catch (Exception e)
            {
                problem = "The C# compiler could not be started: " + e.Message;
                return false;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            EditorApplication.update += Poll;
            return true;
        }

        void Poll()
        {
            if (!_process.HasExited)
            {
                return;
            }

            EditorApplication.update -= Poll;
            _process.WaitForExit();
            string text;
            lock (_output)
            {
                text = _output.ToString();
            }

            CompilerMessage[] messages = Parse(text);
            if (_process.ExitCode != 0 && !messages.Any(m => m.type == CompilerMessageType.Error))
            {
                messages = messages.Concat(new[] { new CompilerMessage { type = CompilerMessageType.Error, message = text.Trim() } }).ToArray();
            }

            _process.Dispose();
            _done(OutputPath, messages);
        }

        // "path(line,col): error CS0246: message" and "error CS0006: message" (no location).
        static readonly Regex Located = new Regex(@"^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\): (?<kind>error|warning) (?<code>\w+): (?<text>.*)$");
        static readonly Regex Unlocated = new Regex(@"^(?<kind>error|warning) (?<code>\w+): (?<text>.*)$");

        public static CompilerMessage[] Parse(string output)
        {
            List<CompilerMessage> messages = new List<CompilerMessage>();
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                Match m = Located.Match(line);
                if (m.Success)
                {
                    messages.Add(new CompilerMessage
                    {
                        file = m.Groups["file"].Value,
                        line = int.Parse(m.Groups["line"].Value),
                        column = int.Parse(m.Groups["column"].Value),
                        type = m.Groups["kind"].Value == "error" ? CompilerMessageType.Error : CompilerMessageType.Warning,
                        message = line,
                    });
                    continue;
                }

                m = Unlocated.Match(line);
                if (m.Success)
                {
                    messages.Add(new CompilerMessage
                    {
                        type = m.Groups["kind"].Value == "error" ? CompilerMessageType.Error : CompilerMessageType.Warning,
                        message = line,
                    });
                }
            }

            return messages.ToArray();
        }

        static string _dotnet;
        static string _csc;

        // The .NET host and Roslyn's csc.dll inside the editor install. Their folders carry the SDK
        // version, so they are searched for rather than spelled out.
        static bool FindCompiler(out string dotnet, out string csc)
        {
            if (_dotnet == null || !File.Exists(_dotnet) || !File.Exists(_csc))
            {
                _dotnet = null;
                _csc = null;
                string root = EditorApplication.applicationContentsPath;
                foreach (string sdk in Directory.GetDirectories(root, "DotNetSdk", SearchOption.AllDirectories))
                {
                    string host = Path.Combine(sdk, UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet");
                    string compiler = Directory.GetFiles(sdk, "csc.dll", SearchOption.AllDirectories)
                        .FirstOrDefault(p => p.Replace('\\', '/').Contains("/Roslyn/bincore/"));
                    if (File.Exists(host) && compiler != null)
                    {
                        _dotnet = host;
                        _csc = compiler;
                        break;
                    }
                }
            }

            dotnet = _dotnet;
            csc = _csc;
            return _dotnet != null;
        }

        static string Quote(string path)
        {
            return "\"" + path + "\"";
        }
    }
}
