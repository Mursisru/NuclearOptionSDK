using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public sealed class LogicInspectorBuilder
{
    private sealed class ParamRow
    {
        public required LogicParamField Field { get; init; }
        public CheckBox? BoolBox { get; init; }
        public ComboBox? ChoiceBox { get; init; }
        public CatalogSearchPickerUi.Handle? SearchPicker { get; init; }
        public TextBox? ManualBox { get; init; }
        public Control? RowHost { get; init; }
    }

    private readonly DisplayLayerService _display = new();
    private sealed class MemberWriteRow
    {
        public required CheckBox EnableBox { get; init; }
        public required StackPanel ValuePanel { get; init; }
        public string? BindingId { get; init; }
        public TextBox? ValueBox { get; init; }
        public ComboBox? BoolChoice { get; init; }
    }

    private sealed class OutputChangeRow
    {
        public required OutputChangeDef Def { get; init; }
        public required CheckBox EnableBox { get; init; }
        public required StackPanel ValuePanel { get; init; }
        public TextBox? ValueBox { get; init; }
        public CheckBox? BoolValueBox { get; init; }
    }

    private readonly List<OutputChangeRow> _outputChanges = new();
    private readonly List<OutputChangeRow> _catalogOutputChanges = new();
    private MemberWriteRow? _memberWriteRow;
    private readonly List<ParamRow> _rows = new();
    private readonly StackPanel _panel;
    private readonly DispatcherTimer _debounce;
    private LogicNode? _node;
    private LogicGraph? _graph;
    private bool _readOnly;
    private bool _suppressApply;
    private string? _watchParamAtBind;

    public LogicInspectorBuilder(StackPanel panel)
    {
        _panel = panel;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            if (!_readOnly && !_suppressApply)
            {
                ParametersEdited?.Invoke();
            }
        };
    }

    public event Action? ParametersEdited;

    public bool ReadOnly => _readOnly;

    public void Bind(LogicNode? node, bool readOnly, LogicGraph? graph = null)
    {
        _debounce.Stop();
        _suppressApply = true;
        _node = node;
        _graph = graph;
        _readOnly = readOnly;
        _watchParamAtBind = node?.parameters.GetValueOrDefault("watchParam");
        _rows.Clear();
        _outputChanges.Clear();
        _catalogOutputChanges.Clear();
        _memberWriteRow = null;
        _panel.Children.Clear();

        if (node == null)
        {
            _suppressApply = false;
            return;
        }

        _panel.Children.Add(new TextBlock
        {
            Text = node.kind switch
            {
                "source" => "Source = parameter from Game Code (drag a field onto the graph).",
                "check" or "gate" => "Select parameter and threshold (value) in the fields below.",
                "output" => "Output: enable game parameter write and/or HUD changes below.",
                _ => "Source → Check → Output. Enter values in the fields below."
            },
            Opacity = 0.55,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        if (node.kind == "source")
        {
            _panel.Children.Add(BuildSourceIdentityPanel(node));
        }
        else if (node.kind is "check" or "gate")
        {
            _panel.Children.Add(BuildCheckContextPanel(node));
        }

        var fields = LogicNodeParameterSchema.GetFields(node, graph);
        LogicParamSection? currentSection = null;

        foreach (var field in fields)
        {
            if (currentSection != field.Section)
            {
                currentSection = field.Section;
                _panel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#1E2430")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 6, 0, 4),
                    Child = new TextBlock
                    {
                        Text = LogicNodeParameterSchema.SectionTitle(field.Section),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 11
                    }
                });
            }

            node.parameters.TryGetValue(field.Key, out var value);
            value ??= ResolveLegacyValue(node, field);
            value ??= string.Empty;

            if (field.Kind == LogicParamKind.Bool)
            {
                var row = CreateBoolRow(field, value);
                _rows.Add(row);
                _panel.Children.Add(row.BoolBox!);
                continue;
            }

            var paramRow = CreateFieldRow(field, value);
            _rows.Add(paramRow);
            _panel.Children.Add(paramRow.RowHost!);
        }

        if (node.kind == "output")
        {
            LogicOutputChangeCatalog.ImportLegacySingleAction(node);
            _panel.Children.Add(BuildOutputChangesSection(node));
        }

        if (_rows.Count == 0 && _outputChanges.Count == 0 && node.kind is not ("source" or "check" or "gate"))
        {
            _panel.Children.Add(new TextBlock
            {
                Text = "No editable parameters for this block type.",
                Opacity = 0.55,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10
            });
        }

        _suppressApply = false;
        EmitPreview(node);
    }

    private Control BuildSourceIdentityPanel(LogicNode node)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = LogicParamCatalog.Title(node.typeId),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        if (NoGameParameterCatalog.TryGet(node.typeId, out var entry))
        {
            stack.Children.Add(new TextBlock
            {
                Text = entry.GamePath,
                FontFamily = new FontFamily("Consolas,Courier New"),
                FontSize = 10,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(entry.Description) ? LogicParamCatalog.Hint(node.typeId) : entry.Description,
                FontSize = 9,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = node.typeId,
                FontSize = 10,
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = "Change source: drag another field from Game Code → Parameters.",
            FontSize = 9,
            Opacity = 0.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#1A2030")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A4A60")),
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private Control BuildCheckContextPanel(LogicNode node)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = LogicCheckCatalog.Title(node.typeId),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = LogicCheckCatalog.Hint(node.typeId),
            FontSize = 9,
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = "1) Parameter — what we read.  2) Expected value — true/false for booleans, number for metrics. Use Compare → Equals for yes/no fields.",
            FontSize = 9,
            Opacity = 0.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#1A2030")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A4A60")),
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    public void EmitPreview(LogicNode? node)
    {
        // Full mod preview is refreshed from MainWindow via synced LogicProject (ParametersEdited / UserGraphChanged).
    }

    public Dictionary<string, string> ReadParameters()
    {
        var result = _node == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(_node.parameters, StringComparer.Ordinal);

        foreach (var row in _rows)
        {
            if (row.Field.Kind == LogicParamKind.Bool)
            {
                result[row.Field.Key] = row.BoolBox!.IsChecked == true ? "true" : "false";
                continue;
            }

            var value = ReadRowValue(row);
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Remove(row.Field.Key);
            }
            else
            {
                result[row.Field.Key] = value.Trim();
            }
        }

        if (_memberWriteRow != null)
        {
            if (_memberWriteRow.EnableBox.IsChecked == true)
            {
                result[LogicOutputMemberWrite.OnKey] = "true";
                var binding = _memberWriteRow.BindingId;
                if (!string.IsNullOrWhiteSpace(binding))
                {
                    result[LogicOutputMemberWrite.BindingKey] = binding;
                }

                var writeVal = ReadMemberWriteValue();
                if (!string.IsNullOrWhiteSpace(writeVal))
                {
                    result[LogicOutputMemberWrite.ValueKey] = writeVal;
                }
            }
            else
            {
                result.Remove(LogicOutputMemberWrite.OnKey);
                result.Remove(LogicOutputMemberWrite.BindingKey);
                result.Remove(LogicOutputMemberWrite.ValueKey);
            }
        }

        foreach (var row in _outputChanges.Concat(_catalogOutputChanges))
        {
            var onKey = LogicOutputChangeCatalog.OnKey(row.Def.Id);
            if (row.EnableBox.IsChecked == true)
            {
                result[onKey] = "true";
                var val = ReadOutputChangeValue(row);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result[LogicOutputChangeCatalog.ValKey(row.Def.Id)] = val;
                }
            }
            else
            {
                result.Remove(onKey);
                result.Remove(LogicOutputChangeCatalog.ValKey(row.Def.Id));
            }
        }

        return result;
    }

    public string BuildHintText(LogicNode node) =>
        $"{node.typeId}\n{_display.Hint(node.typeId)}";

    public string BuildSummaryText(LogicNode node) =>
        LogicNodeParameterSchema.FormatSummary(node, _display);

    private Control BuildOutputChangesSection(LogicNode node)
    {
        var section = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        section.Children.Add(new TextBlock
        {
            Text = "What to change when the check passes",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap
        });

        section.Children.Add(BuildGameParameterWriteSection(node));

        section.Children.Add(new TextBlock
        {
            Text = "Common (Unity / HUD)",
            FontSize = 9,
            Opacity = 0.65,
            Margin = new Thickness(0, 4, 0, 0)
        });

        foreach (var def in LogicOutputChangeCatalog.BuiltIn)
        {
            var row = CreateOutputChangeRow(node, def);
            _outputChanges.Add(row);
            section.Children.Add(row.EnableBox);
            section.Children.Add(row.ValuePanel);
        }

        section.Children.Add(new TextBlock
        {
            Text = "Dump catalog (Write / UI / SerializeField)",
            FontSize = 9,
            Opacity = 0.65,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var catalogSearch = new TextBox
        {
            PlaceholderText = "Search: gear, isLanded, landingGear, Write.*…",
            IsReadOnly = _readOnly,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        section.Children.Add(catalogSearch);

        var catalogHost = new StackPanel { Spacing = 4 };
        section.Children.Add(new ScrollViewer
        {
            MaxHeight = 280,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = catalogHost
        });

        void RefreshCatalogRows()
        {
            catalogHost.Children.Clear();
            _catalogOutputChanges.Clear();
            var query = catalogSearch.Text?.Trim() ?? string.Empty;
            IEnumerable<OutputChangeDef> defs = LogicOutputChangeCatalog.CatalogChanges;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var scored = defs
                    .Select(d => new { Def = d, Score = Math.Max(
                        FuzzySearchService.Score(d.Label, query),
                        FuzzySearchService.Score(d.Id, query)) })
                    .Where(x => x.Score >= 0)
                    .ToDictionary(x => x.Def.Id, x => x.Score, StringComparer.OrdinalIgnoreCase);

                foreach (var (member, score) in GameCodeIndexCache.SearchWatchableMembers(query, 40))
                {
                    if (!LogicOutputMemberWrite.CanWriteBinding(member.BindingId))
                    {
                        continue;
                    }

                    if (!scored.TryGetValue(member.BindingId, out var prev) || score > prev)
                    {
                        scored[member.BindingId] = score;
                    }
                }

                defs = scored
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => LogicOutputChangeCatalog.ResolveDef(kv.Key));
            }
            else
            {
                defs = defs.Take(40);
            }

            foreach (var def in defs.Take(80))
            {
                var row = CreateOutputChangeRow(node, def);
                _catalogOutputChanges.Add(row);
                catalogHost.Children.Add(row.EnableBox);
                catalogHost.Children.Add(row.ValuePanel);
            }

            if (!catalogHost.Children.Any())
            {
                catalogHost.Children.Add(new TextBlock
                {
                    Text = "Nothing found. Enter a field name from the dump.",
                    Opacity = 0.55,
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        RefreshCatalogRows();
        if (!_readOnly)
        {
            catalogSearch.TextChanged += (_, _) => RefreshCatalogRows();
        }

        return new Border
        {
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#14141A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2A34")),
            BorderThickness = new Thickness(1),
            Child = section
        };
    }

    private Control BuildGameParameterWriteSection(LogicNode node)
    {
        var binding = _graph != null
            ? LogicParamCatalog.ResolveUpstreamWatchParam(node, _graph)
            : node.parameters.GetValueOrDefault(LogicOutputMemberWrite.BindingKey);
        if (string.IsNullOrWhiteSpace(binding))
        {
            binding = node.parameters.GetValueOrDefault(LogicOutputMemberWrite.BindingKey);
        }

        var clr = node.parameters.GetValueOrDefault(GameBindingValueSchema.ClrTypeParameterKey)
                  ?? (binding != null ? GameCodeIndexCache.TryGetClrType(binding) : null);
        var valueKind = GameBindingValueSchema.ClassifyClrType(clr);
        var currentValue = LogicOutputMemberWrite.GetValue(node);
        var enabled = LogicOutputMemberWrite.IsEnabled(node);

        var enableBox = new CheckBox
        {
            Content = "Write game parameter (from Source chain)",
            IsChecked = enabled,
            IsEnabled = !_readOnly && !string.IsNullOrWhiteSpace(binding),
            Margin = new Thickness(0, 6, 0, 0)
        };

        var bindingLabel = new TextBlock
        {
            Text = LogicOutputMemberWrite.FriendlyBindingLabel(binding ?? string.Empty, _graph),
            FontSize = 9,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18, 0, 0, 0)
        };
        var dependencyWarning = new TextBlock
        {
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.Parse("#D39F3B")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18, 0, 0, 0),
            IsVisible = false
        };
        if (!string.IsNullOrWhiteSpace(binding))
        {
            var radar = GameCodeIndexCache.TryGetDependencyRadar(binding);
            if (radar is { warnings.Length: > 0 })
            {
                dependencyWarning.IsVisible = true;
                dependencyWarning.Text = "Warning: " + string.Join(" ", radar.warnings);
            }
        }

        var valuePanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(18, 0, 0, 8),
            IsEnabled = enabled && !_readOnly
        };

        TextBox? valueBox = null;
        ComboBox? boolChoice = null;

        if (valueKind == LogicParamKind.Bool)
        {
            boolChoice = new ComboBox
            {
                IsEnabled = !_readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            };
            boolChoice.Items.Add(new ComboBoxItem { Content = "true", Tag = "true" });
            boolChoice.Items.Add(new ComboBoxItem { Content = "false", Tag = "false" });
            boolChoice.SelectedIndex = currentValue == "false" ? 1 : 0;
            if (!_readOnly)
            {
                boolChoice.SelectionChanged += (_, _) => FireImmediate();
            }

            valuePanel.Children.Add(boolChoice);
        }
        else
        {
            valueBox = new TextBox
            {
                Text = currentValue,
                PlaceholderText = valueKind == LogicParamKind.Number ? "Numeric value" : "Value to write",
                IsReadOnly = _readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (!_readOnly)
            {
                valueBox.TextChanged += (_, _) => ScheduleEdit();
            }

            valuePanel.Children.Add(valueBox);
        }

        if (!_readOnly)
        {
            enableBox.IsCheckedChanged += (_, _) =>
            {
                valuePanel.IsEnabled = enableBox.IsChecked == true;
                FireImmediate();
            };
        }

        _memberWriteRow = new MemberWriteRow
        {
            EnableBox = enableBox,
            ValuePanel = valuePanel,
            ValueBox = valueBox,
            BoolChoice = boolChoice,
            BindingId = binding
        };

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = "Game parameter (Member / Write)",
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.75
        });
        stack.Children.Add(enableBox);
        stack.Children.Add(bindingLabel);
        stack.Children.Add(dependencyWarning);
        stack.Children.Add(valuePanel);

        if (string.IsNullOrWhiteSpace(binding))
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Connect Source → Check → Output. Source must be a field from Game Code.",
                FontSize = 9,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        return stack;
    }

    private string ReadMemberWriteValue()
    {
        if (_memberWriteRow?.BoolChoice?.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            return tag;
        }

        return _memberWriteRow?.ValueBox?.Text?.Trim() ?? string.Empty;
    }

    private OutputChangeRow CreateOutputChangeRow(LogicNode node, OutputChangeDef def)
    {
        var enabled = LogicOutputChangeCatalog.IsEnabled(node, def.Id);
        var value = LogicOutputChangeCatalog.GetValue(node, def.Id, def.DefaultValue);

        var enableBox = new CheckBox
        {
            Content = def.Label,
            IsChecked = enabled,
            IsEnabled = !_readOnly,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var valuePanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(18, 0, 0, 6),
            IsEnabled = enabled && !_readOnly
        };

        TextBox? valueBox = null;
        CheckBox? boolValueBox = null;

        if (def.ValueKind == LogicParamKind.Bool)
        {
            boolValueBox = new CheckBox
            {
                Content = "Show (true) / hide (false)",
                IsChecked = value != "false",
                IsEnabled = !_readOnly
            };
            if (!_readOnly)
            {
                boolValueBox.IsCheckedChanged += (_, _) => FireImmediate();
            }

            valuePanel.Children.Add(boolValueBox);
        }
        else
        {
            valueBox = new TextBox
            {
                Text = value,
                PlaceholderText = def.Placeholder ?? def.DefaultValue,
                IsReadOnly = _readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120
            };
            if (!_readOnly)
            {
                valueBox.TextChanged += (_, _) => ScheduleEdit();
            }

            valuePanel.Children.Add(valueBox);

            if (def.ValueKind == LogicParamKind.Color)
            {
                valuePanel.Children.Add(CreateColorSwatchPanel(valueBox));
            }
        }

        if (!_readOnly)
        {
            enableBox.IsCheckedChanged += (_, _) =>
            {
                valuePanel.IsEnabled = enableBox.IsChecked == true;
                FireImmediate();
            };
        }

        return new OutputChangeRow
        {
            Def = def,
            EnableBox = enableBox,
            ValuePanel = valuePanel,
            ValueBox = valueBox,
            BoolValueBox = boolValueBox
        };
    }

    private static string ReadOutputChangeValue(OutputChangeRow row)
    {
        if (row.BoolValueBox != null)
        {
            return row.BoolValueBox.IsChecked == true ? "true" : "false";
        }

        return row.ValueBox?.Text?.Trim() ?? string.Empty;
    }

    private ParamRow CreateBoolRow(LogicParamField field, string value)
    {
        var check = new CheckBox
        {
            Content = field.Label,
            IsChecked = value != "false",
            IsEnabled = !_readOnly,
            Margin = new Thickness(0, 2, 0, 6)
        };
        if (!_readOnly)
        {
            check.IsCheckedChanged += (_, _) => FireImmediate();
        }

        return new ParamRow { Field = field, BoolBox = check };
    }

    private ParamRow CreateFieldRow(LogicParamField field, string value)
    {
        var root = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8)
        };

        root.Children.Add(new TextBlock
        {
            Text = field.Label,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.9
        });

        TextBox? manualBox = null;
        ComboBox? choiceBox = null;
        CatalogSearchPickerUi.Handle? searchPicker = null;

        if (field.Kind == LogicParamKind.Choice)
        {
            if (field.Key == "watchParam" && _node != null)
            {
                searchPicker = CatalogSearchPickerUi.CreateWatchParamPicker(_node, _graph, value, _readOnly, FireImmediate);
                root.Children.Add(searchPicker.Root);
                manualBox = new TextBox { IsVisible = false, Text = value };
            }
            else
            {
            var choices = field.Choices?.ToArray() ?? Array.Empty<string>();
            if (CatalogSearchPickerUi.UseSearchPicker(choices.Length))
            {
                searchPicker = CatalogSearchPickerUi.Create(choices, value, _readOnly, FireImmediate);
                root.Children.Add(searchPicker.Root);
                manualBox = new TextBox { IsVisible = false, Text = value };
            }
            else
            {
                choiceBox = CreateChoiceBox(field, value);
                root.Children.Add(choiceBox);
                var hintBlock = new TextBlock
                {
                    Text = DescribeChoice(field, value),
                    FontSize = 9,
                    Opacity = 0.55,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                root.Children.Add(hintBlock);
                if (!_readOnly && choiceBox != null)
                {
                    choiceBox.SelectionChanged += (_, _) =>
                    {
                        if (choiceBox.SelectedItem is ComboBoxItem { Tag: string tag })
                        {
                            hintBlock.Text = DescribeChoice(field, tag);
                        }
                    };
                }

                manualBox = new TextBox { IsVisible = false, Text = value };
            }
            }
        }
        else
        {
            manualBox = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(value) ? field.Placeholder ?? string.Empty : value,
                PlaceholderText = field.Placeholder ?? "Enter value…",
                IsReadOnly = _readOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 120,
                FontFamily = field.Kind is LogicParamKind.Binding or LogicParamKind.Text
                    ? new FontFamily("Consolas,Courier New")
                    : FontFamily.Default
            };

            if (!_readOnly)
            {
                manualBox.TextChanged += (_, _) => ScheduleEdit();
            }

            root.Children.Add(manualBox);

            if (field.Kind == LogicParamKind.Color)
            {
                root.Children.Add(CreateColorSwatchPanel(manualBox));
            }
        }

        var host = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#14141A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2A34")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = root
        };

        return new ParamRow
        {
            Field = field,
            ChoiceBox = choiceBox,
            SearchPicker = searchPicker,
            ManualBox = manualBox,
            RowHost = host
        };
    }

    private Control CreateColorSwatchPanel(TextBox manualBox)
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var color in new[] { "#FFFFFF", "#FF0000", "#FF4400", "#FFAA00", "#00FF00", "#44AAFF", "#000000" })
        {
            var captured = color;
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(3),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = TryBrush(captured),
                Cursor = _readOnly ? null : new Cursor(StandardCursorType.Hand)
            };

            if (!_readOnly)
            {
                swatch.PointerPressed += (_, _) =>
                {
                    manualBox.Text = captured;
                    FireImmediate();
                };
            }

            wrap.Children.Add(swatch);
        }

        return wrap;
    }

    private ComboBox CreateChoiceBox(LogicParamField field, string value)
    {
        var choices = (field.Choices ?? Array.Empty<string>())
            .OrderBy(id => LogicParamCatalog.FriendlyTitle(id), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var combo = new ComboBox
        {
            IsEnabled = !_readOnly && choices.Length > 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 120
        };

        var selectedIndex = 0;
        for (var i = 0; i < choices.Length; i++)
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = FormatChoiceLabel(field, choices[i]),
                Tag = choices[i]
            });

            if (string.Equals(choices[i], value, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
            }
        }

        combo.SelectedIndex = choices.Length == 0 ? -1 : selectedIndex;
        if (!_readOnly)
        {
            combo.SelectionChanged += (_, _) => FireImmediate();
        }

        return combo;
    }

    private string ReadRowValue(ParamRow row)
    {
        if (row.Field.Kind == LogicParamKind.Choice)
        {
            if (row.SearchPicker != null)
            {
                return row.SearchPicker.SelectedId;
            }

            if (row.ChoiceBox?.SelectedItem is ComboBoxItem { Tag: string choice })
            {
                return choice;
            }

            return string.Empty;
        }

        return row.ManualBox?.Text?.Trim() ?? string.Empty;
    }

    private string FormatChoiceLabel(LogicParamField field, string value) =>
        field.Key switch
        {
            "branch" => LogicParamCatalog.BranchLabel(value),
            "watchParam" => string.IsNullOrWhiteSpace(value)
                ? "— not selected —"
                : LogicParamCatalog.WatchParamDisplayLabel(value, _graph),
            _ when field.Kind == LogicParamKind.Choice => LogicParamCatalog.FriendlyTitle(value),
            _ => value
        };

    private static string DescribeChoice(LogicParamField field, string value)
    {
        if (field.Key == "branch")
        {
            return LogicParamCatalog.BranchLabel(value);
        }

        var desc = LogicParamCatalog.Description(value);
        return string.IsNullOrWhiteSpace(desc) ? LogicParamCatalog.Hint(value) : desc;
    }

    private static string ResolveLegacyValue(LogicNode node, LogicParamField field) =>
        field.Key switch
        {
            "expectValue" when node.parameters.TryGetValue("threshold", out var th) => th,
            "targetId" when node.parameters.TryGetValue("labelId", out var lid) => lid,
            "watchParam" when node.typeId.StartsWith("Telemetry.", StringComparison.Ordinal) => node.typeId,
            "holdSeconds" when node.parameters.TryGetValue("seconds", out var sec) => sec,
            _ => string.Empty
        };

    private static IBrush TryBrush(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    private void ScheduleEdit()
    {
        if (_readOnly || _suppressApply)
        {
            return;
        }

        _debounce.Stop();
        _debounce.Start();
    }

    private void FireImmediate()
    {
        if (_readOnly || _suppressApply || _node == null)
        {
            return;
        }

        if (_node.kind is "check" or "gate")
        {
            var watchParam = ReadCurrentWatchParam();
            if (!string.Equals(watchParam, _watchParamAtBind, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(watchParam))
                {
                    _node.parameters["watchParam"] = watchParam;
                }
                else
                {
                    _node.parameters.Remove("watchParam");
                }

                GameBindingValueSchema.ApplyWatchParamMetadata(_node, _graph);
                GameBindingValueSchema.ApplyDefaultExpectForKind(_node, _graph);
                _watchParamAtBind = watchParam;
                var node = _node;
                var graph = _graph;
                var readOnly = _readOnly;
                Bind(node, readOnly, graph);
                ParametersEdited?.Invoke();
                EmitPreview(node);
                return;
            }
        }

        ParametersEdited?.Invoke();
        EmitPreview(_node);
    }

    private string? ReadCurrentWatchParam()
    {
        var row = _rows.FirstOrDefault(r => r.Field.Key == "watchParam");
        if (row == null)
        {
            return _node?.parameters.GetValueOrDefault("watchParam");
        }

        if (row.SearchPicker != null)
        {
            return row.SearchPicker.SelectedId;
        }

        if (row.ChoiceBox?.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            return tag;
        }

        return row.ManualBox?.Text?.Trim();
    }
}
