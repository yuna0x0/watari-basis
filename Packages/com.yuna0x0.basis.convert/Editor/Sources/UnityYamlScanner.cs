using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Splits a Unity YAML asset file into its documents.
    /// <para>
    /// This deliberately does not use a general YAML parser. Unity emits a narrow, regular
    /// subset, and the data this package reads out of it is flat scalars, inline maps such as
    /// {x: 0, y: 0, z: 0} and {fileID: 123}, and AnimationCurve blocks. A focused scanner is
    /// more predictable than a general parser here, and keeps the package dependency free.
    /// </para>
    /// </summary>
    public static class UnityYamlScanner
    {
        public const int ClassIdGameObject = 1;
        public const int ClassIdMonoBehaviour = 114;
        public const int ClassIdPrefabInstance = 1001;

        private static readonly Regex HeaderPattern = new Regex(
            @"^--- !u!(?<class>\d+) &(?<file>-?\d+)(?<stripped>\s+stripped)?\s*$",
            RegexOptions.Compiled);

        public static List<UnityYamlDocument> ScanFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path is required", nameof(path));
            }

            // Not every asset worth planning is text. An FBX is a perfectly good avatar and is
            // binary, so reading one yields no documents rather than failing the conversion.
            try
            {
                return Scan(File.ReadLines(path));
            }
            catch (System.Exception)
            {
                return new List<UnityYamlDocument>();
            }
        }

        public static List<UnityYamlDocument> Scan(IEnumerable<string> lines)
        {
            List<UnityYamlDocument> documents = new List<UnityYamlDocument>();
            UnityYamlDocument current = null;
            bool expectingTypeLine = false;

            foreach (string line in lines)
            {
                Match header = HeaderPattern.Match(line);
                if (header.Success)
                {
                    current = new UnityYamlDocument
                    {
                        ClassId = int.Parse(header.Groups["class"].Value,
                            CultureInfo.InvariantCulture),
                        FileId = long.Parse(header.Groups["file"].Value,
                            CultureInfo.InvariantCulture),
                        Stripped = header.Groups["stripped"].Success,
                    };
                    documents.Add(current);
                    expectingTypeLine = true;
                    continue;
                }

                if (current == null)
                {
                    // %YAML / %TAG preamble.
                    continue;
                }

                if (expectingTypeLine)
                {
                    current.TypeName = line.TrimEnd().TrimEnd(':');
                    expectingTypeLine = false;
                    continue;
                }

                current.Lines.Add(line);
            }

            return documents;
        }
    }
}
