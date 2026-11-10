using Grpc.Core;

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

        public static Api.Grpc.Protobuf.SolidDimensions ToProtoSolidDimensions(
            Repositories.SolidDimensions value) => new()
            {
                Id = value.Id,
                AxisDimensionsId = value.AxisDimensionsId ?? 0,
                ModuleDimensionsId = value.ModuleDimensionsId ?? 0,
                Name = value.Name,
                Description = value.Description ?? string.Empty,
                ShapeName = value.ShapeName ?? string.Empty,
                Material = value.Material ?? string.Empty,
                OperationType = value.OperationType,
                SolidType = value.SolidType,
                LengthMm = value.Length_MM,
                WidthMm = value.Width_MM,
                HeightMm = value.Height_MM,
                ThicknessMm = value.Thickness_MM,
                RadiusMm = value.Radius_MM,
                Radius1Mm = value.Radius1_MM,
                Radius2Mm = value.Radius2_MM,
                CylinderHeightMm = value.CylinderHeight_MM,
                XOffsetMm = value.XOffset_MM,
                YOffsetMm = value.YOffset_MM,
                ZOffsetMm = value.ZOffset_MM,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                ScaleX = value.ScaleX,
                ScaleY = value.ScaleY,
                ScaleZ = value.ScaleZ,
                RoundRMm = value.Round_r_MM,
                RoundHMm = value.Round_h_MM,
                Resolution = value.Resolution,
                VolumeCm3 = value.Volume_CM3,
                VolumeIn3 = value.Volume_IN3,
                LengthIn = value.Length_IN,
                WidthIn = value.Width_IN,
                HeightIn = value.Height_IN,
                ThicknessIn = value.Thickness_IN,
                RadiusIn = value.Radius_IN,
                Radius1In = value.Radius1_IN,
                Radius2In = value.Radius2_IN,
                CylinderHeightIn = value.CylinderHeight_IN,
                XOffsetIn = value.XOffset_IN,
                YOffsetIn = value.YOffset_IN,
                ZOffsetIn = value.ZOffset_IN
            };

        public static Repositories.SolidDimensions ToCoreSolidDimensions(
            Api.Grpc.Protobuf.SolidDimensions value) => new()
            {
                Id = value.Id,
                AxisDimensionsId = value.AxisDimensionsId == 0 ? null : value.AxisDimensionsId,
                ModuleDimensionsId = value.ModuleDimensionsId == 0 ? null : value.ModuleDimensionsId,
                Name = value.Name,
                Description = value.Description,
                ShapeName = value.ShapeName,
                Material = value.Material,
                OperationType = value.OperationType,
                SolidType = value.SolidType,
                Length_MM = value.LengthMm,
                Width_MM = value.WidthMm,
                Height_MM = value.HeightMm,
                Thickness_MM = value.ThicknessMm,
                Radius_MM = value.RadiusMm,
                Radius1_MM = value.Radius1Mm,
                Radius2_MM = value.Radius2Mm,
                CylinderHeight_MM = value.CylinderHeightMm,
                XOffset_MM = value.XOffsetMm,
                YOffset_MM = value.YOffsetMm,
                ZOffset_MM = value.ZOffsetMm,
                XRotate = value.XRotate,
                YRotate = value.YRotate,
                ZRotate = value.ZRotate,
                ScaleX = value.ScaleX,
                ScaleY = value.ScaleY,
                ScaleZ = value.ScaleZ,
                Round_r_MM = value.RoundRMm,
                Round_h_MM = value.RoundHMm,
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
