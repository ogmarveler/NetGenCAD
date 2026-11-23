using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NetGenCAD.Axis.Scad.Models;
using NetGenCAD.Axis.Scad.Utility;
using NetGenCAD.Core.Measurements;
using NetGenCAD.UI.Converters;
using NetGenCAD.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace NetGenCAD.UI.Views;

public partial class CreateAxesView : UserControl, INotifyPropertyChanged
{
    private CreateAxesViewModel ViewModel => (CreateAxesViewModel)DataContext!;
    private Window? _parentWindow;
    private IDisposable? _clientSizeObserver;
    private const double WRAP_THRESHOLD_WIDTH = 1400;

    // Track if we've added the button columns
    private bool _metricButtonColumnAdded = false;
    private bool _imperialButtonColumnAdded = false;

    public CreateAxesView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<CreateAxesViewModel>();
        // ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Wait for the control to be attached to the visual tree to access the parent window
        this.AttachedToVisualTree += CreateAxesView_AttachedToVisualTree;

        // Set fixed DataGrid widths after XAML is initialized
        SetDataGridWidths();

        // Subscribe to Loaded events to add button columns after ItemsSource is bound
        var metricDataGrid = this.FindControl<DataGrid>("AxesListMetric");
        var imperialDataGrid = this.FindControl<DataGrid>("AxesListImperial");

        if (metricDataGrid != null)
        {
            metricDataGrid.Loaded += MetricDataGrid_Loaded;
        }

