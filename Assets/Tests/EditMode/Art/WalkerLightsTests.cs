using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A LIGHT ON HIS HEAD</b> — world-lighting PR 3 (owner, 2026-09-03: <i>"a latern and a spotlight"</i>),
    /// asserted as RULES rather than as pixels: who owns the switch, who owns the one decor lamp, what
    /// the two presets mean, and (owner, 2026-11-16) how the headlamp goes from his pocket onto his head.
    /// The plates are the owner's walk; these are the things a plate cannot show you.
    ///
    /// <para><b>⭐ The fixture this file exists for is <see cref="TheHeadlampWritesTheDecorLamp_WhenTheBoatsStandOff"/>.</b>
    /// The lit-decor path has ONE global lamp, and PR 3 makes every <c>BoatSpotlight</c> stand off it while
    /// he is walking his own beam. Standing them off is only half a handover — and it was the half that
    /// was written. With nothing put in their place the five globals keep whatever a boat wrote last, so
    /// the trees in front of him are lit by a beam frozen at a moored dory's bow, or by nothing at all in a
    /// region with no boat in it. Nothing errors, nothing warns, and no render test on the boat's beam
    /// moves: it is exactly the shape of defect that ships. This asserts the other half.</para>
    /// </summary>
    public class WalkerLightsTests
    {
        private static readonly int IdPos     = Shader.PropertyToID("_BoatLightPos");
        private static readonly int IdDir     = Shader.PropertyToID("_BoatLightDir");
        private static readonly int IdColor   = Shader.PropertyToID("_BoatLightColor");
        private static readonly int IdParams  = Shader.PropertyToID("_BoatLightParams");
        private static readonly int IdParams2 = Shader.PropertyToID("_BoatLightParams2");

        private readonly List<Object> _spawned = new List<Object>();
        private Vector4 _posBefore, _dirBefore, _paramsBefore, _params2Before;
        private Color _colorBefore;

        [SetUp]
        public void SetUp()
        {
            // ⚠️ These five are SHARED global state, and a fixture that leaves them moved poisons every
            // later lighting test in the run — the lesson the shade-receiver fixture is chartered for.
            _posBefore = Shader.GetGlobalVector(IdPos);
            _dirBefore = Shader.GetGlobalVector(IdDir);
            _colorBefore = Shader.GetGlobalColor(IdColor);
            _paramsBefore = Shader.GetGlobalVector(IdParams);
            _params2Before = Shader.GetGlobalVector(IdParams2);
            WalkerLights.ResetInstallForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            WalkerLights.ResetInstallForTests();
            InteractActorProbe.Clear();
            Shader.SetGlobalVector(IdPos, _posBefore);
            Shader.SetGlobalVector(IdDir, _dirBefore);
            Shader.SetGlobalColor(IdColor, _colorBefore);
            Shader.SetGlobalVector(IdParams, _paramsBefore);
            Shader.SetGlobalVector(IdParams2, _params2Before);
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>A headlamp on its own object, configured the way <c>WalkerLights.Build</c> configures
        /// his, still in the pocket. (EditMode runs no <c>Awake</c>/<c>OnEnable</c>, which is why nothing
        /// here relies on either: <see cref="Headlamp.Light"/> resolves itself lazily.)</summary>
        private Headlamp PocketHeadlamp(Vector2 at, Vector2 facing)
        {
            var go = new GameObject("headlamp");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.up = new Vector3(facing.x, facing.y, 0f);
            var lamp = go.AddComponent<Headlamp>();
            lamp.Configure(WalkerLights.HeadlampLiftMetres);
            return lamp;
        }

        /// <summary>The same lamp, put on and switched on.</summary>
        private Headlamp LitHeadlamp(Vector2 at, Vector2 facing)
        {
            Headlamp lamp = PocketHeadlamp(at, facing);
            lamp.SetOn(true);
            return lamp;
        }

        private SpriteRenderer GiveItADrawnLamp(Headlamp lamp)
        {
            var drawn = new GameObject("HeadlampFitting").AddComponent<SpriteRenderer>();
            _spawned.Add(drawn.gameObject);
            drawn.transform.SetParent(lamp.transform, false);
            var field = typeof(Headlamp).GetField("_fitting",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Headlamp keeps its drawn lamp in _fitting");
            field.SetValue(lamp, drawn);
            return drawn;
        }

        private static void PutHimIn(ControlMode mode) =>
            InteractActorProbe.Set(InteractActor.For(Vector2.zero, Vector2.up, mode));

        // ---- from the pocket to his head ----------------------------------------------------------

        /// <summary>
        /// <b>It starts in his pocket.</b> A new lamp is neither worn nor lit, and nothing of it burns: the
        /// player begins with a bare head and a dark lamp, which is the face the #1259 fix gave him.
        /// </summary>
        [Test]
        public void TheHeadlamp_StartsInHisPocket_DarkAndNotWorn()
        {
            Headlamp lamp = PocketHeadlamp(Vector2.zero, Vector2.up);
            Assert.IsFalse(lamp.IsWorn, "in the pocket");
            Assert.IsFalse(lamp.IsOn, "and dark");
            Assert.IsFalse(lamp.Light.enabled, "nothing burns from a pocket");
        }

        /// <summary>
        /// <b>The drawn lamp follows WEARING, not the switch.</b> Worn, it is on his head lit or unlit; in
        /// the pocket nothing is drawn; it still needs him on his feet with a mesh head drawn to wear it.
        /// </summary>
        [Test]
        public void TheDrawnLamp_ShowsWhileWorn_LitOrNot()
        {
            Assert.IsTrue(Headlamp.FittingShows(headDrawn: true, live: true, worn: true), "worn: on his head");
            Assert.IsFalse(Headlamp.FittingShows(headDrawn: true, live: true, worn: false), "pocketed: no lamp on his head");
            Assert.IsFalse(Headlamp.FittingShows(headDrawn: false, live: true, worn: true), "no drawn head: nothing to wear it");
            Assert.IsFalse(Headlamp.FittingShows(headDrawn: true, live: false, worn: true), "at a wheel: the lamp stands down");
            Assert.IsFalse(Headlamp.FittingShows(headDrawn: false, live: false, worn: false));
        }

        /// <summary>
        /// <b>Put on, switched, taken off.</b> Putting it on lights it; switching it off leaves it on his
        /// head; taking it off puts it out and in the pocket. Lighting a pocketed lamp puts it on first —
        /// a lit lamp is always a worn one.
        /// </summary>
        [Test]
        public void PutOn_Switch_TakeOff_AndALitLampIsAlwaysWorn()
        {
            Headlamp lamp = PocketHeadlamp(Vector2.zero, Vector2.up);

            lamp.PutOn();
            Assert.IsTrue(lamp.IsWorn); Assert.IsTrue(lamp.IsOn, "out of the pocket and on, lit");
            Assert.IsTrue(lamp.Light.enabled);

            lamp.Toggle();
            Assert.IsTrue(lamp.IsWorn, "switched off, it stays on his head");
            Assert.IsFalse(lamp.IsOn); Assert.IsFalse(lamp.Light.enabled);

            lamp.Toggle();
            Assert.IsTrue(lamp.IsOn, "and a second tap lights it again");

            lamp.TakeOff();
            Assert.IsFalse(lamp.IsWorn, "back in the pocket");
            Assert.IsFalse(lamp.IsOn, "and out as it came off");
            Assert.IsFalse(lamp.Light.enabled);

            lamp.Toggle();
            Assert.IsTrue(lamp.IsWorn && lamp.IsOn, "the switch on a pocketed lamp puts it on, lit");

            lamp.SetWorn(false);
            Assert.IsFalse(lamp.IsOn, "a pocketed lamp is never lit");
            lamp.SetOn(true);
            Assert.IsTrue(lamp.IsWorn, "lighting it puts it on his head");
            lamp.SetOn(false);
            Assert.IsTrue(lamp.IsWorn, "switching it off does not pocket it");
        }

        /// <summary>The drawn lamp leaves his head on the same call that pockets it or stands it down,
        /// not one LateUpdate later — and switching the beam off does NOT take it off his head.</summary>
        [Test]
        public void PocketingOrStandingDown_HidesTheDrawnLampAtOnce_SwitchingOffDoesNot()
        {
            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            SpriteRenderer drawn = GiveItADrawnLamp(lamp);

            drawn.enabled = true;
            lamp.SetOn(false);
            Assert.IsTrue(drawn.enabled, "switched off: still worn, still drawn");
            Assert.AreSame(Headlamp.SpriteFor(lamp.DrawnView, lit: false), drawn.sprite, "with an unlit lens");

            lamp.SetOn(true);
            Assert.AreSame(Headlamp.SpriteFor(lamp.DrawnView, lit: true), drawn.sprite, "lit: the lens lights");

            lamp.TakeOff();
            Assert.IsFalse(drawn.enabled, "pocketed: off his head at once");

            lamp.PutOn();
            drawn.enabled = true;
            lamp.SetLive(false);
            Assert.IsFalse(drawn.enabled, "stood down: the drawn lamp goes too");
        }

        /// <summary>
        /// <b>One key, a press, a tap and a hold.</b> With the lamp in his pocket the press puts it on at
        /// once. Worn, a tap (released before the hold time) switches the beam, and a hold pockets it — and
        /// the release after a hold does nothing more.
        /// </summary>
        [Test]
        public void TheLampKey_PressPutsOn_TapSwitches_HoldPockets()
        {
            const float hold = GameConfig.DefaultWalkerHeadlampHoldSeconds;
            bool answered = true;

            Assert.AreEqual(WalkerLights.LampKeyAction.PutOn,
                WalkerLights.ReadLampKey(true, true, false, 0f, hold, worn: false, ref answered), "pocket: the press puts it on");
            Assert.AreEqual(WalkerLights.LampKeyAction.None,
                WalkerLights.ReadLampKey(false, true, false, hold * 2f, hold, worn: true, ref answered),
                "that press is spent: holding it on does not pocket the lamp it just put on");
            Assert.AreEqual(WalkerLights.LampKeyAction.None,
                WalkerLights.ReadLampKey(false, false, true, hold * 2f, hold, worn: true, ref answered), "nor does its release");

            Assert.AreEqual(WalkerLights.LampKeyAction.None,
                WalkerLights.ReadLampKey(true, true, false, 0f, hold, worn: true, ref answered), "worn: a press waits");
            Assert.AreEqual(WalkerLights.LampKeyAction.Toggle,
                WalkerLights.ReadLampKey(false, false, true, hold * .5f, hold, worn: true, ref answered), "a tap switches");

            WalkerLights.ReadLampKey(true, true, false, 0f, hold, worn: true, ref answered);
            Assert.AreEqual(WalkerLights.LampKeyAction.None,
                WalkerLights.ReadLampKey(false, true, false, hold * .5f, hold, worn: true, ref answered), "still short of a hold");
            Assert.AreEqual(WalkerLights.LampKeyAction.Pocket,
                WalkerLights.ReadLampKey(false, true, false, hold, hold, worn: true, ref answered), "a hold pockets it");
            Assert.AreEqual(WalkerLights.LampKeyAction.None,
                WalkerLights.ReadLampKey(false, false, true, hold * 1.5f, hold, worn: false, ref answered),
                "the release after a hold does not put it straight back on");

            Assert.AreEqual(WalkerLights.LampKeyAction.Toggle,
                WalkerLights.ReadLampKey(true, false, true, 0f, hold, worn: true, ref answered),
                "a press and release inside one frame is a tap");
        }

        /// <summary>The key's actions land on the lamp as the rule says.</summary>
        [Test]
        public void TheLampKeyActions_DriveTheLamp()
        {
            Headlamp lamp = PocketHeadlamp(Vector2.zero, Vector2.up);
            WalkerLights.Apply(lamp, WalkerLights.LampKeyAction.PutOn);
            Assert.IsTrue(lamp.IsWorn && lamp.IsOn);
            WalkerLights.Apply(lamp, WalkerLights.LampKeyAction.Toggle);
            Assert.IsTrue(lamp.IsWorn && !lamp.IsOn);
            WalkerLights.Apply(lamp, WalkerLights.LampKeyAction.Pocket);
            Assert.IsFalse(lamp.IsWorn || lamp.IsOn);
            WalkerLights.Apply(lamp, WalkerLights.LampKeyAction.None);
            Assert.IsFalse(lamp.IsWorn || lamp.IsOn);
        }

        // ---- the drawn lamp -----------------------------------------------------------------------

        /// <summary>
        /// <b>The eight views.</b> Facing the viewer the lamp is seen from the front; turning to screen
        /// right it goes front-quarter, side, back-quarter; facing away it is the back. Left mirrors
        /// right. Only the views from behind hide the lens.
        /// </summary>
        [Test]
        public void TheView_FollowsTheHeadsFacing_AndOnlyTheBackHidesTheLens()
        {
            Assert.AreEqual(Headlamp.View.Front, Headlamp.ViewOfAngle(0f));
            Assert.AreEqual(Headlamp.View.FrontQuarterRight, Headlamp.ViewOfAngle(45f));
            Assert.AreEqual(Headlamp.View.SideRight, Headlamp.ViewOfAngle(90f));
            Assert.AreEqual(Headlamp.View.BackQuarterRight, Headlamp.ViewOfAngle(135f));
            Assert.AreEqual(Headlamp.View.Back, Headlamp.ViewOfAngle(180f));
            Assert.AreEqual(Headlamp.View.Back, Headlamp.ViewOfAngle(-180f));
            Assert.AreEqual(Headlamp.View.FrontQuarterLeft, Headlamp.ViewOfAngle(-45f));
            Assert.AreEqual(Headlamp.View.SideLeft, Headlamp.ViewOfAngle(-90f));
            Assert.AreEqual(Headlamp.View.BackQuarterLeft, Headlamp.ViewOfAngle(-135f));

            foreach (Headlamp.View v in Enum.GetValues(typeof(Headlamp.View)))
                Assert.AreEqual(v != Headlamp.View.Back && v != Headlamp.View.BackQuarterRight && v != Headlamp.View.BackQuarterLeft,
                    Headlamp.LensShows(v), v + ": only from behind is the lens hidden by his head");
        }

        /// <summary>
        /// The view is read from the head's facing through the figure's own projection: a facet turned so
        /// the figure's +y faces each screen direction gives the matching view.
        /// </summary>
        [Test]
        public void TheView_IsReadThroughTheFacetsProjection()
        {
            var facet = new GameObject("facet").transform;
            _spawned.Add(facet.gameObject);
            // A foreshortened ground: figure y (depth) shrinks on screen, as the iso projection does.
            facet.localScale = new Vector3(1f, .5f, 1f);
            (float yaw, Headlamp.View view)[] cases =
            {
                (180f, Headlamp.View.Front), (90f, Headlamp.View.SideRight), (0f, Headlamp.View.Back),
                (-90f, Headlamp.View.SideLeft), (135f, Headlamp.View.FrontQuarterRight), (-45f, Headlamp.View.BackQuarterLeft),
            };
            foreach (var (yaw, view) in cases)
            {
                // Figure +y turned by yaw about the figure's up (z); 0 points away from the viewer (screen up).
                float r = yaw * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0f);
                Assert.AreEqual(view, Headlamp.ViewOf(forward, facet), "yaw " + yaw);
            }
        }

        /// <summary>
        /// <b>The beam leaves the way his drawn head faces</b> (the owner, 2026-11-25: "headlamp should always
        /// follow players sprite"). In all eight facings, through a foreshortened facet, the beam's ground
        /// direction is the drawn head's, so the view the lamp is drawn in and the way its cone points agree.
        /// </summary>
        [Test]
        public void TheBeamPointsTheWayTheDrawnHeadFaces_InAllEightFacings()
        {
            var facet = new GameObject("facet").transform;
            _spawned.Add(facet.gameObject);
            facet.localScale = new Vector3(1f, .5f, 1f);
            for (int k = 0; k < 8; k++)
            {
                float yaw = k * 45f;
                float r = yaw * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0f);
                Assert.IsTrue(Headlamp.DrawnGroundDirection(forward, facet, out Vector2 aim), "yaw " + yaw);
                Assert.AreEqual(1f, aim.magnitude, 1e-5f, "yaw " + yaw);
                // On screen: the figure's +x is east, its +y north (shrunk, but never turned round).
                Assert.AreEqual(Mathf.Sign(Mathf.Round(forward.x * 1000f)), Mathf.Sign(Mathf.Round(aim.x * 1000f)), "east-west, yaw " + yaw);
                Assert.AreEqual(Mathf.Sign(Mathf.Round(forward.y * 1000f)), Mathf.Sign(Mathf.Round(aim.y * 1000f)), "north-south, yaw " + yaw);
                // The view drawn and the way the beam points are one reading: the beam's angle from
                // straight toward the viewer (screen down), positive to screen right, gives the same view.
                float fromViewer = Mathf.Atan2(aim.x, -aim.y) * Mathf.Rad2Deg;
                Assert.AreEqual(Headlamp.ViewOf(forward, facet), Headlamp.ViewOfAngle(fromViewer), "yaw " + yaw);
            }
            Assert.IsFalse(Headlamp.DrawnGroundDirection(Vector3.forward, facet, out _),
                "a head pointing straight up has no ground direction, and the beam keeps its last aim");
        }

        /// <summary>
        /// <b>The lens pixel is the sprite's pivot</b>, in every view that shows a lens, lit and unlit — so
        /// placing the sprite at the light's origin puts the lens exactly there. The lens is the only pixel
        /// the switch changes, and left views are right views mirrored.
        /// </summary>
        [Test]
        public void TheLensPixel_IsThePivot_InEveryViewThatShowsIt()
        {
            foreach (Headlamp.View v in Enum.GetValues(typeof(Headlamp.View)))
            {
                Sprite lit = Headlamp.SpriteFor(v, true), unlit = Headlamp.SpriteFor(v, false);
                Assert.IsNotNull(lit, v.ToString()); Assert.IsNotNull(unlit, v.ToString());
                Assert.AreEqual(Headlamp.ArtPixelsPerMetre, lit.pixelsPerUnit, "drawn on the figure's art grid");
                Vector2 p = lit.pivot;
                Assert.AreEqual(.5f, p.x - Mathf.Floor(p.x), 1e-4f, v + ": pivot on a pixel centre (x)");
                Assert.AreEqual(.5f, p.y - Mathf.Floor(p.y), 1e-4f, v + ": pivot on a pixel centre (y)");
                int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
                Color32 litPivot = lit.texture.GetPixel(px, py), unlitPivot = unlit.texture.GetPixel(px, py);
                int changed = 0;
                Color32[] a = lit.texture.GetPixels32(), b = unlit.texture.GetPixels32();
                for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) changed++;
                if (Headlamp.LensShows(v))
                {
                    Assert.AreEqual(Headlamp.LensLitColour, litPivot, v + ": the lit lens is the pivot pixel");
                    Assert.AreEqual(Headlamp.LensUnlitColour, unlitPivot, v + ": the unlit lens is the pivot pixel");
                    Assert.AreEqual(1, changed, v + ": the lens is the only pixel the switch changes");
                }
                else
                {
                    Assert.AreEqual(0, changed, v + ": no lens to light from behind");
                    Assert.Greater(litPivot.a, 0, v + ": the strap crosses the back of his head at the pivot");
                }
            }
            AssertMirrored(Headlamp.View.FrontQuarterRight, Headlamp.View.FrontQuarterLeft);
            AssertMirrored(Headlamp.View.SideRight, Headlamp.View.SideLeft);
            AssertMirrored(Headlamp.View.BackQuarterRight, Headlamp.View.BackQuarterLeft);
        }

        private static void AssertMirrored(Headlamp.View right, Headlamp.View left)
        {
            Texture2D r = Headlamp.SpriteFor(right, true).texture, l = Headlamp.SpriteFor(left, true).texture;
            Assert.AreEqual(r.width, l.width); Assert.AreEqual(r.height, l.height);
            for (int y = 0; y < r.height; y++)
            for (int x = 0; x < r.width; x++)
                Assert.AreEqual((Color32)r.GetPixel(x, y), (Color32)l.GetPixel(r.width - 1 - x, y), left + " mirrors " + right);
        }

        /// <summary>No near-black block: the drawn lamp is a strap, a coloured housing and a pale lens, and
        /// nothing in it is the dark 38,42,46 of the old housing that read as a black item on his face.</summary>
        [Test]
        public void TheDrawnLamp_IsNotABlackBlock()
        {
            Sprite front = Headlamp.SpriteFor(Headlamp.View.Front, false);
            int housing = 0, total = 0;
            foreach (Color32 c in front.texture.GetPixels32())
            {
                if (c.a == 0) continue;
                total++;
                Assert.Greater(c.r + c.g + c.b, 3 * 45, "no near-black pixel in the drawn lamp");
                if (c.Equals(Headlamp.HousingColour) || c.Equals(Headlamp.HousingTopColour)) housing++;
            }
            Assert.Greater(housing, 0, "a housing you can see");
            Assert.LessOrEqual(front.texture.height, 2, "a lamp on a strap, two art pixels tall — not a block over his eyes");
            Assert.Greater(total, housing, "on a strap");
        }

        // ---- the switch ---------------------------------------------------------------------------

        /// <summary>
        /// <b>⭐ THE PARTITION, over every member of <see cref="ControlMode"/>.</b> The charter asked that
        /// only one of the two beams answer the key at a time. PR 3 delivers that by CONSTRUCTION rather
        /// than by arbitrating — the walker answers for <c>OnFoot</c>, and the boat answers when the helm
        /// slot says the player is aboard it, which <c>ControlSwitcher</c> makes true for exactly
        /// <c>Aboard</c> and <c>OnDeck</c>. A claim like that is worth exactly as much as the test that
        /// walks the enum.
        ///
        /// <para><b>Why over EVERY member and not over the three interesting ones.</b>
        /// <see cref="ControlMode"/> is append-only Core contract. The next mode appended to it either
        /// belongs to nobody (a beam that answers no key) or to both (two beams on one press), and both
        /// are silent. <c>Driving</c> was the last mode appended and it belongs to NEITHER by design: a
        /// road vehicle's headlights are the road fleet's, not his.</para>
        /// </summary>
        [Test]
        public void HisBeamAndTheBoatsNeverBothAnswerTheKey_OverEveryControlMode()
        {
            foreach (ControlMode mode in Enum.GetValues(typeof(ControlMode)))
            {
                PutHimIn(mode);

                bool his = WalkerLights.WalksHisOwnBeam;
                // The boat's half, stated as ControlSwitcher establishes it: the helm slot holds the
                // player's boat at the wheel and on the deck, and nowhere else.
                bool theBoats = mode == ControlMode.Aboard || mode == ControlMode.OnDeck;

                Assert.IsFalse(his && theBoats,
                    "both beams would answer the key in " + mode + " — one press, two lamps");
                Assert.AreEqual(mode == ControlMode.OnFoot, his,
                    "the walker's beam must answer in OnFoot and nowhere else; " + mode + " disagreed");
            }

            PutHimIn(ControlMode.Driving);
            Assert.IsFalse(WalkerLights.WalksHisOwnBeam,
                "behind a wheel the beam is the vehicle's problem, not his");
        }

        /// <summary>
        /// With nobody published at all — a bare art scene, a fixture, the frames before the player is
        /// spawned — he owns nothing. <see cref="InteractActorProbe"/>'s <c>Current</c> is <c>default</c>
        /// then, whose context is <see cref="InteractContext.None"/>; a <c>default</c> that happened to
        /// read as OnFoot would hand him the key in the main menu.
        /// </summary>
        [Test]
        public void WithNoActorPublished_HeOwnsNothing()
        {
            InteractActorProbe.Clear();
            Assert.IsFalse(WalkerLights.WalksHisOwnBeam);
        }

        // ---- the one decor lamp -------------------------------------------------------------------

        /// <summary>
        /// <b>⭐ THE OTHER HALF OF "ONE PUBLISHER".</b> While he owns the decor lamp the boats stand off
        /// it — so he must WRITE it, or the trees hold whatever a boat wrote last. Driven exactly that
        /// way: a boat's numbers go into the globals first, he takes ownership, and the globals must
        /// then be HIS.
        /// </summary>
        [Test]
        public void TheHeadlampWritesTheDecorLamp_WhenTheBoatsStandOff()
        {
            // What a boat last wrote: a lamp somewhere else entirely, pointing the other way.
            var stale = new Vector4(-40f, 12f, 2.5f, 0f);
            Shader.SetGlobalVector(IdPos, stale);
            Shader.SetGlobalVector(IdDir, new Vector4(0f, -1f, 0f, 0f));

            Headlamp lamp = LitHeadlamp(new Vector2(7f, 3f), Vector2.right);
            WalkerLights.SetOwnsDecorLightForTests(true);

            Assert.IsTrue(lamp.PublishDecorLampIfOwned(),
                "he owns the lamp and his beam is lit — he must publish");

            Vector4 pos = Shader.GetGlobalVector(IdPos);
            Vector4 dir = Shader.GetGlobalVector(IdDir);

            Assert.AreNotEqual(stale, pos,
                "the decor lamp is still the boat's — the trees are lit by a beam that is not there");
            Assert.AreEqual(7f, pos.x, 1e-4f, "the lamp is where he is standing");
            Assert.AreEqual(3f, pos.y, 1e-4f);
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, pos.z, 1e-4f,
                "and its height is his brow — the height the water's relief reads");
            Assert.AreEqual(1f, dir.x, 1e-3f, "pointing where he is looking");
            Assert.AreEqual(0f, dir.y, 1e-3f);
            Assert.Greater(Shader.GetGlobalVector(IdParams).x, 0f,
                "and burning: a zero intensity is how a lamp says it is not lit");
        }

        /// <summary>
        /// The negative control, and it asserts its OWN premise: when he does not own the lamp he must
        /// not touch it, or the boats and the walker would fight over it every frame instead of handing
        /// it between them. Without the premise assertion this would also pass if the publish were simply
        /// broken.
        /// </summary>
        [Test]
        public void WhenHeDoesNotOwnTheDecorLamp_HeDoesNotTouchIt()
        {
            Headlamp lamp = LitHeadlamp(new Vector2(7f, 3f), Vector2.right);

            WalkerLights.SetOwnsDecorLightForTests(true);
            Assert.IsTrue(lamp.PublishDecorLampIfOwned(),
                "premise: this same lamp DOES publish when it owns the singleton");
            WalkerLights.SetOwnsDecorLightForTests(false);

            var boats = new Vector4(-40f, 12f, 2.5f, 0f);
            Shader.SetGlobalVector(IdPos, boats);

            Assert.IsFalse(lamp.PublishDecorLampIfOwned(), "not his — he writes nothing");
            Assert.AreEqual(boats, Shader.GetGlobalVector(IdPos),
                "and the boat's lamp is exactly where the boat left it");
        }

        /// <summary>
        /// An unlit beam publishes nothing even while he owns the flag — the decor lamp cannot be driven
        /// by a lamp that is switched off, worn or pocketed.
        /// </summary>
        [Test]
        public void AnUnlitHeadlamp_PublishesNothing()
        {
            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            lamp.SetOn(false);
            WalkerLights.SetOwnsDecorLightForTests(true);
            Assert.IsTrue(lamp.IsWorn, "premise: switched off on his head");
            Assert.IsFalse(lamp.PublishDecorLampIfOwned());
            lamp.TakeOff();
            Assert.IsFalse(lamp.PublishDecorLampIfOwned(), "nor from the pocket");
        }

        /// <summary>
        /// <b>ONE PACKING.</b> The decor globals and the beam's water-bridge state must be the same
        /// numbers, because they are the same lamp: his beam cannot light a spruce unlike it lights the
        /// sea he is pointing it over. This is what keeps the two publishing routes from drifting apart
        /// the way two hand-written copies of one packing always do.
        /// </summary>
        [Test]
        public void TheDecorLampAndHisWaterLight_AreTheSameNumbers()
        {
            Headlamp lamp = LitHeadlamp(new Vector2(-2f, 5f), new Vector2(0f, -1f));
            WalkerLights.SetOwnsDecorLightForTests(true);

            Assert.IsTrue(lamp.TryGetWaterLight(out WaterLightState water), "his beam lights water too");
            Assert.IsTrue(lamp.PublishDecorLampIfOwned());

            Vector4 pos = Shader.GetGlobalVector(IdPos);
            Vector4 prm = Shader.GetGlobalVector(IdParams);

            Assert.AreEqual(water.LampWorld.x, pos.x, 1e-4f);
            Assert.AreEqual(water.LampWorld.y, pos.y, 1e-4f);
            Assert.AreEqual(water.LampHeightMeters, pos.z, 1e-4f);
            Assert.AreEqual(water.Intensity, prm.x, 1e-4f);
            Assert.AreEqual(water.Range, prm.y, 1e-4f);
            Assert.AreEqual(water.CosHalfAngle, prm.z, 1e-4f);
            Assert.AreEqual(water.CosInnerAngle, prm.w, 1e-4f);
        }

        // ---- the two lamps ------------------------------------------------------------------------

        /// <summary>
        /// The lantern is a FLAME and the headlamp is a BEAM, and the presets have to say so: the one
        /// visible flicker in the library against a steady battery lamp, a radial pool against the only
        /// cone in the library, and a throw that goes further than the pool precisely because it is aimed.
        /// </summary>
        [Test]
        public void TheLanternIsAFlame_AndTheHeadlampIsABeam()
        {
            LightPresets.Config lantern = LightPresets.For(LightPresets.Kind.Lantern);
            LightPresets.Config headlamp = LightPresets.For(LightPresets.Kind.Headlamp);

            Assert.AreEqual(SceneLight.LightShape.Radial, lantern.Shape, "a lantern pools around him");
            Assert.AreEqual(SceneLight.LightShape.Cone, headlamp.Shape, "a headlamp is thrown");

            Assert.Greater(lantern.FlickerAmount, 0f, "a wick behind glass breathes");
            Assert.AreEqual(0f, headlamp.FlickerAmount, "a battery lamp does not");

            Assert.Greater(lantern.Color.r, lantern.Color.b, "flame is warm");
            Assert.Greater(headlamp.Color.b, lantern.Color.b, "an LED is not");

            float lanternReach = LightPresets.ReachMetres(LightPresets.Kind.Lantern);
            float headlampReach = LightPresets.ReachMetres(LightPresets.Kind.Headlamp);
            Assert.Greater(headlampReach, lanternReach,
                "the cone concentrates what the radial spreads — that is what he switches it on FOR");
            Assert.Greater(lanternReach, 0f);
        }

        /// <summary>
        /// Both lamps ride at a height ON HIM, and the beam rides higher than the lantern — his brow above
        /// the sling on his back. The heights are what the bloom hangs at (#733) and what the cast shadows
        /// are thrown from, so a lamp left at ground level would pool correctly and glow at his feet.
        /// </summary>
        [Test]
        public void LegacyMountFallbacksRemainOnTheCarrier()
        {
            Assert.Greater(WalkerLights.LanternLiftMetres, 0f, "the lantern is not on the ground");
            Assert.Greater(WalkerLights.HeadlampLiftMetres, WalkerLights.LanternLiftMetres,
                "his brow is above the sling on his back");
            Assert.Less(WalkerLights.HeadlampLiftMetres, 2f, "and both are on a person, not on a post");

            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, lamp.Light.BloomLiftMetres, 1e-4f,
                "the lit lens hangs at his brow");
            Assert.AreEqual(WalkerLights.HeadlampLiftMetres, lamp.Light.LampHeightMeters, 1e-4f,
                "and its shadows are thrown from there");
            Assert.AreEqual(SceneLight.LightShape.Cone, lamp.Light.Shape);
            // The angle comes from the preset library, not from a constant on the walker: one place it is
            // written, and LightPresetsTests' shape guard can assert it there.
            Assert.IsTrue(LightPresets.IsDirected(LightPresets.Kind.Headlamp),
                "the library must declare the headlamp a beam, or its cone is undeclared");
            Assert.AreEqual(LightPresets.ConeHalfDegrees(LightPresets.Kind.Headlamp),
                lamp.Light.ConeHalfAngle, 1e-4f);
        }

        /// <summary>
        /// Stepping aboard and back off again gives him the lamp he HAD — worn and lit. <c>SetLive</c>
        /// stands the whole lamp down off his feet, but it must not clear the switch or pocket the lamp — a
        /// light he has to put on and turn on again is a light he stops trusting.
        /// </summary>
        [Test]
        public void SteppingAboardAndBackOff_GivesHimTheLampHeHad()
        {
            Headlamp lamp = LitHeadlamp(Vector2.zero, Vector2.up);
            Assert.IsTrue(lamp.IsOn);

            lamp.SetLive(false);
            Assert.IsTrue(lamp.IsOn, "the switch is his; the liveness is the mode's");
            Assert.IsTrue(lamp.IsWorn, "and it stays on his head");
            Assert.IsFalse(lamp.Light.enabled, "but nothing of his burns while he is at a wheel");

            lamp.SetLive(true);
            Assert.IsTrue(lamp.Light.enabled, "and it comes back lit, without a second press");
        }

        /// <summary>The lens offset is the owner's to tune, and the ship defaults put it forward of and
        /// above the head's mid point — on the forehead, not over the eyes.</summary>
        [Test]
        public void TheLensSitsForwardAndAboveTheHeadMid_AndIsTunable()
        {
            Assert.Greater(GameConfig.DefaultWalkerHeadlampLensForwardMetres, 0f, "out on the face, not inside the head");
            Assert.Greater(GameConfig.DefaultWalkerHeadlampLensUpMetres, 0f, "above the eyes");
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            GameConfig old = GameServices.Config;
            try
            {
                config.WalkerHeadlampLensForwardMetres = .03f;
                config.WalkerHeadlampLensUpMetres = .2f;
                GameServices.Config = config;
                Assert.AreEqual(new Vector3(0f, .03f, .2f), Headlamp.LensOffsetHeadLocal(), "read from the live config");
                GameServices.Config = null;
                Assert.AreEqual(new Vector3(0f, GameConfig.DefaultWalkerHeadlampLensForwardMetres, GameConfig.DefaultWalkerHeadlampLensUpMetres),
                    Headlamp.LensOffsetHeadLocal(), "and the ship default with none wired");
            }
            finally { GameServices.Config = old; }
        }
    }
}
