using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// A nested piece of a Unity YAML document, read by indentation.
    /// <para>
    /// The document reader answers top-level keys. Data serialized as managed references sits
    /// several levels down, under <c>references.RefIds[].data</c>, with lists and maps of its
    /// own, so this walks a block by its indent: a child block is the lines indented past a key,
    /// and a sequence is split on the dashes at its own indent, the way Unity writes a list
    /// under a key at the key's indent.
    /// </para>
    /// </summary>
    public sealed class UnityYamlBlock
    {
        private static readonly Regex FlowPair = new Regex(
            @"(?<key>[A-Za-z_][A-Za-z0-9_]*):\s*(?<value>-?[0-9A-Za-z_.\-]+)", RegexOptions.Compiled);

        public readonly List<string> Lines;
        public readonly int Indent;

        private UnityYamlBlock(List<string> lines, int indent)
        {
            Lines = lines;
            Indent = indent;
        }

        public static UnityYamlBlock Of(List<string> lines)
        {
            int indent = int.MaxValue;
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                indent = System.Math.Min(indent, IndentOf(line));
            }

            return new UnityYamlBlock(lines, indent == int.MaxValue ? 0 : indent);
        }

        public bool IsEmpty => Lines.Count == 0;

        /// <summary>The raw value after <c>key:</c> at this block's own indent, or null.</summary>
        public string Scalar(string key)
        {
            string prefix = key + ":";
            foreach (string line in Lines)
            {
                if (IndentOf(line) != Indent)
                {
                    continue;
                }

                string body = line.Substring(Indent);
                if (body.StartsWith("- "))
                {
                    body = "  " + body.Substring(2);
                    body = body.TrimStart();
                }

                if (body.StartsWith(prefix))
                {
                    return body.Substring(prefix.Length).Trim();
                }
            }

            return null;
        }

        public bool Bool(string key)
        {
            return Scalar(key) == "1";
        }

        public int Int(string key, int fallback = 0)
        {
            return int.TryParse(Scalar(key), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value) ? value : fallback;
        }

        public long Long(string key, long fallback = 0L)
        {
            return long.TryParse(Scalar(key), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out long value) ? value : fallback;
        }

        public float Float(string key, float fallback = 0f)
        {
            return float.TryParse(Scalar(key), NumberStyles.Float, CultureInfo.InvariantCulture,
                out float value) ? value : fallback;
        }

        public string Text(string key)
        {
            return Scalar(key) ?? string.Empty;
        }

        /// <summary>The file id inside a <c>{fileID: n}</c> reference, or 0.</summary>
        public long FileId(string key)
        {
            Dictionary<string, string> flow = Flow(key);
            return flow != null && flow.TryGetValue("fileID", out string id)
                && long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out long fileId)
                ? fileId
                : 0L;
        }

        /// <summary>The guid inside a <c>{fileID: n, guid: g, type: t}</c> reference, or null.</summary>
        public string Guid(string key)
        {
            Dictionary<string, string> flow = Flow(key);
            return flow != null && flow.TryGetValue("guid", out string guid) ? guid : null;
        }

        /// <summary>The pairs of a flow map written on one line, <c>{a: 1, b: x}</c>.</summary>
        public Dictionary<string, string> Flow(string key)
        {
            string raw = Scalar(key);
            if (raw == null || !raw.StartsWith("{"))
            {
                return null;
            }

            Dictionary<string, string> pairs = new Dictionary<string, string>();
            foreach (Match match in FlowPair.Matches(raw))
            {
                pairs[match.Groups["key"].Value] = match.Groups["value"].Value;
            }

            return pairs;
        }

        /// <summary>
        /// The lines nested under <c>key:</c>, or an empty block. A list written at the key's own
        /// indent belongs to it, so dashes at this indent right after the key are taken too.
        /// </summary>
        public UnityYamlBlock Child(string key)
        {
            string prefix = key + ":";
            List<string> child = new List<string>();

            for (int i = 0; i < Lines.Count; i++)
            {
                string line = Lines[i];
                if (IndentOf(line) != Indent || !line.Substring(Indent).StartsWith(prefix))
                {
                    continue;
                }

                for (int j = i + 1; j < Lines.Count; j++)
                {
                    string candidate = Lines[j];
                    int indent = IndentOf(candidate);
                    bool dashAtOwnIndent = indent == Indent
                        && candidate.Substring(Indent).StartsWith("- ");

                    if (candidate.Trim().Length == 0 || indent > Indent || dashAtOwnIndent)
                    {
                        child.Add(candidate);
                        continue;
                    }

                    break;
                }

                break;
            }

            return Of(child);
        }

        /// <summary>
        /// The entries of a sequence, each as a block of its own with the dash replaced by
        /// spaces so its keys line up.
        /// </summary>
        public List<UnityYamlBlock> Entries()
        {
            List<List<string>> entries = new List<List<string>>();
            List<string> current = null;

            foreach (string line in Lines)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                int indent = IndentOf(line);
                if (indent == Indent && line.Substring(Indent).StartsWith("- "))
                {
                    current = new List<string>();
                    entries.Add(current);
                    current.Add(line.Substring(0, Indent) + "  " + line.Substring(Indent + 2));
                    continue;
                }

                current?.Add(line);
            }

            List<UnityYamlBlock> blocks = new List<UnityYamlBlock>();
            foreach (List<string> entry in entries)
            {
                blocks.Add(new UnityYamlBlock(entry, Indent + 2));
            }

            return blocks;
        }

        public static int IndentOf(string line)
        {
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            return indent;
        }
    }
}
