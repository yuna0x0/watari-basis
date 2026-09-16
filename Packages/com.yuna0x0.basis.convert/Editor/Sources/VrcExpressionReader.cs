using System.Collections.Generic;
using System.Text.RegularExpressions;
using yuna0x0.Basis.Convert.Model;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Reads VRChat expression menu and parameter assets.
    /// <para>
    /// Both are ScriptableObjects from the VRChat SDK, so in a Basis project their scripts are
    /// missing and the data is read from the asset's YAML, the same way components are.
    /// </para>
    /// </summary>
    public static class VrcExpressionReader
    {
        /// <summary>Indent of one entry in the menu's own controls list.</summary>
        private const int ControlIndent = 2;

        private static int IndentOf(string line)
        {
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            return indent;
        }

        private static readonly Regex FieldPattern = new Regex(
            @"^(?<indent>\s*)-?\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex GuidPattern = new Regex(
            @"guid:\s*(?<guid>[0-9a-fA-F]{32})", RegexOptions.Compiled);

        public static VrcExpressionMenu ReadMenu(UnityYamlDocument document, string guid)
        {
            VrcExpressionMenu menu = new VrcExpressionMenu
            {
                Guid = guid,
                Name = document.GetTopLevelValue("m_Name") ?? string.Empty,
            };

            if (!document.TryGetTopLevelBlock("controls", out List<string> block))
            {
                return menu;
            }

            VrcExpressionControl current = null;
            bool inParameterBlock = false;
            bool inSubParameters = false;

            foreach (string line in block)
            {
                // A control starts at the list's own indent. Deeper entries starting with a
                // dash belong to a nested list, subParameters being the one that matters, and
                // treating those as controls invents entries the menu does not have.
                int indent = IndentOf(line);
                bool startsEntry = indent == ControlIndent && line.TrimStart().StartsWith("-");

                if (startsEntry)
                {
                    current = new VrcExpressionControl();
                    menu.Controls.Add(current);
                    inParameterBlock = false;
                    inSubParameters = false;
                }

                if (inSubParameters && indent > ControlIndent
                    && line.TrimStart().StartsWith("-"))
                {
                    Match sub = FieldPattern.Match(line);
                    if (sub.Success && sub.Groups["key"].Value == "name"
                        && current != null)
                    {
                        current.SubParameters.Add(sub.Groups["value"].Value);
                    }

                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                Match match = FieldPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string key = match.Groups["key"].Value;
                string value = match.Groups["value"].Value;

                // "parameter:" opens a nested map whose only field is also called "name", so the
                // two have to be told apart by which block is currently open.
                if (key == "parameter" && string.IsNullOrEmpty(value))
                {
                    inParameterBlock = true;
                    inSubParameters = false;
                    continue;
                }

                if (key == "subParameters")
                {
                    // "subParameters: []" is an empty inline list with no block after it. Only a
                    // bare key opens one.
                    inSubParameters = string.IsNullOrEmpty(value);
                    inParameterBlock = false;
                    continue;
                }

                // Any other field of the control closes that block, so a list further down, such
                // as the labels a puppet carries, is not read as more parameter names.
                inSubParameters = false;

                switch (key)
                {
                    case "name":
                        if (inParameterBlock)
                        {
                            current.Parameter = value;
                            inParameterBlock = false;
                        }
                        else
                        {
                            current.Name = value;
                        }

                        break;

                    case "type":
                        if (UnityYamlValues.TryParseInt(value, out int type))
                        {
                            current.Type = (VrcExpressionControlType)type;
                        }

                        inParameterBlock = false;
                        break;

                    case "value":
                        if (UnityYamlValues.TryParseFloat(value, out float number))
                        {
                            current.Value = number;
                        }

                        inParameterBlock = false;
                        break;

                    case "icon":
                        current.HasIcon = GuidPattern.IsMatch(value);
                        inParameterBlock = false;
                        break;

                    case "subMenu":
                        Match guidMatch = GuidPattern.Match(value);
                        if (guidMatch.Success)
                        {
                            current.SubMenuGuid = guidMatch.Groups["guid"].Value;
                            Match idMatch = Regex.Match(value, @"fileID:\s*(?<id>-?\d+)");
                            if (idMatch.Success
                                && long.TryParse(idMatch.Groups["id"].Value, out long subMenuId)
                                && subMenuId != 11400000L)
                            {
                                current.SubMenuFileId = subMenuId;
                            }
                        }

                        inParameterBlock = false;
                        break;

                    default:
                        inParameterBlock = false;
                        break;
                }
            }

            return menu;
        }

        public static List<VrcExpressionParameter> ReadParameters(UnityYamlDocument document)
        {
            List<VrcExpressionParameter> parameters = new List<VrcExpressionParameter>();

            if (!document.TryGetTopLevelBlock("parameters", out List<string> block))
            {
                return parameters;
            }

            VrcExpressionParameter current = null;

            foreach (string line in block)
            {
                if (line.TrimStart().StartsWith("-"))
                {
                    current = new VrcExpressionParameter();
                    parameters.Add(current);
                }

                if (current == null)
                {
                    continue;
                }

                Match match = FieldPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string value = match.Groups["value"].Value;
                switch (match.Groups["key"].Value)
                {
                    case "name":
                        current.Name = value;
                        break;
                    case "valueType":
                        if (UnityYamlValues.TryParseInt(value, out int type))
                        {
                            current.Type = (VrcExpressionParameterType)type;
                        }

                        break;
                    case "saved":
                        current.Saved = UnityYamlValues.TryParseInt(value, out int saved)
                            && saved != 0;
                        break;
                    case "defaultValue":
                        if (UnityYamlValues.TryParseFloat(value, out float number))
                        {
                            current.DefaultValue = number;
                        }

                        break;
                    case "networkSynced":
                        current.NetworkSynced =
                            !UnityYamlValues.TryParseInt(value, out int synced) || synced != 0;
                        break;
                }
            }

            return parameters;
        }
    }
}
