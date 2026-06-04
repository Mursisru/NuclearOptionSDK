using Avalonia.Controls;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class ProjectWindowPanel : UserControl
{
    private string _selectedFolder = string.Empty;

    public ProjectWindowPanel()
    {
        InitializeComponent();
        RefreshProjectTreeButton.Click += (_, _) => RefreshProjectTree();
        ProjectTree.SelectionChanged += (_, _) => OnProjectFolderSelected();
        Loaded += (_, _) => RefreshProjectTree();
    }

    public void RefreshProjectTree()
    {
        var roots = new List<TreeViewItem>
        {
            BuildFolderNode("Favorites", isFavorite: true),
            BuildFolderNode("Assets", LogicProjectStore.NosdkDir),
            BuildFolderNode("Logic", Path.GetDirectoryName(LogicProjectStore.ProjectPath) ?? LogicProjectStore.NosdkDir),
            BuildFolderNode("Defaults", ResolveDefaultsDir())
        };

        ProjectTree.ItemsSource = roots;
        if (roots.Count > 0 && ProjectTree.SelectedItem == null)
        {
            ProjectTree.SelectedItem = roots[1];
        }
    }

    private static string ResolveDefaultsDir()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "Defaults");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        return baseDir;
    }

    private TreeViewItem BuildFolderNode(string label, string? path = null, bool isFavorite = false)
    {
        var fullPath = path ?? string.Empty;
        var item = new TreeViewItem
        {
            Header = label,
            Tag = fullPath
        };

        if (isFavorite)
        {
            item.Items.Add(new TreeViewItem { Header = "Logic project", Tag = LogicProjectStore.ProjectPath });
            item.Items.Add(new TreeViewItem { Header = "Audio library", Tag = AudioLibraryService.LibraryDir });
            return item;
        }

        if (string.IsNullOrWhiteSpace(fullPath) || !Directory.Exists(fullPath))
        {
            return item;
        }

        try
        {
            foreach (var dir in Directory.GetDirectories(fullPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.') || name is "bin" or "obj")
                {
                    continue;
                }

                item.Items.Add(BuildFolderNode(name, dir));
            }
        }
        catch
        {
            // ignore locked folders
        }

        return item;
    }

    private void OnProjectFolderSelected()
    {
        if (ProjectTree.SelectedItem is not TreeViewItem { Tag: string path } || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            _selectedFolder = Path.GetDirectoryName(path) ?? path;
            LoadAssets(_selectedFolder);
            ProjectPathLabel.Text = path;
            return;
        }

        _selectedFolder = path;
        LoadAssets(path);
        ProjectPathLabel.Text = path;
    }

    private void LoadAssets(string folder)
    {
        if (!Directory.Exists(folder))
        {
            AssetList.ItemsSource = Array.Empty<string>();
            return;
        }

        var entries = new List<string>();
        try
        {
            foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add("📁 " + Path.GetFileName(dir));
            }

            foreach (var file in Directory.GetFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            entries.Add($"! {ex.Message}");
        }

        AssetList.ItemsSource = entries;
    }
}
