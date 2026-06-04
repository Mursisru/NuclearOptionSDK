using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Поисковый выбор из большого каталога (Read/Write/UI из дампа).</summary>
public sealed class CatalogSearchPickerUi
{
    public sealed class Handle
    {
        public required StackPanel Root { get; init; }
        public required TextBox FilterBox { get; init; }
        public required ListBox ResultList { get; init; }
        public required TextBlock SelectedLabel { get; init; }
        public string SelectedId { get; set; } = string.Empty;
    }

    private const int DefaultVisibleCount = 80;
    private const int LargeCatalogThreshold = 40;

    public static bool UseSearchPicker(int choiceCount) => choiceCount > LargeCatalogThreshold;

    public static Handle Create(
        IReadOnlyList<string> choices,
        string value,
        bool readOnly,
        Action onChanged)
    {
        var sorted = choices
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => LogicParamCatalog.FriendlyTitle(id), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => LogicParamCatalog.Title(id), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var handle = new Handle
        {
            SelectedId = value ?? string.Empty,
            Root = new StackPanel { Spacing = 4 },
            FilterBox = new TextBox
            {
                PlaceholderText = "Search: AoA, fuel, radar, enabled…",
                IsReadOnly = readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            },
            ResultList = new ListBox
            {
                MaxHeight = 200,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            },
            SelectedLabel = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 9,
                Opacity = 0.7
            }
        };

        handle.Root.Children.Add(handle.FilterBox);
        handle.Root.Children.Add(handle.ResultList);
        handle.Root.Children.Add(handle.SelectedLabel);

        void RefreshList()
        {
            var query = handle.FilterBox.Text?.Trim() ?? string.Empty;
            IEnumerable<string> items = sorted;
            if (!string.IsNullOrWhiteSpace(query))
            {
                items = sorted
                    .Select(id => new { Id = id, Score = Math.Max(
                        Math.Max(
                            FuzzySearchService.Score(LogicParamCatalog.FriendlyTitle(id), query),
                            FuzzySearchService.Score(LogicParamCatalog.Title(id), query)),
                        FuzzySearchService.Score(id, query)) })
                    .Where(x => x.Score >= 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => LogicParamCatalog.FriendlyTitle(x.Id), StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => x.Id);
            }

            var list = items.Take(DefaultVisibleCount).ToList();
            handle.ResultList.ItemsSource = list.Select(id => new ListBoxItem
            {
                Content = FormatItemLabel(id),
                Tag = id
            }).ToList();

            if (!string.IsNullOrWhiteSpace(handle.SelectedId)
                && list.Any(id => string.Equals(id, handle.SelectedId, StringComparison.OrdinalIgnoreCase)))
            {
                var idx = list.FindIndex(id => string.Equals(id, handle.SelectedId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    handle.ResultList.SelectedIndex = idx;
                }
            }
        }

        void UpdateSelectedLabel()
        {
            if (string.IsNullOrWhiteSpace(handle.SelectedId))
            {
                handle.SelectedLabel.Text = "Not selected";
                return;
            }

            handle.SelectedLabel.Text = LogicParamCatalog.DisplayLabel(handle.SelectedId);
        }

        void SelectId(string id)
        {
            handle.SelectedId = id;
            handle.FilterBox.Text = LogicParamCatalog.FriendlyTitle(id);
            UpdateSelectedLabel();
            onChanged();
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            handle.FilterBox.Text = LogicParamCatalog.FriendlyTitle(value);
        }

        RefreshList();
        UpdateSelectedLabel();

        if (!readOnly)
        {
            handle.FilterBox.TextChanged += (_, _) => RefreshList();
            handle.ResultList.SelectionChanged += (_, _) =>
            {
                if (handle.ResultList.SelectedItem is ListBoxItem { Tag: string id })
                {
                    SelectId(id);
                }
            };
        }

        return handle;
    }

    /// <summary>Поиск watchParam без загрузки/сортировки всего каталога (~5600) в память.</summary>
    public static Handle CreateWatchParamPicker(
        LogicNode node,
        LogicGraph? graph,
        string value,
        bool readOnly,
        Action onChanged)
    {
        var handle = new Handle
        {
            SelectedId = value ?? string.Empty,
            Root = new StackPanel { Spacing = 4 },
            FilterBox = new TextBox
            {
                PlaceholderText = "Search: isLanded, fuel, Member.Aircraft…",
                IsReadOnly = readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            },
            ResultList = new ListBox
            {
                MaxHeight = 160,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            },
            SelectedLabel = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 9,
                Opacity = 0.7
            }
        };

        handle.Root.Children.Add(handle.FilterBox);
        handle.Root.Children.Add(handle.ResultList);
        handle.Root.Children.Add(handle.SelectedLabel);

        void RefreshList()
        {
            var query = handle.FilterBox.Text?.Trim() ?? string.Empty;
            var list = LogicParamCatalog.SearchWatchParamIds(query, node, graph, DefaultVisibleCount).ToList();

            handle.ResultList.ItemsSource = list.Select(id => new ListBoxItem
            {
                Content = FormatWatchItemLabel(id, graph),
                Tag = id
            }).ToList();

            if (!string.IsNullOrWhiteSpace(handle.SelectedId))
            {
                var idx = list.FindIndex(id =>
                    string.Equals(id, handle.SelectedId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    handle.ResultList.SelectedIndex = idx;
                }
            }
        }

        void UpdateSelectedLabel()
        {
            handle.SelectedLabel.Text = string.IsNullOrWhiteSpace(handle.SelectedId)
                ? "Type to search or pick from chain"
                : LogicParamCatalog.WatchParamDisplayLabel(handle.SelectedId, graph);
        }

        void SelectId(string id)
        {
            handle.SelectedId = id;
            handle.FilterBox.Text = LogicParamCatalog.WatchParamFriendlyTitle(id, graph);
            UpdateSelectedLabel();
            onChanged();
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            handle.FilterBox.Text = LogicParamCatalog.WatchParamFriendlyTitle(value, graph);
        }

        RefreshList();
        UpdateSelectedLabel();

        if (!readOnly)
        {
            handle.FilterBox.TextChanged += (_, _) => RefreshList();
            handle.ResultList.SelectionChanged += (_, _) =>
            {
                if (handle.ResultList.SelectedItem is ListBoxItem { Tag: string id })
                {
                    SelectId(id);
                }
            };
        }

        return handle;
    }

    private static string FormatItemLabel(string id) => LogicParamCatalog.DisplayLabel(id);

    private static string FormatWatchItemLabel(string id, LogicGraph? graph) =>
        LogicParamCatalog.WatchParamDisplayLabel(id, graph);
}
