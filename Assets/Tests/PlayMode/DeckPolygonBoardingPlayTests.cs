using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// M2-37's data half, live: <b>boarding a measured hull lands the player ON her authored deck</b>,
    /// and every heading keeps them there — through the real component lifecycle, the real
    /// <see cref="ControlSwitcher"/> boarding path, and the real drawn-facing read.
    ///
    /// <para>The claim the EditMode maths cannot make on its own is the round trip: the clamp happens in
    /// HULL metres, the player's transform lives in SCREEN metres, and the two are joined by the
    /// artwork's own iso foreshortening. A test that only checked the clamp would pass happily while the
    /// player stood a metre and a half off the pictured hull — which is exactly what the old
    /// un-foreshortened rectangle did at every heading but N/S.</para>
    /// </summary>
    public class DeckPolygonBoardingPlayTests
    {
        const float Iso = 40f;                       // every iso rig bakes here
        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>The lobster boat's shape, in her own metres: a 3.4 × 6.5 m cockpit aft at deck
        /// height, and a raised foredeck forward of the wheelhouse. Built through the very
        /// <see cref="DeckArea.From"/> bake the sidecar importer uses.</summary>
        BoatDeckDef LobsterLikeDeck()
        {
            var def = ScriptableObject.CreateInstance<BoatDeckDef>();
            def.Id = "deck.play_test";
            def.Areas = new[]
            {
                DeckArea.From("cockpit_sole", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.7f, -6f, 0.5f), new Vector3(1.7f, -6f, 0.5f),
                    new Vector3(1.7f, 0.55f, 0.5f), new Vector3(-1.7f, 0.55f, 0.5f),
                }),
                DeckArea.From("foredeck", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.2f, 3.85f, 2.1f), new Vector3(1.2f, 3.85f, 2.1f),
                    new Vector3(1.2f, 6.3f, 2.7f), new Vector3(-1.2f, 6.3f, 2.7f),
                }),
            };
            def.WalkCenter = new Vector2(0f, 0.15f);
            def.WalkHalfExtents = new Vector2(1.7f, 6.15f);
            _spawned.Add(def);
            return def;
        }

        /// <summary>A minimal snap-directional skin: eight 1×1 facings and a 40° bake elevation, which is
        /// all the deck-walk reads off a hull picture (the drawn heading and the artwork's camera).</summary>
        void GiveHerASkin(GameObject boatGo)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _spawned.Add(tex);

            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 32f);
            _spawned.Add(sprite);
            var facings = new Sprite[8];
            for (int i = 0; i < facings.Length; i++) facings[i] = sprite;

            var child = new GameObject("Visual");
            child.transform.SetParent(boatGo.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();

            var directional = boatGo.AddComponent<DirectionalBoatSprite>();
            directional.Configure(facings, sr, zeroHeadingDegrees: 0f, smoothModeSprite: sprite,
                                  mode: DirectionalBoatSprite.RotationMode.SnapDirectional,
                                  facingsAreCounterClockwise: true,      // the iso kits' bake order
                                  bakeElevationDegrees: Iso);
        }

        /// <summary>Is a boat-relative WORLD offset standing on one of her DECK areas? Un-projects it the
        /// way the walk projects it and asks the polygons — with a millimetre of slack, because a clamped
        /// player stands exactly ON the outline, where a crossing test may read either way.</summary>
        static bool OnTheDeck(BoatDeckDef deck, Vector2 worldOffset, float headingDeg, float elevationDeg)
        {
            foreach (DeckArea a in deck.Areas)
            {
                if (a.Kind != DeckAreaKind.Deck) continue;
                float height = DeckAreaMath.HeightAt(a.HeightPlane,
                    DeckAreaMath.WorldToDeck(worldOffset, 0f, headingDeg, elevationDeg));
                Vector2 local = DeckAreaMath.WorldToDeck(worldOffset, height, headingDeg, elevationDeg);
                if (DeckAreaMath.Contains(a.Outline, a.Bounds, local)) return true;
                DeckAreaMath.ClosestPointOnOutline(a.Outline, local, out float sqr);
                if (sqr < 1e-2f) return true;                 // within 10 cm of the outline = on the rail
            }
            return false;
        }

        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Boarding_LandsThePlayerInsideHerAuthoredDeck()
        {
            var playerGo = NewGo("Player", new Vector3(0.5f, 0f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;

            var boatGo = NewGo("Boat", Vector3.zero);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            GiveHerASkin(boatGo);

            BoatDeckDef deck = LobsterLikeDeck();
            BoatDeckAreas.Write(boatGo, deck);

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.deck_polygon_test";
            hull.DisplayName = "Polygon Deck Test";
            _spawned.Add(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dockZone: null, zoneRadius: 3f, disembarkPoint: null);

            yield return null;
            boat.SetHull(hull);

            Assert.IsTrue(sw.TryInteract(), "boarding within reach must land the player on the deck");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);

            yield return null;                       // one Update: the deck-walk steps, clamps, publishes

            float heading = boatGo.GetComponent<DirectionalBoatSprite>().DrawnHeadingDegrees();
            Vector2 offset = (Vector2)playerGo.transform.position - (Vector2)boatGo.transform.position;
            Assert.IsTrue(OnTheDeck(deck, offset, heading, Iso),
                $"boarding put the player at {offset} — off her authored deck (heading {heading}°)");
        }

        [UnityTest]
        public IEnumerator TheClampFollowsTheDrawnHullThroughEveryFacing()
        {
            var playerGo = NewGo("Player", Vector3.zero);
            var deckWalk = playerGo.AddComponent<DeckWalkController>();

            var boatGo = NewGo("Boat", new Vector3(12f, -7f, 0f));
            GiveHerASkin(boatGo);
            BoatDeckDef deck = LobsterLikeDeck();
            BoatDeckAreas.Write(boatGo, deck);

            deckWalk.Bind(boatGo.transform);
            yield return null;

            var directional = boatGo.GetComponent<DirectionalBoatSprite>();
            for (int step = 0; step < 8; step++)
            {
                // Turn her: bow = transform.up, so the drawn facing follows the physics root's rotation.
                boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -step * 45f);
                // …and shove the player well off the deck before the tick, so the clamp has real work.
                playerGo.transform.position = boatGo.transform.position + new Vector3(9f, 9f, 0f);

                yield return null;                   // the facing settles…
                yield return null;                   // …and one full Update of the deck-walk clamps in it

                float heading = directional.DrawnHeadingDegrees();
                Vector2 offset = (Vector2)playerGo.transform.position - (Vector2)boatGo.transform.position;
                Assert.IsTrue(OnTheDeck(deck, offset, heading, Iso),
                    $"facing {step} (drawn {heading:0.#}°): the player ended at {offset}, off the deck");
            }
        }

        [UnityTest]
        public IEnumerator TheProjectionIsActuallyApplied_NotJustTheRotation()
        {
            // The regression guard for the whole point of this slice. Pointing NORTH, the cockpit runs
            // 6 m aft in hull metres but only 6·sin(40°) ≈ 3.86 m of SCREEN. A clamp that rotated without
            // foreshortening would let the player stand ~2 m past the drawn transom — the old behaviour.
            var playerGo = NewGo("Player", Vector3.zero);
            var deckWalk = playerGo.AddComponent<DeckWalkController>();

            var boatGo = NewGo("Boat", Vector3.zero);
            boatGo.transform.rotation = Quaternion.identity;            // bow due North
            GiveHerASkin(boatGo);
            BoatDeckDef deck = LobsterLikeDeck();
            BoatDeckAreas.Write(boatGo, deck);

            deckWalk.Bind(boatGo.transform);
            deckWalk.SnapTo(new Vector2(0f, -20f));                     // hard astern, way off the boat
            yield return null;

            // 6 m of deck astern draws as 6·sin(40°) of screen, and standing 0.5 m up on the sole lifts
            // her back UP-screen by 0.5·cos(40°) — both terms, both measured.
            float astern = -((Vector2)playerGo.transform.position).y;
            Assert.AreEqual(6f * Mathf.Sin(Iso * Mathf.Deg2Rad) - 0.5f * Mathf.Cos(Iso * Mathf.Deg2Rad),
                            astern, 0.05f,
                            "the transom must sit where the ¾ camera DRAWS it, lifted by the sole's height");
            Assert.Less(astern, 6f, "an un-foreshortened clamp would have put her at the full 6 m");
        }

        [UnityTest]
        public IEnumerator TheStanceCarriesHerOwnBoxAndWhereTheAnglerStands()
        {
            var playerGo = NewGo("Player", Vector3.zero);
            var deckWalk = playerGo.AddComponent<DeckWalkController>();

            var boatGo = NewGo("Boat", Vector3.zero);
            GiveHerASkin(boatGo);
            BoatDeckDef deck = LobsterLikeDeck();
            BoatDeckAreas.Write(boatGo, deck);

            deckWalk.Bind(boatGo.transform);
            deckWalk.SnapTo(new Vector2(0.4f, -1f));
            yield return null;

            Assert.IsTrue(DeckStance.TryGet(out DeckStanceState stance));
            Assert.AreEqual(deck.WalkHalfExtents.x, stance.DeckHalfExtents.x, 1e-3f,
                "the stance publishes THIS hull's box, not the greybox rectangle");
            Assert.AreEqual(deck.WalkHalfExtents.y, stance.DeckHalfExtents.y, 1e-3f);
            Assert.AreEqual(deckWalk.DeckLocalPosition.x, stance.AnglerDeckPosition.x, 1e-3f,
                "…and where the angler actually stands in it");
            Assert.AreEqual(deckWalk.DeckLocalPosition.y, stance.AnglerDeckPosition.y, 1e-3f);

            deckWalk.enabled = false;
            Assert.IsFalse(DeckStance.IsActive, "stepping off a deck clears the stance (dock parity)");
        }

        [UnityTest]
        public IEnumerator AnUnmeasuredHullKeepsTheGreyboxRectangle()
        {
            // Absence is data: no BoatDeckAreas at all must behave exactly as it did before this slice.
            var playerGo = NewGo("Player", Vector3.zero);
            var deckWalk = playerGo.AddComponent<DeckWalkController>();

            var boatGo = NewGo("Boat", Vector3.zero);
            deckWalk.Bind(boatGo.transform);
            deckWalk.SnapTo(new Vector2(0f, -9f));
            yield return null;

            Assert.IsNull(deckWalk.Deck);
            Assert.AreEqual(-deckWalk.DeckHalfExtents.y, ((Vector2)playerGo.transform.position).y, 1e-3f,
                "the un-foreshortened rectangle, unchanged");
            Assert.IsTrue(DeckStance.TryGet(out DeckStanceState stance));
            Assert.AreEqual(deckWalk.DeckHalfExtents, stance.DeckHalfExtents);
        }
    }
}
