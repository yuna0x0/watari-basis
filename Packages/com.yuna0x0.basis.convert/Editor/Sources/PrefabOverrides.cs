using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>One entry of a PrefabInstance's modification list.</summary>
    public sealed class PrefabModification
    {
        public string TargetGuid = string.Empty;
        public long TargetFileId;
        public string PropertyPath = string.Empty;
        public string Value = string.Empty;

        /// <summary>The reference written when the property is an object, else fileID 0.</summary>
        public string ObjectReference = string.Empty;
    }

    /// <summary>
    /// Applies a prefab variant's overrides onto the documents of the prefabs it is built from.
    /// <para>
    /// A variant's file holds a PrefabInstance whose modification list names an object in a
    /// source prefab, a property path and a value. Reading the source prefab alone gives the base
    /// values, so a variant that retunes a PhysBone or renames a shape would convert with the
    /// wrong numbers. The overrides are written into the scanned documents' lines here, before
    /// any reader sees them, so every reader honours them without knowing.
    /// </para>
    /// </summary>
    public static class PrefabOverrides
    {
        private static readonly Regex TargetPattern = new Regex(
            @"target:\s*\{fileID:\s*(?<id>-?\d+),\s*guid:\s*(?<guid>[0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        private static readonly Regex IndexPattern = new Regex(
            @"^data\[(?<index>\d+)\]$", RegexOptions.Compiled);

        /// <summary>The modifications every PrefabInstance in these documents carries.</summary>
        public static List<PrefabModification> Read(List<UnityYamlDocument> documents)
        {
            List<PrefabModification> modifications = new List<PrefabModification>();
            foreach (UnityYamlDocument document in documents)
            {
                if (document.ClassId != UnityYamlScanner.ClassIdPrefabInstance)
                {
                    continue;
                }

                // The list sits under m_Modification, one level down, so it is found by name.
                PrefabModification current = null;
                bool inList = false;
                foreach (string line in document.Lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("m_Modifications:"))
                    {
                        inList = true;
                        continue;
                    }

                    if (!inList)
                    {
                        continue;
                    }

                    int indent = IndentOf(line);
                    if (indent <= 4 && !trimmed.StartsWith("- ") && !trimmed.StartsWith("type:"))
                    {
                        break;
                    }

                    if (trimmed.StartsWith("- target:"))
                    {
                        current = new PrefabModification();
                        modifications.Add(current);
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    Match target = TargetPattern.Match(trimmed);
                    if (target.Success)
                    {
                        current.TargetGuid = target.Groups["guid"].Value.ToLowerInvariant();
                        current.TargetFileId = long.Parse(target.Groups["id"].Value,
                            CultureInfo.InvariantCulture);
                    }
                    else if (trimmed.StartsWith("propertyPath:"))
                    {
                        current.PropertyPath = ValueOf(trimmed);
                    }
                    else if (trimmed.StartsWith("value:"))
                    {
                        current.Value = ValueOf(trimmed);
                    }
                    else if (trimmed.StartsWith("objectReference:"))
                    {
                        current.ObjectReference = ValueOf(trimmed);
                    }
                }
            }

            return modifications;
        }

        /// <summary>
        /// Writes each modification into the document it names, following a stripped document
        /// to the file that defines it. <paramref name="byGuid"/> maps a file's guid to its
        /// scanned documents; files not in the map are left alone.
        /// </summary>
        public static int Apply(
            List<PrefabModification> modifications,
            Dictionary<string, Dictionary<long, UnityYamlDocument>> byGuid)
        {
            int applied = 0;
            foreach (PrefabModification modification in modifications)
            {
                UnityYamlDocument document = Resolve(
                    modification.TargetGuid, modification.TargetFileId, byGuid, 0);
                if (document == null || document.Stripped)
                {
                    continue;
                }

                string value = IsReference(modification.ObjectReference)
                    ? modification.ObjectReference
                    : modification.Value;

                if (SetPath(document.Lines, modification.PropertyPath, value))
                {
                    applied++;
                }
            }

            return applied;
        }

        private static UnityYamlDocument Resolve(string guid, long fileId,
            Dictionary<string, Dictionary<long, UnityYamlDocument>> byGuid, int depth)
        {
            if (depth > 8 || string.IsNullOrEmpty(guid)
                || !byGuid.TryGetValue(guid, out Dictionary<long, UnityYamlDocument> documents)
                || !documents.TryGetValue(fileId, out UnityYamlDocument document))
            {
                return null;
            }

            if (!document.Stripped)
            {
                return document;
            }

            // A stripped document stands in for one defined further up; the override lands there.
            if (document.TryGetTopLevelObjectReference(
                    "m_CorrespondingSourceObject", out string sourceGuid, out long sourceFileId)
                && !string.IsNullOrEmpty(sourceGuid))
            {
                return Resolve(sourceGuid.ToLowerInvariant(), sourceFileId, byGuid, depth + 1);
            }

            return null;
        }

        private static bool IsReference(string objectReference)
        {
            return !string.IsNullOrEmpty(objectReference)
                && !objectReference.Replace(" ", "").Contains("fileID:0}");
        }

        /// <summary>
        /// Sets a property named the way Unity names it in a modification: keys separated by
        /// dots, <c>Array.data[i]</c> for a list entry, <c>Array.size</c> for its length. A list
        /// grown past its end repeats its last entry, which is what Unity does on resize.
        /// </summary>
        public static bool SetPath(List<string> lines, string propertyPath, string value)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            string[] parts = propertyPath.Split('.');
            return Set(lines, 0, lines.Count, 2, parts, 0, value);
        }

        private static bool Set(List<string> lines, int start, int end, int indent,
            string[] parts, int at, string value)
        {
            string key = parts[at];
            bool last = at == parts.Length - 1;

            int keyLine = FindKey(lines, start, end, indent, key);
            if (keyLine < 0)
            {
                return false;
            }

            int blockEnd = BlockEnd(lines, keyLine, end, indent);

            if (last)
            {
                lines[keyLine] = ReplaceValue(lines[keyLine], value);
                if (blockEnd > keyLine + 1)
                {
                    lines.RemoveRange(keyLine + 1, blockEnd - keyLine - 1);
                }

                return true;
            }

            if (parts[at + 1] == "Array")
            {
                if (at + 2 >= parts.Length)
                {
                    return false;
                }

                if (parts[at + 2] == "size")
                {
                    return Resize(lines, keyLine, blockEnd, indent, value);
                }

                Match index = IndexPattern.Match(parts[at + 2]);
                if (!index.Success)
                {
                    return false;
                }

                int wanted = int.Parse(index.Groups["index"].Value, CultureInfo.InvariantCulture);
                if (!TryEntry(lines, keyLine, ref blockEnd, indent, wanted,
                        out int entryStart, out int entryEnd))
                {
                    return false;
                }

                if (at + 3 >= parts.Length)
                {
                    // The entry is the value itself: a reference or scalar list.
                    lines[entryStart] = new string(' ', indent) + "- " + value;
                    if (entryEnd > entryStart + 1)
                    {
                        lines.RemoveRange(entryStart + 1, entryEnd - entryStart - 1);
                    }

                    return true;
                }

                // Fields of an entry sit two columns in from the dash, and the first field
                // shares the dash line. Normalising the dash to spaces lets FindKey see it.
                lines[entryStart] = Undash(lines[entryStart], indent);
                bool set = Set(lines, entryStart, entryEnd, indent + 2, parts, at + 3, value);
                lines[entryStart] = Redash(lines[entryStart], indent);
                return set;
            }

            return Set(lines, keyLine + 1, blockEnd, indent + 2, parts, at + 1, value);
        }

        private static int FindKey(List<string> lines, int start, int end, int indent, string key)
        {
            string prefix = new string(' ', indent) + key + ":";
            for (int i = start; i < end; i++)
            {
                string line = lines[i];
                if (IndentOf(line) == indent && line.StartsWith(prefix)
                    && (line.Length == prefix.Length || line[prefix.Length] == ' '))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The line after the last line belonging to the key at <paramref name="keyLine"/>.</summary>
        private static int BlockEnd(List<string> lines, int keyLine, int end, int indent)
        {
            int i = keyLine + 1;
            while (i < end)
            {
                string line = lines[i];
                int lineIndent = IndentOf(line);
                bool entry = lineIndent == indent && line.Length > indent && line[indent] == '-';
                if (lineIndent > indent || entry)
                {
                    i++;
                    continue;
                }

                break;
            }

            return i;
        }

        private static bool TryEntry(List<string> lines, int keyLine, ref int blockEnd, int indent,
            int wanted, out int entryStart, out int entryEnd)
        {
            List<int> starts = EntryStarts(lines, keyLine, blockEnd, indent);

            // Unity fills a grown list by repeating its last entry.
            while (starts.Count <= wanted && starts.Count > 0)
            {
                int lastStart = starts[starts.Count - 1];
                List<string> copy = lines.GetRange(lastStart, blockEnd - lastStart);
                lines.InsertRange(blockEnd, copy);
                blockEnd += copy.Count;
                starts = EntryStarts(lines, keyLine, blockEnd, indent);
            }

            if (wanted >= starts.Count)
            {
                entryStart = entryEnd = -1;
                return false;
            }

            entryStart = starts[wanted];
            entryEnd = wanted + 1 < starts.Count ? starts[wanted + 1] : blockEnd;
            return true;
        }

        private static List<int> EntryStarts(List<string> lines, int keyLine, int blockEnd, int indent)
        {
            List<int> starts = new List<int>();
            for (int i = keyLine + 1; i < blockEnd; i++)
            {
                string line = lines[i];
                if (IndentOf(line) == indent && line.Length > indent && line[indent] == '-')
                {
                    starts.Add(i);
                }
            }

            return starts;
        }

        private static bool Resize(List<string> lines, int keyLine, int blockEnd, int indent,
            string value)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int size))
            {
                return false;
            }

            List<int> starts = EntryStarts(lines, keyLine, blockEnd, indent);
            if (size < starts.Count)
            {
                lines.RemoveRange(starts[size], blockEnd - starts[size]);
                if (size == 0)
                {
                    lines[keyLine] = ReplaceValue(lines[keyLine], "[]");
                }

                return true;
            }

            if (size > starts.Count && starts.Count > 0)
            {
                int end = blockEnd;
                TryEntry(lines, keyLine, ref end, indent, size - 1, out int _, out int _);
            }

            return true;
        }

        private static string ReplaceValue(string line, string value)
        {
            int colon = line.IndexOf(':');
            return colon < 0 ? line : line.Substring(0, colon + 1) + " " + value;
        }

        private static string Undash(string line, int indent)
        {
            return line.Length > indent + 1 && line[indent] == '-' && line[indent + 1] == ' '
                ? line.Substring(0, indent) + "  " + line.Substring(indent + 2)
                : line;
        }

        private static string Redash(string line, int indent)
        {
            return line.Length > indent + 1 && line[indent] == ' ' && line[indent + 1] == ' '
                && IndentOf(line) == indent + 2
                ? line.Substring(0, indent) + "- " + line.Substring(indent + 2)
                : line;
        }

        private static string ValueOf(string trimmed)
        {
            int colon = trimmed.IndexOf(':');
            return colon < 0 ? string.Empty : trimmed.Substring(colon + 1).Trim();
        }

        private static int IndentOf(string line)
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
