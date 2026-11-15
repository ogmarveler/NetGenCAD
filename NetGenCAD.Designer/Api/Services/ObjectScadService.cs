using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Data.Sqlite;
using NetGenCAD.Axis.Scad.Utility;
using NetGenCAD.Core.Interfaces;
using NetGenCAD.Core.Measurements;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Repositories;
using NetGenCAD.Designer.Utility;
using AxisDimensionsEntity = NetGenCAD.Designer.Repositories.AxisDimensions;
using AxisDimensionsExtensions = NetGenCAD.Designer.Repositories.AxisDimensionsExtensions;
using ModuleDimensions = NetGenCAD.Designer.Api.Grpc.Protobuf.ModuleDimensions;
using SolidDimensions = NetGenCAD.Designer.Api.Grpc.Protobuf.SolidDimensions;

namespace NetGenCAD.Designer.Api.Services
{
    /// <summary>
    /// gRPC service implementation for object creation.
    /// Delegates to the existing static ObjectScadFunctions.
    /// </summary>

    public class ObjectScadServiceImpl : ObjectScadService.ObjectScadServiceBase
    {
        private readonly SqliteConnection _db;
        private readonly string _objectFilePath;

        // Constructor serves as DB initializer, and table dependency ensures tables exist before any calls are applied
        public ObjectScadServiceImpl(SqliteConnection db, IScadPathProvider scadPathProvider)
        {
            _db = db;
            _objectFilePath = scadPathProvider.ScadPath;
            ObjectScadFunctions.GetDimensionsPartsAsync(_db, string.Empty).Wait(); // Make sure tables exist in the database before any calls are applied, patience can be a virtue
        }

