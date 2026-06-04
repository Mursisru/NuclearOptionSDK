using System.Collections.Generic;
using NuclearOptionSDK.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearOptionSDK.Bridge;

public static class SceneScanner
{
    public static SceneTreePayload CaptureActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var payload = new SceneTreePayload { sceneName = scene.name };

        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root == null)
            {
                continue;
            }

            payload.roots.Add(BuildNode(root.transform));
        }

        return payload;
    }

    private static GameObjectNode BuildNode(Transform transform)
    {
        var go = transform.gameObject;
        var node = new GameObjectNode
        {
            name = go.name,
            id = go.GetInstanceID(),
            active = go.activeSelf,
            components = GetComponentNames(go)
        };

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child != null)
            {
                node.children.Add(BuildNode(child));
            }
        }

        return node;
    }

    private static string[] GetComponentNames(GameObject go)
    {
        var components = go.GetComponents<Component>();
        var names = new List<string>(components.Length);
        foreach (var component in components)
        {
            if (component == null)
            {
                names.Add("<missing>");
                continue;
            }

            names.Add(component.GetType().FullName ?? component.GetType().Name);
        }

        return names.ToArray();
    }

    public static SceneResolveResponse Resolve(int instanceId)
    {
        var all = Object.FindObjectsOfType<GameObject>();
        foreach (var go in all)
        {
            if (go == null || go.GetInstanceID() != instanceId)
            {
                continue;
            }

            return new SceneResolveResponse
            {
                instanceId = instanceId,
                name = go.name,
                components = GetComponentNames(go)
            };
        }

        return new SceneResolveResponse
        {
            instanceId = instanceId,
            name = "<not found>",
            components = System.Array.Empty<string>()
        };
    }
}
