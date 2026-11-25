using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using NetGenCAD.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NetGenCAD.UI.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        // Set MainView as the initial content
        private object? _mainViewContent = App.Services!.GetRequiredService<MainView>();
        private static readonly HttpClient ApiClient = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer _apiTimer = new() { Interval = TimeSpan.FromSeconds(60) };
        private CancellationTokenSource? _apiMonitoring;
        private string? _apiHealthToken;
        private bool _apiCheckInProgress;
        private bool _isApiOnline;
        private string _apiStatus = "Local API starting";

        [RequiresUnreferencedCode("MainWindowViewModel may use code that is not referenced directly and could be trimmed by the linker.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public MainWindowViewModel()
        {
            MainViewContent = _mainViewContent; // Start with this view
            // Initialize menu commands
            NewAxesCommand = ReactiveCommand.Create(LoadCreateAxesView);
            NewObjectCommand = ReactiveCommand.Create(LoadScadObjectView);
            NewShapeCommand = ReactiveCommand.Create(LoadScadShapeView);
            OpenFolderCommand = ReactiveCommand.CreateFromTask(MainWindow.OpenFolderAsync);
            ToggleCommand = ReactiveCommand.Create(ToggleTheme);
            AxisViewCommand = ReactiveCommand.Create(LoadAxisView);
            DesignerViewCommand = ReactiveCommand.Create(LoadDesignerView);
            ShapeViewCommand = ReactiveCommand.Create(LoadShapeView);
            MainViewCommand = ReactiveCommand.Create(LoadMainView);
            ApiGuideCommand = ReactiveCommand.Create(LoadApiGuide);
            CheckApiCommand = ReactiveCommand.CreateFromTask(CheckApiAsync);
            RefreshAllCommand = ReactiveCommand.Create(RefreshFromApiAsync);
            _apiTimer.Tick += async (_, _) => await CheckApiAsync();
        }

        public object? MainViewContent { get => _mainViewContent; set => this.RaiseAndSetIfChanged(ref _mainViewContent, value); }
        public static void ToggleTheme() => Application.Current?.RequestedThemeVariant = Application.Current.ActualThemeVariant == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
        public string Version => "0.1.0";  // NetGenCAD version
        public const string LocalApiAddress = "http://localhost:5000";
        public const string NamedPipeApiAddress = "NetGenCAD.Local";
        public static string UnixSocketApiAddress => Path.Combine(Path.GetTempPath(), "netgencad.socket");
        public IReadOnlyList<string> ApiAddress { get; } = Array.AsReadOnly(new[] { LocalApiAddress, OperatingSystem.IsWindows() ? @"\\.\pipe\" + NamedPipeApiAddress : UnixSocketApiAddress });
        public bool IsApiOnline => _isApiOnline;
        public IBrush ApiStatusBrush => _isApiOnline ? Brushes.ForestGreen : Brushes.Firebrick;
        public string ApiStatus => _apiStatus;
        public string ApiStatusLabel => _apiStatus == "Local API starting" ? "Starting" : _isApiOnline ? "Online" : "Offline";

        public void StartApiMonitoring(string healthToken)
        {
            _apiHealthToken = healthToken;
            _apiMonitoring = new CancellationTokenSource();
            _apiTimer.Start();
            _ = CheckApiAsync();
        }

        public void StopApiMonitoring()
        {
            _apiTimer.Stop();
            _apiMonitoring?.Cancel();
            _apiMonitoring?.Dispose();
            _apiMonitoring = null;
            _apiHealthToken = null;
            SetApiUnavailable("Local API stopped");
        }

        public void SetApiUnavailable(string message) => UpdateApiStatus(false, message);

        private async Task CheckApiAsync()
        {
            if (_apiMonitoring is null || _apiCheckInProgress)
                return;

            var cancellationToken = _apiMonitoring.Token;
            _apiCheckInProgress = true;
            try
            {
                using var response = await ApiClient.GetAsync($"{LocalApiAddress}/health", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                    UpdateApiStatus(response.IsSuccessStatusCode && body == _apiHealthToken,
                        response.IsSuccessStatusCode && body == _apiHealthToken ? "Local API online" : "Local API health check returned an unexpected response");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                UpdateApiStatus(false, "Local API health check timed out");
            }
            catch (HttpRequestException exception)
            {
                UpdateApiStatus(false, $"Local API unreachable: {exception.Message}");
            }
            finally
            {
                _apiCheckInProgress = false;
            }
        }

        private void UpdateApiStatus(bool online, string status)
        {
            if (_isApiOnline != online || _apiStatus != status)
                Debug.WriteLine(status);
            this.RaiseAndSetIfChanged(ref _isApiOnline, online, nameof(IsApiOnline));
            this.RaiseAndSetIfChanged(ref _apiStatus, status, nameof(ApiStatus));
            this.RaisePropertyChanged(nameof(ApiStatusBrush));
            this.RaisePropertyChanged(nameof(ApiStatusLabel));
        }

        // SPA - Swap out views
        public void LoadCreateAxesView() => MainViewContent = App.Services!.GetRequiredService<CreateAxesView>();
        public void LoadAxisView() => MainViewContent = App.Services!.GetRequiredService<AxisView>();
        public void LoadDesignerView() => MainViewContent = App.Services!.GetRequiredService<DesignerView>();
        public void LoadShapeView() => MainViewContent = App.Services!.GetRequiredService<ShapeView>();
        public void LoadMainView() => MainViewContent = App.Services!.GetRequiredService<MainView>();
        public void LoadApiGuide() => MainViewContent = new ApiGuideView();
        public void LoadScadShapeView()
        {
            App.Services!.GetRequiredService<ScadShapeViewModel>().GetAxesList();  // Refresh Axes List if using singleton or scoped services
            MainViewContent = App.Services!.GetRequiredService<ScadShapeView>();
        }
        public void LoadScadObjectView()
        {
            App.Services!.GetRequiredService<ScadObjectViewModel>().GetAxesList();  // Refresh Axes List if using singleton or scoped services
            App.Services!.GetRequiredService<ScadObjectViewModel>().GetPolyhedronsList(); // Refresh AvailablePolyhedrons List if using singleton
            MainViewContent = App.Services!.GetRequiredService<ScadObjectView>();
        }

        public async Task RefreshFromApiAsync()
        {
            // Refresh Axes List for all views that depend on it
            await App.Services!.GetRequiredService<CreateAxesViewModel>().GetAxesList();  // Refresh Axes List on Create Axes View
            App.Services!.GetRequiredService<ScadObjectViewModel>().GetAxesList();  // Refresh Axes List on Create Object View
            App.Services!.GetRequiredService<ScadShapeViewModel>().GetAxesList();  // Refresh Axes List on Create Shape View

            // Refresh Objects List for all views that depend on it
            App.Services!.GetRequiredService<ScadObjectViewModel>().GetDimensionsParts(); // Refresh Object List on Create Object View
        }

        // Menu Commands
        public ICommand NewAxesCommand { get; }
        public ICommand NewObjectCommand { get; }
        public ICommand NewShapeCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand AxisViewCommand { get; }
        public ICommand DesignerViewCommand { get; }
        public ICommand ShapeViewCommand { get; }
        public ICommand MainViewCommand { get; }
        public ICommand ApiGuideCommand { get; }
        public ICommand CheckApiCommand { get; }
        public ICommand ToggleCommand { get; }
        public ICommand RefreshAllCommand { get; }
    }
}