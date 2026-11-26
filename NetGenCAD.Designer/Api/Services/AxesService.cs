using Grpc.Core;
using Microsoft.Data.Sqlite;

using NetGenCAD.Core.Interfaces;
using NetGenCAD.Core.Measurements;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Repositories;
using NetGenCAD.Designer.Utility;
using System.Text.RegularExpressions;


namespace NetGenCAD.Designer.Api.Services
{
    /// <summary>
    /// gRPC service implementation for axes operations.
    /// Delegates to the existing static CreateAxesFunctions.
    /// </summary>
    public class AxesServiceImpl : AxesService.AxesServiceBase
    {

        private readonly SqliteConnection _db;
        private readonly IScadPathProvider _pathProvider;
        private readonly string? _axesPath;
        private readonly object _dbLock = new object(); // Lock for thread-safe database access

        // Constructor serves as DB initializer, and table dependency ensures tables exist before any calls are applied
        public AxesServiceImpl(SqliteConnection db, IScadPathProvider pathProvider)
        {
            _db = db;
            _pathProvider = pathProvider;
            _axesPath = Path.Combine(pathProvider.ScadPath, "Axes");  // Use compatible Windows or Unix format as standard response
            ObjectScadFunctions.GetDimensionsPartsAsync(_db, string.Empty).Wait(); // Make sure tables exist in the database before any calls are applied, patience can be a virtue
        }

        public override Task<AxisConversionResult> ConvertUnits(ConvertUnitsRequest request, ServerCallContext context) => ConvertUnitsAsync(request);

        public Task<AxisConversionResult> ConvertUnitsAsync(ConvertUnitsRequest request)
        {
            if (request.FromUnit == UnitSystem.Unspecified)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "from_unit is not specified with IMPERIAL or METRIC"));

            else if (request.FromUnit == UnitSystem.Imperial)  // From Imperial to Metric
                return ConvertToMetricAsync(request);

            else if (request.FromUnit == UnitSystem.Metric)  // From Metric to Imperial
                return ConvertToImperialAsync(request);

