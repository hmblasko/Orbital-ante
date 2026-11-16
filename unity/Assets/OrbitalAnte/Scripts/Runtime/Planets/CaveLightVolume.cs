using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The light inside a cave network, worked out once when it is built and kept in a coarse box of cells
    /// (2 m) that the game's shaders read (see "cave light" in OA_Look.hlsl):
    ///
    /// - how much of the open sky reaches each cell (1 in the open air, falling off with every turn of a passage
    ///   from a hole, and further still from a walk-in entrance than down a shaft under a hole in the roof). It
    ///   scales sunlight, skylight and their bounce, so the Sun never lights anything through rock;
    /// - the light of the network's own lamps and glowing crystals, carried through the air of the cave only
    ///   (round corners, never through a wall), added like fill light.
    ///
    /// Cells inside the rock take the light of the air beside them, so a wall read between cells gets the light
    /// of the air in front of it. Pure sums, no Unity types; safe on a background thread.
    /// </summary>
    public sealed class CaveLightVolume
    {
        public const double Cell = 2.0;
        /// <summary>Lamp light is stored divided by this, so a bright pool near a lamp still fits in a byte.</summary>
        public const float LightScale = 4f;

        public int Nx, Ny, Nz;
        /// <summary>Local position (in the network's frame) of the corner of the first cell.</summary>
        public Vec3d LocalOrigin;
        /// <summary>Four bytes a cell, x fastest then y then z: lamp red, green, blue (divided by <see cref="LightScale"/>) and sky reached.</summary>
        public byte[] Rgba;

        // Plain numbers too, for the rules tests and the preview tool.
        public float[] Sky;
        public float[] LampR, LampG, LampB;

        public int Index(int i, int j, int k) => (k * Ny + j) * Nx + i;

        /// <summary>Sky reached and lamp light at a point (metres from the body's centre), read between cells like the shader does.</summary>
        public void Sample(CaveNetwork net, Vec3d p, out float sky, out float r, out float g, out float b)
        {
            Vec3d l = net.ToLocal(p) - LocalOrigin;
            double fx = l.X / Cell - 0.5, fy = l.Y / Cell - 0.5, fz = l.Z / Cell - 0.5;
            if (fx < -0.5 || fy < -0.5 || fz < -0.5 || fx > Nx - 0.5 || fy > Ny - 0.5 || fz > Nz - 0.5)
            {
                sky = 1; r = g = b = 0;
                return;
            }
            int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy), k = (int)Math.Floor(fz);
            double tx = fx - i, ty = fy - j, tz = fz - k;
            sky = r = g = b = 0;
            for (int c = 0; c < 8; c++)
            {
                int ci = Clamp(i + (c & 1), Nx), cj = Clamp(j + ((c >> 1) & 1), Ny), ck = Clamp(k + ((c >> 2) & 1), Nz);
                double w = ((c & 1) != 0 ? tx : 1 - tx) * (((c >> 1) & 1) != 0 ? ty : 1 - ty) * (((c >> 2) & 1) != 0 ? tz : 1 - tz);
                int at = Index(ci, cj, ck);
                sky += (float)(Sky[at] * w);
                r += (float)(LampR[at] * w);
                g += (float)(LampG[at] * w);
                b += (float)(LampB[at] * w);
            }
        }

        static int Clamp(int v, int n) => v < 0 ? 0 : v >= n ? n - 1 : v;

        enum Kind : byte { Rock, Cave, Open, Crust }

        public static CaveLightVolume Build(CaveNetwork net, PlanetTerrain terrain)
        {
            var v = new CaveLightVolume();
            Vec3d min = net.LocalMin - new Vec3d(Cell, Cell, Cell), max = net.LocalMax + new Vec3d(Cell, Cell * 2, Cell);
            foreach (CaveOpening o in net.Openings)
            {
                Vec3d mouth = net.ToLocal(o.Direction * (terrain.Radius + terrain.Height(o.Direction)));
                max = new Vec3d(max.X, Math.Max(max.Y, mouth.Y + Cell * 2), max.Z);
            }
            v.LocalOrigin = min;
            v.Nx = Math.Max(2, (int)Math.Ceiling((max.X - min.X) / Cell));
            v.Ny = Math.Max(2, (int)Math.Ceiling((max.Y - min.Y) / Cell));
            v.Nz = Math.Max(2, (int)Math.Ceiling((max.Z - min.Z) / Cell));
            int count = v.Nx * v.Ny * v.Nz;
            var kind = new Kind[count];
            v.Sky = new float[count];
            v.LampR = new float[count];
            v.LampG = new float[count];
            v.LampB = new float[count];

            // What each cell is: rock, cave air, or the open air above the ground.
            for (int k = 0; k < v.Nz; k++)
                for (int i = 0; i < v.Nx; i++)
                {
                    double ground = double.NaN;
                    for (int j = 0; j < v.Ny; j++)
                    {
                        Vec3d p = v.Centre(net, i, j, k);
                        double r = p.Magnitude;
                        Kind what = Kind.Rock;
                        double below = double.MaxValue;
                        if (r > net.GroundLow - 4)
                        {
                            if (double.IsNaN(ground)) ground = terrain.Radius + terrain.Height(p.Normalized);
                            if (r > ground + 0.5) what = Kind.Open;
                            below = ground - r;
                        }
                        if (what == Kind.Rock && net.Air(p) < 0.2) what = Kind.Cave;
                        // The skin of rock just under the ground keeps full light, so the ground over a passage
                        // (read between cells) is never darkened by the cave below it.
                        else if (what == Kind.Rock && below < Cell * 1.4) what = Kind.Crust;
                        kind[v.Index(i, j, k)] = what;
                    }
                }

            // Sky: spread from the open air through the cave, losing some at every step: least going straight down
            // (light falling through a hole), most going up or sideways round a passage.
            var queue = new PriorityQueue();
            for (int q = 0; q < count; q++)
            {
                if (kind[q] != Kind.Open) continue;
                v.Sky[q] = 1;
                queue.Push(q, 1);
            }
            int[] di = { 1, -1, 0, 0, 0, 0 }, dj = { 0, 0, 1, -1, 0, 0 }, dk = { 0, 0, 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                queue.Pop(out int at, out float level);
                if (level < v.Sky[at] - 1e-6f) continue;
                int k = at / (v.Nx * v.Ny), rest = at - k * v.Nx * v.Ny, j = rest / v.Nx, i = rest - j * v.Nx;
                Vec3d here = v.Centre(net, i, j, k);
                Vec3d up = here.Normalized;
                for (int s = 0; s < 6; s++)
                {
                    int ni = i + di[s], nj = j + dj[s], nk = k + dk[s];
                    if (ni < 0 || nj < 0 || nk < 0 || ni >= v.Nx || nj >= v.Ny || nk >= v.Nz) continue;
                    int to = v.Index(ni, nj, nk);
                    if (kind[to] != Kind.Cave) continue;
                    Vec3d step = v.Centre(net, ni, nj, nk) - here;
                    double downward = -Vec3d.Dot(step.Normalized, up);
                    float keep = (float)(downward > 0.6 ? 0.965 : downward < -0.6 ? 0.7 : 0.75);
                    float next = level * keep;
                    if (next <= v.Sky[to] + 1e-4f || next < 0.004f) continue;
                    v.Sky[to] = next;
                    queue.Push(to, next);
                }
            }

            // Lamps: light carried through the cave's air from each lamp, dimming with the distance walked.
            var distance = new float[count];
            foreach (CaveLamp lamp in net.Lamps)
            {
                Vec3d head = lamp.Crystal ? lamp.Position : lamp.Position + lamp.Up * 1.7;
                Vec3d local = net.ToLocal(head) - v.LocalOrigin;
                int li = (int)(local.X / Cell), lj = (int)(local.Y / Cell), lk = (int)(local.Z / Cell);
                if (li < 0 || lj < 0 || lk < 0 || li >= v.Nx || lj >= v.Ny || lk >= v.Nz) continue;
                for (int q = 0; q < count; q++) distance[q] = float.MaxValue;
                int start = v.Index(li, lj, lk);
                distance[start] = (float)Vec3d.Distance(head, v.Centre(net, li, lj, lk));
                var front = new PriorityQueue();
                front.Push(start, -distance[start]);
                while (front.Count > 0)
                {
                    front.Pop(out int at, out float negative);
                    float d = -negative;
                    if (d > distance[at] + 1e-4f) continue;
                    // Light falls off with the square of the distance, softened so a lamp lights its whole room.
                    float t = Math.Max(0, 1 - d / lamp.Reach);
                    float amount = lamp.Strength * t * t / (1 + d * d * 0.02f);
                    v.LampR[at] += lamp.R * amount;
                    v.LampG[at] += lamp.G * amount;
                    v.LampB[at] += lamp.B * amount;
                    int k = at / (v.Nx * v.Ny), rest = at - k * v.Nx * v.Ny, j = rest / v.Nx, i = rest - j * v.Nx;
                    for (int s = 0; s < 6; s++)
                    {
                        int ni = i + di[s], nj = j + dj[s], nk = k + dk[s];
                        if (ni < 0 || nj < 0 || nk < 0 || ni >= v.Nx || nj >= v.Ny || nk >= v.Nz) continue;
                        int to = v.Index(ni, nj, nk);
                        if ((kind[to] == Kind.Rock || kind[to] == Kind.Crust) && at != start) continue;
                        float nd = d + (float)Cell;
                        if (nd >= lamp.Reach || nd >= distance[to]) continue;
                        distance[to] = nd;
                        front.Push(to, -nd);
                    }
                }
            }

            // Rock cells take the light of the air beside them, so walls read between cells are lit like the air
            // in front of them; twice, for walls that fall between two rows of rock cells.
            for (int pass = 0; pass < 2; pass++)
            {
                var fillSky = (float[])v.Sky.Clone();
                var fillR = (float[])v.LampR.Clone();
                var fillG = (float[])v.LampG.Clone();
                var fillB = (float[])v.LampB.Clone();
                var filled = new bool[count];
                for (int k = 0; k < v.Nz; k++)
                    for (int j = 0; j < v.Ny; j++)
                        for (int i = 0; i < v.Nx; i++)
                        {
                            int at = v.Index(i, j, k);
                            if (kind[at] != Kind.Rock) continue;
                            float sky = 0, r = 0, g = 0, b = 0;
                            int found = 0;
                            for (int s = 0; s < 6; s++)
                            {
                                int ni = i + di[s], nj = j + dj[s], nk = k + dk[s];
                                if (ni < 0 || nj < 0 || nk < 0 || ni >= v.Nx || nj >= v.Ny || nk >= v.Nz) continue;
                                int from = v.Index(ni, nj, nk);
                                if (kind[from] == Kind.Rock || kind[from] == Kind.Crust) continue;
                                sky += v.Sky[from]; r += v.LampR[from]; g += v.LampG[from]; b += v.LampB[from];
                                found++;
                            }
                            if (found == 0) continue;
                            fillSky[at] = sky / found; fillR[at] = r / found; fillG[at] = g / found; fillB[at] = b / found;
                            filled[at] = true;
                        }
                v.Sky = fillSky; v.LampR = fillR; v.LampG = fillG; v.LampB = fillB;
                for (int q = 0; q < count; q++) if (filled[q]) kind[q] = Kind.Cave;
            }
            for (int q = 0; q < count; q++) if (kind[q] == Kind.Crust) v.Sky[q] = 1;
            // Rock that touches no air at all is as open as the ground above it: whatever it is, nobody sees it, and
            // the ground on top (read between cells) must not be darkened by rock under it.
            for (int q = 0; q < count; q++) if (kind[q] == Kind.Rock) v.Sky[q] = 1;

            v.Rgba = new byte[count * 4];
            for (int q = 0; q < count; q++)
            {
                v.Rgba[q * 4] = ToByte(v.LampR[q] / LightScale);
                v.Rgba[q * 4 + 1] = ToByte(v.LampG[q] / LightScale);
                v.Rgba[q * 4 + 2] = ToByte(v.LampB[q] / LightScale);
                v.Rgba[q * 4 + 3] = ToByte(v.Sky[q]);
            }
            return v;
        }

        static byte ToByte(float x) => (byte)Math.Round(Math.Max(0, Math.Min(1, x)) * 255);

        /// <summary>Middle of a cell, metres from the body's centre.</summary>
        public Vec3d Centre(CaveNetwork net, int i, int j, int k)
        {
            return net.FromLocal(LocalOrigin + new Vec3d((i + 0.5) * Cell, (j + 0.5) * Cell, (k + 0.5) * Cell));
        }

        /// <summary>A small binary heap of cells, biggest key first.</summary>
        sealed class PriorityQueue
        {
            readonly List<int> _items = new List<int>();
            readonly List<float> _keys = new List<float>();
            public int Count => _items.Count;

            public void Push(int item, float key)
            {
                _items.Add(item);
                _keys.Add(key);
                int c = _items.Count - 1;
                while (c > 0)
                {
                    int parent = (c - 1) / 2;
                    if (_keys[parent] >= _keys[c]) break;
                    Swap(c, parent);
                    c = parent;
                }
            }

            public void Pop(out int item, out float key)
            {
                item = _items[0];
                key = _keys[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _keys[0] = _keys[last];
                _items.RemoveAt(last);
                _keys.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, best = c;
                    if (l < _items.Count && _keys[l] > _keys[best]) best = l;
                    if (r < _items.Count && _keys[r] > _keys[best]) best = r;
                    if (best == c) break;
                    Swap(c, best);
                    c = best;
                }
            }

            void Swap(int a, int b)
            {
                (_items[a], _items[b]) = (_items[b], _items[a]);
                (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            }
        }
    }
}
