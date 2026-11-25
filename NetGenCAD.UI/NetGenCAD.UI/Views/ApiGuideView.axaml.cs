using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.IO;
using NetGenCAD.UI.ViewModels;
using Markdown.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Diagnostics.CodeAnalysis;

namespace NetGenCAD.UI.Views;

public partial class ApiGuideView : UserControl
{
    public ApiGuideView()
    {
        InitializeComponent();
        DataContext = App.Services!.GetRequiredService<ApiGuideViewModel>();
        LoadMarkdownAsync("avares://NetGenCAD.UI/Assets/Guides/Api.markdown");  // Relative or absolute path
    }

    private async void LoadMarkdownAsync(string avaPath)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(avaPath));
            using var reader = new StreamReader(stream);
            var markdownContent = await reader.ReadToEndAsync();
            if (MarkdownView is MarkdownScrollViewer viewer)
            {
                viewer.Markdown = markdownContent; // Now markdownContent is a string
                viewer.Plugins.HyperlinkCommand = ReactiveCommand.Create<string>(url =>
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                });
            }
        }
        catch (Exception ex)
        {
            // Fallback: Display error in the viewer
            if (MarkdownView is MarkdownScrollViewer viewer)
            {
                viewer.Markdown = $"**Error loading Markdown file:** {ex.Message}";
            }
        }
    }
}
