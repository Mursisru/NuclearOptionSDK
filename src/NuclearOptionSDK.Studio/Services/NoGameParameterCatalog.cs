namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Полный каталог параметров, методов и значений Nuclear Option.
/// Источники: дамп Assembly-CSharp, моды at747, GitHub (NO_Tactitools, MKModsNO, NOMNOM и др.).
/// Curated — <see cref="NoGameParameterCatalog.Curated.cs"/>; dump — <see cref="NoGameParameterCatalog.Data.cs"/>.
/// </summary>
public static partial class NoGameParameterCatalog
{
    public enum Direction
    {
        Read,
        Write,
        Both
    }

    public enum Category
    {
        FlightTelemetry,
        PilotInputs,
        Weapons,
        Countermeasures,
        RadarEw,
        MapTactical,
        HudFlightHud,
        HudGauges,
        CombatHud,
        MfdHmd,
        Audio,
        Camera,
        PlayerSettings,
        GameSession,
        OutputAction,
        CompareMode,
        LogicGeneral
    }

    public sealed record Entry(
        string Id,
        string Title,
        string Description,
        Direction Direction,
        Category Category,
        string ValueType,
        string GamePath,
        string? DefaultValue = null,
        string[]? QuickValues = null);

    public sealed record MethodEntry(
        string Id,
        string Title,
        string Description,
        Category Category,
        string GamePath,
        string ReturnType,
        string[] ParameterHints);

    private static readonly Lazy<CatalogSnapshot> Snapshot = new(BuildSnapshot);

    private sealed record CatalogSnapshot(
        Entry[] Entries,
        MethodEntry[] Methods,
        Dictionary<string, Entry> ById,
        Dictionary<string, MethodEntry> MethodsById);

    private static CatalogSnapshot BuildSnapshot()
    {
        var curatedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in BuildCuratedEntries())
        {
            curatedIds.Add(e.Id);
        }

        var entries = BuildCuratedEntries().Select(NormalizeEntry).ToArray();

