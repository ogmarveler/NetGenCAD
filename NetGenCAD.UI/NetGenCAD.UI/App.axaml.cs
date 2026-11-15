using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NetGenCAD.Designer.Api.Services;
using NetGenCAD.UI.Services;
using NetGenCAD.UI.ViewModels;
using NetGenCAD.UI.Views;
using Scalar.AspNetCore;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace NetGenCAD.UI
{
    public partial class App : Application
    {
        private WebApplication? app;

        // Static Host property to access DI container
        public override void Initialize() => AvaloniaXamlLoader.Load(this);
        public static IServiceProvider? Services { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = Services!.GetRequiredService<MainWindowViewModel>()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainWindow
                {
                    DataContext = Services!.GetRequiredService<MainWindowViewModel>()
                };
            }

            await StartLocalApi(Services!.GetRequiredService<AxesServiceImpl>(), Services!.GetRequiredService<ObjectScadServiceImpl>(), Services!.GetRequiredService<ShapeScadServiceImpl>());   // or resolve from the container

            base.OnFrameworkInitializationCompleted();
        }

        // Api functionality built-in to the application for local use
        [RequiresUnreferencedCode("Calls Microsoft.Extensions.DependencyInjection.IApiVersioningBuilderExtensions.extension(Asp.Versioning.IApiVersioningBuilder).AddGrpcApiExplorer()")]
        private async Task StartLocalApi(AxesServiceImpl axisState, ObjectScadServiceImpl objectState ,ShapeScadServiceImpl shapeState)
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (OperatingSystem.IsWindows())
                {
                    options.ListenNamedPipe("NetGenCAD.Local", listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                }
                else
                {
                    var socketPath = Path.Combine(Path.GetTempPath(), "netgencad.socket");
                    if (File.Exists(socketPath)) File.Delete(socketPath);

                    options.ListenUnixSocket(socketPath, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                } 

                // Localhost HTTP for JSON Transcoding + Scalar / Swagger UI
                options.ListenLocalhost(5000, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                });

            });

            builder.Services.AddSingleton(axisState); // AxesServiceImpl instance for DI
            builder.Services.AddSingleton(objectState); // ObjectScadServiceImpl instance for DI
            builder.Services.AddSingleton(shapeState); // ShapeScadServiceImpl instance for DI
            builder.Services.AddApiVersioning()
                .AddApiExplorer()
                .AddGrpc()
                .AddGrpcApiExplorer()
                .AddOpenApi(options => options.Document.AddScalarTransformers());

            var notifications = Services!.GetRequiredService<ApiChangeNotifications>();
            builder.Services.AddSingleton(notifications);
            builder.Services.AddGrpc(options => options.Interceptors.Add<ApiChangeInterceptor>()).AddJsonTranscoding();

            app = builder.Build();
            app.MapOpenApi().WithDocumentPerVersion();
            app.MapScalarApiReference();

            app.MapGrpcService<AxesServiceImpl>();
            app.MapGrpcService<ObjectScadServiceImpl>();
            app.MapGrpcService<ShapeScadServiceImpl>();

            app.Use(async (context, next) =>
            {
                await next(); // Run the API endpoint first. For JSON transcoding, this will be the controller action. For gRPC, interceptors will handle it.

                if ((!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsDelete(context.Request.Method)) || context.Response.StatusCode != StatusCodes.Status200OK)
                    return; // Only handle POST and DELETE requests that succeeded

                var contentType = context.Request.ContentType;
                if (contentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true)
                    return; // gRPC requests are handled by the ApiChangeInterceptor

                switch (context.Request.Path.Value)
                {
                    case "/v1/axes/createUpdate":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Axes List for all views that depend on it
                        notifications.Publish("axes");
                        break;

                    case "/v1/axes/deleteAxis":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Axes List for all views that depend on it
                        notifications.Publish("axes");
                        break;

                    case "/v1/objects/createUpdate":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Objects for all views that depend on it
                        notifications.Publish("object");
                        break;

                    case "/v1/objects/deleteItem":
                        await Services!.GetRequiredService<ScadObjectViewModel>().PartsToScadFilesAsync().ConfigureAwait(false); // Match if user was deleting item from UI
                        notifications.Publish("object");
                        break;
                }
            });

            await app.RunAsync();
        }
    }
}