namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Флаги сборки Studio. UI-трейс включается только при define STUDIO_UI_TRACE (Debug / smoke).
/// Релизный publish НЕ должен передавать -p:StudioUiTrace=true — см. tools/publish-release.ps1.
/// </summary>
public static class StudioFeatures
{
#if STUDIO_UI_TRACE
    public const bool UiInteractionTraceCompiled = true;
#else
    public const bool UiInteractionTraceCompiled = false;
#endif
}
