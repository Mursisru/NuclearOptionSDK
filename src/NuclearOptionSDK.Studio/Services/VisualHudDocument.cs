using NuclearOptionSDK.Protocol;



namespace NuclearOptionSDK.Studio.Services;



public sealed class VisualHudLabelItem

{

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string Text { get; set; } = "New label";

    public double X { get; set; } = 40;

    public double Y { get; set; } = 40;

    public double FontSize { get; set; } = 20;

    public string ColorHtml { get; set; } = "#FFFFFF";

    public bool Visible { get; set; } = true;

}



public sealed class VisualHudPrimitiveItem

{

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string Kind { get; set; } = "line";

    public double X1 { get; set; } = 80;

    public double Y1 { get; set; } = 80;

    public double X2 { get; set; } = 240;

    public double Y2 { get; set; } = 160;

    public double Radius { get; set; } = 48;

    public string ColorHtml { get; set; } = "#FF4444";

}



public sealed class VisualHudSelection

{

    public string Kind { get; init; } = "none";

    public string Id { get; init; } = string.Empty;

    public string? Text { get; set; }

    public string ColorHtml { get; set; } = "#FFFFFF";

    public double FontSize { get; set; } = 20;

    public double X { get; set; }

    public double Y { get; set; }

    public double X2 { get; set; }

    public double Y2 { get; set; }

    public double Radius { get; set; }

    public bool Visible { get; set; } = true;

}



public static class VisualHudDocument

{

    public const double CanvasWidth = 960;

    public const double CanvasHeight = 540;



    public static VisualHudLayoutPayload ToPayload(

        string name,

        IEnumerable<VisualHudLabelItem> labels,

        IEnumerable<VisualHudPrimitiveItem>? primitives = null)

    {

        return new VisualHudLayoutPayload

        {

            name = name,

            labels = labels.Select(l => new OverlayLabel

            {

                id = l.Id,

                text = l.Text,

                x = (float)l.X,

                y = (float)l.Y,

                fontSize = (float)l.FontSize,

                colorHtml = l.ColorHtml,

                visible = l.Visible

            }).ToArray(),

            primitives = (primitives ?? Array.Empty<VisualHudPrimitiveItem>()).Select(p => new OverlayPrimitive

            {

                kind = p.Kind,

                x1 = (float)p.X1,

                y1 = (float)p.Y1,

                x2 = (float)p.X2,

                y2 = (float)p.Y2,

                radius = (float)p.Radius,

                colorHtml = p.ColorHtml

            }).ToArray()

        };

    }



    public static void Save(

        string path,

        string name,

        IEnumerable<VisualHudLabelItem> labels,

        IEnumerable<VisualHudPrimitiveItem>? primitives = null)

    {

        var json = System.Text.Json.JsonSerializer.Serialize(

            ToPayload(name, labels, primitives),

            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, json);

    }



    public static (List<VisualHudLabelItem> labels, List<VisualHudPrimitiveItem> primitives) Load(string path)

    {

        var json = File.ReadAllText(path);

        var payload = System.Text.Json.JsonSerializer.Deserialize<VisualHudLayoutPayload>(json);

        if (payload == null)

        {

            return (new List<VisualHudLabelItem>(), new List<VisualHudPrimitiveItem>());

        }



        var labels = payload.labels?.Select(l => new VisualHudLabelItem

        {

            Id = l.id,

            Text = l.text,

            X = l.x,

            Y = l.y,

            FontSize = l.fontSize,

            ColorHtml = l.colorHtml,

            Visible = l.visible

        }).ToList() ?? new List<VisualHudLabelItem>();



        var primitives = payload.primitives?.Select(p => new VisualHudPrimitiveItem

        {

            Id = Guid.NewGuid().ToString("N")[..8],

            Kind = p.kind,

            X1 = p.x1,

            Y1 = p.y1,

            X2 = p.x2,

            Y2 = p.y2,

            Radius = p.radius,

            ColorHtml = p.colorHtml

        }).ToList() ?? new List<VisualHudPrimitiveItem>();



        return (labels, primitives);

    }

}

