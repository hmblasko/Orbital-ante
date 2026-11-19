using NUnit.Framework;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    public class SceneryKindsBoundaryTests
    {
        [TestCase(-1, 3)]
        [TestCase(-4, 0)]
        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(int.MinValue, 0)]
        [TestCase(int.MaxValue, 3)]
        public void PieceNamesWrapEveryKindEvenAtIntegerLimits(int variant, int expected)
        {
            // The model contract supplies four variants; no .bytes file needs to be loaded to check lookup.
            Assert.AreEqual(4, FeatureShapes.Variants);
            foreach (FeatureKind kind in new[] { FeatureKind.Birch, FeatureKind.Cactus, FeatureKind.Crag, FeatureKind.Boulder })
                Assert.AreEqual(kind.ToString() + expected, SceneryKinds.Piece(kind, variant));
        }

        [TestCase(FeatureKind.Conifer, SceneryKinds.Trees, ScenerySolid.Trunk, SceneryKinds.TreeTriangles)]
        [TestCase(FeatureKind.Cactus, SceneryKinds.Plants, ScenerySolid.Trunk, SceneryKinds.PlantTriangles)]
        [TestCase(FeatureKind.Log, SceneryKinds.Plants, ScenerySolid.None, SceneryKinds.PlantTriangles)]
        [TestCase(FeatureKind.SnowCrag, SceneryKinds.Rocks, ScenerySolid.Shape, SceneryKinds.RockTriangles)]
        public void ModelFamilyAndCollisionContractAgreeForTheExceptionalKinds(FeatureKind kind, string model, ScenerySolid solid, int cap)
        {
            Assert.AreEqual(model, SceneryKinds.ModelOf(kind));
            Assert.AreEqual(solid, SceneryKinds.SolidOf(kind));
            Assert.AreEqual(cap, SceneryKinds.TriangleCap(kind));
        }

        [TestCase(FeatureKind.Boulder)]
        [TestCase(FeatureKind.Crystal)]
        [TestCase((FeatureKind)(-1))]
        [TestCase((FeatureKind)int.MaxValue)]
        public void ProceduralAndUnknownKindsHaveNoModelOrModelCollider(FeatureKind kind)
        {
            Assert.IsNull(SceneryKinds.ModelOf(kind));
            Assert.AreEqual(ScenerySolid.None, SceneryKinds.SolidOf(kind));
            Assert.Greater(SceneryKinds.TriangleCap(kind), 0, "Fallback lookup still returns a finite budget.");
            string first = SceneryKinds.Piece(kind, 0);
            SceneryKinds.ModelOf(FeatureKind.Conifer);
            Assert.AreEqual(first, SceneryKinds.Piece(kind, 0));
        }
    }
}
