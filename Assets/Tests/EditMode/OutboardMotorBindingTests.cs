using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The outboard binds to the hull SKIN as data — the append <see cref="BoatHullSkinner.Rig"/> was
    /// designed for. Under test: the all-or-nothing gate on the motor sheets, the installer in both
    /// directions (a hull that gains an engine, a hull that loses one), the twin fit's second engine, and
    /// the oars-vs-motor exclusion.
    ///
    /// <para><b>Why the exclusion matters.</b> The two overlays' sorting bands OVERLAP: the oars take
    /// hull+1/+2, and so does the motor's lower layer whenever it draws over the hull. A visual binding both
    /// would z-fight — and worse, only at some headings, because the lower band flips across the stern-away
    /// arc. Nothing authored does it, which is exactly why it needs a test: it is the kind of thing that
    /// ships silently the day someone adds an auxiliary outboard to a rowing hull.</para>
    ///
    /// <para>Everything is built in-code. The helm PULL (rather than a pushed copy) is covered here too,
    /// since it is the reason <see cref="BoatController.Steer"/> now exists.</para>
    /// </summary>
    public class OutboardMotorBindingTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
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

        /// <summary>A skiff visual at the real sheet shape: 8 headings, a 64-cell rock grid, and 72-cell
        /// motor sheets (8 headings × 9 steer columns).</summary>
        private BoatVisualDef MakeSkiffVisual(OutboardMotorLayer.MotorFit fit = OutboardMotorLayer.MotorFit.Single,
                                              bool motor = true)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = "visual.test_skiff";
            v.Facings = MakeSprites("Hull", 8);
            v.RockFrameCount = 8;
            v.RockGrid = MakeSprites("Rock", 64);
            v.MotorColumnCount = OutboardMotorMath.SteerColumns;
            v.MotorVariant = OutboardMotorLayer.MotorVariant.Sport;
            v.MotorFit = fit;
            v.MotorLower = motor ? MakeSprites("Lower", 72) : System.Array.Empty<Sprite>();
            v.MotorUpper = motor ? MakeSprites("Upper", 72) : System.Array.Empty<Sprite>();
            v.MotorRockRollDegrees = 3.8f;
            v.MotorRockPitchOffsetMeters = 0.0147f;
            v.MotorRockHeavePixels = 1.5f;
            _spawned.Add(v);
            return v;
        }

        /// <summary>The PUNT's visual at her real sheet shape — same 8×8 hull/rock and same 9 steer columns
        /// as a skiff, but her own paint build, her own rock lean, and her own ±32 degrees of authority.</summary>
        private BoatVisualDef MakePuntVisual(OutboardMotorLayer.MotorVariant variant
                                                 = OutboardMotorLayer.MotorVariant.Basic)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = "visual.test_punt";
            v.Facings = MakeSprites("PuntHull", 8);
            v.RockFrameCount = 8;
            v.RockGrid = MakeSprites("PuntRock", 64);
            v.MotorColumnCount = OutboardMotorMath.SteerColumns;
            v.MotorMaxSteerDegrees = 32f;                              // her rig, NOT the skiffs' 30
            v.MotorVariant = variant;
            v.MotorFit = OutboardMotorLayer.MotorFit.Single;           // she never hangs a twin
            v.MotorLower = MakeSprites("PuntLower", 72);
            v.MotorUpper = MakeSprites("PuntUpper", 72);
            v.MotorRockRollDegrees = 4.2f;
            v.MotorRockPitchOffsetMeters = 0.016f;
            v.MotorRockHeavePixels = 1.5f;
            _spawned.Add(v);
            return v;
        }

        private BoatHullDef MakeHull(string id, BoatVisualDef visual)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = id;
            h.Visual = visual;
            h.Sprite = MakeSprite(id + "Plain");
            h.Propulsion = PropulsionType.Engine;
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, SpriteRenderer sr, BoatController boat) MakeBoat(BoatHullDef startHull)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var sr = go.AddComponent<SpriteRenderer>();
            boat.SetHull(startHull);
            return (go, sr, boat);
        }

        // ---- the gate -----------------------------------------------------------------------

        [Test]
        public void MotorSheets_AreBothOrNeither_AndMustBeComplete()
        {
            var v = MakeSkiffVisual();
            Assert.IsTrue(v.HasMotor(), "72 + 72 = 8 headings × 9 steer columns each");

            v.MotorUpper = System.Array.Empty<Sprite>();
            Assert.IsFalse(v.HasMotor(), "a leg with no cowl is a broken engine — both or neither");

            v = MakeSkiffVisual();
            v.MotorLower[17] = null;
            Assert.IsFalse(v.HasMotor(), "a hole would index a stale cell mid-swivel");

            v = MakeSkiffVisual();
            v.MotorColumnCount = 8;
            Assert.IsFalse(v.HasMotor(), "72 ≠ 8 × 8 — a mismatched grid draws the wrong steer angle");

            var noMotor = MakeSkiffVisual(motor: false);
            Assert.IsTrue(noMotor.HasFullCompass(), "the compass stands on its own…");
            Assert.IsFalse(noMotor.HasMotor(), "…with no engine drawn (the dory, the fishing boat)");
        }

        [Test]
        public void MotorNeedsAFullCompass_BecauseItIndexesTheHullsHeadingRow()
        {
            var v = MakeSkiffVisual();
            v.Facings[4] = null;
            Assert.IsFalse(v.HasMotor(), "no compass to pick a row from = no motor");
        }

        // ---- the installer ------------------------------------------------------------------

        [Test]
        public void SkinningAPoweredHull_BoltsOnTheEngine_UnderTheHullsVisual()
        {
            var visual = MakeSkiffVisual();
            var hull = MakeHull("boat.sport_skiff", visual);
            var (go, sr, boat) = MakeBoat(hull);

            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsNotNull(rig.Motor, "a visual binding motor sheets installs the outboard");
            Assert.IsTrue(rig.Motor.IsWired, "…fully wired: sheets, renderers, counts");
            Assert.AreSame(go, rig.Motor.gameObject, "the layer rides the physics ROOT, like every other layer");
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Sport, rig.Motor.Variant);
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, rig.Motor.Fit);

            // The renderers must hang UNDER the counter-rotated visual child, or they'd have to re-derive
            // the hull's screen-alignment themselves (and would be stomped if they tried).
            var lower = rig.Visual.Find(BoatHullSkinner.LowerMotorAChildName);
            var upper = rig.Visual.Find(BoatHullSkinner.UpperMotorAChildName);
            Assert.IsNotNull(lower, "engine A's lower layer parents to the hull's visual child");
            Assert.IsNotNull(upper, "engine A's upper layer too");
            Assert.AreEqual(Vector3.zero, lower.localPosition,
                "shared pivot ⇒ localPosition zero. Pin by PIVOT, never by corners");
            Assert.AreEqual(Vector3.zero, upper.localPosition);
        }

        [Test]
        public void SingleFit_HangsOneEngine_TwinFitHangsTwo()
        {
            var single = MakeHull("boat.sport_skiff", MakeSkiffVisual(OutboardMotorLayer.MotorFit.Single));
            var (goS, srS, boatS) = MakeBoat(single);
            var rigS = BoatHullSkinner.ApplyHull(goS, srS, single, boatS);

            Assert.IsNull(rigS.Visual.Find(BoatHullSkinner.LowerMotorBChildName),
                "a single fit is one engine on the centreline — no second engine");
            Assert.IsTrue(rigS.Motor.IsWired);

            var twin = MakeHull("boat.sport_skiff_twin", MakeSkiffVisual(OutboardMotorLayer.MotorFit.Twin));
            var (goT, srT, boatT) = MakeBoat(twin);
            var rigT = BoatHullSkinner.ApplyHull(goT, srT, twin, boatT);

            Assert.AreEqual(OutboardMotorLayer.MotorFit.Twin, rigT.Motor.Fit);
            Assert.IsNotNull(rigT.Visual.Find(BoatHullSkinner.LowerMotorBChildName),
                "the twin's second engine costs no art — the same sheets, blitted twice");
            Assert.IsNotNull(rigT.Visual.Find(BoatHullSkinner.UpperMotorBChildName));
            Assert.IsTrue(rigT.Motor.IsWired, "…and IsWired demands both engines' renderers for a twin fit");
        }

        [Test]
        public void SwappingTwinToSingle_TakesTheSecondEngineOff()
        {
            // The transition the owner will actually make with F: sport twin → (wrap) → dory → … and back.
            // A leftover engine B would hang in the water off a boat that doesn't have it.
            var hull = MakeHull("boat.sport_skiff", MakeSkiffVisual(OutboardMotorLayer.MotorFit.Twin));
            var (go, sr, boat) = MakeBoat(hull);
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);
            Assert.IsNotNull(rig.Visual.Find(BoatHullSkinner.LowerMotorBChildName), "precondition: two engines");

            hull.Visual = MakeSkiffVisual(OutboardMotorLayer.MotorFit.Single);
            rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsNull(rig.Visual.Find(BoatHullSkinner.LowerMotorBChildName),
                "the second engine is removed, not left hanging off a single-engine transom");
            Assert.IsNull(rig.Visual.Find(BoatHullSkinner.UpperMotorBChildName));
        }

        [Test]
        public void SwappingToAHullWithNoMotor_TakesTheEngineOff()
        {
            // Skiff → dory. The engine must not stay bolted to a rowboat.
            var skiff = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (go, sr, boat) = MakeBoat(skiff);
            BoatHullSkinner.ApplyHull(go, sr, skiff, boat);
            Assert.IsNotNull(go.GetComponent<OutboardMotorLayer>(), "precondition: the skiff has its engine");

            var dory = MakeHull("boat.dory", MakeSkiffVisual(motor: false));
            var rig = BoatHullSkinner.ApplyHull(go, sr, dory, boat);

            Assert.IsNull(rig.Motor);
            Assert.IsNull(go.GetComponent<OutboardMotorLayer>(), "the layer goes…");
            Assert.IsNull(rig.Visual.Find(BoatHullSkinner.LowerMotorAChildName), "…and so do its renderers");
            Assert.IsNull(rig.Visual.Find(BoatHullSkinner.UpperMotorAChildName));
        }

        [Test]
        public void SwappingToAPlainHull_StripsTheEngineWithTheSkin()
        {
            // Skiff → Punt (no facings at all): RemoveSkin must take the motor too, or the layer idles
            // pointing at renderers that went down with the visual child.
            var skiff = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (go, sr, boat) = MakeBoat(skiff);
            BoatHullSkinner.ApplyHull(go, sr, skiff, boat);

            var punt = MakeHull("boat.punt", null);
            BoatHullSkinner.ApplyHull(go, sr, punt, boat);

            Assert.IsNull(go.GetComponent<OutboardMotorLayer>(),
                "an unskinned hull strips the whole rig — engine included");
            Assert.IsTrue(sr.enabled, "…and the plain rotating picture comes back");
        }

        [Test]
        public void ApplyingTwice_ReusesTheEngine_InsteadOfStackingASecondOne()
        {
            var hull = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (go, sr, boat) = MakeBoat(hull);

            BoatHullSkinner.ApplyHull(go, sr, hull, boat);
            hull.Visual = MakeSkiffVisual();
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.AreEqual(1, go.GetComponents<OutboardMotorLayer>().Length, "one engine, not a pile");
            Assert.AreEqual(1, CountChildrenNamed(rig.Visual, BoatHullSkinner.LowerMotorAChildName));
        }

        private static int CountChildrenNamed(Transform parent, string name)
        {
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) n++;
            return n;
        }

        // ---- oars vs motor: the overlapping sorting bands ------------------------------------

        [Test]
        public void OarsAndMotor_OverlapTheirSortingBands_WhichIsWhyTheyAreExclusive()
        {
            // The concrete reason for the exclusion, asserted rather than asserted-in-a-comment: at a
            // heading where the lower layer draws OVER the hull, it lands on the very orders the oars use.
            const int hullOrder = 1;
            int portOar = hullOrder + 1;
            int starOar = hullOrder + 2;

            int lowerOver = OutboardMotorMath.SortingOrder(
                hullOrder, OutboardMotorMath.MotorPart.Lower, headingRow: 0, headingCount: 8, isFarEngine: true);
            int lowerOverNear = OutboardMotorMath.SortingOrder(
                hullOrder, OutboardMotorMath.MotorPart.Lower, headingRow: 0, headingCount: 8, isFarEngine: false);

            Assert.AreEqual(portOar, lowerOver, "the engine leg lands exactly on the port oar's order at N");
            Assert.AreEqual(starOar, lowerOverNear, "…and on the starboard oar's");
        }

        [Test]
        public void AVisualBindingBoth_IsFlaggedAsConflicting()
        {
            var v = MakeSkiffVisual();
            Assert.IsFalse(v.HasConflictingOverlays(), "a powered hull with no oars is fine");

            v.OarColumnCount = 10;
            v.OarPort = MakeSprites("Port", 80);
            v.OarStar = MakeSprites("Star", 80);
            Assert.IsTrue(v.HasOarSheets());
            Assert.IsTrue(v.HasConflictingOverlays(),
                "oars AND an outboard on one hull is an authoring mistake, not a boat");
        }

        [Test]
        public void SkinningAConflictingVisual_DropsTheMotor_AndKeepsTheOars()
        {
            var visual = MakeSkiffVisual();
            visual.OarColumnCount = 10;
            visual.OarPort = MakeSprites("Port", 80);
            visual.OarStar = MakeSprites("Star", 80);
            var hull = MakeHull("boat.frankenboat", visual);
            var (go, sr, boat) = MakeBoat(hull);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("binds BOTH oar sheets and motor"));
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            Assert.IsNull(rig.Motor, "the motor is dropped rather than left to z-fight the oars");
            Assert.IsNotNull(rig.Oars, "the oars stand — they're the older, load-bearing rig");
        }

        // ---- the helm: PULL, not push --------------------------------------------------------

        [Test]
        public void Helm_IsPulledFromTheBoat_NotPushedIn()
        {
            // Why BoatController.Steer exists. A pushed copy is only ever as good as the last system that
            // remembered to write it — the dropped-state blind spot #205 fixed for the oars.
            var hull = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (go, sr, boat) = MakeBoat(hull);
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            boat.SetControl(0f, 0.75f);
            Assert.AreEqual(0.75f, boat.Steer, 0.0001f, "the controller exposes the wheel…");
            Assert.AreEqual(0.75f, rig.Motor.Helm, 0.0001f,
                "…and the engine sources it itself — nobody has to remember to push it");

            boat.SetControl(0f, -1f);
            Assert.AreEqual(-1f, rig.Motor.Helm, 0.0001f, "hard a-port, with no intervening write");
        }

        [Test]
        public void Steer_IsClamped_AndClearedByStop()
        {
            var hull = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (_, _, boat) = MakeBoat(hull);

            boat.SetControl(0f, 5f);
            Assert.AreEqual(1f, boat.Steer, 0.0001f, "over-driven helm pins at hard-over, it doesn't run off");

            boat.Stop();
            Assert.AreEqual(0f, boat.Steer, 0.0001f,
                "Stop() drops the helm — a boat left near shore must not sit hard-over");
        }

        [Test]
        public void AnUnmannedHelm_ReadsDeadAhead_EvenWithAStaleWheel()
        {
            // The #205 blind spot, in its motor form: a player who disembarks mid-turn must not leave the
            // engine pinned hard-over on an empty skiff.
            var hull = MakeHull("boat.sport_skiff", MakeSkiffVisual());
            var (go, sr, boat) = MakeBoat(hull);
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);

            boat.SetControl(0f, 1f);
            Assert.AreEqual(1f, rig.Motor.Helm, 0.0001f, "precondition: hard over while driving");

            boat.enabled = false;                       // the player steps off, mid-turn
            Assert.IsFalse(rig.Motor.IsHelmManned);
            Assert.AreEqual(0f, rig.Motor.Helm, 0.0001f,
                "nobody at the helm = nobody steering: the engine comes back to dead ahead");
            Assert.AreEqual(1f, boat.Steer, 0.0001f,
                "…without the SIM state being rewritten — the gate is on the read (rule 5: visual-only)");
        }

        // ---- the rock coupling ---------------------------------------------------------------

        // ---- the steer authority ------------------------------------------------------------

        [Test]
        public void SteerAuthority_ComesFromTheVisual_SoTheSkiffsAndThePuntCanDiffer()
        {
            // The skiffs bake ±30 across their 9 columns, the punt ±32 across hers. It was a bare serialized
            // 30 on the layer with no field behind it, so a hull could not say what its own art holds.
            var skiffRig = MakeBoat(MakeHull("boat.sport_skiff", MakeSkiffVisual()));
            var skiffMotor = BoatHullSkinner.ApplyHull(skiffRig.go, skiffRig.sr, skiffRig.boat.Hull, skiffRig.boat).Motor;
            Assert.IsNotNull(skiffMotor);
            Assert.AreEqual(30f, skiffMotor.MaxSteerDegrees, 0.001f,
                "the skiffs keep ±30 — the punt's arrival must not have moved them");

            var puntRig = MakeBoat(MakeHull("boat.punt", MakePuntVisual()));
            var puntMotor = BoatHullSkinner.ApplyHull(puntRig.go, puntRig.sr, puntRig.boat.Hull, puntRig.boat).Motor;
            Assert.IsNotNull(puntMotor);
            Assert.AreEqual(32f, puntMotor.MaxSteerDegrees, 0.001f,
                "the punt's ±32 must survive the trip from her BoatVisualDef to the layer that draws her — " +
                "a hard-coded 30 here would report her engine 2° shy of the hard-over her sheet holds");
        }

        [Test]
        public void ANonPositiveAuthority_KeepsTheDefault_RatherThanFreezingTheEngineDeadAhead()
        {
            // Mirrors ConfigureRock's contract. A half-authored visual (0 left in the field) must degrade to
            // the skiffs' ±30, NOT to an authority of 0 — which ColumnForSteerDegrees reads as "this engine
            // can never leave dead ahead", i.e. a silently frozen motor.
            var v = MakePuntVisual();
            v.MotorMaxSteerDegrees = 0f;

            var rig = MakeBoat(MakeHull("boat.half_authored", v));
            var motor = BoatHullSkinner.ApplyHull(rig.go, rig.sr, rig.boat.Hull, rig.boat).Motor;

            Assert.IsNotNull(motor);
            Assert.AreEqual(OutboardMotorMath.MaxSteerDegrees, motor.MaxSteerDegrees, 0.001f,
                "0 degrades to the serialized default, not to a motor that cannot steer");
        }

        [Test]
        public void ThePunt_WearsHerOwnEngine_AndNeverASecondOne()
        {
            var rig = MakeBoat(MakeHull("boat.punt", MakePuntVisual(OutboardMotorLayer.MotorVariant.Upgraded)));
            var motor = BoatHullSkinner.ApplyHull(rig.go, rig.sr, rig.boat.Hull, rig.boat).Motor;

            Assert.IsNotNull(motor, "the punt's outboard is bolted on");
            Assert.IsTrue(motor.IsWired);
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, motor.Fit,
                "the punt is single-engine: her kit ships no twin, and faking one is not on");
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Upgraded, motor.Variant,
                "the wired rig must say which paint build it is wearing");

            var visual = rig.go.transform.Find(BoatHullSkinner.VisualChildName);
            Assert.IsNotNull(visual.Find(BoatHullSkinner.LowerMotorAChildName), "her one engine's leg");
            Assert.IsNotNull(visual.Find(BoatHullSkinner.UpperMotorAChildName), "…and its cowl");
            Assert.IsNull(visual.Find(BoatHullSkinner.LowerMotorBChildName),
                "there is no second engine on a punt's transom");
            Assert.IsNull(visual.Find(BoatHullSkinner.UpperMotorBChildName));
        }

        [Test]
        public void ThePunt_Rows_Nothing_SoHerOverlaysNeverConflict()
        {
            // She is powered, not rowed. If her visual ever grew oar sheets they would z-fight the engine leg
            // (hull+1/+2 on both), and the skinner would silently drop the MOTOR — an engine-less punt.
            var v = MakePuntVisual();
            Assert.IsTrue(v.HasMotor(), "precondition: she has an engine");
            Assert.IsFalse(v.HasOarSheets(), "…and no oars");
            Assert.IsFalse(v.HasConflictingOverlays(),
                "so nothing is dropped: the punt is authored as powered, not both");
        }

        [Test]
        public void RockAmplitudes_ComeFromTheVisual_SoTwoHullsCanLeanDifferently()
        {
            // These are per-hull ART FACTS (the console is heavier and stiffer than the sport), so they must
            // ride the data. One component serves both hulls; consts here would make them lean identically.
            var console = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(console);
            Assert.AreEqual(3.4f, console.MotorRockRollDegrees, 0.001f,
                "the type default is the console's roll (the heavier, stiffer hull)");
            Assert.AreEqual(1.3f, console.MotorRockHeavePixels, 0.001f);

            var sport = MakeSkiffVisual();
            Assert.AreEqual(3.8f, sport.MotorRockRollDegrees, 0.001f, "the sport hull rolls more…");
            Assert.Greater(sport.MotorRockHeavePixels, console.MotorRockHeavePixels, "…and heaves more");
            Assert.Less(sport.MotorRockRollDegrees, 5f,
                "…but still less than the dory's 5° — she is a bigger boat than a dory");
        }
    }
}
