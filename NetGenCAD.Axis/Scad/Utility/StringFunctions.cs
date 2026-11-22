using System.Text.RegularExpressions;

namespace NetGenCAD.Axis.Scad.Utility
{
    public static class StringFunctions
    {
        // Debugging seems to require local version of this function
        public static string FormatAxisDisplay(string? axisUsed)
        {
            if (axisUsed == "Select Axis") return "Select Axis";

            if (Regex.IsMatch(axisUsed?.ToString() ?? "", @"^Get")) // If the Calling Method has already been converted, then two-way is causing debugging exceptions
            {
                string formatted = $"{axisUsed?
                            .Replace("use <../Axes/axes.scad>; ", "")
                            .Replace("Get_", "")
                            .Replace("_", " ")
                            .Replace("();", "")
                            .Replace(" Orig ", ", Origin: ")
                            .Replace("N", "-", comparisonType: StringComparison.InvariantCulture)
                            .Replace("MM", "mm")
                            .Replace("Inch", "in")
                            .Replace("x", " x ")}"; // Format for display

                // Trim from ", Origin: " to the end of the string
                int originIndex = formatted.IndexOf(", Origin: ");
                if (originIndex >= 0)
                {
                    int mirrorIndex = formatted.IndexOf("Mirror", originIndex, StringComparison.Ordinal);
                    if (mirrorIndex >= 0)
                    {
                        return $"{formatted.Substring(0, originIndex)} {formatted.Substring(mirrorIndex)}";
                    }
                    return formatted.Substring(0, originIndex);
                }
                return formatted;
            }
            else
            {
#pragma warning disable CS8603 // Possible null reference return.
                return axisUsed; // Format it your damn self...
#pragma warning restore CS8603 // Possible null reference return.
            }
        }

        // For lowercase names, etc.
        public static string NameSanitize(string value)
        {
            System.Text.RegularExpressions.Regex NameSanitizerRegex = new System.Text.RegularExpressions.Regex(@"[^a-z0-9]+", System.Text.RegularExpressions.RegexOptions.Compiled);
            return NameSanitizerRegex.Replace(value.Trim().ToLower(), "_");
        }
    }
}
