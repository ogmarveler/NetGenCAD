using Grpc.Core;
using static NetGenCAD.Core.Measurements.Selector;

namespace NetGenCAD.Designer.Utility
{
    public static class GrpcUtilities
    {
        public static Core.Measurements.Selector.UnitSystem ToCoreUnitSystem(
            Api.Grpc.Protobuf.UnitSystem value) => value switch
            {
                Api.Grpc.Protobuf.UnitSystem.Metric =>
                    Core.Measurements.Selector.UnitSystem.Metric,
                Api.Grpc.Protobuf.UnitSystem.Imperial =>
                    Core.Measurements.Selector.UnitSystem.Imperial,
                _ => throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "A valid unit system is required"))
            };

        public static Core.Measurements.Selector.BackgroundType ToCoreBackgroundType(
            Api.Grpc.Protobuf.BackgroundType value) => value switch
            {
                Api.Grpc.Protobuf.BackgroundType.Light =>
                    Core.Measurements.Selector.BackgroundType.Light,
                Api.Grpc.Protobuf.BackgroundType.Dark =>
                    Core.Measurements.Selector.BackgroundType.Dark,
                _ => throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "A valid background type is required"))
            };

        public static Core.Primitives.OperationType ToCoreOperationType(
            Api.Grpc.Protobuf.OperationType value) => value switch
            {
                Api.Grpc.Protobuf.OperationType.Union => Core.Primitives.OperationType.Union,
                Api.Grpc.Protobuf.OperationType.Difference => Core.Primitives.OperationType.Difference,
                Api.Grpc.Protobuf.OperationType.Intersection => Core.Primitives.OperationType.Intersection,
                _ => throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "A valid operation type is required"))
            };

        public static CoreSolidType ToCoreSolidType(Api.Grpc.Protobuf.SolidType selectedSolidType)
        {

            return selectedSolidType switch
            {
                Api.Grpc.Protobuf.SolidType.Cube => CoreSolidType.Cube,
                Api.Grpc.Protobuf.SolidType.RoundCube => CoreSolidType.RoundCube,
                Api.Grpc.Protobuf.SolidType.Cylinder => CoreSolidType.Cylinder,
                Api.Grpc.Protobuf.SolidType.RoundCylinder => CoreSolidType.RoundCylinder,
                Api.Grpc.Protobuf.SolidType.Polyhedron => CoreSolidType.Polyhedron,
                Api.Grpc.Protobuf.SolidType.Sphere => CoreSolidType.Sphere,
                Api.Grpc.Protobuf.SolidType.Surface => CoreSolidType.Surface,
                Api.Grpc.Protobuf.SolidType.Text => CoreSolidType.Text,
                _ => throw new ArgumentException($"Unknown solid type: {selectedSolidType}", nameof(selectedSolidType))
            };
        }

        public static Api.Grpc.Protobuf.OperationType ToProtoOperationType(string value) => value switch
        {
            nameof(Core.Primitives.OperationType.Union) => Api.Grpc.Protobuf.OperationType.Union,
            nameof(Core.Primitives.OperationType.Difference) => Api.Grpc.Protobuf.OperationType.Difference,
            nameof(Core.Primitives.OperationType.Intersection) => Api.Grpc.Protobuf.OperationType.Intersection,
            _ => throw new ArgumentException($"Unknown operation type: {value}", nameof(value))
        };

        public static Api.Grpc.Protobuf.SolidType ToProtoSolidType(string value) => value switch
        {
            nameof(CoreSolidType.Cube) => Api.Grpc.Protobuf.SolidType.Cube,
            "Round Cube" => Api.Grpc.Protobuf.SolidType.RoundCube,
            nameof(CoreSolidType.Cylinder) => Api.Grpc.Protobuf.SolidType.Cylinder,
            "Round Cylinder" => Api.Grpc.Protobuf.SolidType.RoundCylinder,
            nameof(CoreSolidType.Polyhedron) => Api.Grpc.Protobuf.SolidType.Polyhedron,
            nameof(CoreSolidType.Sphere) => Api.Grpc.Protobuf.SolidType.Sphere,
            nameof(CoreSolidType.Surface) => Api.Grpc.Protobuf.SolidType.Surface,
            nameof(CoreSolidType.Text) => Api.Grpc.Protobuf.SolidType.Text,
            _ => throw new ArgumentException($"Unknown solid type: {value}", nameof(value))
        };

        public static Api.Grpc.Protobuf.SolidDimensions ToProtoSolidDimensions(
            Repositories.SolidDimensions value) => new()
            {
                Id = value.Id,
                AxisDimensionsId = value.AxisDimensionsId ?? 0,
                ModuleDimensionsId = value.ModuleDimensionsId ?? 0,
                //Name = value.Name,
                Description = value.Description ?? string.Empty,
                ShapeName = value.ShapeName ?? string.Empty,
                Material = value.Material ?? string.Empty,
                OperationType = ToProtoOperationType(value.OperationType),
                SolidType = ToProtoSolidType(value.SolidType),
                Length = value.Length_MM,
                Width = value.Width_MM,
                Height = value.Height_MM,
                Thickness = value.Thickness_MM,
                Radius = value.Radius_MM,
                Radius1 = value.Radius1_MM,
                Radius2 = value.Radius2_MM,
                CylinderHeight = value.CylinderHeight_MM,
                XOffset = value.XOffset_MM,
                YOffset = value.YOffset_MM,
                ZOffset = value.ZOffset_MM,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                SurfaceScaleX = value.ScaleX,
                SurfaceScaleY = value.ScaleY,
                SurfaceScaleZ = value.ScaleZ,
                RoundR = value.Round_r_MM,
                RoundH = value.Round_h_MM,
                Resolution = value.Resolution,
                VolumeCm3 = value.Volume_CM3,
                VolumeIn3 = value.Volume_IN3
            };

        public static Repositories.SolidDimensions ToCoreSolidDimensions(
            Api.Grpc.Protobuf.SolidDimensions value) => new()
            {
                Id = value.Id,
                AxisDimensionsId = value.AxisDimensionsId == 0 ? null : value.AxisDimensionsId,
                ModuleDimensionsId = value.ModuleDimensionsId == 0 ? null : value.ModuleDimensionsId,
                //Name = value.Name,
                Description = value.Description,
                ShapeName = value.ShapeName,
                Material = value.Material,
                OperationType = ToCoreOperationType(value.OperationType).ToString(),
                SolidType = ToCoreSolidType(value.SolidType).ToString(),
                Length_MM = value.Length,
                Width_MM = value.Width,
                Height_MM = value.Height,
                Thickness_MM = value.Thickness,
                Radius_MM = value.Radius,
                Radius1_MM = value.Radius1,
                Radius2_MM = value.Radius2,
                CylinderHeight_MM = value.CylinderHeight,
                XOffset_MM = value.XOffset,
                YOffset_MM = value.YOffset,
                ZOffset_MM = value.ZOffset,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                ScaleX = value.SurfaceScaleX,
                ScaleY = value.SurfaceScaleY,
                ScaleZ = value.SurfaceScaleZ,
                Round_r_MM = value.RoundR,
                Round_h_MM = value.RoundH,
                Resolution = value.Resolution,
                Volume_CM3 = value.VolumeCm3,
                Volume_IN3 = value.VolumeIn3
            };

        public static Api.Grpc.Protobuf.ModuleDimensions ToProtoModuleDimensions(
            Repositories.ModuleDimensions value) => new()
            {
                Id = value.Id,
                Name = value.Name,
                ModuleType = value.ModuleType,
                SolidType = value.SolidType,
                ObjectName = value.ObjectName,
                ObjectDescription = value.ObjectDescription,
                XOffsetMm = value.XOffset_MM,
                YOffsetMm = value.YOffset_MM,
                ZOffsetMm = value.ZOffset_MM,
                XOffsetIn = value.XOffset_IN,
                YOffsetIn = value.YOffset_IN,
                ZOffsetIn = value.ZOffset_IN,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                IncludeMethod = value.IncludeMethod,
                OscadMethod = value.OSCADMethod,
                CreatedAtUnix = new DateTimeOffset(value.CreatedAt).ToUnixTimeSeconds(),
                Layer = value.Layer,
                ModuleColor = value.ModuleColor
            };

        public static Repositories.ModuleDimensions ToCoreModuleDimensions(
            Api.Grpc.Protobuf.ModuleDimensions value) => new()
            {
                Id = value.Id,
                Name = value.Name,
                ModuleType = value.ModuleType,
                SolidType = value.SolidType,
                ObjectName = value.ObjectName,
                ObjectDescription = value.ObjectDescription,
                XOffset_MM = value.XOffsetMm,
                YOffset_MM = value.YOffsetMm,
                ZOffset_MM = value.ZOffsetMm,
                XOffset_IN = value.XOffsetIn,
                YOffset_IN = value.YOffsetIn,
                ZOffset_IN = value.ZOffsetIn,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                IncludeMethod = value.IncludeMethod,
                OSCADMethod = value.OscadMethod,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(value.CreatedAtUnix).UtcDateTime,
                Layer = value.Layer,
                ModuleColor = value.ModuleColor
            };
    }
}
