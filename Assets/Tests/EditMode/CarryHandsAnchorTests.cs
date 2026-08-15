using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Which pose the hands actually choose</b> — the branch table between the rig's hand-prop row and
    /// the single fallback offset that everything used before it.
    ///
    /// <para><b>Why the fallback gets more tests than the feature.</b> The new path is the loud one: if it
    /// breaks, a clam hangs in the air and you see it immediately. The dangerous failure is the opposite —
    /// the table quietly NOT being used, or being used where it should not be, because both look like a
    /// pose someone chose. So every road back to the fallback is pinned by name: no table, no row, no iso
    /// skin, a skin with no art, and a skin baked at a facing count these rows do not describe. And the
    /// fallback itself gets an A/B, because a jerry can and a shovel must look exactly as they did before
    /// this landed.</para>
    ///
    /// <para><b>⚠️ EditMode never runs <c>Awake</c>, <c>OnEnable</c> or <c>LateUpdate</c> on a plain
    /// MonoBehaviour</b> (only <c>[ExecuteAlways]</c> wakes on AddComponent), so
    /// <see cref="IsoCharacterSprite"/> never resolves a drawn cell here: its facing row, frame and
    /// heading all stay at their defaults. <see cref="DrawnCell"/> states all three together, which is
    /// honest for a unit test of the CONSUMER — and is exactly why the PlayMode twin exists, since only a
    /// running frame loop can show the fisher really turning and the pin really following.</para>
    /// </summary>
    public class CarryHandsAnchorTests
    {
        const int Directions = 8;
        const int IdleFrames = 6;

        /// <summary>The shipped fallback offset — restated so a change to it is a deliberate red here.</summary>
        static readonly Vector2 ShippedHipOffset = new Vector2(0.28f, 0.34f);

        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }

        // ---- the fixture -----------------------------------------------------------------------------

        static Sprite[] Sheet(int frames)
        {
            var set = new Sprite[Directions * frames];
            var tex = new Texture2D(4, 4);
            for (int i = 0; i < set.Length; i++)
                set[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.1f), 32f);
            return set;
        }

        CharacterVisualDef Visual(int facings = Directions)
        {
            var d = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            d.Id = "visual.test";
            d.FacingCount = facings;
            d.IdleFrameCount = IdleFrames;
            d.IdleSheet = Sheet(IdleFrames);
            return d;
        }

        /// <summary>A table whose clam row is DISTINCT per facing and per frame, so a consumer that read
        /// the wrong index cannot pass by luck.</summary>
        CarryAnchorTableDef Table()
        {
            var t = Track(ScriptableObject.CreateInstance<CarryAnchorTableDef>());
            t.Id = "carryanchors.test";
            t.FacingCount = Directions;

            var rows = new CarryAnchorRow[Directions];
            for (int d = 0; d < Directions; d++)
                rows[d] = new CarryAnchorRow
                {
                    Hand = CarryHandSide.Right,
                    Mount = CarryMountKind.Hand,
                    GripOffsetMeters = Vector2.zero,
                    RestOffsetMeters = new Vector2(-99f, -99f),   // never the right answer while live
                    ItemFacing = (d + 2) % Directions,            // deliberately NOT the carrier's facing
                    Behind = d == 0,                              // behind at N only — see the order test
                };
            t.Props = new[]
            {
                new CarryPropAnchors
                { PropKey = CarryPropKeys.Clam, Rig = "Shellfish", ItemScale = 1f, Rows = rows },
            };

            var left = new Vector2[Directions * IdleFrames];
            var right = new Vector2[Directions * IdleFrames];
            for (int d = 0; d < Directions; d++)
                for (int f = 0; f < IdleFrames; f++)
                {
                    right[d * IdleFrames + f] = new Vector2(d, f);    // readable in a failure message
                    left[d * IdleFrames + f] = new Vector2(-d, -f);
                }
            t.Wrists = new[]
            {
                new CarryWristFrames
                { Gait = CharacterGait.Idle, FramesPerDir = IdleFrames, Left = left, Right = right },
            };
            return t;
        }

        /// <summary>A fisher with a body renderer, an iso skin and a pair of hands.</summary>
        (CarryHands hands, IsoCharacterSprite skin, SpriteRenderer body) Fisher(
            CarryAnchorTableDef table, CharacterVisualDef visual)
        {
            var go = Track(new GameObject("Fisher"));
            var body = go.AddComponent<SpriteRenderer>();
            body.sortingOrder = 40;

            IsoCharacterSprite skin = null;
            if (visual != null)
            {
                skin = go.AddComponent<IsoCharacterSprite>();
                skin.Configure(visual);
            }

            var hands = go.AddComponent<CarryHands>();
            hands.ConfigureCarryAnchors(table);
            return (hands, skin, body);
        }

        /// <summary>
        /// State the cell the body would be DRAWING — the facing row, the frame, and the heading that goes
        /// with them, all three together so the two pose paths are asked about the same picture. See the
        /// class remarks on why this is stated rather than played.
        /// </summary>
        static void DrawnCell(IsoCharacterSprite skin, int facingRow, int frame)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(IsoCharacterSprite).GetField("_facingRow", F).SetValue(skin, facingRow);
            typeof(IsoCharacterSprite).GetField("_frame", F).SetValue(skin, frame);
            typeof(IsoCharacterSprite).GetField("_headingDegrees", F).SetValue(skin, facingRow * 45f);
        }

        static CatchItem Clam() =>
            new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", FishCategory.Shellfish,
                          0.15f, 3, 0.1f, 0.5f, Freshness.Landed(0d));

        CarriableTool Shovel()
        {
            var def = Track(ScriptableObject.CreateInstance<ToolDef>());
            def.Id = ToolDef.ShovelId;
            def.DisplayName = "Clam Shovel";
            def.HandPropKey = null;               // ⚠️ the rig bakes NO spade row — this is the real state
            var go = Track(new GameObject("Shovel"));
            go.AddComponent<SpriteRenderer>();
            var tool = go.AddComponent<CarriableTool>();
            tool.Configure(def, null, "tool.shovel#test");
            return tool;
        }

        /// <summary>An <see cref="ICarriable"/> that RECORDS what it is told — the only way to see the
        /// facing index and the sorting slot the hands hand out, since every shipped carriable's
        /// <c>ShowFacing</c> is a documented no-op while the carried art is non-directional.</summary>
        private sealed class SpyCarriable : MonoBehaviour, ICarriable, ICarryAnchored
        {
            public string Key = CarryPropKeys.Clam;
            public int LastFacing = -1;
            public int LastOrder = int.MinValue;
            public int LastLayer;

            public string HandPropKey => Key;
            public string DefId => "spy.thing";
            public bool IsCarriable => true;
            public Transform Transform => transform;
            public int BakedFacings => Directions;
            public void ShowFacing(int facingIndex) => LastFacing = facingIndex;
            public void RideSortingBand(int layer, int order) { LastLayer = layer; LastOrder = order; }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        SpyCarriable Spy(string key)
        {
            var go = Track(new GameObject("Spy"));
            var spy = go.AddComponent<SpyCarriable>();
            spy.Key = key;
            return spy;
        }

        // ---- the table path --------------------------------------------------------------------------

        [Test]
        public void AClamInHand_IsPinnedToTheLiveWrist_NotToTheFallbackOffset()
        {
            var (hands, skin, _) = Fisher(Table(), Visual());
            DrawnCell(skin, facingRow: 3, frame: 4);

            Assert.That(hands.TryPutInHand(Clam()), Is.True);

            // The fixture's right wrist at (dir 3, frame 4) is (3, 4), and the grip is zero.
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(new Vector2(3f, 4f)).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void ThePin_FollowsTheDrawnFacing_AND_TheDrawnFrame()
        {
            var (hands, skin, _) = Fisher(Table(), Visual());
            DrawnCell(skin, 0, 0);
            hands.TryPutInHand(Clam());
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(Vector2.zero).Using(Vec2Within(1e-4f)));

            // Turn her, then advance the cycle. BOTH indices must reach the pin — a consumer that read
            // only the facing passes the first of these and fails the second, which is precisely the
            // half-done version of this feature.
            DrawnCell(skin, 6, 0);
            hands.ApplyCarriedPose();
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(new Vector2(6f, 0f)).Using(Vec2Within(1e-4f)), "the facing reached it");

            DrawnCell(skin, 6, 5);
            hands.ApplyCarriedPose();
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(new Vector2(6f, 5f)).Using(Vec2Within(1e-4f)), "the frame reached it");
        }

        [Test]
        public void TheCarriedThing_IsShownTheRowsOwnTurntableCell_NotTheCarriersFacing()
        {
            var (hands, skin, _) = Fisher(Table(), Visual());
            SpyCarriable spy = Spy(CarryPropKeys.Clam);
            DrawnCell(skin, 5, 0);

            Assert.That(hands.TryPickUp(spy), Is.EqualTo(CarryRefusal.None));

            // The fixture's row 5 declares cell 7. A fish is genuinely turned across the view at some
            // headings, so the two indices are different questions and the item's own is the answer.
            Assert.That(spy.LastFacing, Is.EqualTo(7));
        }

        [Test]
        public void TheDrawOrder_ComesFromTheRowsBehindFlag_NotFromTheHeading()
        {
            var (hands, skin, body) = Fisher(Table(), Visual());
            SpyCarriable spy = Spy(CarryPropKeys.Clam);

            // Both N and NE read as "facing away" to the old cos(heading) rule, so it would put a held
            // item BEHIND the body at both. The table separates them — which is the whole point: the rig
            // knows the wrist is 0.215 m out and the torso only ~0.115 m half-wide, so at most headings a
            // small held item is clear of the silhouette and must draw over. That is the bug that made a
            // held clam disappear at north.
            Assert.That(CarryMath.AheadOrdersFor(0f, 1), Is.LessThan(0), "precondition: N reads as away");
            Assert.That(CarryMath.AheadOrdersFor(45f, 1), Is.LessThan(0), "precondition: NE reads as away");

            DrawnCell(skin, 0, 0);
            hands.TryPickUp(spy);
            Assert.That(spy.LastOrder, Is.LessThan(body.sortingOrder), "the row says BEHIND at N");

            DrawnCell(skin, 1, 0);
            hands.ApplyCarriedPose();
            Assert.That(spy.LastOrder, Is.GreaterThan(body.sortingOrder),
                        "the row says OVER at NE, where the heading rule would still say behind — so the " +
                        "order is coming from the table and not from cos(heading)");
            Assert.That(spy.LastLayer, Is.EqualTo(body.sortingLayerID), "and still inside the body's band");
        }

        // ---- every road back to the fallback ---------------------------------------------------------

        [Test]
        public void NoTableWired_KeepsTheFallbackOffset()
        {
            var (hands, skin, _) = Fisher(table: null, visual: Visual());
            DrawnCell(skin, 3, 4);

            hands.TryPutInHand(Clam());

            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void ACarriableWithNoBakedRow_KeepsTheFallbackOffset()
        {
            var (hands, skin, _) = Fisher(Table(), Visual());
            DrawnCell(skin, 3, 4);

            // ⚠️ THE SHOVEL. It is carried, the table is wired, the skin is drawing — and there is still
            // no row for a spade, so it must land on the fallback rather than borrowing another prop's
            // grip. This is the trap the arc was warned about, pinned at the component.
            CarriableTool shovel = Shovel();
            Assert.That(hands.TryPickUp(shovel), Is.EqualTo(CarryRefusal.None));

            Assert.That((Vector2)shovel.transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void ACarriableWhoseKeyNamesNoRow_KeepsTheFallbackOffset()
        {
            var (hands, skin, _) = Fisher(Table(), Visual());
            DrawnCell(skin, 3, 4);

            SpyCarriable stranger = Spy("shovelTrail");     // a plausible key the rig does not bake
            hands.TryPickUp(stranger);

            Assert.That((Vector2)stranger.transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void NoIsoSkin_KeepsTheFallbackOffset_BecauseThereIsNoDrawnFacingToTrust()
        {
            var (hands, _, _) = Fisher(Table(), visual: null);

            hands.TryPutInHand(Clam());

            // Without the skin the body is drawn by the four-way fallback sheets, which are not the
            // pictures these rows describe. Deriving a row index from the heading instead would borrow the
            // FUEL kit's bake convention for the CHARACTER kit's art — two lineages that agree today by
            // coincidence and are documented as free to disagree.
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void ASkinWithNoArt_KeepsTheFallbackOffset()
        {
            var unbuilt = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            unbuilt.Id = "visual.unbuilt";
            unbuilt.FacingCount = Directions;                     // no sheets at all

            var (hands, skin, _) = Fisher(Table(), unbuilt);
            DrawnCell(skin, 3, 4);
            hands.TryPutInHand(Clam());

            Assert.That(skin.HasArt, Is.False, "precondition");
            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        [Test]
        public void ASkinBakedAtADifferentFacingCount_KeepsTheFallbackOffset()
        {
            // A four-row skin indexing an eight-row table would pose from a heading the body is not
            // facing — right at some headings and wrong at others, which is the worst shape of wrong.
            var (hands, skin, _) = Fisher(Table(), Visual(facings: 4));
            DrawnCell(skin, 3, 4);
            hands.TryPutInHand(Clam());

            Assert.That((Vector2)hands.Carried.Transform.localPosition,
                        Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)));
        }

        // ---- the A/B: nothing already shipped moved ---------------------------------------------------

        [Test]
        public void TheFallbackPose_IsExactlyWhatItWasBeforeTheTableExisted_AtEveryHeading()
        {
            var (hands, skin, body) = Fisher(Table(), Visual());
            CarriableTool shovel = Shovel();
            hands.TryPickUp(shovel);

            for (int d = 0; d < Directions; d++)
            {
                DrawnCell(skin, d, d % IdleFrames);
                hands.ApplyCarriedPose();

                Assert.That((Vector2)shovel.transform.localPosition,
                            Is.EqualTo(ShippedHipOffset).Using(Vec2Within(1e-4f)),
                            $"the shipped hip offset at d{d}");
                Assert.That(shovel.GetComponent<SpriteRenderer>().sortingOrder,
                            Is.EqualTo(body.sortingOrder + CarryMath.AheadOrdersFor(d * 45f, 1)),
                            $"the pre-table heading-derived order at d{d}");
            }
        }

        static IEqualityComparer<Vector2> Vec2Within(float tol) => new Vec2Comparer(tol);

        private sealed class Vec2Comparer : IEqualityComparer<Vector2>
        {
            private readonly float _tol;
            public Vec2Comparer(float tol) => _tol = tol;
            public bool Equals(Vector2 a, Vector2 b) => (a - b).magnitude <= _tol;
            public int GetHashCode(Vector2 v) => 0;
        }
    }
}
