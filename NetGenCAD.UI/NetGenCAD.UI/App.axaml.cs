using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetGenCAD.Axis.Scad.Models;
using NetGenCAD.Designer.Api.Grpc.Protobuf;
using NetGenCAD.Designer.Api.Services;
using NetGenCAD.Designer.Repositories;
using NetGenCAD.UI.Services;
using NetGenCAD.UI.ViewModels;
using NetGenCAD.UI.Views;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace NetGenCAD.UI
{
    public partial class App : Application
    {
        private WebApplication? app;
        private readonly CancellationTokenSource apiShutdown = new();
        private readonly string apiHealthToken = Guid.NewGuid().ToString("N");

        // Auto-generated DataGrid columns discover row properties through reflection.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Axis.Scad.Models.GeneratedModule))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Designer.Repositories.PolyhedronDimensions))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Designer.Repositories.ShapeDimensions))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Designer.Repositories.SolidDimensions))]
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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                desktopLifetime.Exit += (_, _) => apiShutdown.Cancel();

            base.OnFrameworkInitializationCompleted();

            try
            {
                await StartLocalApi(Services!.GetRequiredService<AxesServiceImpl>(), Services!.GetRequiredService<ObjectScadServiceImpl>(), Services!.GetRequiredService<ShapeScadServiceImpl>());
            }
            catch (OperationCanceledException) when (apiShutdown.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Local API failed: {exception}");
                await Dispatcher.UIThread.InvokeAsync(() => Services!.GetRequiredService<MainWindowViewModel>().SetApiUnavailable($"Local API failed: {exception.Message}"));
            }
        }

        // Api functionality built-in to the application for local use
        [RequiresUnreferencedCode("Calls Microsoft.Extensions.DependencyInjection.IApiVersioningBuilderExtensions.extension(Asp.Versioning.IApiVersioningBuilder).AddGrpcApiExplorer()")]
        private async Task StartLocalApi(AxesServiceImpl axisState, ObjectScadServiceImpl objectState ,ShapeScadServiceImpl shapeState)
        {
            var builder = WebApplication.CreateSlimBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (OperatingSystem.IsWindows())
                {
                    options.ListenNamedPipe(MainWindowViewModel.NamedPipeApiAddress, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                }
                else
                {
                    var socketPath = MainWindowViewModel.UnixSocketApiAddress;
                    if (File.Exists(socketPath)) File.Delete(socketPath);

                    options.ListenUnixSocket(socketPath, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                } 

                // Localhost HTTP API
                options.ListenLocalhost(new Uri(MainWindowViewModel.LocalApiAddress).Port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                });

            });

            var notifications = Services!.GetRequiredService<ApiChangeNotifications>();

            builder.Services.AddSingleton(axisState); // AxesServiceImpl instance for DI
            builder.Services.AddSingleton(objectState); // ObjectScadServiceImpl instance for DI
            builder.Services.AddSingleton(shapeState); // ShapeScadServiceImpl instance for DI

            builder.Services.AddSingleton(notifications);
            builder.Services.AddGrpc(options => options.Interceptors.Add<ApiChangeInterceptor>()).AddJsonTranscoding(options =>
            {
                options.JsonSettings.WriteIndented = true;
                options.JsonSettings.PropertyNameCaseInsensitive = true;
            });
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, GrpcJsonSerializerContext.Default);
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
            });
            app = builder.Build();

            app.MapGrpcService<AxesServiceImpl>();
            app.MapGrpcService<ObjectScadServiceImpl>();
            app.MapGrpcService<ShapeScadServiceImpl>();
            app.MapGet("/health", async (HttpContext context) => await context.Response.WriteAsync(apiHealthToken));
            app.Use(async (context, next) =>
            {
                await next(); // Run the API endpoint first. For JSON transcoding, this will be the controller action. For gRPC, interceptors will handle it.

                if ((!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsDelete(context.Request.Method)) || context.Response.StatusCode != StatusCodes.Status200OK)
                    return; // Only handle POST and DELETE requests that succeeded

                var contentType = context.Request.ContentType;
                if (contentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true)
                    return; // gRPC requests are handled by the ApiChangeInterceptor

                switch (context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant())
                {
                    case "/v1/axes/createupdate":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Axes List for all views that depend on it
                        notifications.Publish("axes");
                        break;

                    case "/v1/axes/deleteaxis":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Axes List for all views that depend on it
                        notifications.Publish("axes");
                        break;

                    case "/v1/object/solid/createupdate":
                        await Services!.GetRequiredService<MainWindowViewModel>().RefreshFromApiAsync().ConfigureAwait(false); // Refresh Objects for all views that depend on it
                        notifications.Publish("object");
                        break;

                    case "/v1/object/solid/deleteitem":
                        await Services!.GetRequiredService<ScadObjectViewModel>().PartsToScadFilesAsync().ConfigureAwait(false); // Match if user was deleting item from UI
                        notifications.Publish("object");
                        break;
                }
            });

            app.UseMiddleware<ProtobufJsonMiddleware>(); // Custom Resolver for native AOT JSONTranscoding issues with Grpc; see IL2026 issue
            var viewModel = Services!.GetRequiredService<MainWindowViewModel>();
            try
            {
                await app.StartAsync(apiShutdown.Token);
                await Dispatcher.UIThread.InvokeAsync(() => viewModel.StartApiMonitoring(apiHealthToken));
                await app.WaitForShutdownAsync(apiShutdown.Token);
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(viewModel.StopApiMonitoring);
                await app.DisposeAsync();
            }
        }
    }
}