// Draws the cockpit dash designs to JSON so they can be looked at without Unity.
//   dotnet run -- <out.json> [style]        then: python3 tools/dash_preview/draw.py <out.json> <out.png>
using System;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Cockpit;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: <out.json> [FlightDeck|Arcade|Glass|GreenScreen]"); return 2; }
        DashStyle style = args.Length > 1 ? (DashStyle)Enum.Parse(typeof(DashStyle), args[1]) : DashStyle.FlightDeck;
        var states = new[]
        {
            new DashInputs { Fuel = 1f, Oxygen = 1f, Altitude = 0f, Against = "EARTH", CameraName = "LANDING" },
            new DashInputs { Fuel = 0.74f, Oxygen = 0.93f, Speed = 86f, DriftY = 1f, DriftZ = 0.4f, ThrustY = 1f, Boost = true, Altitude = 412f, Vertical = 38f, Against = "EARTH", CameraName = "LANDING" },
            new DashInputs { Fuel = 0.46f, Oxygen = 0.71f, Speed = 12500f, DriftX = -0.3f, DriftY = 0.15f, DriftZ = 1f, ThrustX = 1f, Altitude = -1f, Against = "MARS", CameraName = "LEFT POLE", Matching = true },
            new DashInputs { Fuel = 0.12f, Oxygen = 0.16f, FuelLow = true, Speed = 310f, DriftX = 0.45f, DriftY = -0.6f, DriftZ = -0.6f, ThrustX = -1f, ThrustY = 0.6f, Altitude = 1840f, Vertical = -52f, SafeSink = 8f, Against = "MOON", CameraName = "RIGHT POLE" },
        };
        var json = new StringBuilder("[");
        var d = new DashDrawing();
        for (int s = 0; s < states.Length; s++)
        {
            var model = new DashModel();
            for (int i = 0; i < 120; i++) model.Tick(1f / 60f, states[s]);
            model.Readings.Clock = 0.2f;
            DashDesigns.Paint(style, model.Readings, d);
            if (s > 0) json.Append(',');
            json.Append("{\"v\":[");
            for (int i = 0; i < d.VertexCount; i++)
            {
                if (i > 0) json.Append(',');
                DashColor c = d.Colors[i];
                json.Append(string.Format(CultureInfo.InvariantCulture, "[{0:F2},{1:F2},{2:F3},{3:F3},{4:F3},{5:F3}]", d.X[i], d.Y[i], c.R, c.G, c.B, c.A));
            }
            json.Append("],\"t\":[");
            for (int i = 0; i < d.Texts.Count; i++)
            {
                if (i > 0) json.Append(',');
                DashText t = d.Texts[i];
                json.Append(string.Format(CultureInfo.InvariantCulture, "{{\"x\":{0:F1},\"y\":{1:F1},\"s\":{2:F1},\"a\":{3},\"b\":{4},\"c\":[{5:F3},{6:F3},{7:F3},{8:F3}],\"w\":\"{9}\"}}",
                    t.X, t.Y, t.Size, (int)t.Align, t.Bold ? 1 : 0, t.Color.R, t.Color.G, t.Color.B, t.Color.A, (t.Text ?? "").Replace("\"", "'")));
            }
            json.Append(string.Format(CultureInfo.InvariantCulture, "],\"cam\":[{0},{1},{2},{3}]}}", d.CameraX, d.CameraY, d.CameraWidth, d.CameraHeight));
        }
        json.Append(']');
        File.WriteAllText(args[0], json.ToString());
        return 0;
    }
}
