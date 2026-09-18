using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Parsers for the handful of value shapes Unity writes inside a document body.
    /// </summary>
    public static class UnityYamlValues
    {
        private static readonly Regex FileIdPattern = new Regex(
            @"fileID:\s*(?<fileId>-?\d+)", RegexOptions.Compiled);

        private static readonly Regex ComponentPattern = new Regex(
            @"(?<axis>[xyzw])\s*:\s*(?<value>-?[\d.]+(?:[eE][-+]?\d+)?)",
            RegexOptions.Compiled);

        private static readonly Regex KeyValuePattern = new Regex(
            @"^\s*-?\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)\s*$",
            RegexOptions.Compiled);

        public static bool TryParseFloat(string raw, out float value)
        {
            value = 0f;
            return !string.IsNullOrEmpty(raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out value);
        }

        /// <summary>
        /// The fileID out of a `{fileID: 123}` reference anywhere in a line. Zero, which Unity
        /// writes for an unset reference, is read as absent.
        /// </summary>
        public static bool TryParseFileId(string raw, out long fileId)
        {
            fileId = 0L;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            Match match = FileIdPattern.Match(raw);
            return match.Success
                && long.TryParse(match.Groups["fileId"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out fileId)
                && fileId != 0L;
        }

        public static bool TryParseInt(string raw, out int value)
        {
            value = 0;
            return !string.IsNullOrEmpty(raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out value);
        }

        public static bool TryParseVector3(string raw, out Vector3 value)
        {
            value = Vector3.zero;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            bool any = false;
            foreach (Match match in ComponentPattern.Matches(raw))
            {
                if (!TryParseFloat(match.Groups["value"].Value, out float component))
                {
                    continue;
                }

                switch (match.Groups["axis"].Value)
                {
                    case "x": value.x = component; any = true; break;
                    case "y": value.y = component; any = true; break;
                    case "z": value.z = component; any = true; break;
                }
            }

            return any;
        }

        public static bool TryParseQuaternion(string raw, out Quaternion value)
        {
            value = Quaternion.identity;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            bool any = false;
            foreach (Match match in ComponentPattern.Matches(raw))
            {
                if (!TryParseFloat(match.Groups["value"].Value, out float component))
                {
                    continue;
                }

                switch (match.Groups["axis"].Value)
                {
                    case "x": value.x = component; any = true; break;
                    case "y": value.y = component; any = true; break;
                    case "z": value.z = component; any = true; break;
                    case "w": value.w = component; any = true; break;
                }
            }

            return any;
        }

        /// <summary>
        /// An int array Unity wrote as a hex byte blob rather than a sequence, which is how it
        /// serializes some small fixed arrays. Little-endian, four bytes per value, so
        /// "1d000000ffffffffffffffff" is 29, -1, -1.
        /// </summary>
        public static List<int> ParseHexInt32Blob(string raw)
        {
            List<int> values = new List<int>();
            if (string.IsNullOrEmpty(raw))
            {
                return values;
            }

            string hex = raw.Trim();
            for (int i = 0; i + 8 <= hex.Length; i += 8)
            {
                int value = 0;
                bool ok = true;

                // Little-endian: the first byte pair is the least significant.
                for (int b = 3; b >= 0; b--)
                {
                    if (!byte.TryParse(hex.Substring(i + (b * 2), 2), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out byte parsed))
                    {
                        ok = false;
                        break;
                    }

                    value = (value << 8) | parsed;
                }

                if (!ok)
                {
                    break;
                }

                values.Add(value);
            }

            return values;
        }

        /// <summary>
        /// Keyframes out of an AnimationCurve block's m_Curve sequence. An absent or empty
        /// m_Curve yields a curve with no keys, which is how Unity writes "no falloff".
        /// </summary>
        public static AnimationCurve ParseCurveKeyframes(IReadOnlyList<string> blockLines)
        {
            List<Keyframe> keys = new List<Keyframe>();

            bool inCurve = false;
            bool inKey = false;
            float time = 0f;
            float value = 0f;
            float inSlope = 0f;
            float outSlope = 0f;

            foreach (string line in blockLines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("m_Curve:"))
                {
                    inCurve = true;
                    continue;
                }

                if (!inCurve)
                {
                    continue;
                }

                if (trimmed.StartsWith("m_PreInfinity")
                    || trimmed.StartsWith("m_PostInfinity")
                    || trimmed.StartsWith("m_RotationOrder"))
                {
                    break;
                }

                bool startsEntry = trimmed.StartsWith("-");
                if (startsEntry)
                {
                    if (inKey)
                    {
                        keys.Add(new Keyframe(time, value, inSlope, outSlope));
                    }

                    inKey = true;
                    time = 0f;
                    value = 0f;
                    inSlope = 0f;
                    outSlope = 0f;
                }

                Match match = KeyValuePattern.Match(trimmed);
                if (!match.Success)
                {
                    continue;
                }

                string key = match.Groups["key"].Value;
                if (!TryParseFloat(match.Groups["value"].Value, out float parsed))
                {
                    continue;
                }

                switch (key)
                {
                    case "time": time = parsed; break;
                    case "value": value = parsed; break;
                    case "inSlope": inSlope = parsed; break;
                    case "outSlope": outSlope = parsed; break;
                }
            }

            if (inKey)
            {
                keys.Add(new Keyframe(time, value, inSlope, outSlope));
            }

            return new AnimationCurve(keys.ToArray());
        }
    }
}
