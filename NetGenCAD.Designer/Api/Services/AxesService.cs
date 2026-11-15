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

        public override Task<GetAxesListResponse> GetAxesList(Empty request, ServerCallContext context)
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

        public override Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToImperial(ConvertUnitsRequest request, ServerCallContext context)
        {
            var result = CreateAxesFunctions.ConvertInputToImperial(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ,
                request.Volume, request.VolumeScale,
                request.DecimalPlaces);

            return Task.FromResult(ToProto(result));
        }

        public override Task<Api.Grpc.Protobuf.AxisConversionResult> ConvertToMetric(ConvertUnitsRequest request, ServerCallContext context)
        {
            var result = CreateAxesFunctions.ConvertInputToMetric(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ,
                request.Volume, request.VolumeScale,
                request.DecimalPlaces);

            return Task.FromResult(ToProto(result));
        }

        public override Task<Api.Grpc.Protobuf.AxisValidationResult> ValidateAxisRanges(ValidateAxesRequest request, ServerCallContext context)
        {
            var result = CreateAxesFunctions.ValidateAxisRanges(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ);

            return Task.FromResult(ToProto(result));
        }

        public override Task<ValidateAllAxesResponse> ValidateAllAxes(ValidateAxesRequest request, ServerCallContext context)
        {
            var (allValid, axisErrors) = CreateAxesFunctions.ValidateAllAxes(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ);

            var response = new ValidateAllAxesResponse { AllValid = allValid };
            foreach (var kvp in axisErrors)
            {
                var list = new StringList();
                list.Values.AddRange(kvp.Value);
                response.AxisErrors[kvp.Key] = list;    
            }

            return Task.FromResult(response);
        }

        public override async Task<Api.Grpc.Protobuf.CustomAxis> CreateUpdateAxis(CreateCustomAxisRequest request, ServerCallContext context)
        {
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
                dbConnection: _db); 

            if (created == null)
                throw new RpcException(new Status(StatusCode.Internal, "Axis creation failed"));

            return ToProto(created, axisId);
        }

        public override async Task<StringResponse> DeleteAxis(DeleteAxisRequest request, ServerCallContext context)
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

            return new Api.Grpc.Protobuf.GeneratedModule
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
                Volume = volume
            };
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

        private static Api.Grpc.Protobuf.AxisValidationResult ToProto(CreateAxesFunctions.AxisValidationResult src) => new()
        {
            IsValid = src.IsValid,
            XAxisValid = src.XAxisValid,
            YAxisValid = src.YAxisValid,
            ZAxisValid = src.ZAxisValid
            // ErrorMessages map can be populated if needed
        };

        private static Api.Grpc.Protobuf.CustomAxis ToProto(Axis.SCAD.Utility.AxisConfig.CustomAxis src, int? axisId) => new()
        {
            //AxisModule = src.AxisModule ?? string.Empty,
            AxisId = axisId ?? -1,
            CallingMethod = src.CallingMethod ?? string.Empty,
            ModuleName = src.ModuleName ?? string.Empty,
            //MainFileInstruction = src.MainFileInstruction ?? string.Empty,
            TotalCubicVolume = src.TotalCubicVolume,
            TotalCubicVolumeScale = src.TotalCubicVolumeScale,
            TotalCubicVolumeUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "in³" : "cm³",
            TotalCubicVolumeScaleUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "ft³" : "m³"
        };
    }
}