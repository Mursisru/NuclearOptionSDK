using Avalonia.Controls;
using Newtonsoft.Json;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class GraphConstructorPanel : UserControl
{
    private string _storageKey = "graph";
    private LogicGraph _graph = new();

    public event Action<string>? StatusChanged;
    public event Func<LogicGraph, Task>? PreviewRequested;

    public GraphConstructorPanel()
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
        Canvas.StatusChanged += msg => StatusChanged?.Invoke(msg);
        Canvas.GraphChanged += () => _graph = Canvas.GetGraph();
    }

    public LogicNodeCanvas GraphCanvas => Canvas;

    public void Configure(string title, string storageKey, LogicGraph? initial = null)
    {
        TitleText.Text = title;
        _storageKey = storageKey;
        _graph = initial ?? new LogicGraph();
        Canvas.SetGraph(_graph);
    }

    public LogicGraph GetGraph()
    {
        _graph = Canvas.GetGraph();
        return _graph;
    }

    private string StoragePath =>
        Path.Combine(LogicProjectStore.NosdkDir, $"{_storageKey}.json");

    private void SaveGraph()
    {
        _graph = Canvas.GetGraph();
        Directory.CreateDirectory(LogicProjectStore.NosdkDir);
        File.WriteAllText(StoragePath, JsonConvert.SerializeObject(_graph));
        StatusChanged?.Invoke($"Saved: {StoragePath}");
    }

    private void LoadGraph()
    {
        if (!File.Exists(StoragePath))
        {
            StatusChanged?.Invoke("No saved graph.");
            return;
        }

        var json = File.ReadAllText(StoragePath);
        _graph = JsonConvert.DeserializeObject<LogicGraph>(json) ?? new LogicGraph();
        Canvas.SetGraph(_graph);
        StatusChanged?.Invoke($"Loaded: {StoragePath}");
    }
}
