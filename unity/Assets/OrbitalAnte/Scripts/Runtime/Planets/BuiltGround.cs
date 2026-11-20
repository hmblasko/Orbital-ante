using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The height of the ground as it is really built: the flat triangles of the tile that is showing, which are
    /// also its collider. Everything that stands something on the ground or checks that something has not sunk
    /// into it must ask this, not <see cref="PlanetTerrain.Height"/>: the ideal height has every fine bump in and
    /// is not cut across by triangles, so on rough ground (crags, ridges, tower cliffs, dunes) it lies up to
    /// several metres above or below the real ground. A safety net measuring the wrong ground lifts someone who
    /// stands honestly (a bounce), or leaves someone sunk knee deep, believing they stand on top.
    ///
    /// Remembers the planes of the triangles asked about lately, so asking again where someone stands costs a
    /// few sums instead of three ground samples. One per body; main thread only (not safe to share between
    /// threads). The terrain never changes after it is built, so a remembered plane never goes stale.
    /// </summary>
    public sealed class BuiltGround
    {
        const int Slots = 256;

        readonly PlanetTerrain _terrain;
        readonly long[] _keys = new long[Slots];
        readonly Vec3d[] _normals = new Vec3d[Slots];
        readonly double[] _offsets = new double[Slots];

        /// <summary>How many questions needed the ground worked out (for tests and the debug readout).</summary>
        public int Misses { get; private set; }

        public BuiltGround(PlanetTerrain terrain)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            for (int k = 0; k < Slots; k++) _keys[k] = -1;
        }

        /// <summary>
        /// Distance from the body's centre to the ground of tile (face, level, x, y) in a direction, in metres:
        /// exactly where its mesh is. A direction just outside the tile is measured on its nearest edge square.
        /// </summary>
        public double Radius(int face, int level, int x, int y, Vec3d direction)
        {
            TerrainMesher.SurfaceCell(face, level, x, y, direction, out int i, out int j, out bool far);
            long key = Key(face, level, x, y, i, j, far);
            int slot = (int)((ulong)(key * unchecked((long)0x9E3779B97F4A7C15UL)) >> 56) & (Slots - 1);
            if (_keys[slot] != key)
            {
                TerrainMesher.CellPlane(_terrain, face, level, x, y, i, j, far, out _normals[slot], out _offsets[slot]);
                _keys[slot] = key;
                Misses++;
            }
            return TerrainMesher.RadiusOnPlane(_normals[slot], _offsets[slot], direction);
        }

        static long Key(int face, int level, int x, int y, int i, int j, bool far)
        {
            // Levels stop at TileRules.DeepestLevel (7), so x and y fit in 8 bits each; squares fit in 6.
            long k = face;
            k = (k << 4) | (uint)level;
            k = (k << 16) | (uint)(x & 0xFFFF);
            k = (k << 16) | (uint)(y & 0xFFFF);
            k = (k << 8) | (uint)(i & 0xFF);
            k = (k << 8) | (uint)(j & 0xFF);
            return (k << 1) | (far ? 1L : 0L);
        }
    }
}
