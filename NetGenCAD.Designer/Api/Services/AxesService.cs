using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NetGenCAD.Core.Measurements;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Functions;

namespace NetGenCAD.Designer.Api.Services
{
    /// <summary>
    /// gRPC service implementation for axes operations.
    /// Delegates to the existing static CreateAxesFunctions.
    /// </summary>
    public class AxesServiceImpl : AxesService.AxesServiceBase
    {

        public override Task<GetAxesListResponse> GetAxesList(Empty request, ServerCallContext context)
        {
            var (all, metric, imperial) = CreateAxesFunctions.GetAxesList();

            var response = new GetAxesListResponse();
            response.AllAxes.AddRange(all.Select(ToProto));
            response.MetricAxes.AddRange(metric.Select(ToProto));
            response.ImperialAxes.AddRange(imperial.Select(ToProto));

            return Task.FromResult(response);
        }

        public override async Task StreamAxesList(Empty request, IServerStreamWriter<Api.Grpc.Protobuf.GeneratedModule> responseStream, ServerCallContext context)
        {
            var (all, _, _) = CreateAxesFunctions.GetAxesList();
            foreach (var axis in all)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;
                await responseStream.WriteAsync(ToProto(axis));
            }
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

        public override async Task<Api.Grpc.Protobuf.CustomAxis> CreateCustomAxis(CreateCustomAxisRequest request, ServerCallContext context)
        {
            Axis.SCAD.Utility.AxisConfig.CustomAxis? created = null;

            await CreateAxesFunctions.CreateCustomAxisWithCallbackAsync(
                request.MinX, request.MaxX,
                request.MinY, request.MaxY,
                request.MinZ, request.MaxZ,
                ToCoreUnitSystem(request.SelectedUnit),
                ToCoreBackgroundType(request.SelectedBackground),
                2, // request.DecimalPlaces,
                false, //request.UnitHasChanged,
                async (customAxis, minX, maxX, minY, maxY, minZ, maxZ, callingMethodLength, unitHasChanged) =>
                {
                    created = customAxis;
                    await Task.CompletedTask;
                });

            if (created == null)
                throw new RpcException(new Status(StatusCode.Internal, "Axis creation failed"));

            return ToProto(created);
        }

        public override async Task StreamCreateCustomAxis(CreateCustomAxisRequest request, IServerStreamWriter<AxisProgress> responseStream, ServerCallContext context)
        {
            var progress = new AxisProgress { Stage = "Starting", PercentComplete = 0, Message = "Initializing axis creation" };
            await responseStream.WriteAsync(progress);

            Axis.SCAD.Utility.AxisConfig.CustomAxis? created = null;

            try
            {
                progress.Stage = "Validating";
                progress.PercentComplete = 20;
                progress.Message = "Validating ranges";
                await responseStream.WriteAsync(progress);

                await CreateAxesFunctions.CreateCustomAxisWithCallbackAsync(
                    request.MinX, request.MaxX,
                    request.MinY, request.MaxY,
                    request.MinZ, request.MaxZ,
                    ToCoreUnitSystem(request.SelectedUnit),
                    ToCoreBackgroundType(request.SelectedBackground),
                    2, // request.DecimalPlaces,
                    false, //request.UnitHasChanged,
                    async (customAxis, minX, maxX, minY, maxY, minZ, maxZ, callingMethodLength, unitHasChanged) =>
                    {
                        created = customAxis;
                        await Task.CompletedTask;
                    });

                progress.Stage = "Complete";
                progress.PercentComplete = 100;
                progress.IsComplete = true;
                progress.Message = "Axis created successfully";
                if (created != null)
                    progress.Result = ToProto(created);

                await responseStream.WriteAsync(progress);
            }
            catch (Exception ex)
            {
                progress.Stage = "Error";
                progress.IsComplete = true;
                progress.Error = ex.Message;
                await responseStream.WriteAsync(progress);
            }
        }

        public override Task<GenerateAxisMethodCallResponse> GenerateAxisMethodCall(GenerateAxisMethodCallRequest request, ServerCallContext context)
        {
            var (callingMethod, includeFile) = CreateAxesFunctions.GenerateAxisMethodCall(
                request.ModuleName, request.CallingMethodLength);

            return Task.FromResult(new GenerateAxisMethodCallResponse
            {
                CallingMethod = callingMethod,
                IncludeFile = includeFile
            });
        }

        // -------------------------------------------------------------------------
        // Mapping helpers (C# model <-> proto)
        // -------------------------------------------------------------------------

        private static Api.Grpc.Protobuf.GeneratedModule ToProto(Axis.Scad.Models.GeneratedModule src) => new()
        {
            CallingMethod = src.CallingMethod ?? string.Empty,
            Unit = src.Unit ?? string.Empty,
            Theme = src.Theme ?? string.Empty,
            RangeX = src.RangeX,
            RangeY = src.RangeY,
            RangeZ = src.RangeZ,
            MinX = src.MinX,
            MaxX = src.MaxX,
            MinY = src.MinY,
            MaxY = src.MaxY,
            MinZ = src.MinZ,
            MaxZ = src.MaxZ,
            Volume = src.Volume ?? string.Empty
        };

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

        private static Api.Grpc.Protobuf.CustomAxis ToProto(Axis.SCAD.Utility.AxisConfig.CustomAxis src) => new()
        {
            //AxisModule = src.AxisModule ?? string.Empty,
            CallingMethod = src.CallingMethod ?? string.Empty,
            ModuleName = src.ModuleName ?? string.Empty,
            //MainFileInstruction = src.MainFileInstruction ?? string.Empty,
            TotalCubicVolume = src.TotalCubicVolume,
            TotalCubicVolumeScale = src.TotalCubicVolumeScale,
            TotalCubicVolumeUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "in³" : "cm³",
            TotalCubicVolumeScaleUnit = src.Settings.UnitSystem == Selector.UnitSystem.Imperial ? "ft³" : "m³"
        };

        private static Core.Measurements.Selector.UnitSystem ToCoreUnitSystem(
            Api.Grpc.Protobuf.UnitSystem value) => value switch
        {
            Api.Grpc.Protobuf.UnitSystem.Metric =>
                Core.Measurements.Selector.UnitSystem.Metric,
            Api.Grpc.Protobuf.UnitSystem.Imperial =>
                Core.Measurements.Selector.UnitSystem.Imperial,
            _ => throw new RpcException(
                new Status(StatusCode.InvalidArgument, "A valid unit system is required"))
        };

        private static Core.Measurements.Selector.BackgroundType ToCoreBackgroundType(
            Api.Grpc.Protobuf.BackgroundType value) => value switch
        {
            Api.Grpc.Protobuf.BackgroundType.Light =>
                Core.Measurements.Selector.BackgroundType.Light,
            Api.Grpc.Protobuf.BackgroundType.Dark =>
                Core.Measurements.Selector.BackgroundType.Dark,
            _ => throw new RpcException(
                new Status(StatusCode.InvalidArgument, "A valid background type is required"))
        };
    }
}