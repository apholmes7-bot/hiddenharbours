using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <see cref="StillWaterGlobals"/> (ADR 0046): the still water the sim wades, handed to the two shaders
    /// that draw a waterline. The sim and the render must read ONE map — the seam's own
    /// (<see cref="GameServices.StillWater"/> → <see cref="IStillWater.Map"/>) — or a pond is drawn where the
    /// fisher walks dry. These pin that a held global publishes exactly the registered map, that it follows
    /// a re-registration without being told, that a live sea is what holds it, and that when the last
    /// holder lets go the globals read "no still water": the value under which both shaders draw what they
    /// drew before the seam.
    ///
    /// <para><b>Independent bars.</b> The global names are spelled here, not read from the subject (a guard
    /// that asked <see cref="StillWaterGlobals"/> for its ids would pass a rename the shaders never saw),
    /// and every expected vector is a literal.</para>
    ///
    /// <para><b>On the base</b> nothing publishes these three globals: <c>_HHStillRange</c> reads
    /// (0, 0, 0, 0) where a held, bound map must read (min, max, 1, 0), and <c>_HHStillTex</c> reads null
    /// where the pond's texture must be — so each test's first bound assert fails there (and the subject
    /// does not exist to compile against).</para>
    ///
    /// <para><b>Teardown matters.</b> A global is sticky: every holder is released, the seam reset and the
    /// globals unset, so no later fixture that draws water inherits a pond.</para>
    /// </summary>
    public sealed class StillWaterGlobalsTests
    {
        private static readonly int TexId = Shader.PropertyToID("_HHStillTex");
        private static readonly int RectId = Shader.PropertyToID("_HHStillRect");
        private static readonly int RangeId = Shader.PropertyToID("_HHStillRange");

        private static readonly Vector4 PondRange = new Vector4(-4f, 7f, 1f, 0f);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<object> _holders = new List<object>();
        private int _holdersBefore;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _holdersBefore = StillWaterGlobals.HolderCount;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (object holder in _holders) StillWaterGlobals.Release(holder);
            _holders.Clear();
            GameServices.Reset();
            StillWaterGlobals.PublishUnset();   // hand the next fixture no pond
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        [Test]
        public void AHeldGlobal_PublishesTheRegisteredStillMap_AndFollowsTheSeam()
        {
            Texture2D pondTex = Tex("pond");
            Texture2D brookTex = Tex("brook");
            GameServices.StillWater = Pond(pondTex);
            Hold(new object());

            Assert.AreSame(pondTex, Shader.GetGlobalTexture(TexId),
                "the seam's own texture reaches the shaders — the map the sim decodes, not a copy of it");
            Assert.AreEqual(new Vector4(-120f, 180f, 64f, 48f), Shader.GetGlobalVector(RectId),
                "the rect is (minX, minY, sizeX, sizeY): the rectangle the sim decodes the map on");
            Assert.AreEqual(PondRange, Shader.GetGlobalVector(RangeId),
                "the range is (min, max, bound, 0): the height scale the map's codes are on, and bound = 1");
            Assert.IsTrue(StillWaterGlobals.IsBound);

            // Another region's terrain registering reaches the shaders with no second Hold: while anything
            // holds the globals they follow the seam's change event.
            GameServices.StillWater = new MapDouble(new StillWaterMap(brookTex, new Vector2(0f, -30f),
                new Vector2(0f, 10f), -6f, 6f));
            Assert.AreSame(brookTex, Shader.GetGlobalTexture(TexId), "the globals follow a re-registration");
            Assert.AreEqual(new Vector4(0f, -30f, 1e-3f, 10f), Shader.GetGlobalVector(RectId),
                "a zero-width rect is floored at 1 mm — the shader divides by the size");
            Assert.AreEqual(new Vector4(-6f, 6f, 1f, 0f), Shader.GetGlobalVector(RangeId));

            GameServices.StillWater = EmptyStillWater.Instance;
            AssertUnset("a region with no still map (every region today)");

            GameServices.StillWater = Pond(pondTex);
            Assert.AreEqual(PondRange, Shader.GetGlobalVector(RangeId), "a pond registered again is drawn again");

            GameServices.StillWater = null;
            AssertUnset("no still water registered at all");
        }

        [Test]
        public void TheLastReleaseUnsets_AndStopsFollowingTheSeam()
        {
            Assert.AreEqual(0, _holdersBefore,
                "a sea from an earlier fixture is still alive and holds the still-water globals: its teardown leaked it");
            GameServices.StillWater = Pond(Tex("pond"));
            object first = new object(), second = new object();
            Hold(first);
            Hold(second);
            StillWaterGlobals.Hold(first);   // a second hold by the same holder is the same hold
            StillWaterGlobals.Hold(null);
            Assert.AreEqual(2, StillWaterGlobals.HolderCount, "two holders, however often they held");

            StillWaterGlobals.Release(first);
            Assert.AreEqual(1, StillWaterGlobals.HolderCount);
            Assert.AreEqual(PondRange, Shader.GetGlobalVector(RangeId), "one sea still live: the pond stays drawn");

            StillWaterGlobals.Release(first);          // released twice
            StillWaterGlobals.Release(new object());   // never held
            StillWaterGlobals.Release(null);
            Assert.AreEqual(1, StillWaterGlobals.HolderCount, "a release by a non-holder is a no-op");
            Assert.AreEqual(PondRange, Shader.GetGlobalVector(RangeId), "and leaves the live holder's pond drawn");

            StillWaterGlobals.Release(second);
            Assert.AreEqual(0, StillWaterGlobals.HolderCount);
            AssertUnset("the last holder let go");

            // Nothing holds them now, so a registration must NOT reach the shaders: a stopped play session
            // cannot haunt the editor with a pond. A subscription left behind would republish here.
            GameServices.StillWater = Pond(Tex("brook"));
            AssertUnset("a registration after the last release");
            StillWaterGlobals.Refresh();
            AssertUnset("a refresh with nothing holding");
        }

        [Test]
        public void ALiveSea_HoldsTheGlobals_AndItsDisableLetsGo()
        {
            Assert.AreEqual(0, _holdersBefore,
                "a sea from an earlier fixture is still alive and holds the still-water globals: its teardown leaked it");
            Texture2D pondTex = Tex("pond");
            GameServices.StillWater = Pond(pondTex);

            var go = new GameObject("~sea") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<WaterSurface>();   // [ExecuteAlways]: its OnEnable has run

            Assert.AreEqual(1, StillWaterGlobals.HolderCount, "a live sea holds the still-water globals");
            Assert.AreSame(pondTex, Shader.GetGlobalTexture(TexId), "so the registered pond is drawn under it");
            Assert.AreEqual(PondRange, Shader.GetGlobalVector(RangeId));

            Object.DestroyImmediate(go);   // its OnDisable
            Assert.AreEqual(0, StillWaterGlobals.HolderCount, "a sea that goes lets go");
            AssertUnset("the only sea gone");
        }

        // ---- fixtures --------------------------------------------------------------------------------

        private static void AssertUnset(string when)
        {
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(RangeId),
                when + ": the range reads unset (bound = 0), under which both shaders cut at the tide alone");
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(RectId), when + ": the rect reads zero");
            Texture published = Shader.GetGlobalTexture(TexId);
            Assert.IsNotNull(published, when + ": the texture slot is never left empty");
            Assert.AreEqual(new Vector2Int(1, 1), new Vector2Int(published.width, published.height),
                when + ": the slot holds the 1x1 'none' texture, not a region's map");
            Assert.IsFalse(StillWaterGlobals.IsBound, when);
        }

        private void Hold(object holder)
        {
            _holders.Add(holder);
            StillWaterGlobals.Hold(holder);
        }

        private static MapDouble Pond(Texture2D tex)
            => new MapDouble(new StillWaterMap(tex, new Vector2(-120f, 180f), new Vector2(64f, 48f), -4f, 7f));

        // The format is immaterial here — the globals hand the shaders the seam's texture by reference. Its
        // 16-bit precision is guarded where it is sampled (StillWaterShaderTests) and where the tool writes
        // and imports it (SeabedHeightImportTests).
        private Texture2D Tex(string name)
        {
            var tex = new Texture2D(2, 2, TextureFormat.R8, false, true)
            {
                name = "~still " + name,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _made.Add(tex);
            return tex;
        }

        /// <summary>A registered still water as the globals see it: only its map. Its levels are never read here.</summary>
        private sealed class MapDouble : IStillWater
        {
            private readonly StillWaterMap _map;
            public MapDouble(StillWaterMap map) { _map = map; }
            public float StillLevelAt(Vector2 worldPos) => StillWaterLevels.None;
            public StillWaterMap Map => _map;
        }
    }
}
