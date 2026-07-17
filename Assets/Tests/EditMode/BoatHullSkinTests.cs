using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The hull SKIN is data, not a const — and a hull swap re-skins the boat.
    ///
    /// <para>Two things under test. (1) <see cref="BoatVisualDef"/>, the binding: its all-or-nothing gates
    /// must never let a PARTIAL set half-ship, because one missing slice snaps the boat into a stale
    /// picture mid-turn or indexes a stale cell. (2) <see cref="BoatHullSkinner"/>, the one installer, in
    /// BOTH directions — because the direction that was missing is the bug this fixes.</para>
    ///
    /// <para><b>The swap gap (real, and covered here).</b> <see cref="OwnedFleet.ApplyHull"/> used to make
    /// the boat's picture change by writing <c>_spriteRenderer.sprite = hull.Sprite</c> — onto the very
    /// renderer the skin had DISABLED. So for as long as the player's boat has worn a directional skin,
    /// buying the Punt swapped your feel, your hold and your camera while the picture stayed the iso dory.
    /// <see cref="Purchase_FromSkinnedHull_ToPlainHull_RestoresThePlainPicture"/> reproduces exactly that
    /// (it fails on the old code) and its siblings cover the other three transitions.</para>
    ///
    /// Defs and sprites are built in-code (CreateInstance / Sprite.Create) — no assets, no art on disk, so
    /// this is a pure logic guard that cannot go stale when the owner re-slices a sheet.
    /// </summary>
    public class BoatHullSkinTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.Clear<BoatPurchased>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            spr.name = name;
            _spawned.Add(spr);
            return spr;
        }

        private Sprite[] MakeSprites(string stem, int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < count; i++) set[i] = MakeSprite($"{stem}_{i}");
            return set;
        }

        /// <summary>The iso dory's shape, in miniature: 8 headings, an 8-frame rock grid per heading (64),
        /// and two 10-column oar sheets (80 each) — the real sheet layout, at test scale.</summary>
        private BoatVisualDef MakeIsoVisual(bool rock = true, bool oars = true)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = "visual.test_iso";
            v.Facings = MakeSprites("Hull", 8);
            v.RockFrameCount = 8;
            v.OarColumnCount = 10;
            v.RockGrid = rock ? MakeSprites("Rock", 64) : System.Array.Empty<Sprite>();
            v.OarPort = oars ? MakeSprites("Port", 80) : System.Array.Empty<Sprite>();
            v.OarStar = oars ? MakeSprites("Star", 80) : System.Array.Empty<Sprite>();
            _spawned.Add(v);
            return v;
        }

        private BoatHullDef MakeHull(string id, BoatVisualDef visual, Sprite plain)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = id;
            h.HoldUnits = 6;
            h.Visual = visual;
            h.Sprite = plain;
            _spawned.Add(h);
            return h;
        }

        /// <summary>A boat root as the builder leaves it: physics body, controller, and the plain hull
        /// renderer already showing the start hull's fallback picture.</summary>
        private (GameObject go, SpriteRenderer sr, BoatController boat) MakeBoat(BoatHullDef startHull)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = startHull.Sprite;
            boat.SetHull(startHull);
            return (go, sr, boat);
        }

        private static Transform Skin(GameObject go) =>
            go.transform.Find(BoatHullSkinner.VisualChildName);

        // ---- the binding: all-or-nothing gates ----------------------------------------------

        [Test]
        public void FullCompass_IsTheGate_AndAPartialOneNeverShips()
        {
            var v = MakeIsoVisual();
            Assert.IsTrue(v.HasFullCompass(), "8 assigned facings is a complete compass");
            Assert.AreEqual(8, v.HeadingCount);

            v.Facings[3] = null;   // one missing picture
            Assert.IsFalse(v.HasFullCompass(),
                "a compass with a hole must NOT half-ship — the boat would snap into a stale facing mid-turn");

            v.Facings = System.Array.Empty<Sprite>();
            Assert.IsFalse(v.HasFullCompass(), "no facings = no skin (the plain rotating hull stands)");
            Assert.AreEqual(0, v.HeadingCount);
        }

        [Test]
        public void RockGrid_NeedsExactlyHeadingsTimesFrames()
        {
            var v = MakeIsoVisual();
            Assert.IsTrue(v.HasRockGrid(), "64 = 8 headings × 8 frames");

            v.RockFrameCount = 7;
            Assert.IsFalse(v.HasRockGrid(), "64 ≠ 8 × 7 — a mismatched grid would index the wrong frame");

            v.RockFrameCount = 8;
            v.RockGrid[10] = null;
            Assert.IsFalse(v.HasRockGrid(), "a hole in the grid is not a grid");

            var noRock = MakeIsoVisual(rock: false);
            Assert.IsTrue(noRock.HasFullCompass(), "the compass stands on its own…");
            Assert.IsFalse(noRock.HasRockGrid(), "…with no rock grid: static facings + the legacy transform rock");
        }

        [Test]
        public void OarSheets_AreBothOrNeither()
        {
            var v = MakeIsoVisual();
            Assert.IsTrue(v.HasOarSheets(), "80 + 80 = 8 headings × 10 columns each");

            v.OarStar = System.Array.Empty<Sprite>();
            Assert.IsFalse(v.HasOarSheets(),
                "one oar drawn and one missing is worse than no oars drawn — both or neither");

            v = MakeIsoVisual();
            v.OarPort[5] = null;
            Assert.IsFalse(v.HasOarSheets(), "a partial sheet would index a stale cell");
        }

        [Test]
        public void CreateRuntime_AdaptsBareFacings_ForCallersWithNoAsset()
        {
            // The seam the ambient fleet and the rotation-test harness use: they carry their own facings
            // as data, so they bind in memory rather than pointing at an asset — and share the installer.
            var v = BoatVisualDef.CreateRuntime(MakeSprites("Hull", 8), sortingOrder: 3);
            _spawned.Add(v);

            Assert.IsTrue(v.HasFullCompass());
            Assert.AreEqual(3, v.SortingOrder);
            Assert.AreEqual(0f, v.ZeroHeadingDegrees, "element 0 is North — the project's bearing convention");
            Assert.IsFalse(v.HasRockGrid(), "decor tier: no rock grid");
            Assert.IsFalse(v.HasOarSheets(), "decor tier: no oars");
        }

        // ---- the installer ------------------------------------------------------------------

        [Test]
        public void SkinnedHull_HidesThePlainPicture_AndBuildsTheCompassChild()
        {
            var visual = MakeIsoVisual();
            var plain = MakeSprite("PlainDory");
            var hull = MakeHull("boat.dory", visual, plain);
            var (go, sr, boat) = MakeBoat(hull);

            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsTrue(rig.Skinned);
            Assert.IsFalse(sr.enabled, "the plain hull PICTURE is hidden while a directional skin is worn");
            Assert.AreSame(plain, sr.sprite,
                "…by .enabled ONLY: the sprite ref must stay intact, so a later swap to an unskinned hull " +
                "can bring this renderer back with something to draw");

            var child = Skin(go);
            Assert.IsNotNull(child,
                $"the skin builds the '{BoatHullSkinner.VisualChildName}' child — BoatSpotlight finds it BY NAME " +
                "to read the hull's rock, so this name is load-bearing");
            Assert.AreSame(child, rig.Visual);
            Assert.AreSame(visual.Facings[0], rig.Renderer.sprite, "starts facing North until the first snap");
            Assert.AreEqual(visual.SortingOrder, rig.Renderer.sortingOrder);

            Assert.IsNotNull(rig.Directional, "the compass component rides the PHYSICS ROOT…");
            Assert.AreSame(go, rig.Directional.gameObject, "…never the counter-rotated child");
            Assert.IsTrue(rig.Directional.HasRockGrid, "the rock grid is wired from data");
            Assert.IsNotNull(rig.Wave, "the hull rides the shared wave field (ADR 0018 B2)");
            Assert.IsNotNull(rig.Oars, "the baked oar overlays are layered from data");
        }

        [Test]
        public void PlainHull_KeepsTheRotatingPicture_AndBuildsNoSkin()
        {
            // The fallback that must not be stranded: the Punt and the FishingSkiff have no facings.
            var plain = MakeSprite("Punt");
            var hull = MakeHull("boat.punt", null, plain);
            var (go, sr, boat) = MakeBoat(hull);

            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsFalse(rig.Skinned);
            Assert.IsTrue(sr.enabled, "no skin → the one rotating picture on the root, exactly as before");
            Assert.AreSame(plain, sr.sprite);
            Assert.IsNull(Skin(go), "no compass child is built");
            Assert.IsNull(go.GetComponent<DirectionalBoatSprite>());
        }

        [Test]
        public void IncompleteCompass_FallsBackToThePlainPicture()
        {
            var visual = MakeIsoVisual();
            visual.Facings[2] = null;                       // the art is mid-import / mid-slice
            var plain = MakeSprite("PlainDory");
            var hull = MakeHull("boat.dory", visual, plain);
            var (go, sr, boat) = MakeBoat(hull);

            BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsTrue(sr.enabled, "a hole in the compass falls all the way back — never a half-state");
            Assert.AreSame(plain, sr.sprite);
            Assert.IsNull(Skin(go));
        }

        [Test]
        public void ApplyingTwice_SwapsTheSkin_InsteadOfStackingASecondRig()
        {
            var first = MakeIsoVisual();
            var hull = MakeHull("boat.dory", first, MakeSprite("PlainDory"));
            var (go, sr, boat) = MakeBoat(hull);

            BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            var second = MakeIsoVisual();
            hull.Visual = second;
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.AreEqual(1, CountChildrenNamed(go.transform, BoatHullSkinner.VisualChildName),
                "one hull picture, not two rigs piled up");
            Assert.AreSame(second.Facings[0], rig.Renderer.sprite, "the re-skin swapped the art");
            Assert.AreEqual(1, go.GetComponents<DirectionalBoatSprite>().Length,
                "the compass component is reused, never doubled");
            Assert.AreEqual(1, CountChildrenNamed(rig.Visual, BoatHullSkinner.PortOarChildName),
                "and the oar overlays are reused too");
        }

        private static int CountChildrenNamed(Transform parent, string name)
        {
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) n++;
            return n;
        }

        // ---- THE SWAP GAP: OwnedFleet must re-skin on a hull change --------------------------

        private OwnedFleet WireFleet(GameObject go, BoatHullDef[] registry, BoatController boat,
                                     SpriteRenderer sr)
        {
            var hold = go.AddComponent<ShipHold>();
            var fleet = go.AddComponent<OwnedFleet>();
            hold.SetHull(registry[0]);
            fleet.Configure(registry, boat, hold, sr);
            EventBus.Clear<BoatPurchased>();
            EventBus.Subscribe<BoatPurchased>(fleet.OnBoatPurchased);   // EditMode doesn't run Awake
            return fleet;
        }

        [Test]
        public void Purchase_FromSkinnedHull_ToPlainHull_RestoresThePlainPicture()
        {
            // THE BUG, reproduced. Before this refactor OwnedFleet's "visible swap" was
            // `_spriteRenderer.sprite = hull.Sprite` — but the skin had DISABLED that renderer, so buying
            // the Punt changed the feel, the hold and the camera while the picture stayed the iso dory.
            var dory = MakeHull("boat.dory", MakeIsoVisual(), MakeSprite("PlainDory"));
            var puntSprite = MakeSprite("Punt");
            var punt = MakeHull("boat.punt", null, puntSprite);

            var (go, sr, boat) = MakeBoat(dory);
            BoatHullSkinner.ApplyHull(go, sr, dory, boat);          // as the start builder leaves it
            Assert.IsFalse(sr.enabled, "precondition: the skin hid the base renderer");
            Assert.IsNotNull(Skin(go), "precondition: the dory wears the compass");

            WireFleet(go, new[] { dory, punt }, boat, sr);
            EventBus.Publish(new BoatPurchased("boat.punt", 1800));

            Assert.AreSame(punt, boat.Hull, "the feel swapped (this always worked)");
            Assert.IsTrue(sr.enabled, "…and now the PICTURE swaps too: the base renderer comes back");
            Assert.AreSame(puntSprite, sr.sprite, "you see the boat you bought, not the dory");
            Assert.IsNull(Skin(go), "the dory's compass child is torn down, not left floating over the Punt");
            Assert.IsNull(go.GetComponent<DoryOarLayer>(), "and the dory's oars go with it");
        }

        [Test]
        public void Purchase_FromPlainHull_ToSkinnedHull_InstallsTheCompass()
        {
            // The other direction — a bought boat that HAS facings must wear them, without the builder.
            var punt = MakeHull("boat.punt", null, MakeSprite("Punt"));
            var skiffVisual = MakeIsoVisual(oars: false);
            var skiff = MakeHull("boat.skiff", skiffVisual, MakeSprite("PlainSkiff"));

            var (go, sr, boat) = MakeBoat(punt);
            BoatHullSkinner.ApplyHull(go, sr, punt, boat);
            Assert.IsTrue(sr.enabled, "precondition: the Punt shows its plain rotating picture");

            WireFleet(go, new[] { punt, skiff }, boat, sr);
            EventBus.Publish(new BoatPurchased("boat.skiff", 4000));

            Assert.AreSame(skiff, boat.Hull);
            Assert.IsFalse(sr.enabled, "the plain picture is hidden once a skin is worn");
            var child = Skin(go);
            Assert.IsNotNull(child, "the skiff's compass is installed at runtime — no builder, no editor API");
            Assert.AreSame(skiffVisual.Facings[0], child.GetComponent<SpriteRenderer>().sprite);
        }

        [Test]
        public void Purchase_BetweenSkinnedHulls_SwapsTheCompassArt()
        {
            var doryVisual = MakeIsoVisual();
            var dory = MakeHull("boat.dory", doryVisual, MakeSprite("PlainDory"));
            var skiffVisual = MakeIsoVisual(oars: false);
            var skiff = MakeHull("boat.skiff", skiffVisual, MakeSprite("PlainSkiff"));

            var (go, sr, boat) = MakeBoat(dory);
            BoatHullSkinner.ApplyHull(go, sr, dory, boat);

            WireFleet(go, new[] { dory, skiff }, boat, sr);
            EventBus.Publish(new BoatPurchased("boat.skiff", 4000));

            var child = Skin(go);
            Assert.IsNotNull(child);
            Assert.AreSame(skiffVisual.Facings[0], child.GetComponent<SpriteRenderer>().sprite,
                "the compass wears the NEW hull's art");
            Assert.IsNull(go.GetComponent<DoryOarLayer>(),
                "the dory's oars must not row a skiff that binds no oar sheets");
        }

        [Test]
        public void Purchase_UnknownId_LeavesTheSkinAlone()
        {
            var dory = MakeHull("boat.dory", MakeIsoVisual(), MakeSprite("PlainDory"));
            var (go, sr, boat) = MakeBoat(dory);
            BoatHullSkinner.ApplyHull(go, sr, dory, boat);

            WireFleet(go, new[] { dory }, boat, sr);
            EventBus.Publish(new BoatPurchased("boat.nonesuch", 10));

            Assert.AreSame(dory, boat.Hull, "an unknown id is a graceful no-op…");
            Assert.IsNotNull(Skin(go), "…and must not strip the skin off the boat you're standing in");
            Assert.IsFalse(sr.enabled);
        }
    }
}
