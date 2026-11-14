using System;
using System.Collections.Generic;
using System.IO;

namespace OrbitalAnte.Ship
{
    /// <summary>One mesh of the cabin's inside, with where it sits while its light is being baked.</summary>
    public sealed class ShipPiece
    {
        public string Name;
        public ShipMesh Mesh;
        /// <summary>Added to the mesh's points for baking (for pieces built at the origin and placed by the game).</summary>
        public P3 Offset;
    }

    /// <summary>
    /// Puts the inside of the cabin together in a fixed order, bakes its light, and packs the baked colours
    /// into a small file that ships with the game, so nothing has to be worked out while playing.
    /// No Unity types.
    /// </summary>
    public static class ShipAssembly
    {
        public const string BakeResource = "Ship/LanderBake";
        const int Magic = 0x4C424B31; // "LBK1"

        /// <summary>
        /// A model holding fittings that stand inside the cabin, with the cabin's light already in their colours
        /// (made by tools/bake). Today that is the body of the cockpit console, copied from the Console model.
        /// </summary>
        public const string BakedFittingsModel = "LanderCabin";
        /// <summary>The model the baked fittings are copied from, and the one piece of it that takes baked light.</summary>
        public const string FittingsSource = "Console", FittingsPiece = "console";

        /// <summary>Every baked piece of the cabin. The order is part of the baked file's format: add new pieces at the end.</summary>
        public static List<ShipPiece> InteriorPieces()
        {
            var pieces = new List<ShipPiece>
            {
                new ShipPiece { Name = "Shell", Mesh = ShipShell.Interior() },
                new ShipPiece { Name = "Floor", Mesh = ShipProps.Floor() },
                new ShipPiece { Name = "Seat", Mesh = ShipProps.Seat() },
                new ShipPiece { Name = "Desk", Mesh = ShipProps.Desk() },
                new ShipPiece { Name = "LeverPost", Mesh = ShipProps.LeverPost() },
                new ShipPiece { Name = "Lockers", Mesh = ShipProps.Lockers() },
                new ShipPiece { Name = "CeilingLight", Mesh = ShipProps.CeilingLight() },
                new ShipPiece { Name = "WallLamps", Mesh = ShipProps.WallLamps() },
                new ShipPiece { Name = "HatchDoor0", Mesh = ShipProps.HatchDoor(0) },
                new ShipPiece { Name = "HatchDoor1", Mesh = ShipProps.HatchDoor(1) },
                new ShipPiece { Name = "Lever", Mesh = ShipProps.Lever(), Offset = LeverPivot },
                new ShipPiece { Name = "Comforts", Mesh = ShipProps.Comforts() },
                new ShipPiece { Name = "FairyLights", Mesh = ShipProps.FairyLights() },
            };
            return pieces;
        }

        /// <summary>Where the lever's foot is: the top of its post.</summary>
        public static readonly P3 LeverPivot = new P3(ShipLayout.HatchLever.X, 0.92f, ShipLayout.HatchLever.Z);

        static ShipMesh Moved(ShipMesh mesh, P3 offset)
        {
            if (offset.X == 0f && offset.Y == 0f && offset.Z == 0f) return mesh;
            var copy = new ShipMesh
            {
                Positions = (float[])mesh.Positions.Clone(),
                Colors = mesh.Colors,
                Glow = mesh.Glow,
                Triangles = mesh.Triangles,
            };
            for (int i = 0; i < copy.Positions.Length; i += 3)
            {
                copy.Positions[i] += offset.X;
                copy.Positions[i + 1] += offset.Y;
                copy.Positions[i + 2] += offset.Z;
            }
            return copy;
        }

        /// <summary>Bakes the light for every piece. Returns linear colours (r, g, b per corner), one array per piece.</summary>
        public static List<float[]> Bake(List<ShipPiece> pieces, LightBake.Settings settings = null, Action<float> progress = null)
        {
            var bake = new LightBake();
            for (int i = 0; i < pieces.Count; i++) bake.Add(Moved(pieces[i].Mesh, pieces[i].Offset));
            return bake.Bake(settings, progress);
        }