        public override async Task<CreateSolidResponse> CreateUpdateSolid(CreateSolidRequest request, ServerCallContext context)
        {

            NetGenCAD.Designer.Repositories.SolidDimensions? created = null;

            // Conversions of JSON supplied values to ObjectScadFunctions.CreateObjectWithCallbackAsync parameters are handled within the function itself, including default values and validation.
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Object name cannot be empty."));

            if (request.Dimensions == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Dimensions cannot be null."));

            // Solid Validation - Cube and Round Cube
            if ((request.SelectedSolidType == SolidType.Cube || request.SelectedSolidType == SolidType.RoundCube) && (request.Dimensions.Length <= 0 || request.Dimensions.Width <= 0 || request.Dimensions.Height <= 0))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Length, Width, and Height must be greater than zero."));

            // Solid Validation - Cylinder and Round Cylinder
            if (request.Dimensions.HasRadius && request.Dimensions.Radius >= 0)  // If radius is present, then set Radius 1 and Radius 2 to same value
            {
                request.Dimensions.Radius1 = request.Dimensions.Radius;
                request.Dimensions.Radius2 = request.Dimensions.Radius;
            }
            if ((request.SelectedSolidType == SolidType.Cylinder || request.SelectedSolidType == SolidType.RoundCylinder) && ((request.Dimensions.Radius <= 0 && !request.Dimensions.HasRadius1 && !request.Dimensions.HasRadius2) || ((request.Dimensions.Radius1 <= 0 || request.Dimensions.Radius2 <= 0) && !request.Dimensions.HasRadius) || request.Dimensions.CylinderHeight <= 0))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Radius or (Radius1 and Radius2) and Cylinder Height must be greater than zero."));

            if (request.SelectedSolidType == 0) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid solid type.")); }

            if (request.SelectedUnit == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid unit type."));

            // Convert the selected solid type from the gRPC request to the corresponding CoreSolidType enum value
            var selectedSolidType = GrpcUtilities.ToCoreSolidType(request.SelectedSolidType);
            var selectedSolidTypeName =  selectedSolidType switch
                {
                    Selector.CoreSolidType.Cube => nameof(Selector.CoreSolidType.Cube),
                    Selector.CoreSolidType.RoundCube => "Round Cube",
                    Selector.CoreSolidType.Cylinder => nameof(Selector.CoreSolidType.Cylinder),
                    Selector.CoreSolidType.RoundCylinder => "Round Cylinder",
                    Selector.CoreSolidType.Polyhedron => nameof(Selector.CoreSolidType.Polyhedron),
                    Selector.CoreSolidType.Sphere => nameof(Selector.CoreSolidType.Sphere),
                    Selector.CoreSolidType.Surface => nameof(Selector.CoreSolidType.Surface),
                    Selector.CoreSolidType.Text => nameof(Selector.CoreSolidType.Text),
                    _ => throw new ArgumentException($"Unknown solid type: {selectedSolidType}", nameof(selectedSolidType))
                };

            var selectedUnit = GrpcUtilities.ToCoreUnitSystem(request.SelectedUnit);
            var colorFromHex = string.Empty;
            var selectedOpenScadColor = Colors.OpenScadColor.Silver; // Default color if no other selected
            var (selectedAxisId, callMethodFromAxisId) = await CreateAxesFunctions.GetAxisByIdAsync(request.AxisId, request.AxisUsedCallingMethod, _db);

            if (request.HasOscadColor)
            {
                if (!System.Enum.TryParse<Colors.OpenScadColor>(request.OscadColor, ignoreCase: true, out selectedOpenScadColor)
                    || !System.Enum.IsDefined(selectedOpenScadColor)
                    || !System.Enum.GetNames<Colors.OpenScadColor>().Contains(request.OscadColor, StringComparer.OrdinalIgnoreCase))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid OpenSCAD color."));
            }

            // Lookup color or if hex color code provided, then use this.
            if (request.HasColorHex)
            {
                // Validate hex color code
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.ColorHex, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$"))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid hex color code."));

                colorFromHex = request.ColorHex;
            }

            var objectId = await ObjectScadFunctions.CreateObjectWithCallbackAsync(
                selectedSolidType: selectedSolidTypeName,
                name: StringFunctions.NameSanitize(request.ObjectName),
                description: request.Description,
                selectedFilament: Core.Material.FilamentType.Other, // Filament option not in current UI, may add in future
                selectedOperationType: GrpcUtilities.ToCoreOperationType(request.SelectedOperationType),
                lengthMM: request.Dimensions.HasLength ? request.Dimensions.Length : 0,
                widthMM: request.Dimensions.HasWidth ? request.Dimensions.Width : 0,
                heightMM: request.Dimensions.HasHeight ? request.Dimensions.Height : 0,
                thicknessMM: request.Dimensions.HasThickness ? request.Dimensions.Thickness : 0,
                radiusMM: request.Dimensions.HasRadius ? request.Dimensions.Radius : 0,
                radius1MM: request.Dimensions.HasRadius1 ? request.Dimensions.Radius1 : 0,
                radius2MM: request.Dimensions.HasRadius2 ? request.Dimensions.Radius2 : 0,
                cylinderHeightMM: request.Dimensions.HasCylinderHeight ? request.Dimensions.CylinderHeight : 0,
                xOffsetMM: request.Dimensions.HasXOffset ? request.Dimensions.XOffset : 0,
                yOffsetMM: request.Dimensions.HasYOffset ? request.Dimensions.YOffset : 0,
                zOffsetMM: request.Dimensions.HasZOffset ? request.Dimensions.ZOffset : 0,
                xRotate: request.Dimensions.HasXRotate ? request.Dimensions.XRotate : 0,
                yRotate: request.Dimensions.HasYRotate ? request.Dimensions.YRotate : 0,
                zRotate: request.Dimensions.HasZRotate ? request.Dimensions.ZRotate : 0,
                selectedOpenScadColor: selectedOpenScadColor,
                layerIntValue: request.Layer,
                alphaIntValue: request.HasAlpha ? request.Alpha : 1,
                surfaceFilePath: request.Dimensions.HasSurfaceFilePath ? request.Dimensions.SurfaceFilePath : string.Empty,
                surfaceCenter: request.Dimensions.HasSurfaceCenter ? 1 : 0,
                surfaceInvert: request.Dimensions.HasSurfaceInvert ? 1 : 0,
                isCubeSelected: selectedSolidType == Selector.CoreSolidType.Cube,
                isRoundCubeSelected: selectedSolidType == Selector.CoreSolidType.RoundCube,
                isSurfaceSelected: selectedSolidType == Selector.CoreSolidType.Surface,
                isRoundSurfaceSelected: false,
                isCylinderSelected: selectedSolidType is Selector.CoreSolidType.Cylinder or Selector.CoreSolidType.RoundCylinder,
                isRoundCylinderSelected: selectedSolidType == Selector.CoreSolidType.RoundCylinder,
                isSphereSelected: selectedSolidType == Selector.CoreSolidType.Sphere,
                isPolyhedronSelected: selectedSolidType == Selector.CoreSolidType.Polyhedron,
                isTextSelected: selectedSolidType == Selector.CoreSolidType.Text,
                selectedPolyhedron: null,
                selectedUnit: selectedUnit,
                decimalPlaces: request.HasDecimalPlaces ? request.DecimalPlaces : 2,
                axisId: selectedAxisId,
                axesSelectEnabled: false,
                appendObject: request.HasAppendObject ? request.AppendObject : false,
                dbConnection: _db,
                generateOscadCallback: solid =>
                {
                    created = solid;
                    return ObjectScadFunctions.GenerateOSCADAsync(
                        oDim: solid,
                        isCubeSelected: selectedSolidType == Selector.CoreSolidType.Cube,
                        isRoundCubeSelected: selectedSolidType == Selector.CoreSolidType.RoundCube,
                        isSurfaceSelected: selectedSolidType == Selector.CoreSolidType.Surface,
                        isRoundSurfaceSelected: false,
                        isCylinderSelected: selectedSolidType is Selector.CoreSolidType.Cylinder or Selector.CoreSolidType.RoundCylinder,
                        isRoundCylinderSelected: selectedSolidType == Selector.CoreSolidType.RoundCylinder,
                        isSphereSelected: selectedSolidType == Selector.CoreSolidType.Sphere,
                        isTextSelected: selectedSolidType == Selector.CoreSolidType.Text,
                        selectedUnit: selectedUnit,
                        decimalPlaces: request.HasDecimalPlaces ? request.DecimalPlaces : 2,
                        surfaceScaleX: request.Dimensions.HasSurfaceScaleX && request.Dimensions.SurfaceScaleX > 0 ? request.Dimensions.SurfaceScaleX : 1,
                        surfaceScaleY: request.Dimensions.HasSurfaceScaleY && request.Dimensions.SurfaceScaleY > 0 ? request.Dimensions.SurfaceScaleY : 1,
                        surfaceScaleZ: request.Dimensions.HasSurfaceScaleZ && request.Dimensions.SurfaceScaleZ > 0 ? request.Dimensions.SurfaceScaleZ : 1,
                        surfaceFilePath: request.Dimensions.HasSurfaceFilePath ? request.Dimensions.SurfaceFilePath : string.Empty,
                        surfaceInvert: request.Dimensions.HasSurfaceInvert ? request.Dimensions.SurfaceInvert : false,
                        surfaceCenter: request.Dimensions.HasSurfaceCenter ? request.Dimensions.SurfaceCenter : false,
                        surfaceConvexity: request.Dimensions.HasSurfaceConvexity && request.Dimensions.SurfaceConvexity > 0 ? request.Dimensions.SurfaceConvexity : 1,
                        isPreRendered: request.HasIsPreRendered ? request.IsPreRendered : true,
                        textInput: request.Dimensions.HasTextInput ? request.Dimensions.TextInput : string.Empty,
                        textSize: request.Dimensions.HasTextSize ? request.Dimensions.TextSize : 0,
                        fontInput: request.Dimensions.HasFontInput ? request.Dimensions.FontInput : string.Empty,
                        textAlign: request.Dimensions.HasTextAlign ? request.Dimensions.TextAlign : string.Empty,
                        verticalAlign: request.Dimensions.HasVerticalAlign ? request.Dimensions.VerticalAlign : string.Empty,
                        textDirection: request.Dimensions.HasTextDirection ? request.Dimensions.TextDirection switch
                        {
                            "L-to-R" => "ltr",
                            "R-to-L" => "rtl",
                            "T-to-B" => "ttb",
                            "B-to-T" => "btt",
                            "" => "ltr",
                            var direction => direction
                        } : "ltr",
                        languageInput: request.Dimensions.HasLanguageInput ? request.Dimensions.LanguageInput : string.Empty);
                },
                refreshDimensionsCallback: () => Task.CompletedTask,
                currentAxisDisplay: string.Empty,
                onObjectCreated: (_, _, _, _) => Task.CompletedTask,
                isColorFromHex: request.HasColorHex,
                openScadColorHex: colorFromHex,
                surfaceScaleX: request.Dimensions.HasSurfaceScaleX && request.Dimensions.SurfaceScaleX > 0 ? request.Dimensions.SurfaceScaleX : 1,
                surfaceScaleY: request.Dimensions.HasSurfaceScaleY && request.Dimensions.SurfaceScaleY > 0 ? request.Dimensions.SurfaceScaleY : 1,
                surfaceScaleZ: request.Dimensions.HasSurfaceScaleZ && request.Dimensions.SurfaceScaleZ > 0 ? request.Dimensions.SurfaceScaleZ : 1,
                textInput: request.Dimensions.HasTextInput ? request.Dimensions.TextInput : string.Empty,
                textSize: request.Dimensions.HasTextSize ? request.Dimensions.TextSize : 0,
                fontInput: request.Dimensions.HasFontInput ? request.Dimensions.FontInput : string.Empty,
                textAlign: request.Dimensions.HasTextAlign ? request.Dimensions.TextAlign : string.Empty,
                verticalAlign: request.Dimensions.HasVerticalAlign ? request.Dimensions.VerticalAlign : string.Empty,
                textDirection: request.Dimensions.HasTextDirection ? request.Dimensions.TextDirection : string.Empty,
                languageInput: request.Dimensions.HasLanguageInput ? request.Dimensions.LanguageInput : string.Empty,
                objectFilePath: _objectFilePath ?? string.Empty,
                isPreRendered: request.HasIsPreRendered ? request.IsPreRendered : true);

            if (objectId == 0 || created == null)
                throw new RpcException(new Status(StatusCode.Internal, "Object creation failed; no solid was saved."));

            var savedSolid = await new NetGenCAD.Designer.Repositories.SolidDimensions().GetByIdAsync(_db, objectId);

            if (savedSolid == null)
                throw new RpcException(new Status(
                    StatusCode.Internal, "The created solid could not be reloaded."));

            var protoSolid = GrpcUtilities.ToProtoSolidDimensions(savedSolid, selectedUnit);

            return new CreateSolidResponse
            {
                ObjectId = objectId,
                ObjectName = request.ObjectName,
                SelectedUnit = request.SelectedUnit,
                AxisId = protoSolid.AxisDimensionsId,
                AxisUsedCallingMethod = callMethodFromAxisId,
                AxisDisplayName = StringFunctions.FormatAxisDisplay(callMethodFromAxisId),
                CreatedSolid = protoSolid
            };
        }

