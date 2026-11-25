using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Data.Sqlite;

using NetGenCAD.Core.Interfaces;
using NetGenCAD.Core.Measurements;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Repositories;
using NetGenCAD.Designer.Utility;
using System.Collections.ObjectModel;


namespace NetGenCAD.Designer.Api.Services
{
    /// <summary>
    /// gRPC service implementation for axes operations.
    /// Delegates to the existing static CreateAxesFunctions.
    /// </summary>
    public class AxesServiceImpl : AxesService.AxesServiceBase
    {

        private readonly SqliteConnection _db;

        // Constructor serves as DB initializer, and table dependency ensures tables exist before any calls are applied
        public AxesServiceImpl(SqliteConnection db)
        {
            _db = db;
            ObjectScadFunctions.GetDimensionsPartsAsync(_db, string.Empty).Wait(); // Make sure tables exist in the database before any calls are applied, patience can be a virtue
        }

        public override Task<GetAxesListResponse> GetAxesList(Empty request, ServerCallContext context) => GetAxesListAsync();

        public Task<GetAxesListResponse> GetAxesListAsync()
        {
            // var (all, metric, imperial) = CreateAxesFunctions.GetAxesList(); // without axisId, from .scad files

            // From DB
            var allAxes = CreateAxesFunctions.GetAllAxesAsync(_db).Result;

            var response = new GetAxesListResponse();
           // response.AllAxes.AddRange(all.Select(ToProto));
            response.MetricAxes.AddRange(allAxes.Where(a => a.Unit == "mm").OrderBy(a => a.Id).Select(ToProto));
            response.ImperialAxes.AddRange(allAxes.Where(a => a.Unit == "in").OrderBy(a => a.Id).Select(ToProto));

            return Task.FromResult(response);
        }

        public override Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToImperial(ConvertUnitsRequest request, ServerCallContext context) => ConvertToImperialAsync(request);

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

        public override Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToMetric(ConvertUnitsRequest request, ServerCallContext context) => ConvertToMetricAsync(request);

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

            return ToProto(created, axisId);
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

        private static Api.Grpc.Protobuf.GeneratedModule ToProto(Repositories.AxisDimensions src)
        {
            var (rangeX, rangeY, rangeZ, volume) = VolumeCalculation.GetVolume(
                src.MinX, src.MaxX, src.MinY, src.MaxY, src.MinZ, src.MaxZ, src.Unit);

            var response = new Api.Grpc.Protobuf.GeneratedModule
            {
                AxisId = src.Id,
                CallingMethod = string.IsNullOrEmpty(src.IncludeMethod)
                    ? src.OSCADMethod.Trim()
                    : src.OSCADMethod.Replace(src.IncludeMethod, string.Empty).Trim(),
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
                AxisColorAlpha = src.AxisColorAlpha
            };

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
            VolumeScale = src.VolumeScale
        };

        private static Api.Grpc.Protobuf.CustomAxis ToProto(Axis.SCAD.Utility.AxisConfig.CustomAxis src, int? axisId)
        {
            var response = new Api.Grpc.Protobuf.CustomAxis
            {
                //AxisModule = src.AxisModule ?? string.Empty,
                AxisId = axisId ?? -1,
                CallingMethod = src.CallingMethod ?? string.Empty,
                ModuleName = src.ModuleName ?? string.Empty,
                //MainFileInstruction = src.MainFileInstruction ?? string.Empty,
                TotalCubicVolume = src.TotalCubicVolume,
                TotalCubicVolumeScale = src.TotalCubicVolumeScale,
                TotalCubicVolumeUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "in³" : "cm³",
                TotalCubicVolumeScaleUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "ft³" : "m³",
                AxisColorAlpha = src.Settings.AxisColorAlpha
            };

            if (src.Settings.ColorFromHex)
                response.ColorHex = src.Settings.AxisColor;
            else
                response.OscadColor = src.Settings.AxisColor;

            return response;
        }
    }
}