using Microsoft.Data.Sqlite;
using NetGenCAD.Designer.Repositories;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NetGenCAD.Designer.Functions
{
    public static class MirroredAxisFunctions
    {
        public static async Task<string> SaveAsync(SqliteConnection connection, string callingMethod, bool mirrorX, bool mirrorY, bool mirrorZ = false, string? previewColor = null, bool colorFromHex = false, double? previewAlpha = null)
        {
            if (!mirrorX && !mirrorY && !mirrorZ)
                throw new InvalidOperationException("Select a mirrored orientation before saving.");
            if (previewAlpha.HasValue && (!double.IsFinite(previewAlpha.Value) || previewAlpha.Value < 0 || previewAlpha.Value > 1))
                throw new ArgumentOutOfRangeException(nameof(previewAlpha), "Alpha must be between 0 and 1.");

            var method = Regex.Match(callingMethod.Trim(), @"^([A-Za-z_][A-Za-z0-9_]*)\(\);$");
            if (!method.Success)
                throw new InvalidDataException("The selected axis has an invalid calling method.");

            var sourceId = await new AxisDimensions().GetIdByCallingMethodAsync(connection, callingMethod);
            var source = sourceId.HasValue ? await new AxisDimensions().GetByIdAsync(connection, sourceId.Value) : null;
            if (source == null)
                throw new InvalidOperationException("The selected axis is not stored in the database.");

            var directory = Path.Combine("Scad", "Axes");
            var indexPath = Path.Combine(directory, "axes.scad");
            string index;
            Encoding encoding;
            using (var reader = new StreamReader(indexPath, detectEncodingFromByteOrderMarks: true))
            {
                index = await reader.ReadToEndAsync();
                encoding = reader.CurrentEncoding;
            }

            var originalIndex = index;
            index = NetGenCAD.Axis.SCAD.Modules.GUI.RepairAxisEntryBoundaries(index);
            var pattern = @"(?m)^// 3D Axis Module - [^\r\n]*\r?\n// Calling Method: [^\r\n]*\r?\n" +
                @"(?<settings>// Settings: [^\r\n]*?MinX=(?<minX>.*?), MaxX=(?<maxX>.*?), MinY=(?<minY>.*?), MaxY=(?<maxY>.*?), MinZ=(?<minZ>.*?), MaxZ=(?<maxZ>[^\r\n]*))\r?\n" +
                @"include[ \t]+<(?<file>[^>\r\n]+)>;[ \t]*\r?\n" +
                @"module[ \t]+" + Regex.Escape(method.Groups[1].Value) + @"[ \t]*\((?<parameters>[^\r\n]*)\)[ \t]*\{\r?\n(?<body>[^{}]*)^\}[ \t]*(?:\r?\n|$)";
            var entry = Regex.Matches(index, pattern).Cast<Match>().LastOrDefault()
                ?? throw new InvalidDataException("The selected axis definition or its bounds could not be found.");
            var filename = entry.Groups["file"].Value;
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || filename.Contains('/') || filename.Contains('\\') ||
                !filename.EndsWith(".scad", StringComparison.OrdinalIgnoreCase) || filename.Equals("axes.scad", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The selected axis has an invalid include file.");

            var sourceFile = Path.Combine(directory, filename);
            var sourceContent = await File.ReadAllTextAsync(sourceFile);
            var baseName = Path.GetFileNameWithoutExtension(filename);
            if (!Regex.IsMatch(baseName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new InvalidDataException("The selected axis has an invalid module filename.");
            var sourceName = method.Groups[1].Value;
            var originIndex = sourceName.IndexOf("_Orig_", StringComparison.Ordinal);
            if (!sourceName.StartsWith("Get_", StringComparison.Ordinal) || originIndex <= 4)
                throw new InvalidDataException("The selected axis name does not contain a valid origin.");
            var callPattern = @"\b" + Regex.Escape(baseName) + @"(?=[ \t]*\()";
            var body = entry.Groups["body"].Value.Trim();
            var call = Regex.Match(body, @"^\s*(?<transforms>.*?)\b" + Regex.Escape(baseName) + @"\s*\(\s*colorVal\s*=\s*colorVal\s*,\s*alpha\s*=\s*alpha\s*\)\s*;\s*$", RegexOptions.Singleline);
            if (!call.Success || Regex.Matches(body, callPattern).Count != 1)
                throw new InvalidDataException("The selected axis wrapper is not a supported single axis call.");

            var x = mirrorX ? ParseBound(entry.Groups["minX"].Value) + ParseBound(entry.Groups["maxX"].Value) : 0;
            var y = mirrorY ? ParseBound(entry.Groups["minY"].Value) + ParseBound(entry.Groups["maxY"].Value) : 0;
            var z = mirrorZ ? ParseBound(entry.Groups["minZ"].Value) + ParseBound(entry.Groups["maxZ"].Value) : 0;
            var xSign = mirrorX ? -1 : 1;
            var ySign = mirrorY ? -1 : 1;
            var zSign = mirrorZ ? -1 : 1;
            var transforms = call.Groups["transforms"].Value;
            var token = new Regex(@"\G\s*(?:translate\(\s*\[\s*(?<x>[^,]+),\s*(?<y>[^,]+),\s*(?<z>[^\]]+)\]\s*\)|mirror\(\s*\[\s*(?<mx>[01]),\s*(?<my>[01]),\s*(?<mz>[01])\s*\]\s*\))\s*");
            var position = 0;
            while (position < transforms.Length)
            {
                var part = token.Match(transforms, position);
                if (!part.Success)
                    throw new InvalidDataException("The selected axis contains an unsupported transform.");
                if (part.Groups["x"].Success)
                {
                    x += xSign * ParseScadNumber(part.Groups["x"].Value);
                    y += ySign * ParseScadNumber(part.Groups["y"].Value);
                    z += zSign * ParseScadNumber(part.Groups["z"].Value);
                }
                else
                {
                    int components = (part.Groups["mx"].Value == "1" ? 1 : 0) + (part.Groups["my"].Value == "1" ? 1 : 0) + (part.Groups["mz"].Value == "1" ? 1 : 0);
                    if (components != 1)
                        throw new InvalidDataException("Only axis-aligned mirrors can be composed.");
                    if (part.Groups["mx"].Value == "1") xSign = -xSign;
                    if (part.Groups["my"].Value == "1") ySign = -ySign;
                    if (part.Groups["mz"].Value == "1") zSign = -zSign;
                }
                position += part.Length;
            }
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                throw new InvalidDataException("The mirrored axis translation is not finite.");
            var newMethod = sourceName[..originIndex] + $"_Orig_{EncodeCoordinate(x)}_x_{EncodeCoordinate(y)}_x_{EncodeCoordinate(z)}_Mirror_{(xSign < 0 ? "X" : "0")}_{(ySign < 0 ? "Y" : "0")}_{(zSign < 0 ? "Z" : "0")}";
            var newBaseName = newMethod[4..].ToLowerInvariant();
            var declaration = @"(?m)^(?<prefix>[ \t]*module[ \t]+)" + Regex.Escape(baseName) + @"(?=[ \t]*\()";
            if (Regex.Matches(sourceContent, declaration).Count != 1)
                throw new InvalidDataException("The selected axis file does not contain exactly one matching module.");
            var newContent = Regex.Replace(sourceContent, declaration, "${prefix}" + newBaseName);
            var transform = $"translate([{x.ToString("R", CultureInfo.InvariantCulture)}, {y.ToString("R", CultureInfo.InvariantCulture)}, {z.ToString("R", CultureInfo.InvariantCulture)}]) ";
            if (xSign < 0) transform += "mirror([1, 0, 0]) ";
            if (ySign < 0) transform += "mirror([0, 1, 0]) ";
            if (zSign < 0) transform += "mirror([0, 0, 1]) ";
            body = $"{newBaseName}(colorVal = colorVal, alpha = alpha);";
            var color = string.IsNullOrEmpty(previewColor) ? source.AxisColor : previewColor;
            var isHex = string.IsNullOrEmpty(previewColor) ? source.ColorFromHex == 1 : colorFromHex;
            if (string.IsNullOrEmpty(color) || (isHex
                ? !Regex.IsMatch(color, @"\A#(?:[A-Fa-f0-9]{3}|[A-Fa-f0-9]{6})\z")
                : !Enum.TryParse<NetGenCAD.Core.Measurements.Colors.OpenScadColor>(color, true, out var namedColor) ||
                  !string.Equals(color, namedColor.ToString(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The axis color is invalid.");
            var parameters = entry.Groups["parameters"].Value;
            var colorParameter = new Regex("\\bcolorVal\\s*=\\s*\"[^\"]*\"");
            if (colorParameter.Matches(parameters).Count != 1)
                throw new InvalidDataException("The selected axis has an invalid color parameter.");
            parameters = colorParameter.Replace(parameters, _ => $"colorVal = \"{color}\"");
            var alpha = NetGenCAD.Axis.SCAD.Utility.AxisConfig.ValidateAxisAlpha(previewAlpha ?? source.AxisColorAlpha);
            var alphaParameter = new Regex(@"\balpha\s*=\s*[^,\s]+\s*$");
            if (alphaParameter.Matches(parameters).Count != 1)
                throw new InvalidDataException("The selected axis has an invalid alpha parameter.");
            parameters = alphaParameter.Replace(parameters, _ => $"alpha = {alpha.ToString("R", CultureInfo.InvariantCulture)}");
            var newCall = newMethod + "();";
            var newEntry = $"\n// 3D Axis Module - {newMethod.Replace('_', ' ')}\n// Calling Method: {newCall}\n" +
                $"{entry.Groups["settings"].Value}\ninclude <{newBaseName}.scad>;\n" +
                $"module {newMethod}({parameters}) {{\n  {transform}{body}\n}}\n\n";

            var newPath = Path.Combine(directory, newBaseName + ".scad");
            if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The mirrored axis would overwrite its source.");
            var targetPattern = @"(?m)^// 3D Axis Module - [^\r\n]*\r?\n// Calling Method: [^\r\n]*\r?\n(?<settings>// Settings: [^\r\n]*)\r?\ninclude[ \t]+<(?<file>[^>\r\n]+)>;[ \t]*\r?\nmodule[ \t]+" + Regex.Escape(newMethod) + @"[ \t]*\([^\r\n]*\)[ \t]*\{\r?\n[^{}]*^\}[ \t]*(?:\r?\n|$)(?:[ \t]*\r?\n)?";
            var existing = Regex.Matches(index, targetPattern).Cast<Match>().ToList();
            if (existing.Any(item => item.Groups["settings"].Value != entry.Groups["settings"].Value ||
                !string.Equals(item.Groups["file"].Value, Path.GetFileName(newPath), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Another axis with different settings already uses this mirrored name.");
            var targetId = await new AxisDimensions().GetIdByCallingMethodAsync(connection, newCall);
            bool fileExists = File.Exists(newPath);
            if (fileExists != (existing.Count > 0) || targetId.HasValue != (existing.Count > 0))
                throw new InvalidOperationException("The existing mirrored axis is incomplete or its file is already in use.");
            var previousFile = fileExists ? await File.ReadAllBytesAsync(newPath) : null;
            var updatedIndex = existing.Count == 0 ? index + newEntry : Regex.Replace(index, targetPattern, string.Empty) + newEntry;
            var temporaryPath = Path.Combine(directory, $".axes-{Guid.NewGuid():N}.tmp");
            bool wroteFile = false;
            bool replacedIndex = false;
            using var transaction = connection.BeginTransaction();
            try
            {
                var mirrored = new AxisDimensions
                {
                    Id = targetId.GetValueOrDefault(),
                    Theme = source.Theme,
                    Unit = source.Unit,
                    MinX = source.MinX,
                    MaxX = source.MaxX,
                    MinY = source.MinY,
                    MaxY = source.MaxY,
                    MinZ = source.MinZ,
                    MaxZ = source.MaxZ,
                    IncludeMethod = source.IncludeMethod,
                    OSCADMethod = $"{source.IncludeMethod} {newCall}",
                    AxesOSCADMethod = newEntry[1..],
                    AxisColor = color,
                    ColorFromHex = isHex ? 1 : 0,
                    AxisColorAlpha = alpha,
                    CreatedAt = DateTime.UtcNow
                };
                if (targetId.HasValue)
                    await mirrored.UpdateAsync(connection, transaction);
                else
                    await mirrored.InsertAsync(connection, transaction);

                await using (var file = new FileStream(newPath, fileExists ? FileMode.Truncate : FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    wroteFile = true;
                    await using var writer = new StreamWriter(file, Encoding.UTF8);
                    await writer.WriteAsync(newContent);
                }
                await using (var writer = new StreamWriter(temporaryPath, false, encoding))
                    await writer.WriteAsync(updatedIndex);
                File.Move(temporaryPath, indexPath, overwrite: true);
                replacedIndex = true;
                await transaction.CommitAsync();
            }
            catch
            {
                if (replacedIndex)
                {
                    await using (var writer = new StreamWriter(temporaryPath, false, encoding))
                        await writer.WriteAsync(originalIndex);
                    File.Move(temporaryPath, indexPath, overwrite: true);
                }
                if (wroteFile)
                {
                    if (previousFile != null) await File.WriteAllBytesAsync(newPath, previousFile);
                    else File.Delete(newPath);
                }
                throw;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            return newCall;
        }

        private static string EncodeCoordinate(double value) => (value == 0 ? "0" : value.ToString("R", CultureInfo.InvariantCulture))
            .Replace("-", "N").Replace("+", "P").Replace(".", "D");

        private static double ParseScadNumber(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result))
                return result;
            throw new InvalidDataException($"Invalid SCAD coordinate: {value}");
        }

        private static double ParseBound(string value)
        {
            if ((double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var result) ||
                 double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) && double.IsFinite(result))
                return result;
            throw new InvalidDataException($"Invalid axis bound: {value}");
        }
    }
}