        public override async Task<CopyObjectResponse> CopyObject(CopyObjectRequest request, ServerCallContext context)
        {
            var (solids, modules) = await ObjectScadFunctions.CopyObjectWithCallbackAsync(request.SourceObjectName, _db);

            var response = new CopyObjectResponse();
            response.Solids.AddRange(solids.Select(solid => GrpcUtilities.ToProtoSolidDimensions(solid, Core.Measurements.Selector.UnitSystem.Metric)));
            response.Modules.AddRange(modules.Select(GrpcUtilities.ToProtoModuleDimensions));
            return response;
        }

        public override async Task<StringResponse> DeleteItem(DeleteItemRequest request, ServerCallContext context)
        {
            object item = request.ItemType switch
            {
                ItemType.Solid => new NetGenCAD.Designer.Repositories.SolidDimensions { Id = request.ItemId },
                ItemType.Module => new NetGenCAD.Designer.Repositories.ModuleDimensions { Id = request.ItemId },
                _ => throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid item type."))
            };

            await ObjectScadFunctions.DeleteSelectedItemAsync(item, _db).ContinueWith(x => {
                if (x.IsFaulted)
                {
                    throw new RpcException(new Status(StatusCode.Internal, $"Failed to delete {item.GetType().Name.Replace("Dimensions", "")} item: {x.Exception?.GetBaseException().Message}"));
                }
            });

            return new StringResponse { Message = $"{item.GetType().Name.Replace("Dimensions", "")} item with {request.ItemId} deleted successfully." };
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
                isPreRendered: true,
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
            response.Solids.AddRange(solids.Select(solid => GrpcUtilities.ToProtoSolidDimensions(solid, Core.Measurements.Selector.UnitSystem.Metric)));
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
            //Name = src.Name ?? string.Empty,
            Description = src.Description ?? string.Empty,
            SolidType = src.SolidType,
            Length = src.Length,
            Width = src.Width,
            Height = src.Height,
            Thickness = src.Thickness,
            Radius = src.Radius,
        };

        private static SolidDimensions ToDomain(SolidDimensions proto) => new()
        {
            Id = proto.Id,
            //Name = proto.Name,
            Description = proto.Description,
            SolidType = proto.SolidType,
            Length = proto.Length,
            Width = proto.Width,
            Height = proto.Height,
            Thickness = proto.Thickness,
            Radius = proto.Radius,
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

        private static Grpc.Protobuf.AxisDimensions ToProto(Repositories.AxisDimensions src) => new()
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