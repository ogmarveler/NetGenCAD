using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NetGenCAD.Axis.Scad.Models;
using NetGenCAD.Axis.SCAD.Utility;
using NetGenCAD.Core.Measurements;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Utility;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static NetGenCAD.Core.Measurements.Colors;
using static NetGenCAD.Core.Measurements.Selector;
using static NetGenCAD.Designer.Functions.CreateAxesFunctions;
using Enum = System.Enum;

namespace NetGenCAD.UI.ViewModels
{
    public enum AxisPreviewCorner { Origin, X, XY, Y }

    public class CreateAxesViewModel : ValidatableBase
    {
        private UnitSystem _selectedUnit;         // Pass these back to backend functions
        private BackgroundType _selectedBackground;
        private double _minX;
        private double _maxX;
        private double _minY;
        private double _maxY;
        private double _minZ;
        private double _maxZ;
        private bool _unitHasChanged;
        private bool _isMetric;
        private bool _isImperial;
        private bool _axisDetailsShown;
        private string _moduleName = string.Empty;
        private string _callingMethod = string.Empty;
        private string _includeFile = string.Empty;
        private double _totalCubicVolume;
        private double _totalCubicVolumeScale;
        public int _decimalPlaces;
        public int _callingMethodLength;
        private string _inputMinX = string.Empty;
        private string _inputMaxX = string.Empty;
        private string _inputMinY = string.Empty;
        private string _inputMaxY = string.Empty;
        private string _inputMinZ = string.Empty;
        private string _inputMaxZ = string.Empty;
        private ObservableCollection<GeneratedModule> _axesList = [];
        private ObservableCollection<GeneratedModule> _axesListImperial = [];
        private ObservableCollection<GeneratedModule> _axesListMetric = [];
        private bool _createButtonEnabled;
        private string _modalTitle = string.Empty;
        private string _modalContent = string.Empty;
        private bool _isModalOpen;
        private SqliteConnection? _dbConnection;
        private string _previewAxisCallingMethod = string.Empty;
        private OpenScadColor? _selectedPreviewColor;
        private bool _isPreviewColorFromHex;
        private string _previewColorHex = string.Empty;
        private AxisPreviewCorner _previewCorner;
        private bool _previewMirrorZ;
        private bool _selectedMirrorX;
        private bool _selectedMirrorY;
        private bool _selectedMirrorZ;
        private bool _selectedIsMirrored;
        private bool _previewOpened;
        private bool _previewOpening;
        private bool _savingMirroredAxis;
        private string _mirrorSaveError = string.Empty;
        private readonly SemaphoreSlim _previewLock = new(1, 1);
        private GeneratedModule? _mirroredAxis;
        private GeneratedModule? _selectedAxis;
        private string? _unitAbbrev;
        private string? _axisModule;
        private string _alphaText = "1";



        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [UnconditionalSuppressMessage("AOT", "IL3050")]
        public CreateAxesViewModel()
        {
            DbConnection = App.Services!.GetRequiredService<SqliteConnection>(); // Get the DbConnection from the DI container
            UnitSystemValues = [.. Enum.GetValues(typeof(UnitSystem)).Cast<UnitSystem>()];
            BackgroundTypeValues = [.. Enum.GetValues(typeof(BackgroundType)).Cast<BackgroundType>()];
            SelectedBackgroundValue = BackgroundType.Light;
            SelectedUnitValue = UnitSystem.Metric;
            MaxXValue = SelectedUnitValue == UnitSystem.Metric ? 300 : 12; // Set based on defaults
            MaxYValue = SelectedUnitValue == UnitSystem.Metric ? 300 : 12;
            MaxZValue = SelectedUnitValue == UnitSystem.Metric ? 300 : 12;
            MinXValue = 0;
            MinYValue = 0;
            MinZValue = 0;
            _unitHasChanged = false;
            _createButtonEnabled = true;
            UnitHasChanged = false;
            _isImperial = SelectedUnitValue != UnitSystem.Metric; // Set based on SelectedUnit
            _isMetric = SelectedUnitValue == UnitSystem.Metric;
            IsImperial = SelectedUnitValue != UnitSystem.Metric;
            IsMetric = SelectedUnitValue == UnitSystem.Metric;
            _axisDetailsShown = true; // Module Details
            AxisDetailsShown = false;
            ModuleName = string.Empty;
            CallingMethod = string.Empty;
            IncludeFile = string.Empty;
            TotalCubicVolume = 0;
            TotalCubicVolumeScale = 0;
            _decimalPlaces = 12; // Rounding for conversions
            _callingMethodLength = 0; // For selectable text for module to be called in SCAD file
            _inputMinX = "Min X <= 0"; // Watermarks for X coordinates
            _inputMaxX = "Max X > Min X";
            _inputMinY = "Min Y <= 0"; // Watermarks for Y coordinates
            _inputMaxY = "Max Y > Min Y";
            _inputMinZ = "Min Z <= 0"; // Watermarks for Z coordinates
            _inputMaxZ = "Max Z > Min Z";
            _ = GetAxesList();  // Get existing list of axes generated
        }