            else  // No conversion needed, return the same values
                return Task.FromResult(new AxisConversionResult
                {
                    MinX = request.MinX,
                    MaxX = request.MaxX,
                    MinY = request.MinY,
                    MaxY = request.MaxY,
                    MinZ = request.MinZ,
                    MaxZ = request.MaxZ,
                    Volume = request.Volume,
                    VolumeScale = request.VolumeScale,
                    Unit = request.FromUnit
                });
        }

        public async Task<GetAxesListResponse> GetAxesListAsync(int? axisId = null)
        {
            if (axisId.HasValue && axisId.Value <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "axis_id must be positive."));

            var response = new GetAxesListResponse();
            if (axisId.HasValue)
            {
                var axis = await new Repositories.AxisDimensions().GetByIdAsync(_db, axisId.Value)
                    ?? throw new RpcException(new Status(StatusCode.NotFound, $"axis_id: {axisId.Value} was not found."));
                var item = await ToProtoAsync(axis, includeOscad: true);
                if (axis.Unit == "mm") response.MetricAxes.Add(item);
                else if (axis.Unit == "in") response.ImperialAxes.Add(item);
                else throw new InvalidDataException($"Axis {axis.Id} has an unsupported unit '{axis.Unit}'.");
            }
            else
            {
                var allAxes = await CreateAxesFunctions.GetAllAxesAsync(_db);
                foreach (var axis in allAxes.OrderBy(a => a.Id))
                {
                    if (axis.Unit == "mm") response.MetricAxes.Add(await ToProtoAsync(axis, includeOscad: false));
                    else if (axis.Unit == "in") response.ImperialAxes.Add(await ToProtoAsync(axis, includeOscad: false));
                    else throw new InvalidDataException($"Axis {axis.Id} has an unsupported unit '{axis.Unit}'.");
                }
            }
            return response;
        }

        public override Task<GetAxesListResponse> GetAxesList(GetAxesListRequest request, ServerCallContext context) =>
            GetAxesListAsync(request.HasAxisId ? request.AxisId : null);

        public Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToImperialAsync(ConvertUnitsRequest request)
        {
            var result = CreateAxesFunctions.ConvertInputToImperial(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ,
                request.Volume, request.VolumeScale,
                request.DecimalPlaces);

            return Task.FromResult(ToProto(result));
        }

        public Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToMetricAsync(ConvertUnitsRequest request)
        {
            var result = CreateAxesFunctions.ConvertInputToMetric(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ,
                request.Volume, request.VolumeScale,
                request.DecimalPlaces);

            return Task.FromResult(ToProto(result));
        }

        public override Task<Api.Grpc.Protobuf.CustomAxis> CreateUpdateAxis(CreateCustomAxisRequest request, ServerCallContext context) => CreateUpdateAxisAsync(request);

        public async Task<Api.Grpc.Protobuf.CustomAxis> CreateUpdateAxisAsync(CreateCustomAxisRequest request)
        {
            if (!double.IsFinite(request.MinX) || !double.IsFinite(request.MaxX) ||
                !double.IsFinite(request.MinY) || !double.IsFinite(request.MaxY) ||
                !double.IsFinite(request.MinZ) || !double.IsFinite(request.MaxZ))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Axis bounds must be finite numbers."));

            var validation = CreateAxesFunctions.ValidateAxisRanges(
                request.MinX, request.MaxX, request.MinY, request.MaxY, request.MinZ, request.MaxZ);
            if (!validation.IsValid)
                throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", validation.ErrorMessages.Values)));

            if (request.HasOscadColor && !Axis.SCAD.Utility.AxisConfig.ValidateAxisColor(request.OscadColor, false, Colors.OpenScadColor.Black).IsOverride)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid OpenSCAD color."));

            if (request.HasColorHex && !Axis.SCAD.Utility.AxisConfig.ValidateAxisColor(request.ColorHex, true, Colors.OpenScadColor.Black).IsOverride)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid hex color code."));

            if (request.HasAxisColorAlpha && (!double.IsFinite(request.AxisColorAlpha) || request.AxisColorAlpha < 0 || request.AxisColorAlpha > 1))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Axis color alpha must be between 0 and 1."));

            Axis.SCAD.Utility.AxisConfig.CustomAxis? created = null;
            var (_minX, _maxX, _minY, _maxY, _minZ, _maxZ) = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0);

            var axisId = await CreateAxesFunctions.CreateCustomAxisWithCallbackAsync(
                minX: request.MinX, 
                maxX: request.MaxX,
                minY: request.MinY,
                maxY: request.MaxY,
                minZ: request.MinZ,
                maxZ: request.MaxZ,
                selectedUnit: GrpcUtilities.ToCoreUnitSystem(request.SelectedUnit),
                selectedBackground: GrpcUtilities.ToCoreBackgroundType(request.SelectedBackground),
                decimalPlaces: request.HasDecimalPlaces ? request.DecimalPlaces : 3, // Default to 3 decimal places if not provided since base 2 and base 10 conversions can result in long decimal values
                unitHasChanged: false, // See Line 351 on CreateAxesFunctions, always false for Api
                onAxisCreated: async (customAxis, minX, maxX, minY, maxY, minZ, maxZ, callingMethodLength, unitHasChanged) =>
                {
                    created = customAxis;
                    (_minX, _maxX, _minY, _maxY, _minZ, _maxZ) = (minX, maxX, minY, maxY, minZ, maxZ);
                    await Task.CompletedTask;
                },
                dbConnection: _db,
                axisColor: request.HasColorHex ? request.ColorHex : request.HasOscadColor ? request.OscadColor : null,
                colorFromHex: request.HasColorHex,
                axisColorAlpha: request.HasAxisColorAlpha ? request.AxisColorAlpha : 1);

            if (created == null)
                throw new RpcException(new Status(StatusCode.Internal, "Axis creation failed"));

            return ToProto(created, axisId, _axesPath);
        }

        public override Task<StringResponse> DeleteAxis(DeleteAxisRequest request, ServerCallContext context) => DeleteAxisAsync(request);

        public async Task<StringResponse> DeleteAxisAsync(DeleteAxisRequest request)
        {
            var (selectedAxisId, callMethodFromAxisId) = await CreateAxesFunctions.GetAxisByIdAsync(request.AxisId, request.AxisCallingMethod, _db);
            await CreateAxesFunctions.DeleteAxisAsync(selectedAxisId, callMethodFromAxisId, _db);
            return new StringResponse { Message = $"Axis with calling method {callMethodFromAxisId} deleted successfully." };
        }

        // -------------------------------------------------------------------------
        // Mapping helpers (C# model <-> proto)
        // -------------------------------------------------------------------------

        private async Task<Api.Grpc.Protobuf.GeneratedModule> ToProtoAsync(Repositories.AxisDimensions src, bool includeOscad)
        {
            var (rangeX, rangeY, rangeZ, volume) = VolumeCalculation.GetVolume(
                src.MinX, src.MaxX, src.MinY, src.MaxY, src.MinZ, src.MaxZ, src.Unit);
            var callingMethod = src.GetCallingMethod();
            var moduleName = callingMethod[4..^3].ToLowerInvariant();
            var axisFilename = Path.Combine(_axesPath!, moduleName + ".scad");

            var response = new Api.Grpc.Protobuf.GeneratedModule
            {
                AxisId = src.Id,
                CallingMethod = callingMethod,
                Unit = src.Unit,
                Theme = src.Theme,
                RangeX = rangeX,
                RangeY = rangeY,
                RangeZ = rangeZ,
                MinX = src.MinX,
                MaxX = src.MaxX,
                MinY = src.MinY,
                MaxY = src.MaxY,
                MinZ = src.MinZ,
                MaxZ = src.MaxZ,
                Volume = volume,
                AxisColorAlpha = src.AxisColorAlpha,
                AxisFilename = axisFilename
            };

            if (includeOscad)
            {
                response.AxisOscad = await src.GetAxisDefinitionAsync(_axesPath!);
                if (!string.IsNullOrWhiteSpace(src.AxesOSCADMethod))
                    response.AxesOscad = src.AxesOSCADMethod;
                else
                {
                    var index = await File.ReadAllTextAsync(Path.Combine(_axesPath!, "axes.scad"));
                    var wrapper = Regex.Escape(callingMethod[..^3]);
                    var pattern = @"(?m)^// 3D Axis Module - [^\r\n]*\r?\n// Calling Method: [^\r\n]*\r?\n// Settings: [^\r\n]*\r?\ninclude[ \t]+<[^>\r\n]+>;[ \t]*\r?\nmodule[ \t]+" + wrapper + @"[ \t]*\([^\r\n]*\)[ \t]*\{\r?\n[^{}]*^\}[ \t]*(?:\r?\n|$)";
                    var entry = Regex.Matches(index, pattern).Cast<Match>().LastOrDefault()
                        ?? throw new InvalidDataException($"Axis {src.Id} was not found in axes.scad.");
                    response.AxesOscad = entry.Value;
                }
            }

            if (!string.IsNullOrEmpty(src.AxisColor))
            {
                if (src.ColorFromHex == 1)
                    response.ColorHex = src.AxisColor;
                else
                    response.OscadColor = src.AxisColor;
            }

            return response;
        }

        private static Api.Grpc.Protobuf.AxisConversionResult ToProto(CreateAxesFunctions.AxisConversionResult src) => new()
        {
            MinX = src.MinX,
            MaxX = src.MaxX,
            MinY = src.MinY,
            MaxY = src.MaxY,
            MinZ = src.MinZ,
            MaxZ = src.MaxZ,
            Volume = src.Volume,
            VolumeScale = src.VolumeScale,
            Unit = src.Unit == Selector.UnitSystem.Imperial ? Api.Grpc.Protobuf.UnitSystem.Imperial : Api.Grpc.Protobuf.UnitSystem.Metric
        };

        private static Api.Grpc.Protobuf.CustomAxis ToProto(Axis.SCAD.Utility.AxisConfig.CustomAxis src, int? axisId, string? axesPath)
        {
            var response = new Api.Grpc.Protobuf.CustomAxis
            {
                //AxisModule = src.AxisModule ?? string.Empty,
                AxisId = axisId ?? -1,
                CallingMethod = src.CallingMethod ?? string.Empty,
                ModuleName = src.ModuleName ?? string.Empty,
                TotalCubicVolume = src.TotalCubicVolume,
                TotalCubicVolumeScale = src.TotalCubicVolumeScale,
                TotalCubicVolumeUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "in³" : "cm³",
                TotalCubicVolumeScaleUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "ft³" : "m³",
                AxisColorAlpha = src.Settings.AxisColorAlpha,
                AxesOscad = src.AxesModule ?? string.Empty,
                AxisOscad = src.AxisModule,
                AxisFilename = Path.Combine(axesPath!, $"{src.ModuleName?.ToLower().Trim()}.scad")
            };

            if (src.Settings.ColorFromHex)
                response.ColorHex = src.Settings.AxisColor;
            else
                response.OscadColor = src.Settings.AxisColor;

            return response;
        }
    }
}