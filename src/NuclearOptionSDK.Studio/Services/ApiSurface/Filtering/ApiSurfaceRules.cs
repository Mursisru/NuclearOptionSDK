namespace NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;

public sealed class ApiSurfaceRules
{
    public bool HideSystemNoise { get; init; } = true;
    public bool HideUnityLifecycle { get; init; } = true;
    public bool HideCompilerGenerated { get; init; } = true;
    public string[] TypePriorityBoost { get; init; } = ["Aircraft", "Unit", "WeaponStation", "CombatHUD", "FlightHud"];
    public string[] LifecycleMethodNames { get; init; } =
        ["Update", "LateUpdate", "FixedUpdate", "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy"];
}
