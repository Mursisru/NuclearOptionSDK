namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Extension point for upcoming Studio innovations. Modules register at startup;
/// future features plug in here without reshaping MainWindow.
/// </summary>
public interface IInnovationModule
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    bool IsEnabled { get; }
}

public static class InnovationRegistry
{
    private static readonly List<IInnovationModule> Modules = new();

    public static IReadOnlyList<IInnovationModule> All => Modules;

    public static void Register(IInnovationModule module)
    {
        if (Modules.Any(m => m.Id == module.Id))
        {
            return;
        }

        Modules.Add(module);
    }

    public static void RegisterDefaults()
    {
        Register(new PlaceholderInnovation(
            "logic-graph-v2",
            "Logic Graph v2",
            "Upcoming: nested subgraphs, live type inference, collaborative edit."));

        Register(new PlaceholderInnovation(
            "runtime-sandbox",
            "Runtime Sandbox",
            "Upcoming: step-through logic evaluation in-game with breakpoints."));
    }

    private sealed class PlaceholderInnovation : IInnovationModule
    {
        public PlaceholderInnovation(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public bool IsEnabled => false;
    }
}