        var curatedMethodIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in BuildCuratedMethods())
        {
            curatedMethodIds.Add(m.Id);
        }

        var methods = BuildCuratedMethods().Select(NormalizeMethod).ToArray();

        return new CatalogSnapshot(
            entries,
            methods,
            entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase),
            methods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase));
    }

    private static Entry[] AllEntries => Snapshot.Value.Entries;
    private static MethodEntry[] AllMethods => Snapshot.Value.Methods;
    private static Dictionary<string, Entry> ById => Snapshot.Value.ById;
    private static Dictionary<string, MethodEntry> MethodsById => Snapshot.Value.MethodsById;

    private static Entry NormalizeEntry(Entry e)
    {
        if (IsCuratedFriendlyId(e.Id))
        {
            return e;
        }

        var systemTitle = SystemTitle(e.GamePath, e.Id);
        var desc = TryResolveRuEntry(e, out _, out var ruDesc)
            ? ruDesc
            : BuildFallbackDesc(e, TypeName(e.GamePath), MemberName(e.GamePath, e.Title));

        return e with { Title = systemTitle, Description = desc };
    }

    private static MethodEntry NormalizeMethod(MethodEntry m)
    {
        if (IsCuratedFriendlyId(m.Id))
        {
            return m;
        }

        var systemTitle = string.IsNullOrWhiteSpace(m.GamePath) ? m.Id : $"{m.GamePath}()";
        var desc = TryResolveRuMethod(m, out _, out var ruDesc)
            ? ruDesc
            : BuildMethodDesc(m, $"Method {m.GamePath}.");

        return m with { Title = systemTitle, Description = desc };
    }

    private static string SystemTitle(string gamePath, string fallbackId) =>
        string.IsNullOrWhiteSpace(gamePath) ? fallbackId : gamePath;

    private static bool IsCuratedFriendlyId(string id) =>
        id.StartsWith("Telemetry.", StringComparison.Ordinal)
        || id.StartsWith("Cockpit.", StringComparison.Ordinal)
        || id.StartsWith("Input.", StringComparison.Ordinal)
        || id.StartsWith("Weapon.", StringComparison.Ordinal)
        || id.StartsWith("CM.", StringComparison.Ordinal)
        || id.StartsWith("EW.", StringComparison.Ordinal)
        || id.StartsWith("Map.", StringComparison.Ordinal)
        || id.StartsWith("Gate.", StringComparison.Ordinal)
        || id.StartsWith("Session.", StringComparison.Ordinal)
        || id.StartsWith("General.", StringComparison.Ordinal)
        || id.StartsWith("Compare.", StringComparison.Ordinal)
        || id.StartsWith("Action.", StringComparison.Ordinal)
        || id.StartsWith("Logic.", StringComparison.Ordinal)
        || id.StartsWith("combat.", StringComparison.Ordinal)
        || id.StartsWith("status.", StringComparison.Ordinal)
        || id.StartsWith("mfd.", StringComparison.Ordinal)
        || id.StartsWith("hmd.", StringComparison.Ordinal)
        || id.StartsWith("hud.", StringComparison.Ordinal)
        || id.StartsWith("camera.", StringComparison.Ordinal)
        || id.StartsWith("settings.", StringComparison.Ordinal)
        || id.StartsWith("state.", StringComparison.Ordinal)
        || id.Contains('-') // overlay ids: aoa-label, speed-text
        || id.StartsWith("voice.", StringComparison.Ordinal)
        || id is "stallHorn" or "stallWarning" or "overspeed" or "pullUp" or "warningBeep"
            or "lockTone" or "missileLaunch" or "weaponRipple" or "clickUI" or "music.takeoff";

    private static string MemberName(string gamePath, string fallback)
    {
        var dot = gamePath.LastIndexOf('.');
        return dot >= 0 ? gamePath[(dot + 1)..] : fallback;
    }

    private static string TypeName(string gamePath)
    {
        var dot = gamePath.LastIndexOf('.');
        return dot >= 0 ? gamePath[..dot] : gamePath;
    }

    private static string Humanize(string name) =>
        System.Text.RegularExpressions.Regex.Replace(
            name, "([a-z])([A-Z])", "$1 $2",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));

    public static IReadOnlyList<Entry> Entries => AllEntries;
    public static IReadOnlyList<MethodEntry> Methods => AllMethods;

    public static bool TryGet(string id, out Entry entry) => ById.TryGetValue(id, out entry!);
    public static bool TryGetMethod(string id, out MethodEntry method) => MethodsById.TryGetValue(id, out method!);
    public static string Title(string id) =>
        TryGet(id, out var e) ? e.Title
        : TryGetMethod(id, out var m) ? m.Title
        : id;

    /// <summary>RU-название для коротких списков (ComboBox ≤40, вкладка «Параметры»).</summary>
    public static string FriendlyTitle(string id)
    {
        if (TryGet(id, out var e))
        {
            return FriendlyTitleForEntry(e);
        }

        if (TryGetMethod(id, out var m))
        {
            return FriendlyTitleForMethod(m);
        }

        return id;
    }

    /// <summary>RU-заголовок для picker: понятное имя + системный путь мелко.</summary>
    public static string DisplayLabel(string id)
    {
        var friendly = FriendlyTitle(id);
        if (TryGet(id, out var e) && !IsCuratedFriendlyId(e.Id) && !string.IsNullOrWhiteSpace(e.GamePath))
        {
            return $"{friendly}\n{e.GamePath}";
        }

        if (TryGetMethod(id, out var m) && !IsCuratedFriendlyId(m.Id) && !string.IsNullOrWhiteSpace(m.GamePath))
        {
            return $"{friendly}\n{m.GamePath}()";
        }

        return friendly;
    }

    public static string GamePathOf(string id) =>
        TryGet(id, out var e) ? e.GamePath
        : TryGetMethod(id, out var m) ? m.GamePath
        : string.Empty;

    /// <summary>RU-метка типа из дампа (Aircraft → ЛА, RadarDisplay → РЛС · индикатор).</summary>
    public static string FriendlyTypeLabel(string typeFullOrShortName)
    {
        if (string.IsNullOrWhiteSpace(typeFullOrShortName))
        {
            return string.Empty;
        }

        var shortName = typeFullOrShortName.Contains('.')
            ? typeFullOrShortName[(typeFullOrShortName.LastIndexOf('.') + 1)..]
            : typeFullOrShortName;
        return ResolveTypeLabelRu(shortName);
    }

    /// <summary>RU-метка поля/метода с контекстом типа.</summary>
    public static string FriendlyMemberLabel(string typeName, string memberName)
    {
        var readId = TryResolveReadId(typeName, memberName);
        if (readId != null)
        {
            return FriendlyTitle(readId);
        }

        var shortType = typeName.Contains('.')
            ? typeName[(typeName.LastIndexOf('.') + 1)..]
            : typeName;
        return ComposeFieldTitle(ResolveTypeLabelRu(shortType), ResolveMemberLabelRu(memberName));
    }

    /// <summary>Строка больших каталогов — только системное имя (GamePath).</summary>
    public static string PaletteCaption(string id) => Title(id);

    public static string Description(string id) =>
        TryGet(id, out var e) ? e.Description
        : TryGetMethod(id, out var m) ? m.Description
        : string.Empty;

    public static string Hint(string id)
    {
        if (TryGet(id, out var e))
        {
            if (IsCuratedFriendlyId(e.Id))
            {
                return string.IsNullOrWhiteSpace(e.GamePath) ? e.Description : $"{e.Description} · {e.GamePath}";
            }

            return e.Description;
        }

        if (TryGetMethod(id, out var m))
        {
            if (IsCuratedFriendlyId(m.Id))
            {
                return $"{m.Description} · {m.GamePath}() → {m.ReturnType}";
            }

            return m.Description;
        }

        return id;
    }

    /// <summary>Краткая RU-подпись поля (без системного пути).</summary>
    public static string RuBrief(string id)
    {
        if (TryGet(id, out var e))
        {
            return IsCuratedFriendlyId(e.Id) ? string.Empty : RuBriefForEntry(e);
        }

        if (TryGetMethod(id, out var m))
        {
            return IsCuratedFriendlyId(m.Id)
                ? string.Empty
                : HumanizeRu(MemberName(m.GamePath, m.Title));
        }

        return string.Empty;
    }

    private static string FriendlyTitleForEntry(Entry e)
    {
        var system = SystemTitle(e.GamePath, e.Id);
        if (!string.Equals(e.Title, system, StringComparison.Ordinal))
        {
            return e.Title;
        }

        if (TryResolveRuEntry(e, out var ruTitle, out _))
        {
            return ruTitle;
        }

        var member = MemberName(e.GamePath, e.Title);
        var typeLabel = ResolveTypeLabelRu(TypeName(e.GamePath));
        return ComposeFieldTitle(typeLabel, ResolveMemberLabelRu(member));
    }

    private static string FriendlyTitleForMethod(MethodEntry m)
    {
        var system = string.IsNullOrWhiteSpace(m.GamePath) ? m.Id : $"{m.GamePath}()";
        if (!string.Equals(m.Title, system, StringComparison.Ordinal))
        {
            return m.Title;
        }

        if (TryResolveRuMethod(m, out var ruTitle, out _))
        {
            return ruTitle;
        }

        var member = MemberName(m.GamePath, m.Title);
        var typeLabel = ResolveTypeLabelRu(TypeName(m.GamePath));
        return ComposeFieldTitle(typeLabel, ResolveMemberLabelRu(member));
    }

    private static string RuBriefForEntry(Entry e)
    {
        if (RuByGamePath.TryGetValue(e.GamePath, out var pathHint))
        {
            return pathHint.Title;
        }

        var member = MemberName(e.GamePath, e.Title);
        if (RuByMember.TryGetValue(member, out var memberHint))
        {
            return ComposeFieldTitle(ResolveTypeLabelRu(TypeName(e.GamePath)), memberHint.Title);
        }

        return ComposeFieldTitle(ResolveTypeLabelRu(TypeName(e.GamePath)), ResolveMemberLabelRu(member));
    }

    public static IReadOnlyList<string> QuickValues(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return ["0", "10", "15", "20", "25"];
        }

        if (TryGet(id, out var e) && e.QuickValues is { Length: > 0 })
        {
            return e.QuickValues;
        }

        return FallbackQuickValues(id);
    }

    public static CatalogGroup[] ReadingGroups => BuildGroups(
        Direction.Read,
        Direction.Both,
        excludeCategory: Category.OutputAction);

    public static CatalogGroup[] TargetGroups => BuildGroups(
        Direction.Write,
        Direction.Both,
        excludeCategory: Category.CompareMode,
        excludeCategory2: Category.OutputAction,
        excludeCategory3: Category.LogicGeneral,
        onlyWriteTargets: true);

    public static CatalogGroup[] CompareModeGroups =>
    [
        new("Compare mode", AllEntries
            .Where(e => e.Category == Category.CompareMode)
            .Select(e => e.Id)
            .ToArray())
    ];

    public static CatalogGroup[] OutputActionGroups =>
    [
        new("Output actions", AllEntries
            .Where(e => e.Category == Category.OutputAction)
            .Select(e => e.Id)
            .ToArray())
    ];

    public static CatalogGroup[] MethodGroups =>
        Enum.GetValues<Category>()
            .Select(cat =>
            {
                var ids = AllMethods.Where(m => m.Category == cat).Select(m => m.Id).ToArray();
                return ids.Length == 0 ? null : new CatalogGroup(CategoryTitle(cat) + " · methods", ids);
            })
            .Where(g => g != null)
            .Cast<CatalogGroup>()
            .ToArray();

    public static IReadOnlyList<string> FlatReadings() =>
        AllEntries
            .Where(e => e.Direction is Direction.Read or Direction.Both)
            .Where(e => e.Category is not Category.OutputAction and not Category.CompareMode)
            .Where(e => !IsLogicPaletteBlockId(e.Id))
            .Select(e => e.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FriendlyTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Параметры для инспектора блока «Проверка» (все читаемые поля + телеметрия).</summary>
    public static IReadOnlyList<string> FlatWatchParams()
    {
        var pinned = new[]
        {
            "Telemetry.AoA", "Telemetry.Speed", "Telemetry.Altitude", "Telemetry.G",
            "Telemetry.Fuel", "Telemetry.Rpm", "Telemetry.Throttle", "Telemetry.GearDown",
            "Telemetry.WeaponLock", "Read.Aircraft.speed", "Read.Aircraft.fuelLevel",
            "Read.Unit.radarAlt", "Read.Aircraft.gForce", "Read.WeaponStation.Ammo"
        }.Where(id => TryGet(id, out _));

        var readable = AllEntries
            .Where(e => e.Direction is Direction.Read or Direction.Both)
            .Where(e => e.Category is not Category.OutputAction and not Category.CompareMode)
            .Where(e => IsWatchParamCandidate(e.Id))
            .Select(e => e.Id);

        return pinned
            .Concat(readable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(FriendlyTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWatchParamCandidate(string id) =>
        !id.StartsWith("Gate.", StringComparison.Ordinal)
        && !id.StartsWith("Compare.", StringComparison.Ordinal)
        && !id.StartsWith("Action.", StringComparison.Ordinal)
        && !id.StartsWith("Logic.", StringComparison.Ordinal)
        && !id.StartsWith("Member.", StringComparison.Ordinal);

    /// <summary>Stable binding id for a game member (Read.* if curated, else Member.*).</summary>
    public static string? TryResolveReadId(string typeFullName, string memberName) =>
        ApiSurface.ApiSymbolIdFactory.TryResolveReadId(typeFullName, memberName);

    private static bool IsLogicPaletteBlockId(string id) =>
        id.StartsWith("Gate.", StringComparison.Ordinal)
        || id.StartsWith("Compare.", StringComparison.Ordinal)
        || id.StartsWith("Telemetry.", StringComparison.Ordinal)
        || id.StartsWith("Action.", StringComparison.Ordinal)
        || id.StartsWith("Logic.", StringComparison.Ordinal)
        || id.StartsWith("Member.", StringComparison.Ordinal);

    public static IReadOnlyList<string> FlatTargets() =>
        AllEntries
            .Where(e => e.Direction is Direction.Write or Direction.Both)
            .Where(e => e.Category is not Category.CompareMode and not Category.OutputAction)
            .Select(e => e.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> FlatCompareModes() =>
        AllEntries
            .Where(e => e.Category == Category.CompareMode)
            .Select(e => e.Id)
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> FlatOutputActions() =>
        AllEntries
            .Where(e => e.Category == Category.OutputAction)
            .Select(e => e.Id)
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> FlatMethods() =>
        AllMethods
            .Select(m => m.Id)
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static CatalogGroup[] BuildGroups(
        Direction primary,
        Direction secondary,
        Category excludeCategory,
        Category? excludeCategory2 = null,
        Category? excludeCategory3 = null,
        bool onlyWriteTargets = false)
    {
        var query = AllEntries.AsEnumerable()
            .Where(e => e.Direction == primary || e.Direction == secondary)
            .Where(e => e.Category != excludeCategory);

        if (excludeCategory2.HasValue)
        {
            query = query.Where(e => e.Category != excludeCategory2.Value);
        }

        if (excludeCategory3.HasValue)
        {
            query = query.Where(e => e.Category != excludeCategory3.Value);
        }

        if (onlyWriteTargets)
        {
            query = query.Where(e => e.Category is
                Category.HudFlightHud or Category.HudGauges or Category.CombatHud or
                Category.MfdHmd or Category.MapTactical or Category.Audio or Category.Camera or
                Category.PlayerSettings or Category.LogicGeneral or Category.Countermeasures);
        }
        else
        {
            query = query.Where(e => e.Category != Category.OutputAction);
        }

        return query
            .GroupBy(e => e.Category)
            .OrderBy(g => g.Key)
            .Select(g => new CatalogGroup(CategoryTitle(g.Key), g.Select(e => e.Id).ToArray()))
            .ToArray();
    }

    private static string CategoryTitle(Category cat) => cat switch
    {
        Category.FlightTelemetry => "Flight telemetry",
        Category.PilotInputs => "Pilot inputs",
        Category.Weapons => "Weapons & targets",
        Category.Countermeasures => "Countermeasures",
        Category.RadarEw => "Radar / EW",
        Category.MapTactical => "Map / tactical",
        Category.HudFlightHud => "FlightHud",
        Category.HudGauges => "HUD gauges (HUDApp)",
        Category.CombatHud => "Combat HUD",
        Category.MfdHmd => "MFD / HMD",
        Category.Audio => "Audio",
        Category.Camera => "Camera",
        Category.PlayerSettings => "PlayerSettings",
        Category.GameSession => "Session / gates",
        Category.OutputAction => "Actions",
        Category.CompareMode => "Compare",
        Category.LogicGeneral => "Logic / state",
        _ => cat.ToString()
    };

    private static IReadOnlyList<string> FallbackQuickValues(string id)
    {
        var p = id.ToUpperInvariant();
        if (p.Contains("AOA")) return Q("0", "8", "10", "12", "15", "18", "20", "25", "30");
        if (p.Contains("FUEL") || p.Contains("DAMAGE")) return Q("0.05", "0.1", "0.15", "0.2", "0.25", "0.5");
        if (p.Contains("SPEED") || p.Contains("VELOCITY")) return Q("0", "10", "50", "80", "100", "150");
        if (p.Contains("ALT") || p.Contains("AIRBORNE") || p.Contains("GROUND")) return Q("0", "0.2", "1", "5", "10", "50", "100", "500");
        if (p.Contains(".G") || p.EndsWith("G") || p.Contains("G-FORCE") || p.Contains("G-TEXT") || p.Contains("G-LABEL"))
            return Q("0", "1", "2", "3", "4", "5", "6", "7", "8", "9");
        if (p.Contains("THROTTLE") || p.Contains("RPM") || p.Contains("COLLECTIVE"))
            return Q("0", "0.25", "0.5", "0.75", "1");
        if (p.Contains("HEADING")) return Q("0", "90", "180", "270", "360");
        if (p.Contains("VERTICAL") || p.Contains("V/S")) return Q("-20", "-10", "0", "10", "20");
        if (p.Contains("TIME") || p.Contains("TIMER") || p.Contains("SECOND")) return Q("0.1", "0.5", "1", "2", "3", "5", "10", "30");
        if (p.Contains("COLOR") || p.Contains("WARN") || p.Contains("LABEL"))
            return Q("#FFFFFF", "#FF0000", "#FF4400", "#FFAA00", "#00FF88", "#44AAFF");
        return Q("0", "1", "5", "10", "15", "20", "25", "50", "100");
    }

    private static Entry E(
        string id, string title, string desc, Direction dir, Category cat, string valueType, string gamePath,
        string? def = null, string[]? quick = null) =>
        new(id, title, desc, dir, cat, valueType, gamePath, def, quick);

    private static Entry W(string id, string title, string desc, Category cat, string valueType, string gamePath,
        string? def = null, string[]? quick = null) =>
        new(id, title, desc, Direction.Write, cat, valueType, gamePath, def, quick);

    private static MethodEntry M(string id, string title, string desc, Category cat, string gamePath, string ret,
        string paramHints) =>
        new(id, title, desc, cat, gamePath, ret,
            paramHints.Contains('|', StringComparison.Ordinal)
                ? paramHints.Split('|', StringSplitOptions.TrimEntries)
                : paramHints.Split(',', StringSplitOptions.TrimEntries));

    private static string[] Q(params string[] v) => v;
}
