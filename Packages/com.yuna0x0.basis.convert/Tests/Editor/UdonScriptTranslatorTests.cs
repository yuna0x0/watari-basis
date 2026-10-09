using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Pipeline;

namespace yuna0x0.Basis.Convert.Tests
{
    // Runs the whole translation on hand-written scripts. Needs the runtime package in the project;
    // skipped without it.
    public class UdonScriptTranslatorTests
    {
        const string Root = "Temp/WatariUdonTest";

        const string Lock = @"using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TestLock : UdonSharpBehaviour
{
    [UdonSynced] public bool isLocked = true;
    [SerializeField] private GameObject icon;

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        isLocked = !isLocked;
        RequestSerialization();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Refresh));
    }

    public void Refresh() { icon.SetActive(isLocked); }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnValidate() { UnityEditor.EditorUtility.SetDirty(this); }
#endif
}
";

        const string Unknown = @"using UdonSharp;
using VRC.SDK3.SomethingThatDoesNotExist;

public class TestUnknown : UdonSharpBehaviour
{
    private Missing value;
}
";

        const string Generic = @"using UdonSharp;

public class TestGeneric : UdonSharpBehaviour
{
    public T Pick<T>(T a) { return a; }
}
";

        // Translated once and shared: a side compilation takes seconds.
        static UdonScriptTranslation result;

        [UnitySetUp]
        public IEnumerator Translate()
        {
            if (!CompilationPipeline.GetAssemblies().Any(a => a.name == UdonScriptTranslator.RuntimeAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            if (result != null)
            {
                yield break;
            }

            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }

            string source = Path.Combine(Root, "Source~");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "TestLock.cs"), Lock);
            File.WriteAllText(Path.Combine(source, "TestLock.cs.meta"), "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n");
            File.WriteAllText(Path.Combine(source, "TestUnknown.cs"), Unknown);
            File.WriteAllText(Path.Combine(source, "TestGeneric.cs"), Generic);

            UdonScriptTranslation done = null;
            UdonScriptTranslator.Run(new UdonScriptTranslation
            {
                SourceFolder = source,
                OutputFolder = Path.Combine(Root, "Out"),
                AssemblyName = "Test.UdonScripts",
            }, t => done = t);

            double limit = EditorApplication.timeSinceStartup + 300;
            while (done == null && EditorApplication.timeSinceStartup < limit)
            {
                yield return null;
            }

            result = done;
        }

        [OneTimeTearDown]
        public void Clean()
        {
            result = null;
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        [UnityTest]
        public IEnumerator SecondTranslationReadsItsOwnTypes()
        {
            if (!CompilationPipeline.GetAssemblies().Any(a => a.name == UdonScriptTranslator.RuntimeAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            // The same class translated twice in one editor session, with a method added in between:
            // the second companion must dispatch the new method.
            string root = Root + "Twice";
            UdonScriptTranslation done = null;
            foreach (string method in new[] { "First", "Second" })
            {
                string source = Path.Combine(root, method, "Source~");
                Directory.CreateDirectory(source);
                File.WriteAllText(Path.Combine(source, "TestTwice.cs"),
                    "using UdonSharp;\npublic class TestTwice : UdonSharpBehaviour\n{\n    public void " + method + "() { }\n}\n");
                done = null;
                UdonScriptTranslator.Run(new UdonScriptTranslation
                {
                    SourceFolder = source,
                    OutputFolder = Path.Combine(root, method, "Out"),
                    AssemblyName = "Test.Twice",
                }, t => done = t);

                double limit = EditorApplication.timeSinceStartup + 300;
                while (done == null && EditorApplication.timeSinceStartup < limit)
                {
                    yield return null;
                }
            }

            string companion = File.ReadAllText(Path.Combine(root, "Second", "Out", "TestTwice.udon.g.cs"));
            Directory.Delete(root, true);
            Assert.IsNotNull(done, "the translation finished");
            StringAssert.Contains("case \"Second\": Second(); return true;", companion);
            StringAssert.DoesNotContain("First()", companion);
        }

        [UnityTest]
        public IEnumerator FinishesWhenEverySourceIsExcluded()
        {
            if (!CompilationPipeline.GetAssemblies().Any(a => a.name == UdonScriptTranslator.RuntimeAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            string root = Root + "AllExcluded";
            string source = Path.Combine(root, "Source~");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "TestUnknown.cs"), Unknown);
            UdonScriptTranslation done = null;
            UdonScriptTranslator.Run(new UdonScriptTranslation
            {
                SourceFolder = source,
                OutputFolder = Path.Combine(root, "Out"),
                AssemblyName = "Test.AllExcluded",
            }, t => done = t);

            double limit = EditorApplication.timeSinceStartup + 300;
            while (done == null && EditorApplication.timeSinceStartup < limit)
            {
                yield return null;
            }

            Directory.Delete(root, true);
            Assert.IsNotNull(done, "the translation finished");
            CollectionAssert.IsEmpty(done.Translated);
            CollectionAssert.AreEquivalent(new[] { "TestUnknown.cs" }, done.Excluded);
        }

        [UnityTest]
        public IEnumerator SetsAsideRefusedClassesAndTheirDependents()
        {
            if (!CompilationPipeline.GetAssemblies().Any(a => a.name == UdonScriptTranslator.RuntimeAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            string root = Root + "Refused";
            string source = Path.Combine(root, "Source~");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "TestBad.cs"),
                "using UdonSharp;\npublic class TestBad : UdonSharpBehaviour\n{\n    public void Go() { }\n}\n");
            File.WriteAllText(Path.Combine(source, "TestUser.cs"),
                "using UdonSharp;\npublic class TestUser : UdonSharpBehaviour\n{\n    public TestBad bad;\n    public void Use() { bad.Go(); }\n}\n");
            File.WriteAllText(Path.Combine(source, "TestFine.cs"),
                "using UdonSharp;\npublic class TestFine : UdonSharpBehaviour\n{\n    public void Run() { }\n}\n");
            UdonScriptTranslation translation = new UdonScriptTranslation
            {
                SourceFolder = source,
                OutputFolder = Path.Combine(root, "Out"),
                AssemblyName = "Test.Refused",
            };
            translation.RefusedClasses.Add("TestBad");
            UdonScriptTranslation done = null;
            UdonScriptTranslator.Run(translation, t => done = t);

            double limit = EditorApplication.timeSinceStartup + 300;
            while (done == null && EditorApplication.timeSinceStartup < limit)
            {
                yield return null;
            }

            Directory.Delete(root, true);
            Assert.IsNotNull(done, "the translation finished");
            CollectionAssert.AreEquivalent(new[] { "TestFine.cs" }, done.Translated);
            CollectionAssert.AreEquivalent(new[] { "TestBad.cs", "TestUser.cs" }, done.Excluded);
            Assert.IsTrue(done.Diagnostics.Any(d => d.Code == "script.cilbox.excluded"));
        }

        [UnityTest]
        public IEnumerator RetranslationRemovesWhatItLeavesOut()
        {
            if (!CompilationPipeline.GetAssemblies().Any(a => a.name == UdonScriptTranslator.RuntimeAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            string root = Root + "Again";
            string source = Path.Combine(root, "Source~");
            string output = Path.Combine(root, "Out");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "TestBad.cs"),
                "using UdonSharp;\npublic class TestBad : UdonSharpBehaviour\n{\n    public void Go() { }\n}\n");
            File.WriteAllText(Path.Combine(source, "TestFine.cs"),
                "using UdonSharp;\npublic class TestFine : UdonSharpBehaviour\n{\n    public void Run() { }\n}\n");
            foreach (bool refuse in new[] { false, true })
            {
                UdonScriptTranslation translation = new UdonScriptTranslation
                {
                    SourceFolder = source,
                    OutputFolder = output,
                    AssemblyName = "Test.Again",
                };
                if (refuse)
                {
                    translation.RefusedClasses.Add("TestBad");
                    File.WriteAllText(Path.Combine(output, "notes.json"), "{}");
                }

                UdonScriptTranslation done = null;
                UdonScriptTranslator.Run(translation, t => done = t);
                double limit = EditorApplication.timeSinceStartup + 300;
                while (done == null && EditorApplication.timeSinceStartup < limit)
                {
                    yield return null;
                }

                Assert.IsNotNull(done, "the translation finished");
            }

            bool badLeft = File.Exists(Path.Combine(output, "TestBad.cs"));
            bool fineWritten = File.Exists(Path.Combine(output, "TestFine.cs"));
            bool notesKept = File.Exists(Path.Combine(output, "notes.json"));
            Directory.Delete(root, true);
            Assert.IsFalse(badLeft, "the refused script from the first run is gone");
            Assert.IsTrue(fineWritten);
            Assert.IsTrue(notesKept, "files the translator did not write stay");
        }

        // The check belongs to the runtime package; its own test fixtures hold one script Cilbox refuses.
        [Test]
        public void CheckLoadReportsRefusedScripts()
        {
            const string fixtures = "yuna0x0.Basis.Udon.Tests.Fixtures";
            if (!UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Any(a => a.GetName().Name == fixtures))
            {
                Assert.Ignore("The runtime package's test fixtures are not loaded.");
            }

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            // The check refuses to run over unsaved scenes; earlier tests leave one.
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            UdonScriptTranslation translation = new UdonScriptTranslation { AssemblyName = fixtures };
            CollectionAssert.AreEqual(new[] { "FixtureRefused" }, UdonScriptTranslator.CheckLoad(translation),
                string.Join("\n", translation.Diagnostics));
            ConversionDiagnostic refused = translation.Diagnostics.Single(d => d.Code == "script.cilbox.refused");
            StringAssert.Contains("allowHDR", refused.Message);
        }

        [Test]
        public void SetsUpSceneThroughTheRuntime()
        {
            if (!UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Any(a => a.GetName().Name == UdonScriptTranslator.RuntimeEditorAssembly))
            {
                Assert.Ignore("The runtime package is not installed.");
            }

            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            UdonScriptTranslation translation = new UdonScriptTranslation();
            UdonScriptTranslator.SetUpScene(scene, translation);
            Assert.IsTrue(translation.Diagnostics.HasCode("script.scene.driver.added"), string.Join("\n", translation.Diagnostics));
            Assert.IsTrue(translation.Diagnostics.HasCode("script.scene.cilbox.added"));
            Assert.AreEqual(DiagnosticSeverity.Warning, translation.Diagnostics.First(d => d.Code == "script.scene.spawnPoint.missing").Severity);
        }

        [Test]
        public void ReadsWhatThePostProcessorRewrote()
        {
            const string fixtures = "yuna0x0.Basis.Udon.Tests.Fixtures";
            if (!UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Any(a => a.GetName().Name == fixtures))
            {
                Assert.Ignore("The runtime package's test fixtures are not loaded.");
            }

            UdonScriptTranslation translation = new UdonScriptTranslation { AssemblyName = fixtures };
            UdonScriptTranslator.ReadRewrites(translation);
            ConversionDiagnostic order = translation.Diagnostics.Single(d => d.Message.StartsWith("FixtureOrder:"));
            Assert.AreEqual("script.rewritten", order.Code);
            StringAssert.Contains("updateOrder", order.Message);
        }

        [Test]
        public void ConvertsWhatCompilesAndReportsTheRest()
        {
            Assert.IsNotNull(result, "the translation finished");
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Diagnostics));
            CollectionAssert.AreEquivalent(new[] { "TestLock.cs" }, result.Translated);
            CollectionAssert.AreEquivalent(new[] { "TestUnknown.cs", "TestGeneric.cs" }, result.Excluded);
            Assert.IsTrue(result.Diagnostics.HasCode("script.compile.excluded"));
            Assert.IsTrue(result.Diagnostics.HasCode("script.genericMethod.excluded"));
            Assert.IsTrue(result.Diagnostics.HasCode("script.editorCode.dropped"));
        }

        [Test]
        public void WritesSourceCompanionAndAsmdef()
        {
            string output = Path.Combine(Root, "Out");
            string script = File.ReadAllText(Path.Combine(output, "TestLock.cs"));
            StringAssert.Contains("public partial class TestLock", script);
            StringAssert.Contains("0123456789abcdef0123456789abcdef", File.ReadAllText(Path.Combine(output, "TestLock.cs.meta")));

            string companion = File.ReadAllText(Path.Combine(output, "TestLock.udon.g.cs"));
            StringAssert.Contains("case \"Refresh\": Refresh(); return true;", companion);
            StringAssert.Contains("writer.Write(isLocked);", companion);
            StringAssert.Contains("__HasInteract() { return true; }", companion);

            string asmdef = File.ReadAllText(Path.Combine(output, "Test.UdonScripts.asmdef"));
            StringAssert.Contains(UdonScriptTranslator.RuntimeAssembly, asmdef);
            StringAssert.Contains("COMPILER_UDONSHARP", asmdef);
        }
    }
}
