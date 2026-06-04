using System.Collections.Generic;
using NuclearOptionSDK.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearOptionSDK.Bridge;

public static class FlightHudService
{
    public static HudTreePayload CaptureHudTree()
    {
        var flightHud = Object.FindObjectOfType<FlightHud>();
        if (flightHud == null)
        {
            return new HudTreePayload { found = false };
        }

        var root = new HudElementNode
        {
            path = flightHud.name,
            instanceId = flightHud.gameObject.GetInstanceID(),
            type = flightHud.GetType().FullName ?? "FlightHud",
            active = flightHud.gameObject.activeSelf
        };

        BuildUiTree(flightHud.transform, root.path, root.children);
        return new HudTreePayload
        {
            found = true,
            elements = new List<HudElementNode> { root }
        };
    }

    private static void BuildUiTree(Transform parent, string path, List<HudElementNode> output)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            var childPath = $"{path}/{child.name}";
            var node = new HudElementNode
            {
                path = childPath,
                instanceId = child.gameObject.GetInstanceID(),
                type = GetUiType(child.gameObject),
                active = child.gameObject.activeSelf,
                text = ReadText(child.gameObject)
            };

            output.Add(node);
            BuildUiTree(child, childPath, node.children);
        }
    }

    private static string GetUiType(GameObject go)
    {
        if (go.GetComponent<TextMeshProUGUI>() != null)
        {
            return "TextMeshProUGUI";
        }

        if (go.GetComponent<Text>() != null)
        {
            return "UnityEngine.UI.Text";
        }

        if (go.GetComponent<Graphic>() != null)
        {
            return go.GetComponent<Graphic>()!.GetType().FullName ?? "Graphic";
        }

        return "GameObject";
    }

    private static string ReadText(GameObject go)
    {
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            return tmp.text ?? string.Empty;
        }

        var text = go.GetComponent<Text>();
        return text != null ? text.text ?? string.Empty : string.Empty;
    }

    public static HudUpdateResponse ApplyUpdate(HudUpdateRequest request)
    {
        var target = FindByInstanceId(request.instanceId);
        if (target == null)
        {
            return new HudUpdateResponse { success = false, error = "HUD element not found." };
        }

        try
        {
            if (request.active.HasValue)
            {
                target.SetActive(request.active.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.colorHtml))
            {
                if (ColorUtility.TryParseHtmlString(request.colorHtml, out var color))
                {
                    var tmp = target.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.color = color;
                    }

                    var text = target.GetComponent<Text>();
                    if (text != null)
                    {
                        text.color = color;
                    }

                    var graphic = target.GetComponent<Graphic>();
                    if (graphic != null)
                    {
                        graphic.color = color;
                    }
                }
            }

            if (request.fontSize.HasValue)
            {
                var tmp = target.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = request.fontSize.Value;
                }

                var text = target.GetComponent<Text>();
                if (text != null)
                {
                    text.fontSize = Mathf.RoundToInt(request.fontSize.Value);
                }
            }

            if (request.text != null)
            {
                var tmp = target.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = request.text;
                }

                var text = target.GetComponent<Text>();
                if (text != null)
                {
                    text.text = request.text;
                }
            }

            return new HudUpdateResponse { success = true };
        }
        catch (System.Exception ex)
        {
            return new HudUpdateResponse { success = false, error = ex.Message };
        }
    }

    private static GameObject? FindByInstanceId(int instanceId)
    {
        var all = Object.FindObjectsOfType<GameObject>();
        foreach (var go in all)
        {
            if (go != null && go.GetInstanceID() == instanceId)
            {
                return go;
            }
        }

        return null;
    }
}
