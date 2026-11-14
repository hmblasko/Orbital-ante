// Bakes the lander cabin's light and writes the files the game ships with.
//
//   dotnet run -c Release -- <repo> [--paths 768] [--dump <folder>] [--no-write]
//
// Writes  unity/Assets/OrbitalAnte/Resources/Ship/LanderBake.bytes    (the cabin's baked light)
//         unity/Assets/OrbitalAnte/Resources/Models/LanderCabin.bytes (the dash housing, lit by the same lamps)
// --dump also writes every piece with its baked colours for tools/blender/preview_cabin.py.
// See README.md beside this file for the project file it needs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OrbitalAnte.Look;
using OrbitalAnte.Ship;

static class CabinBake
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: <repo> [--paths n] [--dump folder] [--no-write]"); return 2; }
        string repo = args[0];
        int paths = 768;
        string dump = null;
        bool write = true;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--paths") paths = int.Parse(args[++i], CultureInfo.InvariantCulture);
            else if (args[i] == "--dump") dump = args[++i];
            else if (args[i] == "--no-write") write = false;
        }
        string resources = Path.Combine(repo, "unity", "Assets", "OrbitalAnte", "Resources");

        List<ShipPiece> pieces = ShipAssembly.InteriorPieces();
        var bake = new LightBake();
        foreach (ShipPiece piece in pieces) bake.Add(Moved(piece.Mesh, piece.Offset));

        // The dash housing is a Blender model, not a cabin piece, but it stands in the cabin's light.
        ModelFile lander = ModelFile.Parse(File.ReadAllBytes(Path.Combine(resources, "Models", "Lander.bytes")), out string problem);
        if (lander == null) { Console.Error.WriteLine("Lander.bytes: " + problem); return 1; }
        ModelPart housing = lander.Part(ShipAssembly.DashHousingPiece);
        ShipMesh housingMesh = null;
        if (housing != null)
        {
            housingMesh = new ShipMesh
            {
                Positions = housing.Positions,
                Colors = housing.Colors,
                Glow = new float[housing.VertexCount],
                Triangles = FacingItsNormals(housing),
            };
            bake.Add(housingMesh);
        }

        var settings = new LightBake.Settings { Paths = paths, LampChecks = Math.Max(24, paths / 6) };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        List<float[]> light = bake.Bake(settings);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "baked {0} triangles with {1} paths in {2:F1} s, exposure {3:F3}",
            bake.TriangleCount, paths, clock.Elapsed.TotalSeconds, bake.Exposure));

        var cabinLight = light.GetRange(0, pieces.Count);
        if (write)
        {
            byte[] packed = ShipAssembly.Pack(pieces, cabinLight);
            File.WriteAllBytes(Path.Combine(resources, "Ship", "LanderBake.bytes"), packed);
            Console.WriteLine("LanderBake.bytes " + packed.Length + " bytes, fingerprint " + ShipAssembly.Fingerprint(pieces));
            if (housing != null)
            {
                float[] lit = light[pieces.Count];
                var colours = new float[lit.Length];
                for (int i = 0; i < lit.Length; i++) colours[i] = LightBake.ToScreen(lit[i]);
                var file = new ModelFile
                {
                    Parts = new[] { new ModelPart { Name = housing.Name, Positions = housing.Positions, Normals = housing.Normals, Colors = colours, Triangles = housing.Triangles } },
                    Sockets = lander.TrySocket(ShipAssembly.DashSocket, out ModelSocket dash) ? new[] { dash } : new ModelSocket[0],
                };
                byte[] bytes = file.ToBytes();
                File.WriteAllBytes(Path.Combine(resources, "Models", ShipAssembly.BakedFittingsModel + ".bytes"), bytes);
                Console.WriteLine(ShipAssembly.BakedFittingsModel + ".bytes " + bytes.Length + " bytes");
            }
        }

        if (dump != null)
        {
            Directory.CreateDirectory(dump);
            using (var w = new BinaryWriter(File.Create(Path.Combine(dump, "cabin.bin"))))
            {
                w.Write(light.Count);
                for (int m = 0; m < light.Count; m++)
                {
                    ShipMesh mesh = m < pieces.Count ? Moved(pieces[m].Mesh, pieces[m].Offset) : housingMesh;
                    w.Write(mesh.VertexCount);
                    w.Write(mesh.TriangleCount);
                    foreach (float v in mesh.Positions) w.Write(v);
                    foreach (float v in light[m]) w.Write(v);          // linear light
                    foreach (int t in mesh.Triangles) w.Write(t);
                }
            }
            int triangles = 0;
            foreach (ShipPiece piece in pieces) { triangles += piece.Mesh.TriangleCount; Console.WriteLine("  " + piece.Name + ": " + piece.Mesh.TriangleCount + " triangles"); }
            Console.WriteLine("cabin: " + triangles + " triangles in " + pieces.Count + " pieces");
        }
        return 0;
    }

    static ShipMesh Moved(ShipMesh mesh, P3 offset)
    {
        if (offset.X == 0f && offset.Y == 0f && offset.Z == 0f) return mesh;
        var copy = new ShipMesh { Positions = (float[])mesh.Positions.Clone(), Colors = mesh.Colors, Glow = mesh.Glow, Triangles = mesh.Triangles };
        for (int i = 0; i < copy.Positions.Length; i += 3) { copy.Positions[i] += offset.X; copy.Positions[i + 1] += offset.Y; copy.Positions[i + 2] += offset.Z; }
        return copy;
    }

    /// <summary>The model's triangles, each wound so the baker sees it facing the way its stored normals say.</summary>
    static int[] FacingItsNormals(ModelPart part)
    {
        var triangles = (int[])part.Triangles.Clone();
        for (int k = 0; k < triangles.Length; k += 3)
        {
            int a = triangles[k], b = triangles[k + 1], c = triangles[k + 2];
            P3 pa = At(part.Positions, a), pb = At(part.Positions, b), pc = At(part.Positions, c);
            P3 face = P3.Cross(pb - pa, pc - pa);
            P3 stored = At(part.Normals, a) + At(part.Normals, b) + At(part.Normals, c);
            if (P3.Dot(face, stored) < 0f) { triangles[k + 1] = c; triangles[k + 2] = b; }
        }
        return triangles;
    }

    static P3 At(float[] values, int i) => new P3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
}
