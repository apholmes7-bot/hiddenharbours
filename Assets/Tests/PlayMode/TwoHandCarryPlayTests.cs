using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>TWO THINGS IN TWO HANDS, over real frames</b> — the half of the two-hand carry that EditMode
    /// cannot show.
    ///
    /// <para>EditMode owns the rule (<c>HandSlotsTests</c> — the truth table, the size rule, the
    /// continuity law) and the data (<c>CarryAnchorHandsTests</c> — both hands measured at all eight
    /// facings). What is left is the two things that only happen in play:
    /// <list type="number">
    ///   <item><b>EACH THING IS POSED FROM ITS OWN HAND.</b> The rod on the wrist it was given, the fish
    ///   on the other — every frame, following the body round. A thing carried ALONE keeps the rig's own
    ///   per-facing preference, which is the shipped look and must not regress.</item>
    ///   <item><b>THE FILL REACHES THE PICTURE ON THE EVENT EDGE.</b> EditMode never fires
    ///   <c>OnEnable</c>, so nothing there can prove the bridge is actually SUBSCRIBED — which is the
    ///   link that, when it broke, made a stored fish invisible.</item>
    /// </list></para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail — it KILLS the editor with no results XML. Every assertion is on state. An
    /// <c>AudioListener</c> is spawned because a listener-less play scene logs a warning on EVERY frame
    /// and a full suite writes it a million times.</para>
    /// </summary>
    public class TwoHandCarryPlayTests
    {
        const int Directions = 8;
        const int IdleFrames = 6;

        readonly List<Object> _spawned = new();

        CarryHands _hands;
        IsoCharacterSprite _skin;
        SpriteRenderer _body;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            Interactables.Clear();

            var listener = Track(new GameObject("Listener"));
            listener.AddComponent<AudioListener>();

            var visual = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            visual.Id = "visual.twohandtest";
            visual.FacingCount = Directions;
            visual.IdleFrameCount = IdleFrames;
            visual.IdleFramesPerSecond = 30f;
            visual.IdleSheet = Sheet(IdleFrames);

            var go = Track(new GameObject("Fisher"));
            _body = go.AddComponent<SpriteRenderer>();
            _body.sortingOrder = 40;
            _skin = go.AddComponent<IsoCharacterSprite>();
            _skin.Configure(visual);
            _hands = go.AddComponent<CarryHands>();
            _hands.ConfigureCarryAnchors(Table());
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            Interactables.Clear();
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }

        static Sprite[] Sheet(int frames)
        {
            var set = new Sprite[Directions * frames];
            for (int i = 0; i < set.Length; i++)
                set[i] = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4),
                                       new Vector2(0.5f, 0.1f), 32f);
            return set;
        }

        /// <summary>
        /// A table whose RIGHT wrist is <c>(dir, frame)</c> and whose LEFT wrist is
        /// <c>(−dir, −frame)</c> — so a pin reads back as the hand it came from AND the cell the body is
        /// drawing, and a wrong one names itself in the failure. Both props resolve to the RIGHT hand at
        /// every facing, which is the interesting case: they cannot both have it.
        /// </summary>
        CarryAnchorTableDef Table()
        {
            var t = Track(ScriptableObject.CreateInstance<CarryAnchorTableDef>());
            t.Id = "carryanchors.twohandtest";
            t.FacingCount = Directions;
            t.Props = new[] { Prop(CarryPropKeys.RodTrail), Prop(CarryPropKeys.Clam) };

            var left = new Vector2[Directions * IdleFrames];
            var right = new Vector2[Directions * IdleFrames];
            for (int d = 0; d < Directions; d++)
                for (int f = 0; f < IdleFrames; f++)
                {
                    right[d * IdleFrames + f] = new Vector2(d, f);
                    left[d * IdleFrames + f] = new Vector2(-d, -f);
                }
            t.Wrists = new[]
            {
                new CarryWristFrames
                { Gait = CharacterGait.Idle, FramesPerDir = IdleFrames, Left = left, Right = right },
            };
            return t;
        }

        static CarryPropAnchors Prop(string key)
        {
            var rows = new CarryAnchorRow[Directions];
            for (int d = 0; d < Directions; d++)
                rows[d] = new CarryAnchorRow
                {
                    Hand = CarryHandSide.Right,
                    Mount = CarryMountKind.Hand,
                    GripOffsetMeters = Vector2.zero,
                    RestOffsetMeters = new Vector2(-99f, -99f),   // reached = the live path failed
                    ItemFacing = d,
                    Behind = false,
                };
            return new CarryPropAnchors { PropKey = key, Rig = "test", ItemScale = 1f, Rows = rows };
        }

        static CatchItem Clam() =>
            new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", FishCategory.Shellfish,
                          0.15f, 3, 0.1f, 0.5f, Freshness.Landed(0d));

        /// <summary>A cod heavy enough that the size rule cradles it — 12 kg is the shipped
        /// <c>AtlanticCod.asset</c>'s own MaxWeightKg, so this is a fish the game really produces.</summary>
        static CatchItem BigCod() =>
            new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish,
                          12f, 40, 0.2f, 0.75f, Freshness.Landed(0d));

        /// <summary>A carried rod: the smallest real <see cref="ICarriable"/> that names a hand-prop
        /// row.</summary>
        Rod SpawnRod()
        {
            var go = Track(new GameObject("Rod"));
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<Rod>();
        }

        private sealed class Rod : MonoBehaviour, ICarriable, ICarryAnchored
        {
            public string DefId => "tool.rod";
            public bool IsCarriable => true;
            public string HandPropKey => CarryPropKeys.RodTrail;
            public Transform Transform => transform;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        // ---- 1. EACH THING IS POSED FROM ITS OWN HAND ----------------------------------------------------

        [UnityTest]
        public IEnumerator ARodAndAFish_AreEachPosedFromTHEIROwnWrist_EveryFacing()
        {
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);
            yield return null;

            Rod rod = SpawnRod();
            Assert.That(_hands.TryPickUp(rod), Is.EqualTo(CarryRefusal.None));
            Assert.That(_hands.TryPutInHand(Clam()), Is.True, "the clam takes the free hand");
            yield return null;

            ICarriable clam = _hands.Slots.Left;
            Assert.That(_hands.Slots.SideOf(rod), Is.EqualTo(CarryHandSide.Right),
                        "the rod asked for the right hand and had it first");
            Assert.That(clam, Is.Not.Null.And.Not.SameAs((ICarriable)rod),
                        "…so the clam is in the left, even though it asked for the right too");

            for (int d = 0; d < Directions; d++)
            {
                _skin.HoldHeading(d * 45f);
                yield return null;

                Assert.That(_skin.FacingRow, Is.EqualTo(d), "precondition: the body is drawn at row d");

                Vector2 rodPin = rod.transform.localPosition;
                Vector2 clamPin = clam.Transform.localPosition;
                int frame = _skin.Frame;

                Assert.That(rodPin.x, Is.EqualTo((float)d).Within(1e-3f),
                            $"d{d}: the rod is not on the RIGHT wrist (that grid is (dir, frame))");
                Assert.That(clamPin.x, Is.EqualTo((float)-d).Within(1e-3f),
                            $"d{d}: the clam is not on the LEFT wrist (that grid is (−dir, −frame)) — " +
                            "either it was posed from the rig's preferred hand anyway, or its anchor was " +
                            "MIRRORED from the right rather than read from the left");
                Assert.That(clamPin.y, Is.EqualTo((float)-frame).Within(1e-3f),
                            $"d{d}: the left pin is not tracking the drawn frame");
            }
        }

        [UnityTest]
        public IEnumerator TheRodDoesNotMove_WhenTheFishArrives()
        {
            // ⭐ THE CONTINUITY LAW, in the picture: while a hand is free, nothing already held moves.
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(90f);          // east — a constant row, so only the ARRIVAL can move it
            yield return null;

            Rod rod = SpawnRod();
            _hands.TryPickUp(rod);
            yield return null;

            Vector2 before = rod.transform.localPosition;
            int frameBefore = _skin.Frame;

            Assert.That(_hands.TryPutInHand(Clam()), Is.True);
            Assert.That(_hands.Slung, Is.Null,
                        "the rod must NOT have slung — a hand was free, which is the whole change");

            _hands.ApplyCarriedPose();       // state it now, without waiting for the cycle to advance
            Assert.That(_skin.Frame, Is.EqualTo(frameBefore),
                        "precondition: the idle cell has not turned over under the comparison");
            Assert.That((((Vector2)rod.transform.localPosition) - before).magnitude,
                        Is.LessThan(1e-4f), "the rod moved when the fish arrived");
        }

        [UnityTest]
        public IEnumerator AThingCarriedALONE_KeepsTheRigsOwnPreferredHand()
        {
            // The shipped single-carry look, unregressed: with one thing held there is no conflict, so
            // the rig's per-facing answer is used exactly as it was before two slots existed.
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);
            yield return null;

            Assert.That(_hands.TryPutInHand(Clam()), Is.True);
            yield return null;
            ICarriable clam = _hands.Carried;

            for (int d = 0; d < Directions; d++)
            {
                _skin.HoldHeading(d * 45f);
                yield return null;
                Assert.That(clam.Transform.localPosition.x, Is.EqualTo((float)d).Within(1e-3f),
                            $"d{d}: carried alone, the clam must sit on the hand the RIG resolved (right)");
            }
        }

        [UnityTest]
        public IEnumerator AnOverSizeFish_RefusesWhileTheRodIsHeld_AndNothingChanges()
        {
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);
            yield return null;

            Rod rod = SpawnRod();
            _hands.TryPickUp(rod);
            yield return null;
            Vector2 before = rod.transform.localPosition;

            Assert.That(_hands.TryPutInHand(BigCod()), Is.False,
                        "a twelve-kilo cod is a two-arm cradle; it will not displace the rod to get them");

            _hands.ApplyCarriedPose();
            Assert.That(_hands.Slots.UsedHands, Is.EqualTo(1), "and nothing moved");
            Assert.That(_hands.Slung, Is.Null, "including the rod, which was NOT slung to make room");
            Assert.That((((Vector2)rod.transform.localPosition) - before).magnitude,
                        Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator AnOverSizeFish_TakesBothHands_WhenSheIsEmptyHanded()
        {
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);
            yield return null;

            Assert.That(_hands.TryPutInHand(BigCod()), Is.True);
            yield return null;

            Assert.That(_hands.Slots.SpansBoth, Is.True, "a cradle commits the pair");
            Assert.That(_hands.Slots.UsedHands, Is.EqualTo(2));
        }

        // ---- 2. THE FILL REACHES THE PICTURE ON THE EVENT EDGE --------------------------------------------

        [UnityTest]
        public IEnumerator StoringAFish_ReachesThePailsPicture_WithNoOneCallingRefresh()
        {
            // ⚠️ THE ONE LINK EDITMODE CANNOT PROVE. The bridge subscribes to FishCaught in OnEnable,
            // which never runs there — so an EditMode test has to call Refresh() itself and would stay
            // green over a bridge that had stopped listening entirely. That is exactly the shape of the
            // reported defect: the hold knew, and the picture did not.
            var go = Track(new GameObject("Pail"));
            go.AddComponent<SpriteRenderer>();

            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(Library(), EmptySprites(), null);

            var pail = go.AddComponent<CarriableBucket>();
            pail.Configure(20, go.GetComponent<SpriteRenderer>(), "container.playtest.bucket");
            go.AddComponent<HoldCatchFillSource>();
            yield return null;

            Assert.That(presenter.ItemCount, Is.EqualTo(0), "an empty pail starts empty");

            Assert.That(_hands.TryPutInHand(Clam()), Is.True);
            Assert.That(_hands.TryGiveCatchTo(pail), Is.True, "the press");
            yield return null;               // the event fired synchronously; this is the presenter's frame

            Assert.That(pail.UsedUnits, Is.EqualTo(1), "the hold took it");
            Assert.That(presenter.ItemCount, Is.EqualTo(1),
                        "…and the picture heard about it, with nobody calling Refresh by hand");
            Assert.That(presenter.Band, Is.Not.EqualTo(CatchFillBand.Empty),
                        "one fish in a pail must never read EMPTY");
        }

        /// <summary>A minimal art table — one species, one kind. Built through the library's own
        /// Configure seam rather than an editor SerializedObject, because a PlayMode assembly must not
        /// reference UnityEditor.</summary>
        CatchItemLibrary Library()
        {
            var library = Track(ScriptableObject.CreateInstance<CatchItemLibrary>());
            library.Configure(
                new[] { new CatchItemLibrary.KindEntry { Kind = "clam", Variants = new Sprite[0] } },
                new[] { new CatchItemLibrary.SpeciesEntry
                        { SpeciesId = "fish.soft_shell_clam", Kind = "clam" } },
                fallbackKind: "clam");
            return library;
        }

        Sprite[] EmptySprites()
        {
            var set = new Sprite[BucketFillPresenter.Facings];
            for (int d = 0; d < set.Length; d++)
                set[d] = Track(Sprite.Create(Track(new Texture2D(4, 4)), new Rect(0, 0, 4, 4),
                                             new Vector2(0.5f, 0.5f), 32f));
            return set;
        }
    }
}
