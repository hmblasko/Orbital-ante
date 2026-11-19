using System;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Audio;

/// <summary>
/// usage: dotnet run --project tools/audio/sim -- path/to/audio_manifest.json out/schedule.json
/// Writes the showcase's schedule: music commands, one-shot sounds, loop moves, captions and the intensity trace.
/// </summary>
static class Program
{
    static string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: sim <audio_manifest.json> <schedule.json>");
            return 2;
        }
        AudioManifest m = AudioManifest.Parse(File.ReadAllText(args[0]));
        var sim = new DemoSimulator();
        sim.Run(m, DemoTimeline.Build(), 0.02, 0.0);

        var sb = new StringBuilder();
        sb.Append("{\n \"length\": ").Append(N(DemoTimeline.Length)).Append(",\n \"music\": [\n");
        for (int i = 0; i < sim.Music.Count; i++)
        {
            MusicCommand c = sim.Music[i];
            sb.Append("  {\"kind\": ").Append(Q(c.Kind.ToString())).Append(", \"track\": ").Append(Q(c.Track)).Append(", \"stem\": ").Append(Q(c.Stem))
              .Append(", \"cue\": ").Append(Q(c.Cue)).Append(", \"time\": ").Append(N(c.Time)).Append(", \"seconds\": ").Append(N(c.Seconds))
              .Append(", \"gain\": ").Append(N(c.Gain)).Append(", \"offset\": ").Append(N(c.Offset)).Append(i + 1 < sim.Music.Count ? "},\n" : "}\n");
        }
        sb.Append(" ],\n \"sounds\": [\n");
        for (int i = 0; i < sim.Sounds.Count; i++)
        {
            DemoSimulator.Sound s = sim.Sounds[i];
            sb.Append("  {\"time\": ").Append(N(s.Time)).Append(", \"event\": ").Append(Q(s.Event)).Append(", \"gainDb\": ").Append(N(s.GainDb))
              .Append(", \"cutoff\": ").Append(N(s.Cutoff)).Append(", \"pitch\": ").Append(N(s.Pitch)).Append(", \"side\": ").Append(N(s.Side))
              .Append(", \"metres\": ").Append(N(s.Metres)).Append(", \"reverb\": ").Append(N(s.Reverb)).Append(i + 1 < sim.Sounds.Count ? "},\n" : "}\n");
        }
        sb.Append(" ],\n \"loops\": [\n");
        for (int i = 0; i < sim.Loops.Count; i++)
        {
            DemoSimulator.LoopMove l = sim.Loops[i];
            sb.Append("  {\"time\": ").Append(N(l.Time)).Append(", \"event\": ").Append(Q(l.Event)).Append(", \"level\": ").Append(N(l.Level))
              .Append(", \"seconds\": ").Append(N(l.Seconds)).Append(", \"gainDb\": ").Append(N(l.GainDb)).Append(", \"cutoff\": ").Append(N(l.Cutoff))
              .Append(i + 1 < sim.Loops.Count ? "},\n" : "}\n");
        }
        sb.Append(" ],\n \"ducks\": [");
        for (int i = 0; i < sim.Ducks.Count; i++) sb.Append(i > 0 ? ", " : "").Append("[").Append(N(sim.Ducks[i].Key)).Append(", ").Append(sim.Ducks[i].Value ? "1" : "0").Append("]");
        sb.Append("],\n \"captions\": [\n");
        for (int i = 0; i < sim.Captions.Count; i++)
            sb.Append("  [").Append(N(sim.Captions[i].Key)).Append(", ").Append(Q(sim.Captions[i].Value)).Append(i + 1 < sim.Captions.Count ? "],\n" : "]\n");
        sb.Append(" ],\n \"trace\": [\n");
        for (int i = 0; i < sim.Traces.Count; i++)
        {
            DemoSimulator.Trace t = sim.Traces[i];
            sb.Append("  [").Append(N(t.Time)).Append(", ").Append(N(t.Intensity)).Append(", ").Append(Q(t.Band.ToString())).Append(", ").Append(Q(t.Track))
              .Append(i + 1 < sim.Traces.Count ? "],\n" : "]\n");
        }
        sb.Append(" ]\n}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
        File.WriteAllText(args[1], sb.ToString());
        Console.WriteLine(sim.Music.Count + " music commands, " + sim.Sounds.Count + " sounds, " + sim.Loops.Count + " loop moves; " + sim.Refused + " sounds turned away by the voice rules");
        return 0;
    }
}
