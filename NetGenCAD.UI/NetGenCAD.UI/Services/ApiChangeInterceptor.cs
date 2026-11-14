using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace NetGenCAD.UI.Services
{
    public sealed class ApiChangeInterceptor : Interceptor
    {
        private readonly ApiChangeNotifications _notifications;

        public ApiChangeInterceptor(ApiChangeNotifications notifications)
        {
            _notifications = notifications;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var response = await continuation(request, context).ConfigureAwait(false);

            // Only handle native gRPC here; JSON is handled by middleware.
            var contentType = context.GetHttpContext().Request.ContentType;
            var isNativeGrpc = contentType?.StartsWith(
                "application/grpc", StringComparison.OrdinalIgnoreCase) == true;

            if (isNativeGrpc && context.Status.StatusCode == StatusCode.OK)
            {
                switch (context.Method)
                {
                    case "/createAxes.AxesService/CreateCustomAxis":
                        _notifications.Publish("axes");
                        break;

                    case "/objectScad.ObjectScadService/CreateSolid":
                        _notifications.Publish("objects");
                        break;
                }
            }

            return response;
        }
    }
}
