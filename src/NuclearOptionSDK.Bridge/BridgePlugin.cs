using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class BridgePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.at747.nuclearoptionsdk.bridge";
    public const string PluginName = "Nuclear SDK Bridge";
    public const string PluginVersion = AppVersion.ReleaseBase;

    private WebSocketHost? _webSocketHost;

    private void Awake()
    {
        try
        {
            AwakeCore();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Bridge failed to start: {ex}");
        }
    }

    private void AwakeCore()
    {
        var enabled = Config.Bind("General", "Enabled", true, "Enable Nuclear SDK Bridge.");
        var listenPort = Config.Bind("Network", "ListenPort", 9005, "WebSocket listen port on localhost.");
        var alwaysOn = Config.Bind("General", "AlwaysOn", true, "Keep bridge active even without Studio connected.");

        if (!enabled.Value)
        {
            Logger.LogInfo($"{PluginName} disabled in config.");
            return;
        }

        var dispatcher = new MainThreadDispatcher();
        var scriptHost = new RoslynScriptHost();
        scriptHost.Configure(Logger, Paths.GameRootPath);

        _webSocketHost = new WebSocketHost();
        var router = new MessageRouter(dispatcher, scriptHost, _webSocketHost);
        _webSocketHost.Bind(router);

        var hostObject = new GameObject("NuclearOptionSDK_Bridge");
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        hostObject.hideFlags = HideFlags.HideAndDontSave;
        hostObject.AddComponent<BridgeRunner>();
        var overlay = hostObject.AddComponent<HudOverlayInjector>();

        BridgeRuntime.Initialize(Logger, Paths.GameRootPath, dispatcher, _webSocketHost, overlay);
        BridgeFileLogger.Initialize(Paths.GameRootPath);
        _webSocketHost.Start(listenPort.Value);

        var harmony = new Harmony(PluginGuid);
        MethodHunterService.EnsureInitialized(MethodHunterService.DefaultHarmonyId);
        var audioPlayMethod = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Play), Type.EmptyTypes);
        var audioPostfix = AccessTools.Method(typeof(AudioSourcePlayPatch), nameof(AudioSourcePlayPatch.Postfix));
        if (audioPlayMethod != null && audioPostfix != null)
        {
            harmony.Patch(audioPlayMethod, postfix: new HarmonyMethod(audioPostfix));
        }

        Logger.LogInfo($"{PluginName} {AppVersion.Display} loaded. ws://127.0.0.1:{listenPort.Value} AlwaysOn={alwaysOn.Value}");
    }

    private void OnDestroy()
    {
        BridgeRuntime.Shutdown();
        _webSocketHost?.Dispose();
    }
}
