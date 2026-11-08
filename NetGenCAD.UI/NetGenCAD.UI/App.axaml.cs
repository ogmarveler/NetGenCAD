using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NetGenCAD.Designer.Api.Services;
using NetGenCAD.UI.ViewModels;
using NetGenCAD.UI.Views;
using System;
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
            builder.Services.AddGrpc().AddJsonTranscoding(); // Add JSON transcoding for RESTful endpoints

            app = builder.Build();
            app.MapGrpcService<AxesServiceImpl>();
            app.MapGrpcService<ObjectScadServiceImpl>();
            app.MapGrpcService<ShapeScadServiceImpl>();

            await app.RunAsync();
        }
    }
}