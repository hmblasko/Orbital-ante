using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OrbitalAnte.Look
{
    /// <summary>One separately movable piece of a model: its corners, their colours and its triangles.</summary>
    public sealed class ModelPart
    {
        public string Name;
        /// <summary>Three numbers per corner, in the model's own space (metres; X right, Y up, Z forward).</summary>
        public float[] Positions;
        /// <summary>Three numbers per corner: which way the surface faces there.</summary>
        public float[] Normals;
        /// <summary>Three numbers per corner, 0..1, as seen on screen (not linear).</summary>
        public float[] Colors;
        /// <summary>
        /// Two numbers per corner, 0..1: how smooth the surface is there (0 chalk, 1 a mirror) and whether it is
        /// bare metal. Null in a model that does not say (every model made before the realistic look), which is
        /// drawn as chalk all over.
        /// </summary>
        public float[] Finish;
        public int[] Triangles;

        public int VertexCount => Positions.Length / 3;
        public int TriangleCount => Triangles.Length / 3;
    }

    /// <summary>A named attachment point on a model (a muzzle, a hand hold).</summary>
    public struct ModelSocket
    {
        public string Name;
        public float X, Y, Z;
    }

    /// <summary>
    /// A model made in Blender by the scripts in tools/blender, as the game reads it. Colour is stored per corner:
    /// there are no texture files. The file layout is written by tools/blender/oa_blender.py: "OAM1", or "OAM2"
    /// when the model also says what its surfaces are made of (<see cref="ModelPart.Finish"/>).
    /// Plain data, no Unity types, so it can be read and tested anywhere.
    /// </summary>
    public sealed class ModelFile
    {
        public const int MaxParts = 64;
        public const int MaxCornersPerPart = 60000;
        /// <summary>Scenery files carry three attachment points a tree (its trunk), so this is generous.</summary>
        public const int MaxSockets = 256;

        public ModelPart[] Parts = new ModelPart[0];
        public ModelSocket[] Sockets = new ModelSocket[0];

        public ModelPart Part(string name)
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i].Name == name) return Parts[i];
            }
            return null;
        }

        /// <summary>
        /// True for a piece that shines by itself. The models' own rule: its name ends in "Glow" (a scope's front
        /// glass, a power cell's window, a lamp). Such a piece holds only the parts that shine.
        /// </summary>
        public static bool Shines(string piece) => piece != null && piece.EndsWith("Glow", StringComparison.Ordinal);

        /// <summary>
        /// On the end of a model's name, the far copy of it: the same pieces by the same names and the same
        /// sockets in about a fifth of the triangles, written by the same Blender script (oa_blender.thin).
        /// </summary>
        public const string FarSuffix = "Far";

        /// <summary>The name of a model's far copy. (A far copy has none of its own.)</summary>
        public static string FarOf(string model) => string.IsNullOrEmpty(model) || IsFar(model) ? null : model + FarSuffix;

        public static bool IsFar(string model) => model != null && model.Length > FarSuffix.Length && model.EndsWith(FarSuffix, StringComparison.Ordinal);

        public bool TrySocket(string name, out ModelSocket socket)
        {
            for (int i = 0; i < Sockets.Length; i++)
            {
                if (Sockets[i].Name == name)
                {
                    socket = Sockets[i];
                    return true;
                }
            }
            socket = default;
            return false;
        }

        public int TriangleCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Parts.Length; i++) total += Parts[i].TriangleCount;
                return total;
            }
        }

        /// <summary>Reads a model. Returns null and says why in <paramref name="problem"/> if the bytes are not a sound model.</summary>
        public static ModelFile Parse(byte[] bytes, out string problem)
        {
            problem = null;
            if (bytes == null || bytes.Length < 8)
            {
                problem = "the file is empty";
                return null;
            }
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadByte() != 'O' || reader.ReadByte() != 'A' || reader.ReadByte() != 'M')
                    {
                        problem = "it is not an Orbital Ante model file";
                        return null;
                    }
                    int layout = reader.ReadByte();
                    if (layout != '1' && layout != '2')
                    {
                        problem = "it is an Orbital Ante model file of a kind this version of the game cannot read";
                        return null;
                    }
                    bool saysFinish = layout == '2';
                    int partCount = reader.ReadInt32();
                    if (partCount < 1 || partCount > MaxParts)
                    {
                        problem = "it claims " + partCount + " pieces";
                        return null;
                    }
                    var model = new ModelFile { Parts = new ModelPart[partCount] };
                    for (int p = 0; p < partCount; p++)
                    {
                        var part = new ModelPart { Name = ReadName(reader) };
                        int corners = reader.ReadInt32();
                        int triangles = reader.ReadInt32();
                        if (corners < 3 || corners > MaxCornersPerPart || triangles < 1 || triangles > MaxCornersPerPart * 2)
                        {
                            problem = "piece '" + part.Name + "' claims " + corners + " corners and " + triangles + " triangles";
                            return null;
                        }
                        part.Positions = ReadFloats(reader, corners * 3);
                        part.Normals = ReadFloats(reader, corners * 3);
                        part.Colors = new float[corners * 3];
                        for (int i = 0; i < part.Colors.Length; i++) part.Colors[i] = reader.ReadByte() / 255f;
                        if (saysFinish)
                        {
                            part.Finish = new float[corners * 2];
                            for (int i = 0; i < part.Finish.Length; i++) part.Finish[i] = reader.ReadByte() / 255f;
                        }
                        part.Triangles = new int[triangles * 3];
                        for (int i = 0; i < part.Triangles.Length; i++)
                        {
                            int index = reader.ReadInt32();
                            if (index < 0 || index >= corners)
                            {
                                problem = "piece '" + part.Name + "' has a triangle pointing at corner " + index + " of " + corners;
                                return null;
                            }
                            part.Triangles[i] = index;
                        }
                        for (int i = 0; i < part.Positions.Length; i++)
                        {
                            float v = part.Positions[i];
                            if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 1000f)
                            {
                                problem = "piece '" + part.Name + "' has a corner that is not a sensible position";
                                return null;
                            }
                        }
                        for (int i = 0; i < part.Normals.Length; i++)
                        {
                            float v = part.Normals[i];
                            if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2f)
                            {
                                problem = "piece '" + part.Name + "' has a corner whose facing is not a sensible direction";
                                return null;
                            }
                        }
                        model.Parts[p] = part;
                    }
                    int socketCount = reader.ReadInt32();
                    if (socketCount < 0 || socketCount > MaxSockets)
                    {
                        problem = "it claims " + socketCount + " attachment points";
                        return null;
                    }
                    model.Sockets = new ModelSocket[socketCount];
                    for (int s = 0; s < socketCount; s++)
                    {
                        model.Sockets[s] = new ModelSocket { Name = ReadName(reader), X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle() };
                        ModelSocket read = model.Sockets[s];
                        if (!Sensible(read.X) || !Sensible(read.Y) || !Sensible(read.Z))
                        {
                            problem = "attachment point '" + read.Name + "' is not at a sensible position";
                            return null;
                        }
                    }
                    return model;
                }
            }
            catch (EndOfStreamException)
            {
                problem = "the file ends early";
                return null;
            }
        }

        /// <summary>Writes a model in the same layout Blender's exporter uses. Used by tests.</summary>
        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                bool saysFinish = false;
                foreach (ModelPart part in Parts) saysFinish |= part.Finish != null;
                writer.Write(new[] { (byte)'O', (byte)'A', (byte)'M', saysFinish ? (byte)'2' : (byte)'1' });
                writer.Write(Parts.Length);
                foreach (ModelPart part in Parts)
                {
                    WriteName(writer, part.Name);
                    writer.Write(part.VertexCount);
                    writer.Write(part.TriangleCount);
                    foreach (float v in part.Positions) writer.Write(v);
                    foreach (float v in part.Normals) writer.Write(v);
                    foreach (float v in part.Colors) writer.Write((byte)Math.Round(Math.Max(0f, Math.Min(1f, v)) * 255f));
                    if (saysFinish)
                    {
                        // A piece that does not say is chalk: nothing smooth, nothing metal.
                        for (int i = 0; i < part.VertexCount * 2; i++)
                        {
                            float v = part.Finish != null && i < part.Finish.Length ? part.Finish[i] : 0f;
                            writer.Write((byte)Math.Round(Math.Max(0f, Math.Min(1f, v)) * 255f));
                        }
                    }
                    foreach (int i in part.Triangles) writer.Write(i);
                }
                writer.Write(Sockets.Length);
                foreach (ModelSocket socket in Sockets)
                {
                    WriteName(writer, socket.Name);
                    writer.Write(socket.X);
                    writer.Write(socket.Y);
                    writer.Write(socket.Z);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The smallest and largest corner along each axis, over every piece.</summary>
        public void Extent(out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
        {
            minX = minY = minZ = float.MaxValue;
            maxX = maxY = maxZ = float.MinValue;
            foreach (ModelPart part in Parts)
            {
                for (int i = 0; i < part.Positions.Length; i += 3)
                {
                    minX = Math.Min(minX, part.Positions[i]); maxX = Math.Max(maxX, part.Positions[i]);
                    minY = Math.Min(minY, part.Positions[i + 1]); maxY = Math.Max(maxY, part.Positions[i + 1]);
                    minZ = Math.Min(minZ, part.Positions[i + 2]); maxZ = Math.Max(maxZ, part.Positions[i + 2]);
                }
            }
        }

        static bool Sensible(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1000f;

        static float[] ReadFloats(BinaryReader reader, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++) values[i] = reader.ReadSingle();
            return values;
        }

        static string ReadName(BinaryReader reader)
        {
            int length = reader.ReadByte();
            byte[] raw = reader.ReadBytes(length);
            if (raw.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(raw);
        }

        static void WriteName(BinaryWriter writer, string name)
        {
            byte[] raw = Encoding.UTF8.GetBytes(name ?? "");
            if (raw.Length > 255) throw new ArgumentException("name too long");
            writer.Write((byte)raw.Length);
            writer.Write(raw);
        }
    }
}
