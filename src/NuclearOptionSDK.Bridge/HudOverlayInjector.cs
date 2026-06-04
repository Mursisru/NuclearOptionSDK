using System.Linq;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public sealed class HudOverlayInjector : MonoBehaviour
{
    private bool _enabled;
    private readonly List<OverlayPrimitive> _primitives = new();
    private readonly List<OverlayLabel> _labels = new();
    private GUIStyle? _labelStyle;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        BridgeFileLogger.Info("overlay", $"enabled={enabled}");
    }

    public void SetDraw(OverlayDrawRequest request)
    {
        _primitives.Clear();
        _labels.Clear();

        if (request.clear)
        {
            BridgeFileLogger.Info("overlay.draw", "clear");
            return;
        }

        if (request.primitives != null)
        {
            _primitives.AddRange(request.primitives);
        }

        if (request.labels != null)
        {
            _labels.AddRange(request.labels);
        }

        BridgeFileLogger.Info("overlay.draw", $"primitives={_primitives.Count} labels={_labels.Count}");
    }

    public void SetLayout(VisualHudLayoutPayload layout)
    {
        _primitives.Clear();
        _labels.Clear();

        if (layout.primitives != null)
        {
            _primitives.AddRange(layout.primitives);
        }

        if (layout.labels != null)
        {
            _labels.AddRange(layout.labels);
        }

        BridgeFileLogger.Info("overlay.layout", $"name={layout.name} labels={_labels.Count}");
    }

    public void ApplyLogicAction(LogicActionResult action)
    {
        if (string.IsNullOrEmpty(action.labelId))
        {
            return;
        }

        var labelId = action.labelId!;
        var label = _labels.FirstOrDefault(l => l.id == labelId);
        if (label == null)
        {
            label = new OverlayLabel { id = labelId, text = labelId, x = 20, y = 20 };
            _labels.Add(label);
        }

        if (action.colorHtml != null)
        {
            label.colorHtml = action.colorHtml;
        }

        if (action.visible != null)
        {
            label.visible = action.visible.Value;
        }
    }

    private void OnGUI()
    {
        if (!_enabled)
        {
            return;
        }

        foreach (var label in _labels)
        {
            if (!label.visible || string.IsNullOrEmpty(label.text))
            {
                continue;
            }

            DrawLabel(label);
        }

        foreach (var primitive in _primitives)
        {
            if (!ColorUtility.TryParseHtmlString(primitive.colorHtml, out var color))
            {
                color = Color.red;
            }

            DrawPrimitive(primitive, color);
        }
    }

    private void DrawLabel(OverlayLabel label)
    {
        if (!ColorUtility.TryParseHtmlString(label.colorHtml, out var color))
        {
            color = Color.white;
        }

        _labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(label.fontSize)
        };

        _labelStyle.fontSize = Mathf.RoundToInt(label.fontSize);
        _labelStyle.normal.textColor = color;
        GUI.Label(new Rect(label.x, label.y, 800f, 40f), label.text, _labelStyle);
    }

    private static void DrawPrimitive(OverlayPrimitive primitive, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;

        switch (primitive.kind)
        {
            case "circle":
                DrawCircle(primitive.x1, primitive.y1, primitive.radius);
                break;
            default:
                DrawLine(primitive.x1, primitive.y1, primitive.x2, primitive.y2);
                break;
        }

        GUI.color = prev;
    }

    private static void DrawLine(float x1, float y1, float x2, float y2)
    {
        var texture = Texture2D.whiteTexture;
        var width = 2f;
        var angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
        var length = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
        var rect = new Rect(x1, y1, length, width);
        GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
        GUI.DrawTexture(rect, texture);
        GUIUtility.RotateAroundPivot(-angle, new Vector2(x1, y1));
    }

    private static void DrawCircle(float cx, float cy, float radius)
    {
        const int segments = 32;
        var prevX = cx + radius;
        var prevY = cy;
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * Mathf.PI * 2f;
            var x = cx + Mathf.Cos(angle) * radius;
            var y = cy + Mathf.Sin(angle) * radius;
            DrawLine(prevX, prevY, x, y);
            prevX = x;
            prevY = y;
        }
    }
}
