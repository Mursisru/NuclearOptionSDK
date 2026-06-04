using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public sealed class BridgeRunner : MonoBehaviour
{
    private void Update()
    {
        BridgeRuntime.Tick();
    }

    private void OnDestroy()
    {
        BridgeRuntime.Shutdown();
    }
}
