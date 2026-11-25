using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Api.Services;

namespace NetGenCAD.Designer.Api.Grpc.Services;

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
        if (!(isGet && path == "/v1/axes") &&
            !(isPost && path is "/v1/axes/convert/imperial" or "/v1/axes/convert/metric" or "/v1/axes/createupdate" or "/v1/object/solid/createupdate" or "/v1/object/createupdate" or "/v1/object/copy") &&
            !(isDelete && path is "/v1/axes/deleteaxis" or "/v1/object/solid/deleteitem"))
        {
            await next(context);
            return;
        }

        try
        {
            IMessage response = path switch
            {
                "/v1/axes" => await axes.GetAxesListAsync(),
                "/v1/axes/convert/imperial" => await axes.ConvertToImperialAsync(await ReadAsync<ConvertUnitsRequest>(context)),
                "/v1/axes/convert/metric" => await axes.ConvertToMetricAsync(await ReadAsync<ConvertUnitsRequest>(context)),
                "/v1/axes/createupdate" => await axes.CreateUpdateAxisAsync(await ReadAsync<CreateCustomAxisRequest>(context)),
                "/v1/axes/deleteaxis" => await axes.DeleteAxisAsync(await ReadAsync<DeleteAxisRequest>(context)),
                "/v1/object/solid/createupdate" => await objects.CreateUpdateSolidAsync(await ReadAsync<CreateUpdateSolidRequest>(context)),
                "/v1/object/solid/deleteitem" => await objects.DeleteItemAsync(await ReadAsync<DeleteItemRequest>(context)),
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
