using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using NetGenCAD.Core.Utility;

namespace NetGenCAD.UI.Converters;

public class ModuleDisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string callMethod ? WrapInModule.ModuleDisplayName(callMethod) : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // We don't need to convert back since the ComboBox stores the original CallingMethod value
        return value;
    }

}
