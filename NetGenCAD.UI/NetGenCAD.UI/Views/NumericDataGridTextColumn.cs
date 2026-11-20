using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetGenCAD.UI.Views;

public class NumericDataGridTextColumn : DataGridTextColumn
{
    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = base.GenerateElement(cell, dataItem);
        if (element is TextBlock textBlock)
            textBlock.TextAlignment = TextAlignment.Center;
        return element;
    }

    public static void Apply(DataGridAutoGeneratingColumnEventArgs e, bool force = false)
    {
        var type = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;
        if (!force && type != typeof(byte) && type != typeof(sbyte) &&
            type != typeof(short) && type != typeof(ushort) &&
            type != typeof(int) && type != typeof(uint) &&
            type != typeof(long) && type != typeof(ulong) &&
            type != typeof(float) && type != typeof(double) && type != typeof(decimal))
            return;

        if (e.Column is DataGridTextColumn column)
        {
            var numericColumn = new NumericDataGridTextColumn
            {
                Header = column.Header,
                Binding = column.Binding,
                ClipboardContentBinding = column.ClipboardContentBinding,
                SortMemberPath = column.SortMemberPath
            };
            if (column.IsReadOnly)
                numericColumn.IsReadOnly = true;
            e.Column = numericColumn;
        }
    }
}
