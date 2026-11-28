using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using OrbitalAnte.Items;

// Offline view of the envelope (no Unity). Writes every piece EnvelopeArt paints as a PNG, and the draw lists
// EnvelopeScene gives at chosen moments as JSON, for compose.py to put together into frames. See README.md.
//
//   dotnet run -- pieces <folder>
//   dotnet run -- show <folder> <runstart|reveal> <Rarity> <fps> <seconds> [itemId] [effectId]
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2) return Usage();
        string folder = args[1];
        Directory.CreateDirectory(folder);
        if (args[0] == "pieces") return Pieces(folder);
        if (args[0] == "show" && args.Length >= 6) return Show(folder, args);
        return Usage();
    }

    static int Usage()
    {
        Console.WriteLine("usage: pieces <folder> | show <folder> <runstart|reveal> <Rarity> <fps> <seconds> [itemId] [effectId]");
        return 1;
    }

    static int Pieces(string folder)
    {
        var timing = new StringBuilder();
        foreach (var piece in EnvelopeArt.Pieces())
        {
            var sw = Stopwatch.StartNew();
            ArtImage img = piece.Value();
            sw.Stop();
            Png.Write(Path.Combine(folder, piece.Key + ".png"), img);
            timing.AppendLine(piece.Key + ": " + img.Width + "x" + img.Height + " in " + sw.ElapsedMilliseconds + " ms");
        }
        File.WriteAllText(Path.Combine(folder, "timing.txt"), timing.ToString());
        Console.Write(timing);
        return 0;
    }

    static int Show(string folder, string[] args)
    {
        var mode = args[2] == "runstart" ? EnvelopeMode.RunStart : EnvelopeMode.Reveal;
        var rarity = (ItemRarity)Enum.Parse(typeof(ItemRarity), args[3]);
        float fps = float.Parse(args[4], CultureInfo.InvariantCulture);
        float seconds = float.Parse(args[5], CultureInfo.InvariantCulture);
        List<ItemSpec> catalog = ItemCatalog.CreateDefaults();
        List<ItemEffect> effects = ItemCatalog.CreateEffects();
        ItemSpec item = null;
        if (args.Length > 6) item = catalog.Find(i => i.id == args[6]);
        if (item == null) item = catalog.Find(i => i.rarity == (rarity == ItemRarity.Celestial ? ItemRarity.Legendary : rarity));
        ItemEffect effect = null;
        if (rarity == ItemRarity.Celestial) effect = args.Length > 7 ? effects.Find(e => e.id == args[7]) : effects[1];

        var show = new EnvelopeShow
        {
            mode = mode,
            serial = 42,
            antesCleared = 0,
            oddsLine = EnvelopeScene.OddsLine(new PrizeOdds()),
            prompt = mode == EnvelopeMode.RunStart ? "Press Enter, A or click to begin" : "Press Enter, A or click to continue",
            rarity = rarity,
            cardTexture = "Card",
            itemName = item.displayName,
            kindLine = ItemLook.KindLine(item, rarity),
            blurb = item.blurb,
            upLine = item.category == ItemCategory.Upgrade ? ItemLook.UpgradeUpLine(item) : "",
            downLine = item.category == ItemCategory.Upgrade ? ItemLook.UpgradeDownLine(item) : "",
            effectName = effect != null ? effect.displayName : "",
            effectColour = effect != null ? effect.colour : null,
            // As on a profile whose place for it is empty: a cosmetic goes to work by itself, an upgrade waits (Locker).
            useLine = Locker.RevealWords(item, item.category != ItemCategory.Upgrade),
            // As the game shows it: a prize that went to work by itself in the target-met green, and an effect saying
            // where it is seen (on your gun, on your lander, round your suit's readout).
            inUse = item.category != ItemCategory.Upgrade && Locker.Usable(item),
            effectNote = effect != null ? Locker.EffectWhere(item) : "",
            effectId = effect != null ? effect.id : "",
        };
        Png.Write(Path.Combine(folder, "Card.png"), EnvelopeArt.Card(item, rarity, effect != null ? effect.colour : null));

        var items = new List<DrawItem>();
        var json = new StringBuilder();
        json.Append("{\"frames\":[");
        int count = (int)Math.Round(seconds * fps);
        for (int f = 0; f < count; f++)
        {
            float t = f / fps;
            EnvelopeScene.Frame(show, t, items);
            if (f > 0) json.Append(',');
            json.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\"items\":[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) json.Append(',');
                Item(json, items[i]);
            }
            json.Append("]}");
        }
        json.Append("],\"beats\":");
        EnvelopeScene.Beats b = EnvelopeScene.BeatsFor(rarity);
        json.Append("{\"skip\":").Append(N(b.SkipTo)).Append(",\"done\":").Append(N(b.Done)).Append("}}");
        File.WriteAllText(Path.Combine(folder, "frames.json"), json.ToString());
        Console.WriteLine("wrote " + count + " frames; reveal done at " + b.Done.ToString("0.00", CultureInfo.InvariantCulture) + " s");
        return 0;
    }

    static void Item(StringBuilder s, DrawItem d)
    {
        s.Append("{\"key\":\"").Append(d.key).Append("\",\"kind\":").Append((int)d.kind);
        s.Append(",\"parent\":\"").Append(d.parent ?? "").Append("\",\"clip\":").Append(d.clip ? "true" : "false");
        s.Append(",\"tex\":\"").Append(d.texture ?? "").Append('"');
        s.Append(",\"text\":\"").Append(Escape(d.text)).Append('"');
        s.Append(",\"x\":").Append(N(d.x)).Append(",\"y\":").Append(N(d.y)).Append(",\"w\":").Append(N(d.w)).Append(",\"h\":").Append(N(d.h));
        s.Append(",\"px\":").Append(N(d.px)).Append(",\"py\":").Append(N(d.py)).Append(",\"sx\":").Append(N(d.sx)).Append(",\"sy\":").Append(N(d.sy));
        s.Append(",\"rot\":").Append(N(d.rotation));
        s.Append(",\"rgba\":[").Append(N(d.r)).Append(',').Append(N(d.g)).Append(',').Append(N(d.b)).Append(',').Append(N(d.a)).Append(']');
        s.Append(",\"size\":").Append(d.fontSize).Append(",\"bold\":").Append(d.bold ? "true" : "false").Append(",\"italic\":").Append(d.italic ? "true" : "false");
        s.Append(",\"align\":").Append((int)d.align).Append('}');
    }

    static string N(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString("0.####", CultureInfo.InvariantCulture);

    static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

/// <summary>A small PNG writer (8-bit RGBA), so the tool needs nothing but .NET.</summary>
static class Png
{
    public static void Write(string path, ArtImage img)
    {
        var raw = new byte[img.Height * (img.Width * 4 + 1)];
        int o = 0;
        for (int y = 0; y < img.Height; y++)
        {
            raw[o++] = 0;
            for (int x = 0; x < img.Width; x++)
            {
                int i = (y * img.Width + x) * 4;
                for (int c = 0; c < 4; c++) raw[o++] = (byte)Math.Round(Math.Clamp(img.Pixels[i + c], 0f, 1f) * 255f);
            }
        }
        byte[] packed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
            packed = ms.ToArray();
        }
        using (var fs = File.Create(path))
        {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BigEndian(ihdr, 0, img.Width);
            BigEndian(ihdr, 4, img.Height);
            ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(fs, "IHDR", ihdr);
            Chunk(fs, "IDAT", packed);
            Chunk(fs, "IEND", new byte[0]);
        }
    }

    static void BigEndian(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BigEndian(len, 0, data.Length);
        s.Write(len);
        byte[] t = Encoding.ASCII.GetBytes(type);
        s.Write(t);
        s.Write(data);
        uint crc = Crc(t, 0xFFFFFFFFu);
        crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4];
        BigEndian(c, 0, (int)crc);
        s.Write(c);
    }

    static uint[] _table;

    static uint Crc(byte[] data, uint crc)
    {
        if (_table == null)
        {
            _table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                _table[n] = c;
            }
        }
        for (int i = 0; i < data.Length; i++) crc = _table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
