using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A carried thing tracking the hand, over real frames</b> — the half of the hand-prop table that
    /// EditMode cannot show.
    ///
    /// <para>EditMode owns the rules (<c>CarryAnchorTableTests</c>) and the branch table
    /// (<c>CarryHandsAnchorTests</c>), and it gets at the drawn cell by stating it, because a plain
    /// MonoBehaviour never runs <c>LateUpdate</c> there. What is left is the only thing that actually
    /// fails in play: whether the pose FOLLOWS the picture rather than merely starting out agreeing with
    /// it. Two claims:
    /// <list type="bullet">
    ///   <item><b>It follows the facing.</b> Turn the fisher and the pin moves to the row for the
    ///   direction she is now DRAWN at — not the one she was.</item>
    ///   <item><b>It follows the frame.</b> Stand still and the idle cycle advances underneath her; the
    ///   pin must move with it, because the wrist does. This is the claim the rest pin cannot make, and
    ///   the reason the live path exists at all.</item>
    /// </list></para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail — it KILLS the editor with no results XML. Every assertion is on state. An
    /// <c>AudioListener</c> is spawned because a listener-less play scene logs a warning on EVERY frame
    /// and a full suite writes it a million times.</para>
    /// </summary>
    public class CarryAnchorPlayTests
    {
        const int Directions = 8;
        const int IdleFrames = 6;

        readonly List<Object> _spawned = new();

        CarryHands _hands;
        IsoCharacterSprite _skin;
        SpriteRenderer _body;
        CharacterVisualDef _visual;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            var listener = Track(new GameObject("Listener"));
            listener.AddComponent<AudioListener>();

            _visual = Track(ScriptableObject.CreateInstance<CharacterVisualDef>());
            _visual.Id = "visual.carrytest";
            _visual.FacingCount = Directions;
            _visual.IdleFrameCount = IdleFrames;
            _visual.IdleFramesPerSecond = 30f;      // fast, so the cycle turns over within a few frames
            _visual.IdleSheet = Sheet(IdleFrames);

            var go = Track(new GameObject("Fisher"));
            _body = go.AddComponent<SpriteRenderer>();
            _body.sortingOrder = 40;
            _skin = go.AddComponent<IsoCharacterSprite>();
            _skin.Configure(_visual);
            _hands = go.AddComponent<CarryHands>();
            _hands.ConfigureCarryAnchors(Table());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }

        static Sprite[] Sheet(int frames)
        {
            // Distinct textures per cell: IsoCharacterSprite skips the assignment when the cell has not
            // changed, so identical sprites would make "did the frame advance" unobservable.
            var set = new Sprite[Directions * frames];
            for (int i = 0; i < set.Length; i++)
                set[i] = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4),
                                       new Vector2(0.5f, 0.1f), 32f);
            return set;
        }

        /// <summary>A table whose clam wrist is <c>(dir, frame)</c> — so the pin reads back as the pair
        /// of indices the body is drawing, and a wrong one names itself in the failure.</summary>
        CarryAnchorTableDef Table()
        {
            var t = Track(ScriptableObject.CreateInstance<CarryAnchorTableDef>());
            t.Id = "carryanchors.playtest";
            t.FacingCount = Directions;

            var rows = new CarryAnchorRow[Directions];
            for (int d = 0; d < Directions; d++)
                rows[d] = new CarryAnchorRow
                {
                    Hand = CarryHandSide.Right,
                    Mount = CarryMountKind.Hand,
                    GripOffsetMeters = Vector2.zero,
                    RestOffsetMeters = new Vector2(-99f, -99f),
                    ItemFacing = d,
                    Behind = d == 0,
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

        static CatchItem Clam() =>
            new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", FishCategory.Shellfish,
                          0.15f, 3, 0.1f, 0.5f, Freshness.Landed(0d));

        Vector2 Pin => _hands.Carried.Transform.localPosition;

        // ---- the two claims --------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThePin_FOLLOWS_TheFacing_AsTheFisherTurns()
        {
            // The helm's own seam: state the heading rather than measuring motion, so the test turns her
            // without also making her walk (which would change the gait and the wrist grid under it).
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);
            yield return null;

            Assert.That(_hands.TryPutInHand(Clam()), Is.True);
            yield return null;

            for (int d = 0; d < Directions; d++)
            {
                _skin.HoldHeading(d * 45f);
                yield return null;      // one LateUpdate for the body, one for the hands (order 100)

                Assert.That(_skin.FacingRow, Is.EqualTo(d),
                            "precondition: the body is drawn at the row this heading names");
                Assert.That(Pin.x, Is.EqualTo((float)d).Within(1e-3f),
                            $"the pin did not follow the fisher round to d{d} — it is still reading " +
                            $"row {Pin.x}");
            }
        }

        [UnityTest]
        public IEnumerator ThePin_FOLLOWS_TheFrame_AsTheIdleCycleTurnsOver()
        {
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(90f);          // east, so the row index is a constant 2 throughout
            yield return null;

            _hands.TryPutInHand(Clam());
            yield return null;

            int startFrame = _skin.Frame;
            Assert.That(Pin.y, Is.EqualTo((float)startFrame).Within(1e-3f), "the pin starts on the cell");

            // ⚠️ Wait on the STATE, not on a frame count — frames are not time, and a slow editor frame
            // would otherwise decide whether this passes.
            const int Guard = 600;
            int spun = 0;
            while (_skin.Frame == startFrame && spun++ < Guard) yield return null;

            Assert.That(spun, Is.LessThan(Guard),
                        "the idle cycle never advanced a frame — the fixture, not the feature");
            yield return null;   // let the hands re-state the pose against the new cell

            Assert.That(Pin.y, Is.EqualTo((float)_skin.Frame).Within(1e-3f),
                        "the wrist moved on and the carried thing stayed behind — this is precisely the " +
                        "failure the rest pin has and the live path exists to fix");
            Assert.That(Pin.x, Is.EqualTo(2f).Within(1e-3f), "and it stayed on the east row");
        }

        [UnityTest]
        public IEnumerator TheCarriedThing_RidesTheBodysLiveSortingBand_EveryFrame()
        {
            _skin.HoldSpeed(0f);
            _skin.HoldHeading(0f);           // row 0 — the row the table marks BEHIND
            yield return null;

            _hands.TryPutInHand(Clam());
            yield return null;

            SpriteRenderer carried = _hands.Carried.Transform.GetComponent<SpriteRenderer>();
            Assert.That(carried.sortingOrder, Is.LessThan(_body.sortingOrder), "behind at N");

            // Move the BODY's band, as YSortSprite does every frame from world Y, and the carried sprite
            // must move with it — no authored order anywhere (ADR 0032).
            _body.sortingOrder = 137;
            yield return null;

            Assert.That(carried.sortingOrder, Is.EqualTo(136),
                        "the carried sprite did not re-derive from the body's new band");

            _skin.HoldHeading(45f);          // row 1 — over
            yield return null;
            Assert.That(carried.sortingOrder, Is.EqualTo(138), "over at NE, still off the live band");
        }
    }
}
