using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Data.Sqlite;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Utility;

namespace NetGenCAD.Designer.Api.Services
{
    /// <summary>
    /// gRPC service implementation for object creation.
    /// Delegates to the existing static ObjectScadFunctions.
    /// </summary>

    public class ObjectScadServiceImpl : ObjectScadService.ObjectScadServiceBase
    {
        private readonly SqliteConnection _db;

        public ObjectScadServiceImpl(SqliteConnection db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public override async Task<UpdateAxisTranslateResponse> UpdateAxisTranslate(UpdateAxisTranslateRequest request, ServerCallContext context)
        {
            // Adapt callback; expand response as needed
            await ObjectScadFunctions.UpdateAxisTranslateWithCallbackAsync(
                axisStored: request.AxisStored,
                axisDimensions: ToDomain(request.AxisDimensions),
                selectedAxis: ToGeneratedModule(request.SelectedAxis),
                axisXPositionMM: request.AxisXPositionMm,
                axisYPositionMM: request.AxisYPositionMm,
                axisZPositionMM: request.AxisZPositionMm,
                selectedAxisUnit: GrpcUtilities.ToCoreUnitSystem(request.SelectedAxisUnit),
                decimalPlaces: request.DecimalPlaces,
                removeAxis: request.RemoveAxis,
                originalRemoveAxis: false,
                originalAxisCall: string.Empty,
                objectFilePath: request.ObjectFilePath,
                dbConnection: _db,
                onAxisTranslateComplete: (_, _, _, _, _) => Task.CompletedTask);

            return new UpdateAxisTranslateResponse { Success = true, Message = "Updated" };
        }

        public override async Task<CreateSolidResponse> CreateSolid(CreateSolidRequest request, ServerCallContext context)
        {
            var selectedSolidType = request.SelectedSolidType switch
            {
                "RoundCube" => "Round Cube",
                "RoundCylinder" => "Round Cylinder",
                var value => value
            };
            var selectedUnit = GrpcUtilities.ToCoreUnitSystem(request.SelectedUnit);
            NetGenCAD.Designer.Repositories.SolidDimensions? created = null;

            var objectId = await ObjectScadFunctions.CreateObjectWithCallbackAsync(
                selectedSolidType: selectedSolidType,
                name: request.Name,
                description: request.Description,
                selectedFilament: Core.Material.FilamentType.Other,
                selectedOperationType: GrpcUtilities.ToCoreOperationType(request.SelectedOperationType),
                lengthMM: request.Dimensions.LengthMm,
                widthMM: request.Dimensions.WidthMm,
                heightMM: request.Dimensions.HeightMm,
                thicknessMM: request.Dimensions.ThicknessMm,
                radiusMM: request.Dimensions.RadiusMm,
                radius1MM: request.Dimensions.Radius1Mm,
                radius2MM: request.Dimensions.Radius2Mm,
                cylinderHeightMM: request.Dimensions.CylinderHeightMm,
                xOffsetMM: request.Dimensions.XOffsetMm,
                yOffsetMM: request.Dimensions.YOffsetMm,
                zOffsetMM: request.Dimensions.ZOffsetMm,
                xRotate: request.Dimensions.XRotate,
                yRotate: request.Dimensions.YRotate,
                zRotate: request.Dimensions.ZRotate,
                selectedOpenScadColor: default,
                layerIntValue: request.Layer,
                alphaIntValue: request.Alpha,
                surfaceFilePath: request.SurfaceFilePath,
                surfaceCenter: request.SurfaceCenter ? 1 : 0,
                surfaceInvert: request.SurfaceInvert ? 1 : 0,
                isCubeSelected: selectedSolidType == "Cube",
                isRoundCubeSelected: selectedSolidType == "Round Cube",
                isSurfaceSelected: selectedSolidType == "Surface",
                isRoundSurfaceSelected: false,
                isCylinderSelected: selectedSolidType is "Cylinder" or "Round Cylinder",
                isRoundCylinderSelected: selectedSolidType == "Round Cylinder",
                isSphereSelected: selectedSolidType == "Sphere",
                isPolyhedronSelected: selectedSolidType == "Polyhedron",
                isTextSelected: selectedSolidType == "Text",
                selectedPolyhedron: null,
                selectedUnit: selectedUnit,
                decimalPlaces: request.DecimalPlaces,
                axisId: request.AxisId == 0 ? null : request.AxisId,
                axesSelectEnabled: false,
                appendObject: request.AppendObject,
                dbConnection: _db,
                generateOscadCallback: solid =>
                {
                    created = solid;
                    return ObjectScadFunctions.GenerateOSCADAsync(
                        oDim: solid,
                        isCubeSelected: selectedSolidType == "Cube",
                        isRoundCubeSelected: selectedSolidType == "Round Cube",
                        isSurfaceSelected: selectedSolidType == "Surface",
                        isRoundSurfaceSelected: false,
                        isCylinderSelected: selectedSolidType is "Cylinder" or "Round Cylinder",
                        isRoundCylinderSelected: selectedSolidType == "Round Cylinder",
                        isSphereSelected: selectedSolidType == "Sphere",
                        isTextSelected: selectedSolidType == "Text",
                        selectedUnit: selectedUnit,
                        decimalPlaces: request.DecimalPlaces,
                        surfaceScaleX: request.SurfaceScaleX == 0 ? 1 : request.SurfaceScaleX,
                        surfaceScaleY: request.SurfaceScaleY == 0 ? 1 : request.SurfaceScaleY,
                        surfaceScaleZ: request.SurfaceScaleZ == 0 ? 1 : request.SurfaceScaleZ,
                        surfaceFilePath: request.SurfaceFilePath,
                        surfaceInvert: request.SurfaceInvert,
                        surfaceCenter: request.SurfaceCenter,
                        surfaceConvexity: request.SurfaceConvexity > 0 ? request.SurfaceConvexity : 1,
                        isPreRendered: request.IsPreRendered,
                        textInput: request.TextInput,
                        textSize: request.TextSize,
                        fontInput: request.FontInput,
                        textAlign: request.TextAlign,
                        verticalAlign: request.VerticalAlign,
                        textDirection: request.TextDirection switch
                        {
                            "L-to-R" => "ltr",
                            "R-to-L" => "rtl",
                            "T-to-B" => "ttb",
                            "B-to-T" => "btt",
                            "" => "ltr",
                            var direction => direction
                        },
                        languageInput: request.LanguageInput);
                },
                refreshDimensionsCallback: () => Task.CompletedTask,
                currentAxisDisplay: string.Empty,
                onObjectCreated: (_, _, _, _) => Task.CompletedTask,
                isColorFromHex: false,
                openScadColorHex: string.Empty,
                surfaceScaleX: request.SurfaceScaleX == 0 ? 1 : request.SurfaceScaleX,
                surfaceScaleY: request.SurfaceScaleY == 0 ? 1 : request.SurfaceScaleY,
                surfaceScaleZ: request.SurfaceScaleZ == 0 ? 1 : request.SurfaceScaleZ,
                textInput: request.TextInput,
                textSize: request.TextSize,
                fontInput: request.FontInput,
                textAlign: request.TextAlign,
                verticalAlign: request.VerticalAlign,
                textDirection: request.TextDirection,
                languageInput: request.LanguageInput);

            if (objectId == 0 || created == null)
                throw new RpcException(new Status(StatusCode.Internal, "Object creation failed; no solid was saved."));

            return new CreateSolidResponse
            {
                ObjectId = objectId,
                CreatedSolid = GrpcUtilities.ToProtoSolidDimensions(created)
            };
        }

        public override async Task<CopyObjectResponse> CopyObject(CopyObjectRequest request, ServerCallContext context)
        {
            var (solids, modules) = await ObjectScadFunctions.CopyObjectWithCallbackAsync(request.SourceObjectName, _db);

            var response = new CopyObjectResponse();
            response.Solids.AddRange(solids.Select(GrpcUtilities.ToProtoSolidDimensions));
            response.Modules.AddRange(modules.Select(GrpcUtilities.ToProtoModuleDimensions));
            return response;
        }

        public override async Task<Empty> DeleteSelectedItem(DeleteSelectedItemRequest request, ServerCallContext context)
        {
            object? item = request.ItemType switch
            {
                "Solid" => new SolidDimensions { Id = request.ItemId },
                "Module" => new ModuleDimensions { Id = request.ItemId },
                _ => null
            };

            await ObjectScadFunctions.DeleteSelectedItemAsync(item, _db);
            return new Empty();
        }

        public override async Task<GenerateOSCADResponse> GenerateOSCAD(GenerateOSCADRequest request, ServerCallContext context)
        {
            var solid = GrpcUtilities.ToCoreSolidDimensions(request.Solid);
            var oscad = await ObjectScadFunctions.GenerateOSCADAsync(
                oDim: solid,
                isCubeSelected: request.IsCubeSelected,
                isRoundCubeSelected: request.IsRoundCubeSelected,
                isSurfaceSelected: request.IsSurfaceSelected,
                isRoundSurfaceSelected: false,
                isCylinderSelected: request.IsCylinderSelected,
                isRoundCylinderSelected: false,
                isSphereSelected: request.IsSphereSelected,
                isTextSelected: request.IsTextSelected,
                selectedUnit: GrpcUtilities.ToCoreUnitSystem(request.SelectedUnit),
                decimalPlaces: request.DecimalPlaces,
                surfaceScaleX: request.SurfaceScaleX,
                surfaceScaleY: request.SurfaceScaleY,
                surfaceScaleZ: request.SurfaceScaleZ,
                surfaceFilePath: request.SurfaceFilePath,
                surfaceInvert: false,
                surfaceCenter: false,
                surfaceConvexity: 1,
                isPreRendered: false,
                textInput: request.TextInput,
                textSize: request.TextSize,
                fontInput: request.FontInput,
                textAlign: string.Empty,
                verticalAlign: string.Empty,
                textDirection: string.Empty,
                languageInput: string.Empty);

            return new GenerateOSCADResponse { OscadMethod = oscad };
        }

        public override async Task<Empty> PartsToScadFiles(PartsToScadFilesRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.PartsToScadFilesAsync(
                request.ObjectName,
                request.ObjectDescription,
                request.ObjectFilePath,
                _db,
                request.SolidDimensions.Select(GrpcUtilities.ToCoreSolidDimensions),
                request.ModuleDimensions.Select(GrpcUtilities.ToCoreModuleDimensions));
            return new Empty();
        }

        public override async Task<Empty> ObjectToScadFiles(ObjectToScadFilesRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.ObjectToScadFilesAsync(
                request.ObjectFilePath,
                request.ModuleDimensions.Select(GrpcUtilities.ToCoreModuleDimensions),
                request.XMirror,
                request.YMirror,
                request.ZMirror);
            return new Empty();
        }

        public override async Task<ModuleDimensions> CreateUnionModule(CreateBooleanModuleRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.CreateUnionModuleAsync(
                request.ObjectName,
                request.ObjectDescription,
                request.SolidDimensions.Select(GrpcUtilities.ToCoreSolidDimensions),
                request.ModuleDimensions.Select(GrpcUtilities.ToCoreModuleDimensions),
                _db,
                request.IsPreRendered);
            return new ModuleDimensions(); // return the created module if the method is adjusted to return it
        }

        public override async Task<ModuleDimensions> CreateDifferenceModule(CreateBooleanModuleRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.CreateDifferenceModuleAsync(
                request.ObjectName,
                request.ObjectDescription,
                request.SolidDimensions.Select(GrpcUtilities.ToCoreSolidDimensions),
                request.ModuleDimensions.Select(GrpcUtilities.ToCoreModuleDimensions),
                _db,
                request.IsPreRendered);
            return new ModuleDimensions();
        }

        public override async Task<ModuleDimensions> CreateIntersectionModule(CreateBooleanModuleRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.CreateIntersectionModuleAsync(
                request.ObjectName,
                request.ObjectDescription,
                request.SolidDimensions.Select(GrpcUtilities.ToCoreSolidDimensions),
                request.ModuleDimensions.Select(GrpcUtilities.ToCoreModuleDimensions),
                _db,
                request.IsPreRendered);
            return new ModuleDimensions();
        }

        public override async Task<GetDimensionsPartsResponse> GetDimensionsParts(GetDimensionsPartsRequest request, ServerCallContext context)
        {
            var (solids, modules) = await ObjectScadFunctions.GetDimensionsPartsAsync(_db, request.ObjectName);
            var response = new GetDimensionsPartsResponse();
            response.Solids.AddRange(solids.Select(GrpcUtilities.ToProtoSolidDimensions));
            response.Modules.AddRange(modules.Select(GrpcUtilities.ToProtoModuleDimensions));
            return response;
        }

        public override Task<GetFilteredAxesListResponse> GetFilteredAxesList(GetFilteredAxesListRequest request, ServerCallContext context)
        {
            var axes = request.AxesModulesList.Select(ToGeneratedModule).ToList();
            var (filtered, selectedValue, selected) = ObjectScadFunctions.GetFilteredAxesList(
                GrpcUtilities.ToCoreUnitSystem(request.SelectedAxisUnit),
                new System.Collections.ObjectModel.ObservableCollection<NetGenCAD.Axis.Scad.Models.GeneratedModule>(axes),
                request.AxisStored,
                request.SelectedAxisValue);

            var response = new GetFilteredAxesListResponse
            {
                SelectedAxisValue = selectedValue ?? string.Empty,
                SelectedAxis = selected != null ? ToProto(selected) : null
            };
            response.FilteredAxes.AddRange(filtered);
            return Task.FromResult(response);
        }

        public override Task<ConvertSolidInputsResponse> ConvertInputsToImperial(ConvertSolidInputsRequest request, ServerCallContext context)
        {
            var result = ObjectScadFunctions.ConvertInputsToImperial(
                request.Length, request.Width, request.Height, request.Thickness,
                request.Radius, request.Radius1, request.Radius2, request.CylinderHeight,
                request.XOffset, request.YOffset, request.ZOffset, request.DecimalPlaces);

            return Task.FromResult(new ConvertSolidInputsResponse
            {
                Length = result.Length,
                Width = result.Width,
                Height = result.Height,
                Thickness = result.Thickness,
                Radius = result.Radius,
                Radius1 = result.Radius1,
                Radius2 = result.Radius2,
                CylinderHeight = result.CylinderHeight,
                XOffset = result.XOffset,
                YOffset = result.YOffset,
                ZOffset = result.ZOffset
            });
        }

        public override Task<ConvertSolidInputsResponse> ConvertInputsToMetric(ConvertSolidInputsRequest request, ServerCallContext context)
        {
            var result = ObjectScadFunctions.ConvertInputsToMetric(
                request.Length, request.Width, request.Height, request.Thickness,
                request.Radius, request.Radius1, request.Radius2, request.CylinderHeight,
                request.XOffset, request.YOffset, request.ZOffset, request.DecimalPlaces);

            return Task.FromResult(new ConvertSolidInputsResponse
            {
                Length = result.Length,
                Width = result.Width,
                Height = result.Height,
                Thickness = result.Thickness,
                Radius = result.Radius,
                Radius1 = result.Radius1,
                Radius2 = result.Radius2,
                CylinderHeight = result.CylinderHeight,
                XOffset = result.XOffset,
                YOffset = result.YOffset,
                ZOffset = result.ZOffset
            });
        }

        public override async Task<Empty> ExportToStl(ExportToStlRequest request, ServerCallContext context)
        {
            await ObjectScadFunctions.ExportToStlAsync(request.ObjectFilePath, request.ShouldRestoreAxis);
            return new Empty();
        }


        // -------------------------------------------------------------------------
        // Mapping helpers (expand as needed)
        // -------------------------------------------------------------------------

        private static SolidDimensions ToProto(SolidDimensions src) => new()
        {
            Id = src.Id,
            Name = src.Name ?? string.Empty,
            Description = src.Description ?? string.Empty,
            SolidType = src.SolidType ?? string.Empty,
            LengthMm = src.LengthMm,
            WidthMm = src.WidthMm,
            HeightMm = src.HeightMm,
            ThicknessMm = src.ThicknessMm,
            RadiusMm = src.RadiusMm,
            // ... map remaining fields
        };

        private static SolidDimensions ToDomain(SolidDimensions proto) => new()
        {
            Id = proto.Id,
            Name = proto.Name,
            Description = proto.Description,
            SolidType = proto.SolidType,
            LengthMm = proto.LengthMm,
            WidthMm = proto.WidthMm,
            HeightMm = proto.HeightMm,
            // ... map remaining fields
        };

        private static ModuleDimensions ToProto(ModuleDimensions src) => new()
        {
            Id = src.Id,
            Name = src.Name ?? string.Empty,
            ModuleType = src.ModuleType ?? string.Empty,
            OscadMethod = src.OscadMethod ?? string.Empty,
            // ...
        };

        private static ModuleDimensions ToDomain(ModuleDimensions proto) => new()
        {
            Id = proto.Id,
            Name = proto.Name,
            ModuleType = proto.ModuleType,
            OscadMethod = proto.OscadMethod,
            // ...
        };

        private static AxisDimensions ToProto(AxisDimensions src) => new()
        {
            Id = src.Id,
            Theme = src.Theme ?? string.Empty,
            Unit = src.Unit ?? string.Empty,
            MinX = src.MinX,
            MaxX = src.MaxX,
            // ...
        };

        private static NetGenCAD.Designer.Repositories.AxisDimensions ToDomain(NetGenCAD.Designer.Api.Grpc.Protobuf.AxisDimensions proto) => new()
        {
            Id = proto.Id,
            Theme = proto.Theme,
            Unit = proto.Unit,
            MinX = proto.MinX,
            // ...
        };

        private static NetGenCAD.Axis.Scad.Models.GeneratedModule ToGeneratedModule(NetGenCAD.Designer.Api.Grpc.Protobuf.GeneratedModule proto) => new()
        {
            CallingMethod = proto.CallingMethod,
            Unit = proto.Unit,
            Theme = proto.Theme,
            MinX = proto.MinX,
            MaxX = proto.MaxX,
            // ...
        };

        private static NetGenCAD.Designer.Api.Grpc.Protobuf.GeneratedModule ToProto(NetGenCAD.Axis.Scad.Models.GeneratedModule src) => new()
        {
            CallingMethod = src.CallingMethod ?? string.Empty,
            Unit = src.Unit ?? string.Empty,
            Theme = src.Theme ?? string.Empty,
            MinX = src.MinX,
            MaxX = src.MaxX,
            // ...
        };
    }
}