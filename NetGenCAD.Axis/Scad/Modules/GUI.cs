using NetGenCAD.Core.Measurements;
using NetGenCAD.Core.Utility;
using System.Text.RegularExpressions;
using static NetGenCAD.Axis.SCAD.Modules.Axis;
using static NetGenCAD.Axis.SCAD.Utility.AxisConfig;

namespace NetGenCAD.Axis.SCAD.Modules
{
    public static class GUI
    {
        // OpenSCAD GUI axis - Saves into main axes file that can be called from your main SCAD project file
        public static async Task<CustomAxis> SetAxis(AxisSettings axSet, CancellationToken cancellationToken = default)
        {
            // Validate and configure axis settings
            // Stateless - always re-check settings
            var _axisSettings = CheckAxisSettings(axSet);
            var _customAxis = ConfigureAxisModule(_axisSettings);
            var _moduleNameLower = _customAxis.ModuleName.ToLower();

            // Write to file - import into output SCAD file
            var _axisFilePath = Path.Combine(_customAxis.Settings.OutputDirectory, $"{_moduleNameLower}.scad");

            // Since this is a code-driven module, always overwrite generated module since it regenerates in real-time
            await Output.WriteToSCAD(content: _customAxis.AxisModule, filePath: _axisFilePath, overWrite: true, cancellationToken: cancellationToken);

            // Store in Axes reference file - Comments and Instructions
            var _moduleComment = $"// 3D Axis Module - {_customAxis.ModuleName.Replace("_", " ")}\n" +
                $"// Calling Method: Get_{_customAxis.CallingMethod}\n" +
                $"// Settings: UnitSystem={_customAxis.Settings.UnitSystem}, BackgroundType={_customAxis.Settings.BackgroundType}, MinX={_customAxis.Settings.MinX}, MaxX={_customAxis.Settings.MaxX}, MinY={_customAxis.Settings.MinY}, " +
                $"MaxY={_customAxis.Settings.MaxY}, MinZ={_customAxis.Settings.MinZ}, MaxZ={_customAxis.Settings.MaxZ}";

            // Custom color for axis lines and labels
            var _axisColor = _customAxis.Settings.AxisColor;
            var _colorAlpha = _customAxis.Settings.AxisColorAlpha.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Store in Axes reference file - Module definition and contents
            var _scadFilePath = Path.Combine(_customAxis.Settings.OutputDirectory, $"axes.scad");
            var _importStatement = $"{_moduleComment}\n" +
                $"include <{_moduleNameLower}.scad>;\n" +
                $"module Get_{_customAxis.ModuleName}(colorVal = \"{_axisColor}\", " +
                $"alpha = {_colorAlpha}) {{\n" +
                $"  {_moduleNameLower}(colorVal = colorVal, alpha = alpha);\n" +
                $"}}\n\n";
            await Output.AppendToSCAD(content: _importStatement, filePath: _scadFilePath, cancellationToken: cancellationToken);
            // AxesModule is the content of the generated module, while the import statement is what gets added to axes.scad for reference. The import statement includes the module definition and comments for clarity.
            _customAxis.AxesModule = _importStatement;
            return _customAxis;
        }

        /// <summary>Removes generated entries for a calling method and their axis file.</summary>
        /// <param name="outputDirectory">Directory containing axes.scad and the generated axis files.</param>
        /// <param name="callingMethod">The module call returned by SetAxis, for example Get_Light_10x10x10_MM_Orig_0x0x0();.</param>
        /// <remarks>The caller is responsible for any database changes. File deletion errors are propagated after axes.scad is updated.</remarks>
        public static async Task<string> RemoveAxis(string callingMethod, CancellationToken cancellationToken = default)
        {
            var _axisSettings = CheckAxisSettings(new AxisSettings(outputDirectory: ""));
            var _customAxis = ConfigureAxisModule(_axisSettings);
            var _moduleNameLower = _customAxis.ModuleName.ToLower();

            // Write to file - import into output SCAD file
            var outputDirectory = _customAxis.Settings.OutputDirectory;
            var _axesFilePath = Path.Combine(outputDirectory, $"axes.scad");

            var method = Regex.Match(callingMethod.Trim(), @"^([^\s();{}]+)\(\);$");
            if (!method.Success)
                throw new ArgumentException("Expected a calling method in the form ModuleName();.", nameof(callingMethod));

            var moduleName = method.Groups[1].Value;
            string content;
            System.Text.Encoding encoding;
            using (var reader = new StreamReader(_axesFilePath, detectEncodingFromByteOrderMarks: true))
            {
                content = await reader.ReadToEndAsync(cancellationToken);
                encoding = reader.CurrentEncoding;
            }

            var escapedModule = Regex.Escape(moduleName);
            var entryPattern = @"(?m)^// 3D Axis Module - [^\r\n]*\r?\n" +
                @"// Calling Method: [^\r\n]*\r?\n// Settings: [^\r\n]*\r?\n" +
                @"include[ \t]+<(?<file>[^>\r\n]+)>;[ \t]*\r?\n" +
                @"module[ \t]+" + escapedModule + @"[ \t]*\([^\r\n]*\)[ \t]*\{\r?\n" +
                @"[^{}]*^\}[ \t]*(?:\r?\n|$)(?:[ \t]*\r?\n)?";
            var matches = Regex.Matches(content, entryPattern);
            if (matches.Count == 0)
                throw new KeyNotFoundException($"No generated axis entry found for '{callingMethod}' in '{_axesFilePath}'.");

            var axisFile = matches[0].Groups["file"].Value;
            if (axisFile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                axisFile.Contains('/') || axisFile.Contains('\\') ||
                !axisFile.EndsWith(".scad", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(axisFile, "axes.scad", StringComparison.OrdinalIgnoreCase) ||
                matches.Cast<Match>().Any(match => !string.Equals(match.Groups["file"].Value, axisFile, StringComparison.Ordinal)))
                throw new InvalidDataException("The axis entries contain an invalid or inconsistent include filename.");

            var remaining = Regex.Replace(content, entryPattern, string.Empty);
            var axisPath = Path.GetFullPath(Path.Combine(outputDirectory, axisFile));
            var directoryPath = Path.GetFullPath(outputDirectory.Length == 0 ? "." : outputDirectory);
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var references = Regex.Matches(remaining, @"\b(?:include|use)[ \t]*<(?<file>[^>\r\n]+)>");
            if (Regex.IsMatch(remaining, @"(?m)^[ \t]*module[ \t]+" + escapedModule + @"[ \t]*\(") ||
                references.Cast<Match>().Any(reference => string.Equals(
                    Path.GetFullPath(reference.Groups["file"].Value.Trim(), directoryPath), axisPath, pathComparison)))
                throw new InvalidDataException("The axis module or its file is still referenced by another entry in axes.scad.");

            if (!File.Exists(axisPath))
                throw new FileNotFoundException("The included axis file was not found.", axisPath);

            var temporaryPath = Path.Combine(outputDirectory, $".axes-{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var writer = new StreamWriter(temporaryPath, false, encoding))
                    await writer.WriteAsync(remaining.AsMemory(), cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, _axesFilePath, overwrite: true);
                File.Delete(axisPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            return _axesFilePath;
        }
    }
}