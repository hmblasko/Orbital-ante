namespace OrbitalAnte.Planets
{
    /// <summary>What stops you when you walk or land on a piece of scenery.</summary>
    public enum ScenerySolid
    {
        /// <summary>Nothing: grass, flowers, ferns.</summary>
        None,
        /// <summary>One capsule along the trunk; the leaves are passed through.</summary>
        Trunk,
        /// <summary>A simple closed shape that follows the rock.</summary>
        Shape,
    }

    /// <summary>
    /// Which model file holds each kind of Blender-made scenery, and what the file must contain. Written by
    /// tools/blender/build_scenery.py. A piece is named after the kind and the variant ("Conifer0"), is 1 unit
    /// tall and stands on its origin. Plain rules, no Unity types.
    /// </summary>
    public static class SceneryKinds
    {
        public const string Trees = "EarthTrees", Plants = "EarthPlants", Rocks = "EarthRocks";
        /// <summary>The most triangles one tree, plant, rock or solid shape may have (the build script enforces the same).</summary>
        public const int TreeTriangles = 1700, PlantTriangles = 620, RockTriangles = 1400, SolidTriangles = 220;

        /// <summary>The model file for a kind, or null for kinds that are worked out in code.</summary>
        public static string ModelOf(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Conifer: case FeatureKind.Broadleaf: case FeatureKind.Palm: case FeatureKind.Birch:
                case FeatureKind.Maple: case FeatureKind.Blossom: case FeatureKind.Pine: case FeatureKind.Acacia:
                case FeatureKind.GiantTree: case FeatureKind.Snag: case FeatureKind.SnowConifer:
                    return Trees;
                case FeatureKind.Bush: case FeatureKind.Cactus: case FeatureKind.Meadow: case FeatureKind.Reeds:
                case FeatureKind.Fern: case FeatureKind.Log:
                    return Plants;
                case FeatureKind.Crag: case FeatureKind.SnowCrag: case FeatureKind.Outcrop:
                    return Rocks;
                default:
                    return null;
            }
        }

        public static ScenerySolid SolidOf(FeatureKind kind)
        {
            string model = ModelOf(kind);
            if (model == Trees || kind == FeatureKind.Cactus) return ScenerySolid.Trunk;
            return model == Rocks ? ScenerySolid.Shape : ScenerySolid.None;
        }

        public static int TriangleCap(FeatureKind kind)
        {
            string model = ModelOf(kind);
            return model == Trees ? TreeTriangles : model == Rocks ? RockTriangles : PlantTriangles;
        }

        public static string Piece(FeatureKind kind, int variant)
        {
            int v = ((variant % FeatureShapes.Variants) + FeatureShapes.Variants) % FeatureShapes.Variants;
            return kind.ToString() + v;
        }
    }
}
