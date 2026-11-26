using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetGenCAD.Designer.Api.Grpc.Protobuf;

namespace NetGenCAD.Designer.Api.Services;

public sealed class ProtobufJsonMiddleware(RequestDelegate next, ILogger<ProtobufJsonMiddleware> logger)
{
    private static readonly JsonFormatter Formatter = new(new JsonFormatter.Settings(true).WithIndentation("  "));

    public async Task InvokeAsync(HttpContext context, AxesServiceImpl axes, ObjectScadServiceImpl objects)
    {
        var path = context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant();
        var method = context.Request.Method;
        var isGet = HttpMethods.IsGet(method);
        var isPost = HttpMethods.IsPost(method);
        var isDelete = HttpMethods.IsDelete(method);
        if (!(isGet && (path == "/v1/axes" || path == "/v1/axes/convertunits" || path == "/v1/object/get")) &&
            !(isPost && (path == "/v1/axes/createupdate" || path == "/v1/object/solid/createupdate" || path == "/v1/object/createupdate" || path == "/v1/object/copy")) &&
            !(isDelete && (path == "/v1/axes/deleteaxis" || path == "/v1/object/solid/deleteitem")))
        {
            await next(context);
            return;
        }

        try
        {
            IMessage response = path switch
            {
                "/v1/axes" => await axes.GetAxesListAsync(ReadAxisId(context)),
                "/v1/axes/convertunits" => await axes.ConvertUnitsAsync(await ReadAsync<ConvertUnitsRequest>(context)),
                "/v1/axes/createupdate" => await axes.CreateUpdateAxisAsync(await ReadAsync<CreateCustomAxisRequest>(context)),
                "/v1/axes/deleteaxis" => await axes.DeleteAxisAsync(await ReadAsync<DeleteAxisRequest>(context)),
                "/v1/object/solid/createupdate" => await objects.CreateUpdateSolidAsync(await ReadAsync<CreateUpdateSolidRequest>(context)),
                "/v1/object/solid/deleteitem" => await objects.DeleteItemAsync(await ReadAsync<DeleteItemRequest>(context)),
                "/v1/object/get" => await objects.GetObjectAsync(new GetObjectRequest { ObjectId = ReadObjectId(context) }),
                "/v1/object/copy" => await objects.CopyObjectAsync(await ReadAsync<CopyObjectRequest>(context)),
                "/v1/object/createupdate" => throw new RpcException(new Status(StatusCode.Unimplemented, "Method is unimplemented.")),
                _ => throw new InvalidOperationException("Unknown API route.")
            };
            var json = Formatter.Format(response);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(json, context.RequestAborted);
        }
        catch (InvalidJsonException exception)
        {
            await WriteErrorAsync(context, StatusCode.InvalidArgument, exception.Message);
        }
        catch (InvalidProtocolBufferException exception)
        {
            await WriteErrorAsync(context, StatusCode.InvalidArgument, exception.Message);
        }
        catch (JsonException exception)
        {
            await WriteErrorAsync(context, StatusCode.InvalidArgument, exception.Message);
        }
        catch (RpcException exception)
        {
            await WriteErrorAsync(context, exception.StatusCode, exception.Status.Detail);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "HTTP API request {Method} {Path} failed", method, path);
            if (context.Response.HasStarted)
                throw;
            await WriteErrorAsync(context, StatusCode.Internal, "Internal server error.");
        }
    }

    private static int? ReadAxisId(HttpContext context)
    {
        var values = context.Request.Query.Where(pair =>
            string.Equals(pair.Key, "axisId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pair.Key, "axis_id", StringComparison.OrdinalIgnoreCase)).SelectMany(pair => pair.Value).ToArray();
        if (values.Length == 0)
            return null;
        if (values.Length != 1 || !int.TryParse(values[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var axisId) || axisId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "axis_id must be a single positive integer."));
        return axisId;
    }

    private static int ReadObjectId(HttpContext context)
    {
        var values = context.Request.Query.Where(pair =>
            string.Equals(pair.Key, "objectId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pair.Key, "object_id", StringComparison.OrdinalIgnoreCase)).SelectMany(pair => pair.Value).ToArray();
        if (values.Length != 1 || !int.TryParse(values[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var objectId) || objectId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "object_id must be a single positive integer."));
        return objectId;
    }

    private static async Task<T> ReadAsync<T>(HttpContext context) where T : IMessage<T>, new()
    {
        using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            WriteNormalizedMessage(writer, document.RootElement, new T().Descriptor);
        return JsonParser.Default.Parse<T>(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static void WriteNormalizedMessage(Utf8JsonWriter writer, JsonElement element, MessageDescriptor descriptor)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            element.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject())
        {
            var field = descriptor.Fields.InFieldNumberOrder().FirstOrDefault(f =>
                string.Equals(f.JsonName, property.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, property.Name, StringComparison.OrdinalIgnoreCase));
            writer.WritePropertyName(field?.JsonName ?? property.Name);
            if (field is { FieldType: FieldType.Message, IsMap: false } && property.Value.ValueKind != JsonValueKind.Null)
            {
                if (field.IsRepeated && property.Value.ValueKind == JsonValueKind.Array)
                {
                    writer.WriteStartArray();
                    foreach (var item in property.Value.EnumerateArray())
                        WriteNormalizedMessage(writer, item, field.MessageType);
                    writer.WriteEndArray();
                }
                else
                    WriteNormalizedMessage(writer, property.Value, field.MessageType);
            }
            else
                property.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static async Task WriteErrorAsync(HttpContext context, StatusCode status, string message)
    {
        context.Response.StatusCode = status switch
        {
            StatusCode.InvalidArgument or StatusCode.OutOfRange or StatusCode.FailedPrecondition => StatusCodes.Status400BadRequest,
            StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
            StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
            StatusCode.NotFound => StatusCodes.Status404NotFound,
            StatusCode.AlreadyExists or StatusCode.Aborted => StatusCodes.Status409Conflict,
            StatusCode.ResourceExhausted => StatusCodes.Status429TooManyRequests,
            StatusCode.Unimplemented => StatusCodes.Status501NotImplemented,
            StatusCode.Unavailable => StatusCodes.Status503ServiceUnavailable,
            StatusCode.DeadlineExceeded => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };
        context.Response.ContentType = "application/json; charset=utf-8";
        await using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("code", (int)status);
            writer.WriteString("message", message);
            writer.WriteStartArray("details");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
