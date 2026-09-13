using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// One "--- !u!&lt;classId&gt; &amp;&lt;fileId&gt;" document out of a Unity YAML asset file.
    /// </summary>
    public sealed class UnityYamlDocument
    {
        public int ClassId;
        public long FileId;
        /// <summary>
        /// A back reference to a component defined in a prefab this one is built from. It
        /// carries the identity and none of the data, so it is resolved but never read.
        /// </summary>
        public bool Stripped;

        /// <summary>Body lines, excluding the header and the type line that follows it.</summary>
        public List<string> Lines = new List<string>();

        /// <summary>Type line, for example "MonoBehaviour:" or "GameObject:".</summary>
        public string TypeName = string.Empty;

        private static readonly Regex ScalarPattern = new Regex(
            @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex FileIdPattern = new Regex(
            @"fileID:\s*(?<id>-?\d+)",
            RegexOptions.Compiled);

        private static readonly Regex GuidPattern = new Regex(
            @"guid:\s*(?<guid>[0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        /// <summary>
        /// Raw text of a top-level key, or null. Only looks at indent level 2, so nested keys of
        /// the same name do not shadow the one being asked for.
        /// </summary>
        public string GetTopLevelValue(string key)
        {
            foreach (string line in Lines)
            {
                if (IndentOf(line) != 2)
                {
                    continue;
                }

                Match match = ScalarPattern.Match(line);
                if (match.Success && match.Groups["key"].Value == key)
                {
                    return match.Groups["value"].Value;
                }
            }

            return null;
        }

        public bool TryGetTopLevelFileIdReference(string key, out long fileId)
        {
            fileId = 0;
            string raw = GetTopLevelValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            Match match = FileIdPattern.Match(raw);
            return match.Success
                && long.TryParse(match.Groups["id"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out fileId);
        }

        /// <summary>
        /// A top-level object reference such as {fileID: 123, guid: abc..., type: 3}. The guid
        /// is null for a reference inside the same file.
        /// </summary>
        public bool TryGetTopLevelObjectReference(string key, out string guid, out long fileId)
        {
            guid = null;
            fileId = 0;

            string raw = GetTopLevelValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            Match guidMatch = GuidPattern.Match(raw);
            if (guidMatch.Success)
            {
                guid = guidMatch.Groups["guid"].Value;
            }

            Match fileIdMatch = FileIdPattern.Match(raw);
            return fileIdMatch.Success
                && long.TryParse(fileIdMatch.Groups["id"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out fileId);
        }

        /// <summary>
        /// The (guid, fileId) pair identifying the script behind a MonoBehaviour document.
        /// For a loose .cs script the fileId is 11500000 and the guid identifies the script;
        /// for a type inside a DLL the fileId is a hash of the class name and the guid
        /// identifies the assembly.
        /// </summary>
        public bool TryGetScriptIdentity(out string guid, out long fileId)
        {
            guid = null;
            fileId = 0;

            string raw = GetTopLevelValue("m_Script");
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            Match guidMatch = GuidPattern.Match(raw);
            Match fileIdMatch = FileIdPattern.Match(raw);
            if (!guidMatch.Success || !fileIdMatch.Success)
            {
                return false;
            }

            guid = guidMatch.Groups["guid"].Value;
            return long.TryParse(fileIdMatch.Groups["id"].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out fileId);
        }

        /// <summary>
        /// The lines belonging to a top-level key, excluding the key line itself. Covers both a
        /// nested map, whose lines are indented further, and a sequence, whose "- " entries sit
        /// at the same indent as the key.
        /// </summary>
        public bool TryGetTopLevelBlock(string key, out List<string> block)
        {
            block = null;
            string prefix = "  " + key + ":";

            for (int i = 0; i < Lines.Count; i++)
            {
                string line = Lines[i];
                if (IndentOf(line) != 2 || !line.StartsWith(prefix))
                {
                    continue;
                }

                block = new List<string>();
                for (int j = i + 1; j < Lines.Count; j++)
                {
                    string candidate = Lines[j];
                    int indent = IndentOf(candidate);
                    bool isSequenceEntry = indent == 2
                        && candidate.Length > 2
                        && candidate[2] == '-';

                    if (indent > 2 || isSequenceEntry)
                    {
                        block.Add(candidate);
                        continue;
                    }

                    break;
                }

                return true;
            }

            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            return UnityYamlValues.TryParseFloat(GetTopLevelValue(key), out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            return UnityYamlValues.TryParseInt(GetTopLevelValue(key), out value);
        }

        /// <summary>
        /// The component's <c>m_Enabled</c> flag. Absent means enabled, which is what Unity
        /// writes for types without one.
        /// </summary>
        public bool IsEnabled => !TryGetBool("m_Enabled", out bool enabled) || enabled;

        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            if (!UnityYamlValues.TryParseInt(GetTopLevelValue(key), out int raw))
            {
                return false;
            }

            value = raw != 0;
            return true;
        }

        public bool TryGetVector3(string key, out Vector3 value)
        {
            return UnityYamlValues.TryParseVector3(GetTopLevelValue(key), out value);
        }

        public bool TryGetQuaternion(string key, out Quaternion value)
        {
            return UnityYamlValues.TryParseQuaternion(GetTopLevelValue(key), out value);
        }

        /// <summary>
        /// File identifiers referenced by a sequence of object references. Null references,
        /// written as {fileID: 0}, are dropped: the VRChat inspector leaves them behind when a
        /// list entry is cleared, and they are not meaningful.
        /// </summary>
        public List<long> GetFileIdList(string key)
        {
            List<long> ids = new List<long>();

            string inline = GetTopLevelValue(key);
            if (!string.IsNullOrEmpty(inline) && inline.StartsWith("["))
            {
                // "[]" or an inline sequence.
                foreach (Match match in FileIdPattern.Matches(inline))
                {
                    AddFileId(ids, match);
                }

                return ids;
            }

            if (!TryGetTopLevelBlock(key, out List<string> block))
            {
                return ids;
            }

            foreach (string line in block)
            {
                foreach (Match match in FileIdPattern.Matches(line))
                {
                    AddFileId(ids, match);
                }
            }

            return ids;
        }

        /// <summary>
        /// An AnimationCurve block. Returns false when the key is absent; returns true with an
        /// empty curve when the block is present but holds no keyframes.
        /// </summary>
        public bool TryGetCurve(string key, out AnimationCurve curve)
        {
            curve = null;
            if (!TryGetTopLevelBlock(key, out List<string> block))
            {
                return false;
            }

            curve = UnityYamlValues.ParseCurveKeyframes(block);
            return true;
        }

        private static void AddFileId(List<long> ids, Match match)
        {
            if (long.TryParse(match.Groups["id"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long id) && id != 0L)
            {
                ids.Add(id);
            }
        }

        private static int IndentOf(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ')
            {
                i++;
            }

            return i;
        }
    }
}