        /// <summary>A stand-in used until the real bake is ready: every surface evenly, softly lit.</summary>
        public static List<float[]> FlatLight(List<ShipPiece> pieces)
        {
            var result = new List<float[]>();
            for (int p = 0; p < pieces.Count; p++)
            {
                ShipMesh mesh = pieces[p].Mesh;
                var colors = new float[mesh.VertexCount * 3];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    float k = mesh.Glow[i] > 0.01f ? 1f : 0.5f;
                    for (int c = 0; c < 3; c++) colors[i * 3 + c] = LightBake.ToLinear(mesh.Colors[i * 3 + c]) * k;
                }
                result.Add(colors);
            }
            return result;
        }

        /// <summary>A fingerprint of the cabin's shape, paint and the placing of each piece. A baked file only fits the cabin it was baked for.</summary>
        public static uint Fingerprint(List<ShipPiece> pieces)
        {
            uint h = 2166136261;
            void Mix(int v)
            {
                unchecked
                {
                    h = (h ^ (uint)(v & 0xFFFF)) * 16777619;
                    h = (h ^ (uint)((v >> 16) & 0xFFFF)) * 16777619;
                }
            }
            for (int p = 0; p < pieces.Count; p++)
            {
                ShipMesh m = pieces[p].Mesh;
                // Where the piece sits counts too: the light is baked with it in place.
                Mix((int)Math.Round(pieces[p].Offset.X * 1000.0));
                Mix((int)Math.Round(pieces[p].Offset.Y * 1000.0));
                Mix((int)Math.Round(pieces[p].Offset.Z * 1000.0));
                Mix(m.VertexCount);
                Mix(m.Triangles.Length);
                for (int i = 0; i < m.Positions.Length; i++) Mix((int)Math.Round(m.Positions[i] * 1000.0));
                for (int i = 0; i < m.Colors.Length; i++) Mix((int)Math.Round(m.Colors[i] * 255.0));
                for (int i = 0; i < m.Glow.Length; i++) Mix((int)Math.Round(m.Glow[i] * 100.0));
                for (int i = 0; i < m.Triangles.Length; i++) Mix(m.Triangles[i]);
            }
            return h;
        }

        /// <summary>Packs baked colours into bytes: one byte each for red, green and blue per corner.</summary>
        public static byte[] Pack(List<ShipPiece> pieces, List<float[]> colors)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Fingerprint(pieces));
                writer.Write(pieces.Count);
                for (int p = 0; p < pieces.Count; p++)
                {
                    writer.Write(colors[p].Length / 3);
                    for (int i = 0; i < colors[p].Length; i++)
                    {
                        // Stored the way screens store colour, which keeps the dark end smooth in one byte.
                        writer.Write((byte)Math.Round(Math.Max(0f, Math.Min(1f, LightBake.ToScreen(colors[p][i]))) * 255f));
                    }
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Unpacks a baked file. Returns null if it is missing, damaged, or was baked for a different cabin.</summary>
        public static List<float[]> Unpack(byte[] bytes, List<ShipPiece> pieces)
        {
            if (bytes == null || bytes.Length < 12) return null;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadInt32() != Magic) return null;
                    if (reader.ReadUInt32() != Fingerprint(pieces)) return null;
                    if (reader.ReadInt32() != pieces.Count) return null;
                    var result = new List<float[]>();
                    for (int p = 0; p < pieces.Count; p++)
                    {
                        int corners = reader.ReadInt32();
                        if (corners != pieces[p].Mesh.VertexCount) return null;
                        var colors = new float[corners * 3];
                        for (int i = 0; i < colors.Length; i++) colors[i] = LightBake.ToLinear(reader.ReadByte() / 255f);
                        result.Add(colors);
                    }
                    return result;
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }
    }
}
