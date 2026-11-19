using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace NetGenCAD.UI.Converters;

public class FileNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string path ? Path.GetFileName(path) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}