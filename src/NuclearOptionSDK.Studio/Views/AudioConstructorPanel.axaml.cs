using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class AudioConstructorPanel : UserControl
{
    private LogicGraph _graph = new();

    public event Action<string>? StatusChanged;
    public event Func<LogicGraph, Task>? PreviewRequested;

    public AudioConstructorPanel()
    {
        InitializeComponent();
        SaveMenuItem.Click += (_, _) => SaveGraph();
        LoadMenuItem.Click += (_, _) => LoadGraph();
        PreviewMenuItem.Click += async (_, _) =>
        {
            if (PreviewRequested != null)
            {
                await PreviewRequested(_graph);
            }
        };
        PlayButton.Click += (_, _) => PlaySelected();
        LoadButton.Click += async (_, _) => await LoadClipAsync();
        DeleteButton.Click += (_, _) => DeleteSelected();
        RestoreButton.Click += (_, _) => RestoreClips();
        ClipList.SelectionChanged += (_, _) => PlayButton.IsEnabled = ClipList.SelectedItem is string;
        ClipList.DoubleTapped += (_, _) => PlaySelected();
        Canvas.StatusChanged += msg => StatusChanged?.Invoke(msg);
        Canvas.GraphChanged += () => _graph = Canvas.GetGraph();
        RefreshClipList();
    }

    public LogicNodeCanvas GraphCanvas => Canvas;

    public void Configure(LogicGraph? initial = null)
    {
        _graph = initial ?? new LogicGraph();
        Canvas.SetGraph(_graph);
        TitleText.Text = "WAV files in audio/ · drag Audio nodes from the left";
    }

    public LogicGraph GetGraph()
    {
        _graph = Canvas.GetGraph();
        return _graph;
    }

    private void RefreshClipList()
    {
        var clips = AudioLibraryService.ListClips();
        ClipList.ItemsSource = clips.Select(c => c.FileName).ToList();
        AudioStatusText.Text = $"{clips.Count} clip(s)";
        PlayButton.IsEnabled = ClipList.SelectedItem is string;
    }

    private void PlaySelected()
    {
        if (ClipList.SelectedItem is not string name)
        {
            return;
        }

        try
        {
            AudioLibraryService.Play(name);
            StatusChanged?.Invoke($"Playing: {name}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Play failed: {ex.Message}");
        }
    }

    private async Task LoadClipAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider == null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import WAV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("WAV") { Patterns = new[] { "*.wav", "*.wave" } }
            }
        });

        if (files.Count == 0)
        {
            return;
        }

        var local = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(local))
        {
            return;
        }

        var dest = AudioLibraryService.ImportFile(local);
        RefreshClipList();
        StatusChanged?.Invoke($"Imported: {Path.GetFileName(dest)}");
    }

    private void DeleteSelected()
    {
        if (ClipList.SelectedItem is not string name)
        {
            return;
        }

        AudioLibraryService.DeleteClip(name);
        RefreshClipList();
        StatusChanged?.Invoke($"Moved to backup: {name}");
    }

    private void RestoreClips()
    {
        var n = AudioLibraryService.RestoreAll();
        RefreshClipList();
        StatusChanged?.Invoke($"Restored {n} clip(s).");
    }

    private string StoragePath => Path.Combine(LogicProjectStore.NosdkDir, "audio-project.json");

    private void SaveGraph()
    {
        _graph = Canvas.GetGraph();
        Directory.CreateDirectory(LogicProjectStore.NosdkDir);
        File.WriteAllText(StoragePath, Newtonsoft.Json.JsonConvert.SerializeObject(_graph));
        StatusChanged?.Invoke($"Saved: {StoragePath}");
    }

    private void LoadGraph()
    {
        if (!File.Exists(StoragePath))
        {
            StatusChanged?.Invoke("No saved audio graph.");
            return;
        }

        _graph = Newtonsoft.Json.JsonConvert.DeserializeObject<LogicGraph>(File.ReadAllText(StoragePath)) ?? new LogicGraph();
        Canvas.SetGraph(_graph);
        StatusChanged?.Invoke("Audio graph loaded.");
    }
}