        public SqliteConnection? DbConnection { get => _dbConnection; set => this.RaiseAndSetIfChanged(ref _dbConnection, value); }
        //public void RaisePropertyChanged(string propertyName) { this.RaisePropertyChanged(propertyName); }
        public double MinXValue { get => _minX; set { this.RaiseAndSetIfChanged(ref _minX, value); _ = ValidateMinMax(); } }
        public double MaxXValue { get => _maxX; set { this.RaiseAndSetIfChanged(ref _maxX, value); _ = ValidateMinMax(); } }
        public double MinYValue { get => _minY; set { this.RaiseAndSetIfChanged(ref _minY, value); _ = ValidateMinMax(); } }
        public double MaxYValue { get => _maxY; set { this.RaiseAndSetIfChanged(ref _maxY, value); _ = ValidateMinMax(); } }
        public double MinZValue { get => _minZ; set { this.RaiseAndSetIfChanged(ref _minZ, value); _ = ValidateMinMax(); } }
        public double MaxZValue { get => _maxZ; set { this.RaiseAndSetIfChanged(ref _maxZ, value); _ = ValidateMinMax(); } }
        public string MinXWatermark { get => _inputMinX; set => this.RaiseAndSetIfChanged(ref _inputMinX, value); }
        public string MaxXWatermark { get => _inputMaxX; set => this.RaiseAndSetIfChanged(ref _inputMaxX, value); }
        public string MinYWatermark { get => _inputMinY; set => this.RaiseAndSetIfChanged(ref _inputMinY, value); }
        public string MaxYWatermark { get => _inputMaxY; set => this.RaiseAndSetIfChanged(ref _inputMaxY, value); }
        public string MinZWatermark { get => _inputMinZ; set => this.RaiseAndSetIfChanged(ref _inputMinZ, value); }
        public string MaxZWatermark { get => _inputMaxZ; set => this.RaiseAndSetIfChanged(ref _inputMaxZ, value); }
        public List<UnitSystem> UnitSystemValues { get; set; }
        public UnitSystem SelectedUnitValue
        {
            get => _selectedUnit;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedUnit, value);
                // Update IsMetric and IsImperial IMMEDIATELY (synchronously)
                IsImperial = _selectedUnit != UnitSystem.Metric;
                IsMetric = _selectedUnit == UnitSystem.Metric;