        if (imperialDataGrid != null)
        {
            imperialDataGrid.Loaded += ImperialDataGrid_Loaded;
        }
    }

    private void MetricDataGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid && !_metricButtonColumnAdded)
        {
            AddActionButtonColumnToAxesDataGrid(dataGrid);
            AddButtonColumn(dataGrid);
            _metricButtonColumnAdded = true;
        }
    }

    private void ImperialDataGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid && !_imperialButtonColumnAdded)
        {
            AddActionButtonColumnToAxesDataGrid(dataGrid);
            AddButtonColumn(dataGrid);
            _imperialButtonColumnAdded = true;
        }
    }

    private void AddButtonColumn(DataGrid dataGrid)
    {
        /* Check if we haven't already added it (to prevent duplicates)
        if (dataGrid.Columns.Any(c => c.Header?.ToString() == ""))
            return;*/

        // Create the button column with template
        var buttonTemplate = new FuncDataTemplate<Axis.Scad.Models.GeneratedModule>((module, _) =>
        {
            var button = new Button
            {
                // Use PathIcon with document/clipboard geometry
                Content = new PathIcon
                {
                    Data = Geometry.Parse("M19,3H14.82C14.4,1.84 13.3,1 12,1C10.7,1 9.6,1.84 9.18,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M12,3A1,1 0 0,1 13,4A1,1 0 0,1 12,5A1,1 0 0,1 11,4A1,1 0 0,1 12,3M7,7H17V5H19V19H5V5H7V7M17,11H7V9H17V11M15,15H7V13H15V15Z"),
                    Width = 15,
                    Height = 15
                },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Padding = new Thickness(4),
                MinWidth = 40,
                Height = 28
            };

            button.Click += (s, e) =>
            {
                if (module?.CallingMethod != null)
                {
                    ShowCallingMethodModal(module);
                }
            };

            return button;
        });

        // Create the template column
        var actionColumn = new DataGridTemplateColumn
        {
            Header = "",  // Column header - empty for icon only
            Width = new DataGridLength(40),
            CellTemplate = buttonTemplate,
            CanUserSort = false,  // Disable sorting if not needed
            CanUserResize = false,  // Disable resizing if not needed
            DisplayIndex = 1 // Always first column after trash bin icon
        };

        // Insert at the beginning after the trash bin icon
        dataGrid.Columns.Add(actionColumn);
    }

    private void ShowCallingMethodModal(GeneratedModule? module)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// If calling the axis in an object file. Translate function can move the axis around the object");
        sb.AppendLine("use <../Axes/axes.scad>;");  // For NetGenCAD.UI where nested folder scad files are used. NetGenCAD-Axis may be slightly different
        sb.AppendLine($"translate ([0, 0, 0])  {module?.CallingMethod}");
        sb.AppendLine();
        sb.AppendLine($"{new string('-', 40)} Axes Module OSCAD {new string('-', 40)}");
        sb.AppendLine();
        sb.AppendLine($"{module?.AxesOscadMethod}");
        sb.AppendLine($"{new string('-', 40)} Axis OSCAD {new string('-', 40)}");
        sb.AppendLine();
        sb.AppendLine(ViewModel.AxisModule);


        // Set ViewModel properties for the modal
        ViewModel.ModalTitle = $"{StringFunctions.FormatAxisDisplay(module?.CallingMethod)}";
        ViewModel.ModalContent = sb.ToString();
        ViewModel.IsModalOpen = true;
    }

    private void SetDataGridWidths()
    {
        // Find the DataGrids by name (from XAML)
        var metricDataGrid = this.FindControl<DataGrid>("AxesListMetric");
        var imperialDataGrid = this.FindControl<DataGrid>("AxesListImperial");

        if (metricDataGrid != null)
        {
            metricDataGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            metricDataGrid.MinWidth = 960;
        }

        if (imperialDataGrid != null)
        {
            imperialDataGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            imperialDataGrid.MinWidth = 960;
        }
    }

    private void CreateAxesView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _parentWindow = this.FindAncestorOfType<Window>();

        if (_parentWindow != null)
        {
            _clientSizeObserver = _parentWindow.GetObservable(Window.ClientSizeProperty)
                .Subscribe(size =>
                {
                    Console.WriteLine($"Window Width: {size.Width}");
                    AdjustLayoutBasedOnWindowWidth(size.Width);
                });

            AdjustLayoutBasedOnWindowWidth(_parentWindow.ClientSize.Width);
        }
    }

    private void AdjustLayoutBasedOnWindowWidth(double windowWidth)
    {
        if (windowWidth < WRAP_THRESHOLD_WIDTH)
        {
            AdjustLayoutForNarrowScreen();
        }
        else
        {
            AdjustLayoutForWideScreen();
        }
    }

    // Optional: Adjust width based on window size
    private void AdjustDataGridWidthBasedOnWindow(double windowWidth)
    {
        var metricDataGrid = this.FindControl<DataGrid>("AxesListMetric");
        var imperialDataGrid = this.FindControl<DataGrid>("AxesListImperial");

        // Responsive width based on window size
        double dataGridWidth = windowWidth < 1400 ? 600 : 960;

        if (metricDataGrid != null)
            metricDataGrid.Width = dataGridWidth;

        if (imperialDataGrid != null)
            imperialDataGrid.Width = dataGridWidth;
    }

    private async void CreateCustomAxis(object? sender, RoutedEventArgs e) => await ViewModel.CreateCustomAxisAsync();

    private async void ClearInputsAsync(object? sender, RoutedEventArgs e)
    {
        AxesListMetric.SelectedItem = null;
        AxesListImperial.SelectedItem = null;
        await ViewModel.ClearInputs();
    }

    private async void ViewAxisOutputButton_Click(object? sender, RoutedEventArgs e) => await ViewModel.ShowAxisPreviewAsync();

    private async void PreviewColor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CreateAxesViewModel viewModel)
            await viewModel.RefreshPreviewColorAsync();
    }

    private async void PreviewColorHex_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && DataContext is CreateAxesViewModel viewModel)
        {
            viewModel.IsPreviewColorFromHex = checkBox.IsChecked == true;
            await viewModel.RefreshPreviewColorAsync();
        }
    }

    private async void PreviewColorHex_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is CreateAxesViewModel viewModel)
        {
            viewModel.PreviewColorHex = textBox.Text ?? string.Empty;
            await viewModel.RefreshPreviewColorAsync();
        }
    }

    private async void Alpha_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is CreateAxesViewModel viewModel)
        {
            viewModel.AlphaText = textBox.Text ?? string.Empty;
            await viewModel.RefreshPreviewColorAsync();
        }
    }

    private async void SaveMirroredAxisButton_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.SaveMirroredAxisAsync();
    }

    private async void MirrorZ_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            await ViewModel.SetPreviewMirrorZAsync(checkBox.IsChecked == true);
        }
    }

    private async void AxisPreviewCorner_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse<AxisPreviewCorner>(tag, out var corner))
            await ViewModel.SetPreviewCornerAsync(corner);
    }

    private async void DeleteAxisButton_Click(object? sender, RoutedEventArgs e)
    {
        // Check each DataGrid for a selected item
        object? selectedItem = AxesListMetric.SelectedItem
                            ?? AxesListImperial.SelectedItem;

        if (selectedItem != null)
        {
            await ViewModel.DeleteAxisAsync(selectedItem);
        }
    }

    // New method to add the button column
    private void AddActionButtonColumnToAxesDataGrid(DataGrid dataGrid)
    {
        // Define the cell template with a button
        var buttonTemplate = new FuncDataTemplate<Axis.Scad.Models.GeneratedModule>((item, _) =>
        {
            var button = new Button
            {
                // Replace text content with an icon
                Content = new PathIcon
                {
                    Data = Geometry.Parse("M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z"),  // Trash bin icon path
                    Width = 15,  // Adjust size to fit the small button
                    Height = 15
                },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Padding = new Thickness(4),
                MinWidth = 40,
                Height = 28
            };

            // Handle the button click (replace with your logic)
            button.Click += async (s, e) =>
            {
                if (item is GeneratedModule)
                {
                    // Example: Call a ViewModel method or perform an action
                    // For instance, populate fields or delete the item
                    await ViewModel.DeleteAxisAsync(item);
                    // Or open an edit dialog, etc.
                }
            };

            return button;
        });

        // Create the template column
        var actionColumn = new DataGridTemplateColumn
        {
            Header = "",  // Column header - empty for icon only
            Width = new DataGridLength(40),
            CellTemplate = buttonTemplate,
            CanUserSort = false,  // Disable sorting if not needed
            CanUserResize = false,  // Disable resizing if not needed
            DisplayIndex = 0 // Always first column
        };

        // Add the column to the DataGrid
        dataGrid.Columns.Insert(0, actionColumn);
    }

    /// <summary>
    /// MVU-style: Simple column customization without adding custom columns
    /// Avoids stateful column tracking and duplication issues
    /// </summary>
    [RequiresDynamicCode("Calls Avalonia.Data.Binding.Binding(String)")]
    [RequiresUnreferencedCode("Calls Avalonia.Data.Binding.Binding(String)")]
    private void DataGrid_AutoGeneratingColumnImperial(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // List of columns to exclude from display - Hide Metric columns for Imperial view
        var excludedColumns = new[] { "AxesOscadMethod", "AxisColorAlpha", "ColorFromHex" };

        if (excludedColumns.Contains(e.PropertyName))
        {
            e.Cancel = true;
            return;
        }

        // Customize CallingMethod column to show formatted axis display
        if (e.PropertyName == nameof(Axis.Scad.Models.GeneratedModule.CallingMethod))
        {
            // Create a custom DataGridTextColumn with value formatting for "Axis Display"
            var axisDisplayColumn = new DataGridTextColumn
            {
                Header = "Axis Display",
                Width = new DataGridLength(320),
                CanUserResize = false,
                IsReadOnly = true,
                Binding = new Binding(nameof(Axis.Scad.Models.GeneratedModule.CallingMethod))
                {
                    Mode = BindingMode.OneWay,
                    Converter = new AxisDisplayConverter()
                }
            };

            // Replace the auto-generated column with Axis Display
            e.Column = axisDisplayColumn;
            return;
        }

        NumericDataGridTextColumn.Apply(e, force: true);

        if (e.PropertyName == nameof(GeneratedModule.AxisColor) && e.Column is DataGridTextColumn colorColumn)
        {
            e.Column = new ColorDataGridTextColumn
            {
                Binding = colorColumn.Binding,
                ClipboardContentBinding = colorColumn.ClipboardContentBinding,
                SortMemberPath = colorColumn.SortMemberPath,
                IsReadOnly = true
            };
        }

        if (e.PropertyName == nameof(GeneratedModule.Volume) && e.Column is DataGridTextColumn volumeColumn)
        {
            e.Column = new VolumeDataGridTextColumn
            {
                Binding = volumeColumn.Binding,
                ClipboardContentBinding = volumeColumn.ClipboardContentBinding,
                SortMemberPath = volumeColumn.SortMemberPath,
                IsReadOnly = true
            };
        }

        // Customize headers with unicode symbols
        e.Column.Header = e.PropertyName switch
        {
            nameof(Axis.Scad.Models.GeneratedModule.Unit) => "Unit",
            nameof(Axis.Scad.Models.GeneratedModule.Theme) => "Theme",
            nameof(Axis.Scad.Models.GeneratedModule.RangeX) => "ΔX",
            nameof(Axis.Scad.Models.GeneratedModule.RangeY) => "ΔY",
            nameof(Axis.Scad.Models.GeneratedModule.RangeZ) => "ΔZ",
            nameof(Axis.Scad.Models.GeneratedModule.MinX) => "Xₘᵢₙ",
            nameof(Axis.Scad.Models.GeneratedModule.MaxX) => "Xₘₐₓ",
            nameof(Axis.Scad.Models.GeneratedModule.MinY) => "Yₘᵢₙ",
            nameof(Axis.Scad.Models.GeneratedModule.MaxY) => "Yₘₐₓ",
            nameof(Axis.Scad.Models.GeneratedModule.MinZ) => "Zₘᵢₙ",
            nameof(Axis.Scad.Models.GeneratedModule.MaxZ) => "Zₘₐₓ",
            nameof(Axis.Scad.Models.GeneratedModule.Volume) => "Volume",
            nameof(Axis.Scad.Models.GeneratedModule.AxisColor) => "Color",
            nameof(Axis.Scad.Models.GeneratedModule.AxisColorAlpha) => "Alpha",
            nameof(Axis.Scad.Models.GeneratedModule.AxisId) => "ID",
            _ => e.PropertyName
        };

        e.Column.Width = new DataGridLength(e.PropertyName switch
        {
            nameof(GeneratedModule.Unit) => 70,
            nameof(GeneratedModule.Theme) => 90,
            nameof(GeneratedModule.Volume) => 115,
            nameof(GeneratedModule.AxisId) => 60,
            nameof(GeneratedModule.AxisColor) => 145,
            _ => 75
        });

        e.Column.CanUserResize = false;
    }

    private sealed class ColorDataGridTextColumn : NumericDataGridTextColumn
    {
        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateElement(cell, dataItem);
            if (element is TextBlock textBlock)
                textBlock.TextWrapping = TextWrapping.NoWrap;
            return element;
        }
    }

    private sealed class VolumeDataGridTextColumn : NumericDataGridTextColumn
    {
        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateElement(cell, dataItem);
            if (dataItem is GeneratedModule axis)
            {
                var volume = axis.RangeX * axis.RangeY * axis.RangeZ;
                var scaledVolume = axis.Unit == "mm"
                    ? VolumeConverter.ConvertMm3ToM3(volume)
                    : VolumeConverter.ConvertIn3ToFt3(volume);
                ToolTip.SetTip(element, $"Volume Scale: {scaledVolume:G} {(axis.Unit == "mm" ? "m³" : "ft³")}");
            }
            return element;
        }
    }

    [RequiresUnreferencedCode("Calls NetGenCAD.UI.Views.CreateAxesView.DataGrid_AutoGeneratingColumnImperial(Object, DataGridAutoGeneratingColumnEventArgs)")]
    [RequiresDynamicCode("Calls NetGenCAD.UI.Views.CreateAxesView.DataGrid_AutoGeneratingColumnImperial(Object, DataGridAutoGeneratingColumnEventArgs)")]
    private void DataGrid_AutoGeneratingColumnMetric(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // Same logic for both grids
        DataGrid_AutoGeneratingColumnImperial(sender, e);
    }

    private void HighlightAxisRow(DataGrid dataGrid, GeneratedModule selected)
    {
        var otherGrid = dataGrid == AxesListMetric ? AxesListImperial : AxesListMetric;
        otherGrid.SelectedItem = null;
        dataGrid.ScrollIntoView(selected, null);
    }

    private async void DataGridImperial_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is GeneratedModule selected)
        {
            HighlightAxisRow(dataGrid, selected);
            ViewModel.SelectAxis(selected, selected.Unit!);
            await ViewModel.ShowAxisPreviewAsync();
        }
    }

    private async void DataGridMetric_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is GeneratedModule selected)
        {
            HighlightAxisRow(dataGrid, selected);
            ViewModel.SelectAxis(selected, selected.Unit!);
            await ViewModel.ShowAxisPreviewAsync();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreateAxesViewModel.SelectedAxis))
            Dispatcher.UIThread.Post(() => Console.WriteLine($"SelectedAxis changed: {ViewModel.SelectedAxis}"));
    }

    private void AdjustLayoutForNarrowScreen()
    {
        var metricAxisGrid = this.FindControl<ScrollViewer>("MetricAxisGrid");
        if (metricAxisGrid != null)
        {
            Grid.SetRow(metricAxisGrid, 2);
            Grid.SetColumn(metricAxisGrid, 0);
            Grid.SetRowSpan(metricAxisGrid, 1);
            Grid.SetColumnSpan(metricAxisGrid, 2);
        }

        var imperialAxisGrid = this.FindControl<ScrollViewer>("ImperialAxisGrid");
        if (imperialAxisGrid != null)
        {
            Grid.SetRow(imperialAxisGrid, 3);
            Grid.SetColumn(imperialAxisGrid, 0);
            Grid.SetRowSpan(imperialAxisGrid, 1);
            Grid.SetColumnSpan(imperialAxisGrid, 2);
        }
    }

    private void AdjustLayoutForWideScreen()
    {
        var metricAxisGrid = this.FindControl<ScrollViewer>("MetricAxisGrid");
        if (metricAxisGrid != null)
        {
            Grid.SetRow(metricAxisGrid, 0);
            Grid.SetColumn(metricAxisGrid, 1);
            Grid.SetRowSpan(metricAxisGrid, 1);
            Grid.SetColumnSpan(metricAxisGrid, 2);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnDetachedFromVisualTree(e);
        _clientSizeObserver?.Dispose();
        _clientSizeObserver = null;
        _parentWindow = null;
    }
}