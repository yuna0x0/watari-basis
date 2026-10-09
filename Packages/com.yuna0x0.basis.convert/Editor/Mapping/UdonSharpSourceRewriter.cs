using System.Collections.Generic;
using System.Text.RegularExpressions;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Mapping
{
    /// <summary>
    /// The text edits made to a converted UdonSharp source. Everything that depends on types
    /// (component lookups, <c>typeof</c>, value semantics, enum attributes, abstract methods) is done
    /// on the compiled IL by the runtime package's post-processor; what is left is lexical:
    /// <list type="bullet">
    /// <item><c>UNITY_EDITOR</c> in preprocessor lines becomes <see cref="UploadEditorSymbol"/>, which is
    /// never defined. UdonSharp compiles uploads without <c>UNITY_EDITOR</c>, while Cilbox exports
    /// from the editor-compiled assembly, so without this a client would run the editor branch and
    /// editor tooling would reach the box. Directives are whole lines, so the edit is exact.</item>
    /// <item><c>partial</c>, inserted by <see cref="InsertPartial"/> where the compiler reports a
    /// declaration that lacks it (CS0260) once the companion is compiled beside it.</item>
    /// </list>
    /// Editor-only code is reported as dropped. Declared type names are collected only to trace a
    /// compile error back to the file that declares the type it names.
    /// </summary>
    public static class UdonSharpSourceRewriter
    {
        public sealed class Result
        {
            public string Text;

            /// <summary>Classes the source declares, so companions are generated for these and nothing else.</summary>
            public List<string> ClassNames = new List<string>();

            /// <summary>Enums the source declares, so a file that fails on one can be traced to its source.</summary>
            public List<string> EnumNames = new List<string>();

            public List<ConversionDiagnostic> Diagnostics = new List<ConversionDiagnostic>();
        }

        // Type names a file declares, for tracing errors to files; never used to edit text.
        static readonly Regex Declaration = new Regex(@"\b(?<kind>class|enum)\s+(?<name>[A-Za-z_]\w*)");

        public const string UploadEditorSymbol = "UNITY_EDITOR_IN_UDON_UPLOAD";

        static readonly Regex EditorDirective = new Regex(@"^(?<lead>[ \t]*#\s*(?:if|elif)\b)(?<cond>[^\r\n]*)$", RegexOptions.Multiline);
        static readonly Regex EditorSymbol = new Regex(@"\bUNITY_EDITOR\b");
        static readonly Regex EditorOnlyBlock = new Regex(@"^[ \t]*#\s*if\s+(?:\(?\s*)?(?:UNITY_EDITOR\s*&&\s*!\s*COMPILER_UDONSHARP|!\s*COMPILER_UDONSHARP\s*&&\s*UNITY_EDITOR|UNITY_EDITOR)\s*\)?\s*$", RegexOptions.Multiline);

        public static Result Rewrite(string source, string fileName)
        {
            Result result = new Result();
            string text = source;

            foreach (Match match in Declaration.Matches(text))
            {
                (match.Groups["kind"].Value == "class" ? result.ClassNames : result.EnumNames).Add(match.Groups["name"].Value);
            }

            int editorBlocks = EditorOnlyBlock.Matches(text).Count;
            if (editorBlocks > 0)
            {
                result.Diagnostics.Add(DiagnosticSeverity.Dropped, "script.editorCode.dropped",
                    $"{fileName} has {editorBlocks} editor-only block(s), such as OnValidate or an inspector; they are left out as in a VRChat upload.");
            }

            text = EditorDirective.Replace(text, match =>
                match.Groups["lead"].Value + EditorSymbol.Replace(match.Groups["cond"].Value, UploadEditorSymbol));

            result.Text = text;
            return result;
        }

        /// <summary>
        /// Inserts <c>partial</c> before the <c>class</c> keyword of the declaration whose name starts
        /// at the given 1-based line and column, as the compiler reports CS0260. Null when the text
        /// there is not such a declaration.
        /// </summary>
        public static string InsertPartial(string text, int line, int column)
        {
            int index = 0;
            for (int l = 1; l < line; l++)
            {
                index = text.IndexOf('\n', index);
                if (index < 0) return null;
                index++;
            }

            index += column - 1;
            int k = index - 1;
            while (k >= 0 && char.IsWhiteSpace(text[k])) k--;
            int end = k + 1;
            while (k >= 0 && (char.IsLetterOrDigit(text[k]) || text[k] == '_')) k--;
            int start = k + 1;
            string keyword = text.Substring(start, end - start);
            if (keyword != "class" && keyword != "struct")
            {
                return null;
            }

            return text.Substring(0, start) + "partial " + text.Substring(start);
        }
    }
}
