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
using System.Globalization;
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

        // Hierarchy of CAD structures: File -> (module Axis, module Set) -> Object -> Module -> Solid.  Each level can be created, updated, or deleted. The gRPC service methods are implemented here to handle these operations.

        /************************* 
         * gRPC Service Methods - SOLID creation, update, and deletion
         *************************/

        // HTTP 1.1 --------------------------------------------- /v1/object/solid/createUpdate -------------------------------------------------//
        // HTTP 2 --------------------------------------------- /objectScad.ObjectScadService/CreateUpdateSolid ---------------------------------//
        public override Task<CreateUpdateSolidResponse> CreateUpdateSolid(CreateUpdateSolidRequest request, ServerCallContext context) => CreateUpdateSolidAsync(request);

        public async Task<CreateUpdateSolidResponse> CreateUpdateSolidAsync(CreateUpdateSolidRequest request)
        {
            // Convert the selected solid type from the gRPC request to the corresponding CoreSolidType enum value
            if (request.SelectedSolidType == 0) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid solid type.")); }
            var selectedSolidType = GrpcUtilities.ToCoreSolidType(request.SelectedSolidType);
            var selectedSolidTypeName = selectedSolidType switch
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

            // Conversions of JSON supplied values to ObjectScadFunctions.CreateObjectWithCallbackAsync parameters are handled within the function itself, including default values and validation.
            if (string.IsNullOrWhiteSpace(request.ObjectName))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Object name cannot be empty."));

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Solid description cannot be empty."));

            var description = request.Description.Trim();

            if (request.Dimensions == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Dimensions cannot be null."));

            // Solid Validation - Cube and Round Cube
            var length = request.Dimensions.HasLength && request.Dimensions.Length > 0 ? request.Dimensions.Length : 0;
            var width = request.Dimensions.HasWidth && request.Dimensions.Width > 0 ? request.Dimensions.Width : 0;
            var height = request.Dimensions.HasHeight && request.Dimensions.Height > 0 ? request.Dimensions.Height : 0;
            var thickness = request.Dimensions.HasThickness && request.Dimensions.Thickness > 0 ? request.Dimensions.Thickness : 0;

            if ((selectedSolidType == Selector.CoreSolidType.Cube || selectedSolidType == Selector.CoreSolidType.RoundCube) && (length <= 0 || width <= 0 || height <= 0))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Length, Width, and Height must be greater than zero."));

            // Solid Validation - Cylinder and Round Cylinder
            var radius = request.Dimensions.HasRadius && request.Dimensions.Radius > 0 ? request.Dimensions.Radius : 0;
            var radius1 = request.Dimensions.HasRadius1 && request.Dimensions.Radius1 > 0 ? request.Dimensions.Radius1 : 0;
            var radius2 = request.Dimensions.HasRadius2 && request.Dimensions.Radius2 > 0 ? request.Dimensions.Radius2 : 0;
            var cylinderHeight = request.Dimensions.HasCylinderHeight && request.Dimensions.CylinderHeight > 0 ? request.Dimensions.CylinderHeight : 0;

            if (radius > 0)  // If radius is present, then set Radius 1 and Radius 2 to same value
            {
                radius1 = radius;
                radius2 = radius;
            }
            if ((selectedSolidType == Selector.CoreSolidType.Cylinder || selectedSolidType == Selector.CoreSolidType.RoundCylinder) && ((radius <= 0 && radius1 <= 0 && radius2 <= 0) || (radius1 <= 0 || radius2 <= 0) || cylinderHeight <= 0))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Radius or (Radius1 and Radius2) and Cylinder Height must be greater than zero."));


            if (request.SelectedUnit == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid unit type."));

            var selectedUnit = GrpcUtilities.ToCoreUnitSystem(request.SelectedUnit);
            var decimalPlaces = request.HasDecimalPlaces ? request.DecimalPlaces : 2;
            var colorFromHex = string.Empty;
            var selectedOpenScadColor = Colors.OpenScadColor.Silver; // Default color if no other selected
            var (selectedAxisId, callMethodFromAxisId) = await CreateAxesFunctions.GetAxisByIdAsync(request.AxisId, request.AxisUsedCallingMethod, _db);

            // Surface Validation and Format - same logic as view model
            var surfaceFilePath = request.Dimensions.HasSurfaceFilePath ? request.Dimensions.SurfaceFilePath : string.Empty;
            var autoSmoothFile = request.Dimensions.HasSurfaceFileFormat ? request.Dimensions.SurfaceFileFormat : false;
            var surfaceScaleX = request.Dimensions.HasSurfaceScaleX && request.Dimensions.SurfaceScaleX > 0 ? request.Dimensions.SurfaceScaleX : 1;
            var surfaceScaleY = request.Dimensions.HasSurfaceScaleY && request.Dimensions.SurfaceScaleY > 0 ? request.Dimensions.SurfaceScaleY : 1;
            var surfaceScaleZ = request.Dimensions.HasSurfaceScaleZ && request.Dimensions.SurfaceScaleZ > 0 ? request.Dimensions.SurfaceScaleZ : 1;
            var surfaceInvert = request.Dimensions.HasSurfaceInvert? request.Dimensions.SurfaceInvert : false;
            var surfaceCenter = request.Dimensions.HasSurfaceCenter ? request.Dimensions.SurfaceCenter : false;
            var surfaceConvexity = request.Dimensions.HasSurfaceConvexity && request.Dimensions.SurfaceConvexity > 0 ? request.Dimensions.SurfaceConvexity : 1;

            if (selectedSolidType == Selector.CoreSolidType.Surface && string.IsNullOrWhiteSpace(surfaceFilePath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Surface file path cannot be empty for Surface solid type."));
            }
            else if (selectedSolidType == Selector.CoreSolidType.Surface)
            {
                // Call the static function to get calculated PNG dimensions
                var (sLength, sWidth, sHeight, sXOffset, _, updatedFilePath) = ObjectScadFunctions.LoadPngDimensionsData(
                    filePath: surfaceFilePath,
                    autoSmoothFile: autoSmoothFile,
                    selectedUnit: selectedUnit,
                    decimalPlaces: decimalPlaces,
                    surfaceScaleX: surfaceScaleX,
                    surfaceScaleY: surfaceScaleY,
                    surfaceScaleZ: surfaceScaleZ,
                    surfaceInvert: surfaceInvert,
                    _objectFilePath);

                // Same logic as the View Model function; interpet the dimensions based on how the user has selected the surface file format
                // Length, Width, and Height are mainly references to the dimensions of the surface file, but the actual values used in the SCAD generation are determined by the selected format and scaling factors.
                length = sLength;
                width = sWidth;
                height = sHeight;
                surfaceFilePath = updatedFilePath; // Update the file path to the processed file path (if any changes were made)
            }

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

            // Text validation - set height to greater than 0 if not provided, but must be greater than 0 if Text solid type is selected
            if (selectedSolidType == Selector.CoreSolidType.Text && !request.Dimensions.HasTextInput)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Text input cannot be empty for Text solid type."));

            // Polyhedron validation - validate if name is provided
            if (selectedSolidType == Selector.CoreSolidType.Polyhedron && !request.Dimensions.HasShapeName)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Shape name cannot be empty for Polyhedron solid type."));

            Repositories.ShapeDimensions? savedShape = null;
            if (request.Dimensions.HasShapeName && selectedSolidType == Selector.CoreSolidType.Polyhedron)
            {
                var _savedShape = await new NetGenCAD.Designer.Repositories.ShapeDimensions().GetByNameAsync(_db, StringFunctions.NameSanitize(request.Dimensions.ShapeName));
                savedShape = _savedShape.FirstOrDefault();
            }

            // Text options use the same defaults and display values as the UI.
            var caseSensitive = CultureInfo.CurrentCulture.TextInfo;
            var isText = selectedSolidType == Selector.CoreSolidType.Text;
            var textInput = isText && request.Dimensions.HasTextInput ? request.Dimensions.TextInput : string.Empty;
            var textSize = isText ? request.Dimensions.HasTextSize && request.Dimensions.TextSize > 0 ? request.Dimensions.TextSize : 10 : 0;
            height = isText ? height > 0 ? height : selectedUnit == Selector.UnitSystem.Metric ? 1 : 0.125 : height;
            var fontInput = isText ? request.Dimensions.HasFontInput ? GrpcUtilities.ToCorePortableFont(request.Dimensions.FontInput).ToString() : "Sans" : string.Empty;
            var textAlign = isText ? caseSensitive.ToTitleCase(GrpcUtilities.ToTextOptionDescription(request.Dimensions.HasTextAlign ? GrpcUtilities.ToCoreTextHAlign(request.Dimensions.TextAlign) : Selector.TextHAlign.left).ToLower()) : string.Empty;
            var verticalAlign = isText ? caseSensitive.ToTitleCase(GrpcUtilities.ToTextOptionDescription(request.Dimensions.HasVerticalAlign ? GrpcUtilities.ToCoreTextVAlign(request.Dimensions.VerticalAlign) : Selector.TextVAlign.baseline).ToLower()) : string.Empty;
            var textDirection = isText ? GrpcUtilities.ToTextOptionDescription(request.Dimensions.HasTextDirection ? GrpcUtilities.ToCoreTextDirection(request.Dimensions.TextDirection) : Selector.TextDirection.ltr).ToUpper().Replace("TO","to") : string.Empty;
            var languageInput = isText ? (request.Dimensions.HasLanguageInput ? GrpcUtilities.ToCoreTextLanguage(request.Dimensions.LanguageInput) : Selector.TextLanguage.en).ToString().ToUpper() : string.Empty;

            // Rest of supplied variables - translate and rotate values are optional, default to 0 if not provided
            var xOffset = request.Dimensions.HasXOffset ? request.Dimensions.XOffset : 0;
            var yOffset = request.Dimensions.HasYOffset ? request.Dimensions.YOffset : 0;
            var zOffset = request.Dimensions.HasZOffset ? request.Dimensions.ZOffset : 0;
            var xRotate = request.Dimensions.HasXRotate ? request.Dimensions.XRotate : 0;
            var yRotate = request.Dimensions.HasYRotate ? request.Dimensions.YRotate : 0;
            var zRotate = request.Dimensions.HasZRotate ? request.Dimensions.ZRotate : 0;
            // Other
            var isPrerendered = request.HasIsPreRendered ? request.IsPreRendered : true;
            var appendObject = request.HasAppendObject ? request.AppendObject : false;


            var solidId = await ObjectScadFunctions.CreateObjectWithCallbackAsync(
                selectedSolidType: selectedSolidTypeName,
                name: StringFunctions.NameSanitize(request.ObjectName),
                description: description,
                selectedFilament: Core.Material.FilamentType.Other, // Filament option not in current UI, may add in future
                selectedOperationType: GrpcUtilities.ToCoreOperationType(request.SelectedOperationType),
                lengthMM: length,
                widthMM: width,
                heightMM: height,
                thicknessMM: thickness,
                radiusMM: radius,
                radius1MM: radius1,
                radius2MM: radius2,
                cylinderHeightMM: cylinderHeight,
                xOffsetMM: xOffset,
                yOffsetMM: yOffset,
                zOffsetMM: zOffset,
                xRotate: xRotate,
                yRotate: yRotate,
                zRotate: zRotate,
                selectedOpenScadColor: selectedOpenScadColor,
                layerIntValue: request.Layer,
                alphaIntValue: request.HasAlpha ? request.Alpha : 1,
                surfaceFilePath: surfaceFilePath,
                surfaceCenter: surfaceCenter ? 1 : 0,
                surfaceInvert: surfaceInvert ? 1 : 0,
                isCubeSelected: selectedSolidType == Selector.CoreSolidType.Cube,
                isRoundCubeSelected: selectedSolidType == Selector.CoreSolidType.RoundCube,
                isSurfaceSelected: selectedSolidType == Selector.CoreSolidType.Surface,
                isRoundSurfaceSelected: false,
                isCylinderSelected: selectedSolidType is Selector.CoreSolidType.Cylinder or Selector.CoreSolidType.RoundCylinder,
                isRoundCylinderSelected: selectedSolidType == Selector.CoreSolidType.RoundCylinder,
                isSphereSelected: selectedSolidType == Selector.CoreSolidType.Sphere,
                isPolyhedronSelected: selectedSolidType == Selector.CoreSolidType.Polyhedron,
                isTextSelected: selectedSolidType == Selector.CoreSolidType.Text,
                selectedPolyhedron: request.Dimensions.HasShapeName && selectedSolidType == Selector.CoreSolidType.Polyhedron ? savedShape : null,
                selectedUnit: selectedUnit,
                decimalPlaces: decimalPlaces,
                axisId: selectedAxisId,
                axesSelectEnabled: false,
                appendObject: appendObject,
                dbConnection: _db,
                generateOscadCallback: solid =>
                {
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
                        decimalPlaces: decimalPlaces,
                        surfaceScaleX: surfaceScaleX,
                        surfaceScaleY: surfaceScaleY,
                        surfaceScaleZ: surfaceScaleZ,
                        surfaceFilePath: surfaceFilePath,
                        surfaceInvert: surfaceInvert,
                        surfaceCenter: surfaceCenter,
                        surfaceConvexity: surfaceConvexity,
                        isPreRendered: isPrerendered,
                        textInput: textInput,
                        textSize: textSize,
                        fontInput: fontInput,
                        textAlign: textAlign,
                        verticalAlign: verticalAlign,
                        textDirection: textDirection,
                        languageInput: languageInput);
                },
                refreshDimensionsCallback: () => Task.CompletedTask,
                currentAxisDisplay: string.Empty,
                onObjectCreated: (_, _, _, _) => Task.CompletedTask,
                isColorFromHex: request.HasColorHex,
                openScadColorHex: colorFromHex,
                surfaceScaleX: surfaceScaleX,
                surfaceScaleY: surfaceScaleY,
                surfaceScaleZ: surfaceScaleZ,
                textInput: textInput,
                textSize: textSize,
                fontInput: fontInput,
                textAlign: textAlign,
                verticalAlign: verticalAlign,
                textDirection: textDirection,
                languageInput: languageInput,
                objectFilePath: _objectFilePath ?? string.Empty,
                isPreRendered: isPrerendered);

            if (solidId <= 0)
                throw new RpcException(new Status(StatusCode.Internal, "Object creation failed."));

            // Get Module name and include method to include in response
            var savedSolid = await new NetGenCAD.Designer.Repositories.SolidDimensions().GetByIdWithModuleAsync(_db, solidId);
            var _savedSolid = savedSolid.FirstOrDefault()
                ?? throw new RpcException(new Status(
                    StatusCode.Internal, "The created solid could not be reloaded."));

            var protoSolid = GrpcUtilities.ToProtoSolidDimensions(_savedSolid, selectedUnit);
            protoSolid.SurfaceFileFormat = autoSmoothFile;
            protoSolid.SurfaceConvexity = surfaceConvexity;

            return new CreateUpdateSolidResponse
            {
                ObjectId = _savedSolid.ObjectDimensionsId,
                ObjectName = _savedSolid.Name,
                OscadFile = Path.Combine(Path.GetDirectoryName(_objectFilePath)!, _savedSolid.ModuleFile!.Replace("include <", "").Replace(">;", "")),
                SelectedUnit = request.SelectedUnit,
                AxisId = (int)_savedSolid.AxisDimensionsId!,
                AxisUsedCallingMethod = callMethodFromAxisId,
                AxisDisplayName = StringFunctions.FormatAxisDisplay(callMethodFromAxisId),
                ObjectModule = new ModuleObject
                {
                    ObjectModuleId = _savedSolid.ModuleDimensionsId ?? 0,
                    ObjectModuleName = _savedSolid.ModuleName ?? string.Empty,
                    ModuleLayer = _savedSolid.Layer,
                    CreatedSolid = protoSolid
                }
            };
        }

        // HTTP 1.1 --------------------------------------------- /v1/object/solid/deleteItem -------------------------------------------------//
        // HTTP 2 --------------------------------------------- /objectScad.ObjectScadService/DeleteItem ---------------------------------//
        public override Task<StringResponse> DeleteItem(DeleteItemRequest request, ServerCallContext context) => DeleteItemAsync(request);

        public async Task<StringResponse> DeleteItemAsync(DeleteItemRequest request)
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

        /************************* 
         * gRPC Service Methods - OBJECT creation, update, and deletion
         *************************/

        // HTTP 1.1 --------------------------------------------- /v1/object/copy -------------------------------------------------//
        // HTTP 2 --------------------------------------------- /objectScad.ObjectScadService/CopyObject ---------------------------------//
        public override Task<CopyObjectResponse> CopyObject(CopyObjectRequest request, ServerCallContext context) => CopyObjectAsync(request);

        public async Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request)
        {
           

            if (request.HasSourceObjectId && request.SourceObjectId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Source object ID must be positive."));
            if (!request.HasSourceObjectId && string.IsNullOrWhiteSpace(request.SourceObjectName))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "A source object ID or name is required."));

            var source = request.HasSourceObjectId
                ? await new ObjectDimensions().GetByIdAsync(_db, request.SourceObjectId)
                : await new ObjectDimensions().GetByNameAsync(_db, request.SourceObjectName.Trim());
            if (source == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Source object was not found."));
            if (request.TargetObjectName.Length > 0 && string.IsNullOrWhiteSpace(request.TargetObjectName))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Target object name cannot be blank."));

            // Make sure it stores lowercase and also spaces replaced with "_"
            var targetObjectName = StringFunctions.NameSanitize(request.TargetObjectName);

            System.Collections.ObjectModel.ObservableCollection<NetGenCAD.Designer.Repositories.SolidDimensions> solids;
            System.Collections.ObjectModel.ObservableCollection<NetGenCAD.Designer.Repositories.ModuleDimensions> modules;
            try
            {
                (solids, modules) = targetObjectName.Length == 0
                    ? await ObjectScadFunctions.CopyObjectWithCallbackAsync(source.ObjectName, _db)
                    : await ObjectScadFunctions.CopyObjectWithCallbackAsync(source.ObjectName, targetObjectName, _db);
            }
            catch (ObjectCopy.ObjectNameAlreadyExistsException exception)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, exception.Message));
            }
            var copyName = solids.FirstOrDefault()?.Name ?? modules.FirstOrDefault()?.ObjectName;
            if (copyName == null)
                throw new RpcException(new Status(StatusCode.Internal, "Copy produced no object data."));
            var copy = await new ObjectDimensions().GetByNameAsync(_db, copyName)
                ?? throw new RpcException(new Status(StatusCode.Internal, "The copied object could not be reloaded."));
            var response = new CopyObjectResponse { ObjectDimensionsId = copy.Id, ObjectName = copy.ObjectName };
            response.Solids.AddRange(solids.Select(solid => GrpcUtilities.ToProtoSolidDimensions(solid, Core.Measurements.Selector.UnitSystem.Metric)));
            response.Modules.AddRange(modules.Select(GrpcUtilities.ToProtoModuleDimensions));
            return response;
        }
    }
}