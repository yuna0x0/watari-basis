using System.Collections.Generic;
using NUnit.Framework;
using yuna0x0.Basis.Convert.Mapping;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Tests
{
    public class UdonSharpSourceRewriterTests
    {
        const string Script = @"using UdonSharp;
using UnityEngine;

public enum Mode { A, B }

public abstract class Base : UdonSharpBehaviour
{
    public abstract int Value();
    protected abstract void Apply(int amount);
}

public class Door : Base
{
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    private void OnValidate() { }
#endif
#if !UNITY_EDITOR
    private int platform = 1;
#endif
    public override int Value() { foreach (Transform child in transform) { } return 1; }
    protected override void Apply(int amount) { }
}
";

        [Test]
        public void LeavesDeclarationsAsTheyAre()
        {
            var result = UdonSharpSourceRewriter.Rewrite(Script, "Door.cs");
            StringAssert.Contains("public abstract class Base : UdonSharpBehaviour", result.Text);
            StringAssert.Contains("public enum Mode { A, B }", result.Text);
            CollectionAssert.AreEquivalent(new[] { "Base", "Door" }, result.ClassNames);
            CollectionAssert.AreEquivalent(new[] { "Mode" }, result.EnumNames);
        }

        [Test]
        public void InsertsPartialWhereTheCompilerPoints()
        {
            // CS0260 points at the type name: line 6, column 23 is "Base" in the script above.
            string text = UdonSharpSourceRewriter.InsertPartial(Script, 6, 23);
            StringAssert.Contains("public abstract partial class Base : UdonSharpBehaviour", text);
            string door = UdonSharpSourceRewriter.InsertPartial("public class Door\n    : Base { }", 1, 14);
            Assert.AreEqual("public partial class Door\n    : Base { }", door);
            Assert.IsNull(UdonSharpSourceRewriter.InsertPartial(Script, 4, 13), "an enum is not a class");
        }

        [Test]
        public void CompilesLikeAnUpload()
        {
            var result = UdonSharpSourceRewriter.Rewrite(Script, "Door.cs");
            StringAssert.Contains("#if " + UdonSharpSourceRewriter.UploadEditorSymbol + " && !COMPILER_UDONSHARP", result.Text);
            StringAssert.Contains("#if !" + UdonSharpSourceRewriter.UploadEditorSymbol, result.Text);
            StringAssert.DoesNotContain("UNITY_EDITOR ", result.Text.Replace(UdonSharpSourceRewriter.UploadEditorSymbol, ""));
            Assert.IsTrue(result.Diagnostics.HasCode("script.editorCode.dropped"));
        }

    }
}
