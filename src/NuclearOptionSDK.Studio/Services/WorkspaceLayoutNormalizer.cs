using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Нормализация сохранённых пропорций (старые layout.json часто давали 80/20 и огромный Project).</summary>
public static class WorkspaceLayoutNormalizer
{
    public const double DefaultConstructorSplit = 0.5;
    public const double DefaultWorkspaceTopWeight = 5;
    public const double DefaultWorkspaceBottomWeight = 2;
    public const double MaxProjectRowShare = 0.32;

    public const double MinConstructorSplit = 0.35;
    public const double MaxConstructorSplit = 0.65;

    public static double NormalizeConstructorSplit(double ratio)
    {
        // Старые layout часто 0.85–0.9 — визуально «застрявший» верх (~90/10).
        if (double.IsNaN(ratio) || ratio < MinConstructorSplit || ratio > MaxConstructorSplit)
        {
            return DefaultConstructorSplit;
        }

        return ratio;
    }

    public static bool IsLegacyConstructorSplit(double ratio) =>
        !double.IsNaN(ratio) && (ratio < MinConstructorSplit || ratio > MaxConstructorSplit);

    public static void NormalizeWorkspaceRows(ref double topWeight, ref double bottomWeight)
    {
        if (topWeight <= 0)
        {
            topWeight = DefaultWorkspaceTopWeight;
        }

        if (bottomWeight <= 0)
        {
            bottomWeight = DefaultWorkspaceBottomWeight;
        }

        var sum = topWeight + bottomWeight;
        if (sum <= 0)
        {
            topWeight = DefaultWorkspaceTopWeight;
            bottomWeight = DefaultWorkspaceBottomWeight;
            return;
        }

        var bottomShare = bottomWeight / sum;
        if (bottomShare > MaxProjectRowShare)
        {
            bottomWeight = topWeight * (MaxProjectRowShare / (1 - MaxProjectRowShare));
        }
    }

    public static LogicUILayout Normalize(LogicUILayout layout)
    {
        layout.splitRatio = NormalizeConstructorSplit(layout.splitRatio);
        var top = layout.workspaceTopRowWeight;
        var bottom = layout.workspaceBottomRowWeight;
        NormalizeWorkspaceRows(ref top, ref bottom);
        layout.workspaceTopRowWeight = top;
        layout.workspaceBottomRowWeight = bottom;
        return layout;
    }
}
