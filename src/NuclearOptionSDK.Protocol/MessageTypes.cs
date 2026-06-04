namespace NuclearOptionSDK.Protocol;

public static class MessageTypes
{
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Error = "error";
    public const string Log = "log";

    public const string SceneGetRoots = "scene.getRoots";
    public const string SceneTree = "scene.tree";
    public const string SceneResolve = "scene.resolve";
    public const string SceneResolved = "scene.resolved";

    public const string ExecuteCode = "execute_code";
    public const string ExecuteResult = "execute_result";

    public const string HudGetTree = "hud.getTree";
    public const string HudTree = "hud.tree";
    public const string HudUpdate = "hud.update";
    public const string HudUpdated = "hud.updated";

    public const string OverlaySetEnabled = "overlay.setEnabled";
    public const string OverlayDraw = "overlay.draw";
    public const string OverlayLayout = "overlay.layout";

    public const string AudioEvent = "audio.event";

    public const string HarmonyGenerate = "harmony.generate";
    public const string HarmonyGenerated = "harmony.generated";

    public const string ModBuild = "mod.build";
    public const string ModBuilt = "mod.built";

    public const string LogicSet = "logic.set";
    public const string LogicStatus = "logic.status";
    public const string LogicPreview = "logic.preview";
    public const string BindingList = "binding.list";
    public const string BindingResolve = "binding.resolve";
    public const string BindingResolved = "binding.resolved";
    public const string BindingWatch = "binding.watch";
    public const string ReferenceList = "reference.list";
    public const string TraceStart = "trace.start";
    public const string TraceStop = "trace.stop";
    public const string TraceEvents = "trace.events";
    public const string DependencyRadar = "dependency.radar";
    public const string DependencyRadarResult = "dependency.radar.result";
}