                UnitHasChanged = true; // For use in conversions when _selectedUnit has changed
                _ = ConvertInputs(_decimalPlaces);
            }
        }
        public List<BackgroundType> BackgroundTypeValues { get; set; }
        public BackgroundType SelectedBackgroundValue { get => _selectedBackground; set => this.RaiseAndSetIfChanged(ref _selectedBackground, value); }
        public bool UnitHasChanged { get => _unitHasChanged; set => this.RaiseAndSetIfChanged(ref _unitHasChanged, value); }
        public bool IsMetric { get => _isMetric; set => this.RaiseAndSetIfChanged(ref _isMetric, value); }
        public bool IsImperial { get => _isImperial; set => this.RaiseAndSetIfChanged(ref _isImperial, value); }
        public bool AxisDetailsShown { get => _axisDetailsShown; set => this.RaiseAndSetIfChanged(ref _axisDetailsShown, value); }
        public string ModuleName { get => _moduleName; set => this.RaiseAndSetIfChanged(ref _moduleName, value); }
        public string CallingMethod { get => _callingMethod; set => this.RaiseAndSetIfChanged(ref _callingMethod, value); }
        public string IncludeFile { get => _includeFile; set => this.RaiseAndSetIfChanged(ref _includeFile, value); }
        public double TotalCubicVolume { get => _totalCubicVolume; set => this.RaiseAndSetIfChanged(ref _totalCubicVolume, value); }
        public double TotalCubicVolumeScale { get => _totalCubicVolumeScale; set => this.RaiseAndSetIfChanged(ref _totalCubicVolumeScale, value); }
        public int CallingMethodLength { get => _callingMethodLength; set => this.RaiseAndSetIfChanged(ref _callingMethodLength, value); }
        public ObservableCollection<GeneratedModule> AxesList { get => _axesList; set => this.RaiseAndSetIfChanged(ref _axesList, value); }
        public ObservableCollection<GeneratedModule> AxesListImperial { get => _axesListImperial; set => this.RaiseAndSetIfChanged(ref _axesListImperial, value); }
        public ObservableCollection<GeneratedModule> AxesListMetric { get => _axesListMetric; set => this.RaiseAndSetIfChanged(ref _axesListMetric, value); }
        public bool CreateButtonEnabled { get => _createButtonEnabled; set => this.RaiseAndSetIfChanged(ref _createButtonEnabled, value); }
        public static SqliteConnection Connection { get; set; } = new SqliteConnection();
        public string ModalTitle { get => _modalTitle; set => this.RaiseAndSetIfChanged(ref _modalTitle, value); }
        public string ModalContent { get => _modalContent; set => this.RaiseAndSetIfChanged(ref _modalContent, value); }
        public bool IsModalOpen { get => _isModalOpen; set => this.RaiseAndSetIfChanged(ref _isModalOpen, value); }
        public GeneratedModule? MirroredAxis { get => _mirroredAxis; set => this.RaiseAndSetIfChanged(ref _mirroredAxis, value); }
        public GeneratedModule? SelectedAxis { get => _selectedAxis; set => this.RaiseAndSetIfChanged(ref _selectedAxis, value); }
        public string? UnitAbbrev { get => _unitAbbrev; set => this.RaiseAndSetIfChanged(ref _unitAbbrev, value); }
        public string? AxisModule { get => _axisModule; set => this.RaiseAndSetIfChanged(ref _axisModule, value); }
        public string AlphaText
        {
            get => _alphaText;
            set
            {
                this.RaiseAndSetIfChanged(ref _alphaText, value);
                this.RaisePropertyChanged(nameof(HasInvalidAlpha));
            }
        }
        public bool HasInvalidAlpha => !TryGetAlpha(out _);

        private bool TryGetAlpha(out double alpha)
        {
            if (double.TryParse(AlphaText, NumberStyles.Float, CultureInfo.CurrentCulture, out alpha) &&
                double.IsFinite(alpha) && alpha >= 0 && alpha <= 1)
            {
                alpha = AxisConfig.ValidateAxisAlpha(alpha);
                return true;
            }
            return false;
        }

        internal void SelectAxis(GeneratedModule selected, string unit)
        {
            MirroredAxis = new GeneratedModule
            {
                MinX = selected.MinX,
                MaxX = selected.MaxX,
                MinY = selected.MinY,
                MaxY = selected.MaxY
            };
            SelectedAxis = new GeneratedModule
            {
                MinX = selected.MinX,
                MaxX = selected.MaxX,
                MinY = selected.MinY,
                MaxY = selected.MaxY,
                MinZ = selected.MinZ,
                MaxZ = selected.MaxZ,
                CallingMethod = selected.CallingMethod,
                AxisId = selected.AxisId,
                Theme = selected.Theme,
                Unit = selected.Unit,
                RangeX = selected.RangeX,
                RangeY = selected.RangeY,
                RangeZ = selected.RangeZ,
                Volume = selected.Volume,
                AxisColor = selected.AxisColor,
                AxisColorAlpha = selected.AxisColorAlpha,
                ColorFromHex = selected.ColorFromHex
            };

            AlphaText = selected.AxisColorAlpha.ToString(CultureInfo.CurrentCulture);
            UnitAbbrev = selected.Unit;
            SetSelectedOrientation(selected.CallingMethod);
            PreviewAxisCallingMethod = selected.CallingMethod ?? string.Empty;
            SelectedUnitValue = unit == "mm" ? UnitSystem.Metric : UnitSystem.Imperial;
            MinXValue = selected.MinX;
            MaxXValue = selected.MaxX;
            MinYValue = selected.MinY;
            MaxYValue = selected.MaxY;
            MinZValue = selected.MinZ;
            MaxZValue = selected.MaxZ;

            var volumeSuffix = unit == "mm" ? " cm³" : " in³";
            if (!string.IsNullOrWhiteSpace(selected.Volume) &&
                double.TryParse(selected.Volume.Replace(volumeSuffix, "").Trim(),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out var volume))
            {
                TotalCubicVolume = volume;
                TotalCubicVolumeScale = unit == "mm"
                    ? VolumeConverter.ConvertCm3ToM3(volume)
                    : VolumeConverter.ConvertIn3ToFt3(volume);
            }
            else
            {
                TotalCubicVolume = 0;
                TotalCubicVolumeScale = 0;
            }

            SelectedBackgroundValue = selected.Theme?.Contains("Light") == true
                ? BackgroundType.Light
                : BackgroundType.Dark;

            if (SelectedAxis != null)
            {
                ModuleName = SelectedAxis.CallingMethod?.Replace("();", "") ?? string.Empty;
                CallingMethod = SelectedAxis.CallingMethod ?? string.Empty;
                IncludeFile = $"include <{ModuleName.ToLower()}.scad>";
                CallingMethodLength = CallingMethod.Length;
            }
        }

        public OpenScadColor[] PreviewColors { get; } = Enum.GetValues<OpenScadColor>();
        public OpenScadColor? SelectedPreviewColor { get => _selectedPreviewColor; set => this.RaiseAndSetIfChanged(ref _selectedPreviewColor, value); }
        public bool IsPreviewColorFromHex
        {
            get => _isPreviewColorFromHex;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPreviewColorFromHex, value);
                this.RaisePropertyChanged(nameof(HasInvalidPreviewColorHex));
                if (HasInvalidPreviewColorHex)
                    PreviewColorHex = string.Empty;
            }
        }
        public string PreviewColorHex
        {
            get => _previewColorHex;
            set
            {
                this.RaiseAndSetIfChanged(ref _previewColorHex, value);
                this.RaisePropertyChanged(nameof(HasInvalidPreviewColorHex));
            }
        }
        public bool HasInvalidPreviewColorHex => IsPreviewColorFromHex && !string.IsNullOrEmpty(PreviewColorHex) &&
            !Regex.IsMatch(PreviewColorHex, @"\A#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6})\z");

        public async Task RefreshPreviewColorAsync()
        {
            if (!HasInvalidPreviewColorHex && !HasInvalidAlpha && (_previewOpened || _previewOpening) && CanPreviewAxis)
                await UpdateAxisPreviewAsync(openFile: false);
        }

        public string PreviewAxisCallingMethod
        {
            get => _previewAxisCallingMethod;
            set
            {
                this.RaiseAndSetIfChanged(ref _previewAxisCallingMethod, value);
                this.RaisePropertyChanged(nameof(CanPreviewAxis));
                this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
            }
        }
        public bool CanPreviewAxis => !string.IsNullOrWhiteSpace(PreviewAxisCallingMethod);
        public bool CanSaveMirroredAxis => CanPreviewAxis && !_selectedIsMirrored && (_previewCorner != AxisPreviewCorner.Origin || _previewMirrorZ) && !_savingMirroredAxis;
        public bool CanTogglePreviewMirrorZ => !_selectedMirrorZ;
        public bool CanSelectPreviewCorner => !_selectedIsMirrored;
        public bool IsPreviewCornerOrigin => _previewCorner == AxisPreviewCorner.Origin;
        public bool IsPreviewCornerX => _previewCorner == AxisPreviewCorner.X;
        public bool IsPreviewCornerXY => _previewCorner == AxisPreviewCorner.XY;
        public bool IsPreviewCornerY => _previewCorner == AxisPreviewCorner.Y;
        public string MirrorSaveError { get => _mirrorSaveError; set => this.RaiseAndSetIfChanged(ref _mirrorSaveError, value); }
        public bool PreviewMirrorZ { get => _previewMirrorZ; private set => this.RaiseAndSetIfChanged(ref _previewMirrorZ, value); }

        private void SetSelectedOrientation(string? callingMethod)
        {
            var match = Regex.Match(callingMethod ?? string.Empty, @"_Mirror_(?<x>X|0)_(?<y>Y|0)_(?<z>Z|0)\(\);$", RegexOptions.CultureInvariant);
            _selectedIsMirrored = match.Success;
            _selectedMirrorX = match.Success && match.Groups["x"].Value == "X";
            _selectedMirrorY = match.Success && match.Groups["y"].Value == "Y";
            _selectedMirrorZ = match.Success && match.Groups["z"].Value == "Z";
            SetPreviewCorner(_selectedMirrorX ? (_selectedMirrorY ? AxisPreviewCorner.XY : AxisPreviewCorner.X) : (_selectedMirrorY ? AxisPreviewCorner.Y : AxisPreviewCorner.Origin));
            PreviewMirrorZ = _selectedMirrorZ;
            this.RaisePropertyChanged(nameof(CanTogglePreviewMirrorZ));
            this.RaisePropertyChanged(nameof(CanSelectPreviewCorner));
            this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
        }

        private void SetPreviewCorner(AxisPreviewCorner corner)
        {
            _previewCorner = corner;
            this.RaisePropertyChanged(nameof(IsPreviewCornerOrigin));
            this.RaisePropertyChanged(nameof(IsPreviewCornerX));
            this.RaisePropertyChanged(nameof(IsPreviewCornerXY));
            this.RaisePropertyChanged(nameof(IsPreviewCornerY));
            this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
        }

        public async Task SetPreviewMirrorZAsync(bool mirrorZ)
        {
            if (!CanTogglePreviewMirrorZ)
                return;
            PreviewMirrorZ = mirrorZ;
            this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
            if ((_previewOpened || _previewOpening) && CanPreviewAxis)
                await UpdateAxisPreviewAsync(openFile: false);
        }

        public async Task SetPreviewCornerAsync(AxisPreviewCorner corner)
        {
            if (!CanSelectPreviewCorner)
                return;

            SetPreviewCorner(corner);
            if ((_previewOpened || _previewOpening) && CanPreviewAxis)
                await UpdateAxisPreviewAsync(openFile: false);
        }

        public async Task<bool> SaveMirroredAxisAsync()
        {
            if (!CanSaveMirroredAxis)
                return false;

            var callingMethod = PreviewAxisCallingMethod;
            var corner = _previewCorner;
            var mirrorZ = _previewMirrorZ;
            var savedSuccessfully = false;
            MirrorSaveError = string.Empty;
            _savingMirroredAxis = true;
            this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
            try
            {
                if (DbConnection == null)
                    throw new InvalidOperationException("The axis database is unavailable.");
                if (HasInvalidPreviewColorHex)
                    throw new InvalidDataException("Enter a preview color in #RGB or #RRGGBB format.");
                if (!TryGetAlpha(out var alpha))
                    throw new InvalidDataException("Enter an alpha value between 0 and 1.");
                var saved = await MirroredAxisFunctions.SaveAsync(DbConnection, callingMethod,
                    corner is AxisPreviewCorner.X or AxisPreviewCorner.XY,
                    corner is AxisPreviewCorner.Y or AxisPreviewCorner.XY, mirrorZ,
                    GetPreviewColor(), IsPreviewColorFromHex && !string.IsNullOrEmpty(PreviewColorHex), alpha);
                savedSuccessfully = true;
                var previewColor = SelectedPreviewColor;
                var isPreviewColorFromHex = IsPreviewColorFromHex;
                var previewColorHex = PreviewColorHex;
                await ClearInputs();
                SelectedPreviewColor = previewColor;
                PreviewColorHex = previewColorHex;
                IsPreviewColorFromHex = isPreviewColorFromHex;
                await GetAxesList();
                var savedAxis = AxesList.FirstOrDefault(axis => string.Equals(axis.CallingMethod, saved, StringComparison.Ordinal))
                    ?? throw new InvalidDataException($"Saved {saved}, but it could not be found in the refreshed axes list.");
                SelectAxis(savedAxis, savedAxis.Unit!);
                if (!await UpdateAxisPreviewAsync(openFile: !_previewOpened, mirrorSave: true))
                    MirrorSaveError = $"The axis was saved, but its preview could not be reloaded. {MirrorSaveError}";
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or SqliteException)
            {
                MirrorSaveError = savedSuccessfully ? $"The axis was saved, but reload failed: {ex.Message}" : ex.Message;
                return savedSuccessfully;
            }
            finally
            {
                _savingMirroredAxis = false;
                this.RaisePropertyChanged(nameof(CanSaveMirroredAxis));
            }
        }

        public async Task ShowAxisPreviewAsync()
        {
            _previewOpening = true;
            try
            {
                await UpdateAxisPreviewAsync(openFile: true);
            }
            finally
            {
                _previewOpening = false;
            }
        }

        private async Task<bool> UpdateAxisPreviewAsync(bool openFile, bool mirrorSave = false)
        {
            await _previewLock.WaitAsync();
            try
            {
                if (!openFile && (!_previewOpened || !CanPreviewAxis))
                    return false;
                // The selected module already contains its saved mirrors; apply only the difference.
                var mirrorX = (_previewCorner is AxisPreviewCorner.X or AxisPreviewCorner.XY) != _selectedMirrorX;
                var mirrorY = (_previewCorner is AxisPreviewCorner.Y or AxisPreviewCorner.XY) != _selectedMirrorY;
                var mirrorZ = _previewMirrorZ != _selectedMirrorZ;
                if (HasInvalidPreviewColorHex)
                    throw new InvalidDataException("Enter a preview color in #RGB or #RRGGBB format.");
                if (!TryGetAlpha(out var alpha))
                    throw new InvalidDataException("Enter an alpha value between 0 and 1.");
                var previewColor = GetPreviewColor();
                var callingMethod = PreviewAxisCallingMethod.Trim();
                var method = Regex.Match(callingMethod, @"^([A-Za-z_][A-Za-z0-9_]*)\(\);$");
                if (!method.Success)
                    throw new InvalidDataException("The selected axis does not have a valid OpenSCAD calling method.");

                var axesPath = Path.Combine("Scad", "Axes", "axes.scad");
                var axesContent = await File.ReadAllTextAsync(axesPath);
                var settings = Regex.Matches(axesContent,
                    @"(?m)^// Settings: [^\r\n]*?MinX=(?<minX>.*?), MaxX=(?<maxX>.*?), MinY=(?<minY>.*?), MaxY=(?<maxY>.*?), MinZ=(?<minZ>.*?), MaxZ=(?<maxZ>[^\r\n]*)\r?\n[ \t]*include[ \t]*<[^>\r\n]+>;[ \t]*\r?\n[ \t]*module[ \t]+" + Regex.Escape(method.Groups[1].Value) + @"[ \t]*\(").Cast<Match>().LastOrDefault();
                if (settings == null)
                    throw new InvalidDataException("The selected axis or its bounds were not found in axes.scad. Create or select an existing axis.");

                var x = (mirrorX ? ParseAxisBound(settings.Groups["minX"].Value) + ParseAxisBound(settings.Groups["maxX"].Value) : 0).ToString("R", CultureInfo.InvariantCulture);
                var y = (mirrorY ? ParseAxisBound(settings.Groups["minY"].Value) + ParseAxisBound(settings.Groups["maxY"].Value) : 0).ToString("R", CultureInfo.InvariantCulture);
                var z = (mirrorZ ? ParseAxisBound(settings.Groups["minZ"].Value) + ParseAxisBound(settings.Groups["maxZ"].Value) : 0).ToString("R", CultureInfo.InvariantCulture);

                var transform = mirrorX || mirrorY || mirrorZ ? $"translate([{x}, {y}, {z}]) " : string.Empty;
                if (mirrorX) transform += "mirror([1, 0, 0]) ";
                if (mirrorY) transform += "mirror([0, 1, 0]) ";
                if (mirrorZ) transform += "mirror([0, 0, 1]) ";

                var previewPath = Path.Combine("Scad", "Axes", "axis_preview.scad");
                var colorArgument = !string.IsNullOrEmpty(previewColor) ? $"colorVal = \"{previewColor}\", " : string.Empty;
                var previewCall = $"{method.Groups[1].Value}({colorArgument}alpha = {alpha.ToString("R", CultureInfo.InvariantCulture)});";

                // Set the viewport to align axis to bottom right, from point of origin
                var sb = new StringBuilder();
                sb.AppendLine($"$vpr=[60,0,270]; // Origin axis based from this port of view");
                sb.AppendLine($"$vpt = [100, 200, 100]; // Center point");
                sb.AppendLine($"$vpd = 800; // Camera distance");
                sb.AppendLine();
                sb.AppendLine($"use <axes.scad>;");

                await File.WriteAllTextAsync(previewPath, $"{sb.ToString()}{transform}{previewCall}{Environment.NewLine}");
                if (openFile)
                {
                    if (!await ScadFileOperations.OpenScadFileAsync(previewPath, allowDuplicates: false))
                        throw new IOException("Could not open the preview. Check the default application for .scad files.");
                    _previewOpened = true;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                if (mirrorSave)
                    MirrorSaveError = ex.Message;
                else
                {
                    ModalTitle = "Axis Preview Error";
                    ModalContent = ex.Message;
                    IsModalOpen = true;
                }
                return false;
            }
            finally
            {
                _previewLock.Release();
            }
        }

        private static double ParseAxisBound(string value)
        {
            if ((double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var bound) ||
                 double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out bound)) && double.IsFinite(bound))
                return bound;
            throw new InvalidDataException($"Invalid axis bound: {value}");
        }

        private string GetPreviewColor() => ObjectScadFunctions.ResolveColor(
            IsPreviewColorFromHex, PreviewColorHex,
            IsPreviewColorFromHex ? string.Empty : SelectedPreviewColor?.ToString() ?? string.Empty);

        public async Task CreateCustomAxisAsync()
        {
            if (!TryGetAlpha(out var alpha))
            {
                ModalTitle = "Axis Creation Error";
                ModalContent = "Enter an alpha value between 0 and 1.";
                IsModalOpen = true;
                return;
            }

            AxisDetailsShown = false; // Disables display of previous generated output details
            SelectedAxis = new GeneratedModule(); // Clear the selected axis when creating a new one

            // Validate the input values before proceeding
            string _color = string.Empty;
            bool _isOverride = false;
            if (SelectedPreviewColor is not null && !IsPreviewColorFromHex)
                _color = SelectedPreviewColor.Value.ToString();
            else
                (_color, _isOverride) = AxisConfig.ValidateAxisColor(PreviewColorHex, IsPreviewColorFromHex, SelectedBackgroundValue == BackgroundType.Dark ? OpenScadColor.White : OpenScadColor.Black);

            // Define the callback to update ViewModel properties
            CreateAxesFunctions.CreateCustomAxisCallbackAsync onAxisCreated = async (
                customAxis,
                displayMinX,
                displayMaxX,
                displayMinY,
                displayMaxY,
                displayMinZ,
                displayMaxZ,
                callingMethodLength,
                newUnitHasChanged) =>
            {
                // Update axis range display values
                MinXValue = displayMinX;
                MaxXValue = displayMaxX;
                MinYValue = displayMinY;
                MaxYValue = displayMaxY;
                MinZValue = displayMinZ;
                MaxZValue = displayMaxZ;

                // Update unit changed flag
                UnitHasChanged = newUnitHasChanged;

                // Set Post-Axis Generation Details
                IsImperial = SelectedUnitValue != UnitSystem.Metric;
                IsMetric = SelectedUnitValue == UnitSystem.Metric;
                TotalCubicVolume = customAxis.TotalCubicVolume;
                TotalCubicVolumeScale = customAxis.TotalCubicVolumeScale;
                ModuleName = customAxis.ModuleName;
                CallingMethod = customAxis.CallingMethod;
                CallingMethodLength = callingMethodLength;
                IncludeFile = $"include <{customAxis.CallingMethod.ToLower().Replace("();", "")}.scad>";
                AxisDetailsShown = true;
                AxisModule = customAxis.AxisModule;
            };

            // Call the static function with callback
            var axisId = await CreateAxesFunctions.CreateCustomAxisWithCallbackAsync(
                minX: _minX, maxX: _maxX,
                minY: _minY, maxY: _maxY,
                minZ: _minZ, maxZ: _maxZ,
                selectedUnit: _selectedUnit,
                selectedBackground: _selectedBackground,
                decimalPlaces: _decimalPlaces,
                unitHasChanged: UnitHasChanged,
                onAxisCreated: onAxisCreated,
                dbConnection: DbConnection,
                axisColor: _color,
                colorFromHex: _isOverride,
                axisColorAlpha: alpha);

            if (axisId is > 0)
            {
                AlphaText = alpha.ToString(CultureInfo.CurrentCulture);
                var selectedAxis = await Designer.Repositories.AxisDimensionsExtensions.GetByIdAsync(new Designer.Repositories.AxisDimensions(), _dbConnection!, (int)axisId);
                if (selectedAxis != null)
                {
                    SelectedAxis = new GeneratedModule
                    {
                        AxisId = selectedAxis.Id,
                        MinX = selectedAxis.MinX,
                        MaxX = selectedAxis.MaxX,
                        MinY = selectedAxis.MinY,
                        MaxY = selectedAxis.MaxY,
                        MinZ = selectedAxis.MinZ,
                        MaxZ = selectedAxis.MaxZ,
                        Theme = selectedAxis.Theme,
                        Unit = selectedAxis.Unit,
                        CallingMethod = CallingMethod,
                        AxesOscadMethod = selectedAxis.AxesOSCADMethod,
                        AxisColor = selectedAxis.AxisColor,
                        AxisColorAlpha = selectedAxis.AxisColorAlpha,
                        ColorFromHex = selectedAxis.ColorFromHex == 1 || (selectedAxis.AxisColor?.Contains('#') ?? false)
                    };

                    MirroredAxis = SelectedAxis; // Initially set MirroredAxis to the newly created axis
                    SetSelectedOrientation(SelectedAxis.CallingMethod);
                    PreviewAxisCallingMethod = SelectedAxis.CallingMethod ?? string.Empty;
                    UnitAbbrev = SelectedAxis?.Unit;
                    IsPreviewColorFromHex = SelectedAxis?.ColorFromHex ?? false;
                    PreviewColorHex = SelectedAxis?.AxisColor ?? string.Empty;
                    SelectedPreviewColor = !IsPreviewColorFromHex ? Enum.TryParse<OpenScadColor>(SelectedAxis?.AxisColor, ignoreCase: true, out var parsedColor) ? parsedColor : null : null;
                }
            }
            else
            {
                // Handle the case where axis creation failed
                ModalTitle = "Axis Creation Error";
                ModalContent = "Failed to create the custom axis. Please check your input values.";
                IsModalOpen = true;
                return;
            }

            // Handle invalid input case
            if (!AxisDetailsShown)
            {
                CallingMethod = "Please enter only numeric coordinates";
                AxisDetailsShown = true;
            }
            // Refresh the axes list
            await GetAxesList();
            await ShowAxisPreviewAsync();
        }

        public Task ClearInputs()
        {
            ClearErrors(nameof(MaxXValue));
            ClearErrors(nameof(MaxYValue));
            ClearErrors(nameof(MaxZValue));
            ClearErrors(nameof(MinXValue));
            ClearErrors(nameof(MinYValue));
            ClearErrors(nameof(MinZValue));
            SelectedUnitValue = UnitSystem.Metric; // Defaults for enums
            SelectedBackgroundValue = BackgroundType.Light;
            AxisDetailsShown = false; // Post-gen of axis details - static resources disabled in XAML
            TotalCubicVolume = 0;
            TotalCubicVolumeScale = 0;
            ModuleName = string.Empty;
            CallingMethod = string.Empty;
            PreviewAxisCallingMethod = string.Empty;
            SelectedPreviewColor = null;
            IsPreviewColorFromHex = false;
            PreviewColorHex = string.Empty;
            MirrorSaveError = string.Empty;
            SetSelectedOrientation(null);
            IncludeFile = string.Empty;
            MaxXValue = 300;
            MaxYValue = 300;
            MaxZValue = 300;
            MinXValue = 0;  // Set to 0 for coordinates
            MinYValue = 0;
            MinZValue = 0;
            MirroredAxis = new GeneratedModule();
            SelectedAxis = new GeneratedModule();
            AlphaText = "1";
            return Task.CompletedTask;
        }

        // Deletes the selected item from any DataGrid (Cube, Cylinder, or Module)
        public async Task DeleteAxisAsync(object? selectedItem)
        {
            if (selectedItem == null) return;

            // Remove from appropriate collection based on type
            switch (selectedItem)
            {
                case GeneratedModule axis:

                    // Call the static deletion function
                    await CreateAxesFunctions.DeleteAxisAsync(axis.AxisId, axis.CallingMethod!, DbConnection!);
                    if (string.Equals(PreviewAxisCallingMethod, axis.CallingMethod, StringComparison.Ordinal))
                    {
                        PreviewAxisCallingMethod = string.Empty;
                        MirroredAxis = new GeneratedModule();
                    }

                    switch (axis.Unit)
                    {
                        case "Metric":
                            AxesListMetric.Remove(axis);
                            break;
                        case "Imperial":
                            AxesListImperial.Remove(axis);
                            break;
                    }
                    AxesList.Remove(axis); // Remove from the main list as well
                    break;
            }

            // Refresh the DataGrids and files after successful deletion
            await ClearInputs();
            await GetAxesList();  // Get the updated list of axes after deletion
        }

        /**** Axes List DataGrid ****/
        public async Task GetAxesList()
        {
            // Call the static function
            var (allAxes, metricAxes, imperialAxes) = CreateAxesFunctions.GetAxesList();

            foreach (var axis in allAxes.Where(axis => !string.IsNullOrWhiteSpace(axis.CallingMethod)))
            {
                var storedAxis = await Designer.Repositories.AxisDimensionsExtensions.GetAllByCallingMethodAsync(new Designer.Repositories.AxisDimensions(), _dbConnection!, axis.CallingMethod!);
                if (storedAxis != null)
                {
                    var color = storedAxis.AxisColor ?? string.Empty;
                    axis.AxisId = storedAxis.Id;
                    axis.AxisColor = color.Contains('#') ? color.ToUpperInvariant() : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(color);
                    axis.AxisColorAlpha = storedAxis.AxisColorAlpha;
                    axis.ColorFromHex = storedAxis.ColorFromHex == 1 || color.Contains('#');
                    axis.AxesOscadMethod = storedAxis.AxesOSCADMethod;
                }
            }

            AxesList = new ObservableCollection<GeneratedModule>(allAxes.OrderBy(axis => axis.AxisId));
            AxesListMetric = new ObservableCollection<GeneratedModule>(metricAxes.OrderBy(axis => axis.AxisId));
            AxesListImperial = new ObservableCollection<GeneratedModule>(imperialAxes.OrderBy(axis => axis.AxisId));
        }

        public async Task ConvertInputs(int decimalPlaces)
        {
            AxisConversionResult result = new AxisConversionResult()
            {
                MinX = _minX,
                MaxX = _maxX,
                MinY = _minY,
                MaxY = _maxY,
                MinZ = _minZ,
                MaxZ = _maxZ,
                Volume = _totalCubicVolume,
                VolumeScale = _totalCubicVolumeScale
            };

            if (_selectedUnit == UnitSystem.Imperial && UnitHasChanged)
            {
                result = CreateAxesFunctions.ConvertInputToImperial(
                    _minX, _maxX, _minY, _maxY, _minZ, _maxZ, _totalCubicVolume, _totalCubicVolumeScale, decimalPlaces);
            }
            else if (_selectedUnit == UnitSystem.Metric && UnitHasChanged)
            {
                result = CreateAxesFunctions.ConvertInputToMetric(
                    _minX, _maxX, _minY, _maxY, _minZ, _maxZ, _totalCubicVolume, _totalCubicVolumeScale, decimalPlaces);
            }

            MinXValue = result.MinX;
            MaxXValue = result.MaxX;
            MinYValue = result.MinY;
            MaxYValue = result.MaxY;
            MinZValue = result.MinZ;
            MaxZValue = result.MaxZ;
            TotalCubicVolume = result.Volume;
            TotalCubicVolumeScale = result.VolumeScale;
            UnitHasChanged = false;

            await Task.CompletedTask;
        }

        private Task ValidateMinMax()
        {
            // Call the static validation function
            var validationResult = CreateAxesFunctions.ValidateAxisRanges(
                _minX, _maxX,
                _minY, _maxY,
                _minZ, _maxZ);

            // Clear all previous errors
            ClearErrors(nameof(MaxXValue));
            ClearErrors(nameof(MaxYValue));
            ClearErrors(nameof(MaxZValue));
            ClearErrors(nameof(MinXValue));
            ClearErrors(nameof(MinYValue));
            ClearErrors(nameof(MinZValue));

            // Apply errors from validation result
            foreach (var errorEntry in validationResult.ErrorMessages)
            {
                string propertyName = errorEntry.Key switch
                {
                    nameof(_minX) or $"{nameof(_minX)}_range" => nameof(MinXValue),
                    nameof(_maxX) or $"{nameof(_maxX)}_range" => nameof(MaxXValue),
                    nameof(_minY) or $"{nameof(_minY)}_range" => nameof(MinYValue),
                    nameof(_maxY) or $"{nameof(_maxY)}_range" => nameof(MaxYValue),
                    nameof(_minZ) or $"{nameof(_minZ)}_range" => nameof(MinZValue),
                    nameof(_maxZ) or $"{nameof(_maxZ)}_range" => nameof(MaxZValue),
                    _ => errorEntry.Key
                };

                AddError(propertyName, errorEntry.Value);
            }

            // Update button enabled state based on validation
            CreateButtonEnabled = validationResult.IsValid;
            return Task.CompletedTask;
        }
    }
}