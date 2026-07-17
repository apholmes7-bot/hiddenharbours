using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for THE PILOTABLE FLEET — the five hulls the owner cycles through at the helm.
    ///
    /// <para>Unlike the in-code logic guards, this reads the REAL committed assets off disk, because the
    /// failure this catches is a STALE ASSET, not a broken algorithm. Builder-generated assets go stale (a
    /// re-slice changes sprite sub-asset ids and a whole compass silently turns to None; a stat tweak in C#
    /// doesn't reach the asset until someone re-runs the builder). That failure is invisible in code review
    /// and shows up as an invisible boat, so it is asserted here.</para>
    ///
    /// <para><b>The speed assertions are the design contract.</b> They restate the owner's targets — console
    /// ≈3.8–4.0, sport ≈4.6, twin ≈5.6 m/s — against the terminal speed BoatController's force model
    /// actually produces, so nobody can tune EnginePower "to taste" and quietly leave the ladder. The
    /// PlayMode sibling measures the same hulls on real physics; this one holds the algebra, which is where
    /// a bad number gets introduced.</para>
    /// </summary>
    public class PilotableFleetContentTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        // BoatController's force assembly, restated (the private consts it applies to the Def's design
        // units). If these ever drift from BoatController, the PlayMode speed test fails and points here.
        const float ForceFeelScale = 0.01f;      // BoatController.ForceFeelScale
        const float LinearDamping = 0.2f;        // the rigidbody damping BoatController.Awake sets

        /// <summary>
        /// Terminal speed under full ahead throttle, from the hull's own stats. The rigidbody's
        /// linearDamping is the term a naive EnginePower/ForwardDrag ratio drops — and it is not small (on
        /// the console it is over half the resistance), which is why this is derived rather than eyeballed.
        /// </summary>
        static float TerminalSpeed(BoatHullDef h)
            => (h.EnginePower * ForceFeelScale)
             / (h.ForwardDrag * ForceFeelScale + (h.MassKg / 100f) * LinearDamping);

        static BoatHullDef Hull(string file)
        {
            var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
            Assert.IsNotNull(h, $"{DataBoats}/{file}.asset is missing — the pilotable fleet needs it. Run " +
                                "the cove builder (it authors the hull assets).");
            return h;
        }

        static BoatVisualDef Visual(string file)
        {
            var v = AssetDatabase.LoadAssetAtPath<BoatVisualDef>($"{DataBoats}/Visuals/{file}.asset");
            Assert.IsNotNull(v, $"{DataBoats}/Visuals/{file}.asset is missing — run Hidden Harbours ▸ Art ▸ " +
                                "Build Boat Visual Defs.");
            return v;
        }

        /// <summary>The five hulls the picker cycles, in cycle order.</summary>
        static string[] FleetFiles => new[] { "Dory", "FishingSkiff", "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" };

        // ---- the fleet exists, and is wired ---------------------------------------------------

        [Test]
        public void EveryPilotableHull_Exists_AndHasAPicture()
        {
            foreach (var file in FleetFiles)
            {
                var h = Hull(file);
                Assert.IsNotNull(h.Visual,
                    $"{file}: no Visual — this hull has no directional skin, and its fallback Sprite is " +
                    "empty, so it would sail INVISIBLE. Re-run Build Boat Visual Defs, then the cove builder.");
                Assert.IsTrue(h.Visual.HasFullCompass(),
                    $"{file}: its Visual '{h.Visual.Id}' has {h.Visual.HeadingCount}/8 facings — a re-slice " +
                    "has gone stale. Re-run Build Boat Visual Defs.");
                Assert.AreEqual(8, h.Visual.HeadingCount, $"{file}: the project's compass is 8-way");
                Assert.AreEqual(0f, h.Visual.ZeroHeadingDegrees, 0.0001f,
                    $"{file}: facing element 0 is NORTH — the project's bearing convention");
            }
        }

        [Test]
        public void FleetIds_AreStable_AndUnique()
        {
            var expected = new Dictionary<string, string>
            {
                { "Dory", "boat.dory" },
                { "FishingSkiff", "boat.fishing_skiff" },   // UN-ORPHANED, not replaced: ids are append-only
                { "ConsoleSkiff", "boat.console_skiff" },
                { "SportSkiff", "boat.sport_skiff" },
                { "SportSkiffTwin", "boat.sport_skiff_twin" },
            };

            var seen = new HashSet<string>();
            foreach (var kv in expected)
            {
                var h = Hull(kv.Key);
                Assert.AreEqual(kv.Value, h.Id,
                    $"{kv.Key}: ids are STABLE and append-only (CLAUDE.md §5) — renaming one strands saves " +
                    "and any asset pointing at it");
                Assert.IsTrue(seen.Add(h.Id), $"duplicate hull id '{h.Id}'");
                Assert.IsFalse(string.IsNullOrWhiteSpace(h.DisplayName),
                    $"{kv.Key}: the picker shows DisplayName on screen — it can't be blank");
            }
        }

        // ---- the skiffs' stats: the design contract ------------------------------------------

        [Test]
        public void TheSkiffs_HitTheirDesignedTerminalSpeeds()
        {
            // The owner's targets. Console = a workboat; sport = its faster glass sister; twin = the second
            // engine's worth. Tuned via EnginePower on the assets — no code needed to move these.
            Assert.AreEqual(3.9f, TerminalSpeed(Hull("ConsoleSkiff")), 0.1f,
                "the console is a WORKBOAT: 3.8–4.0 m/s");
            Assert.AreEqual(4.64f, TerminalSpeed(Hull("SportSkiff")), 0.1f,
                "the sport skiff is the fast sister: ≈4.6 m/s");
            Assert.AreEqual(5.63f, TerminalSpeed(Hull("SportSkiffTwin")), 0.1f,
                "the twin is the fastest thing afloat here: ≈5.6 m/s");
        }

        [Test]
        public void TheSpeedLadder_IsMonotonic_AndInsideTheSpraySheetsFrame()
        {
            float fishing = TerminalSpeed(Hull("FishingSkiff"));
            float console = TerminalSpeed(Hull("ConsoleSkiff"));
            float sport = TerminalSpeed(Hull("SportSkiff"));
            float twin = TerminalSpeed(Hull("SportSkiffTwin"));

            Assert.Less(fishing, console, "the little fishing skiff is the slowest powered boat");
            Assert.Less(console, sport, "the workboat gives way to its glass sister");
            Assert.Less(sport, twin, "…and the second engine is worth something, or why fit it");

            // BowSprayGrading frames speed over 1.7 → 6 m/s. The dory could never reach the top of that
            // sheet; these hulls are the reason that art was drawn, so they must live inside the frame —
            // over it and the spray would just clip flat at max.
            foreach (var (name, v) in new[] { ("fishing", fishing), ("console", console), ("sport", sport), ("twin", twin) })
            {
                Assert.Greater(v, 1.7f, $"{name}: below the spray sheet's floor — it would never throw spray");
                Assert.LessOrEqual(v, 6f, $"{name}: past the spray sheet's 6 m/s ceiling — the spray would " +
                                          "clip flat and the hull would outrun its own art");
            }
        }

        [Test]
        public void TheSkiffs_AreSevenMetres_AndFramedOnTheCameraLadder()
        {
            foreach (var file in new[] { "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" })
            {
                var h = Hull(file);
                Assert.AreEqual(7.0f, h.LengthMeters, 0.001f,
                    $"{file}: art fact — the 244 px hull cell at PPU 32, less its padding");
                // The ladder is 14 m of framing at 4.5 m of boat, 17 at 6 m → ~18–19 at 7 m.
                Assert.GreaterOrEqual(h.CameraWorldHeightMeters, 18f, $"{file}: a 7 m boat needs more water");
                Assert.LessOrEqual(h.CameraWorldHeightMeters, 20f, $"{file}: …but not a whole ocean");
            }
        }

        [Test]
        public void ConsoleIsTheWorkboat_AndSportIsTheGlassSister()
        {
            var console = Hull("ConsoleSkiff");
            var sport = Hull("SportSkiff");

            Assert.Greater(console.MassKg, sport.MassKg, "the console is the heavier boat");
            Assert.Greater(console.HoldUnits, sport.HoldUnits,
                "…and the point of a workboat is that it brings the catch home");
            Assert.Less(console.SeakeepingLiveliness, sport.SeakeepingLiveliness,
                "the console corks about LESS in a sea — that's what heavier and stiffer means");
            Assert.Greater(console.SeakeepingDamping, sport.SeakeepingDamping,
                "…and settles faster between crests");
            Assert.Greater(console.SeakeepingMassFactor, sport.SeakeepingMassFactor,
                "…because it opposes more inertia to the waves");
        }

        [Test]
        public void TheTwin_IsTheSportHull_WithASecondEngine()
        {
            var sport = Hull("SportSkiff");
            var twin = Hull("SportSkiffTwin");

            Assert.AreEqual(sport.LengthMeters, twin.LengthMeters, 0.001f, "the SAME hull…");
            Assert.AreEqual(sport.ForwardDrag, twin.ForwardDrag, 0.001f, "…so the same slipperiness…");
            Assert.AreEqual(sport.LateralDrag, twin.LateralDrag, 0.001f, "…and the same tracking");
            Assert.Greater(twin.MassKg, sport.MassKg, "…carrying one more engine");
            Assert.Greater(twin.EnginePower, sport.EnginePower, "…which pushes harder");
            Assert.Less(twin.EnginePower, sport.EnginePower * 2f,
                "…but NOT twice as hard: drag here is linear in v, so 2× thrust would exactly double the " +
                "terminal speed to ~9 m/s — past the spray sheet and past anything real. EnginePower is a " +
                "design-unit tunable calibrated to a designed speed, not a Newton count.");
        }

        [Test]
        public void BothSkiffs_AreFarMoreSeaworthyThanTheDory()
        {
            var dory = Hull("Dory");
            Assert.AreEqual(SeaState.Lively, dory.MaxSafeSeaState, "precondition: the dory tops out at Lively");

            foreach (var file in new[] { "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" })
                Assert.Greater((int)Hull(file).MaxSafeSeaState, (int)dory.MaxSafeSeaState,
                    $"{file}: a 7 m decked skiff works water that would swamp a dory");
        }

        [Test]
        public void EveryPoweredHull_TakesTheEngineHelm()
        {
            foreach (var file in new[] { "FishingSkiff", "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" })
            {
                var h = Hull(file);
                Assert.AreEqual(PropulsionType.Engine, h.Propulsion,
                    $"{file}: a hull with an outboard drawn on it must DRIVE like one — the propulsion " +
                    "branch is what DevBoatInput and the physics both read");
                Assert.Greater(h.EnginePower, 0f, $"{file}: an Engine hull with no power can't move");
                Assert.Greater(h.RudderAuthority, 0f, $"{file}: …or can't be steered");
            }

            Assert.AreEqual(PropulsionType.Oars, Hull("Dory").Propulsion, "the dory still rows");
        }

        [Test]
        public void TheSkiffs_CarryNoDeckContainer_UntilTheEightWayPropsLand()
        {
            // The owner's standing decision: the FishTray is drawn screen-upright and does NOT sit in an iso
            // hull at most headings, and there are to be NO code workarounds (no rotation hacks). The
            // 8-direction deck props are coming from his art director; until then these stay null. This test
            // is the reminder of WHY — so nobody "fixes" the missing tray by attaching the upright one.
            foreach (var file in new[] { "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" })
                Assert.IsNull(Hull(file).DeckContainer,
                    $"{file}: deliberately no deck container — the upright tray doesn't sit in an iso hull, " +
                    "and the fix is the incoming 8-direction props, not a rotation hack");
        }

        // ---- the skins ------------------------------------------------------------------------

        [Test]
        public void BothSkiffSkins_WearTheirOutboard_AndTheRightPaint()
        {
            var console = Visual("ConsoleSkiff");
            Assert.IsTrue(console.HasMotor(),
                "the console's outboard is unwired — the boat would sail with no engine drawn. Re-run " +
                "Build Boat Visual Defs.");
            Assert.AreEqual(SkiffMotorLayer.MotorVariant.Work, console.MotorVariant,
                "the console is the workboat: the graphite-cowl paint build");
            Assert.AreEqual(SkiffMotorLayer.MotorFit.Single, console.MotorFit,
                "the console workboat is single-engine — the twin is sport only");

            var sport = Visual("SportSkiffSingle");
            Assert.IsTrue(sport.HasMotor());
            Assert.AreEqual(SkiffMotorLayer.MotorVariant.Sport, sport.MotorVariant,
                "the sport skiff takes the white-cowl build");
            Assert.AreEqual(SkiffMotorLayer.MotorFit.Single, sport.MotorFit);

            var twin = Visual("SportSkiffTwin");
            Assert.IsTrue(twin.HasMotor());
            Assert.AreEqual(SkiffMotorLayer.MotorVariant.Sport, twin.MotorVariant);
            Assert.AreEqual(SkiffMotorLayer.MotorFit.Twin, twin.MotorFit, "…and this one hangs two of them");
        }

        [Test]
        public void TheTwinSkin_ReusesTheSingleSheets_ExactlyAsAuthored()
        {
            // The twin costs NO art: the bake is orthographic, so a ±0.34 m clamp shift is an exact screen
            // offset and the one sheet is blitted twice. If these ever diverge, someone has quietly baked a
            // second set of sheets that nobody needs.
            var single = Visual("SportSkiffSingle");
            var twin = Visual("SportSkiffTwin");

            CollectionAssert.AreEqual(single.MotorLower, twin.MotorLower,
                "the twin uses the SAME lower sheet as the single — a second engine needs no new art");
            CollectionAssert.AreEqual(single.MotorUpper, twin.MotorUpper);
            CollectionAssert.AreEqual(single.Facings, twin.Facings, "…on the same hull, too");
            CollectionAssert.AreEqual(single.RockGrid, twin.RockGrid);
        }

        [Test]
        public void TheSkiffSkins_CarryTheirRockGrid_AndTheirPerHullMotorLean()
        {
            foreach (var name in new[] { "ConsoleSkiff", "SportSkiffSingle", "SportSkiffTwin" })
            {
                var v = Visual(name);
                Assert.IsTrue(v.HasRockGrid(),
                    $"{name}: no rock grid — the hull would sit dead still on a moving sea. Re-run Build " +
                    "Boat Visual Defs.");
                Assert.AreEqual(8, v.RockFrameCount, $"{name}: the skiff rock sheets ship 8 frames");
                Assert.AreEqual(64, v.RockGrid.Length, $"{name}: 8 headings × 8 frames");
                Assert.AreEqual(SkiffMotorMath.SteerColumns, v.MotorColumnCount,
                    $"{name}: the motor sheets bake 9 steer columns");
            }

            // The motor lean is per-hull ART FACT, and the two hulls must NOT lean the same.
            var console = Visual("ConsoleSkiff");
            var sport = Visual("SportSkiffSingle");
            Assert.AreEqual(3.4f, console.MotorRockRollDegrees, 0.001f, "the console's rollA");
            Assert.AreEqual(1.3f, console.MotorRockHeavePixels, 0.001f, "…and heaveA");
            Assert.AreEqual(3.8f, sport.MotorRockRollDegrees, 0.001f, "the sport's rollA — livelier");
            Assert.AreEqual(1.5f, sport.MotorRockHeavePixels, 0.001f);
            Assert.Less(console.MotorRockRollDegrees, sport.MotorRockRollDegrees,
                "the heavier console leans LESS than the light glass sport");
        }

        [Test]
        public void TheRowedAndTheUnpoweredSkins_HaveNoEngineDrawn()
        {
            var dory = Visual("DoryIso");
            Assert.IsTrue(dory.HasOarSheets(), "the dory rows");
            Assert.IsFalse(dory.HasMotor(), "…so it has no engine bolted to it");

            var fishing = Visual("FishingBoat");
            Assert.IsTrue(fishing.HasFullCompass(),
                "the 8-direction fishing boat's compass is 8 separate FILES — if this is empty, one of the " +
                "FishingBoat_*.png files is missing or was renamed");
            Assert.IsFalse(fishing.HasRockGrid(), "it ships no rock grid — static facings + the legacy rock");
            Assert.IsFalse(fishing.HasMotor(), "and no motor sheets are baked for it");
        }

        [Test]
        public void NoAuthoredSkin_WearsBothOarsAndAnOutboard()
        {
            // The invariant that lets the two overlays share a sorting band without anyone getting hurt.
            // Asserted across EVERY visual in Data/, not just the fleet's, so a future skin can't sneak past.
            var all = AssetDatabase.FindAssets($"t:{nameof(BoatVisualDef)}", new[] { DataBoats })
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .Select(AssetDatabase.LoadAssetAtPath<BoatVisualDef>)
                                   .Where(v => v != null)
                                   .ToList();
            Assert.IsNotEmpty(all, "no BoatVisualDefs found — the search path is wrong");

            foreach (var v in all)
                Assert.IsFalse(v.HasConflictingOverlays(),
                    $"{AssetDatabase.GetAssetPath(v)}: binds BOTH oar sheets and motor sheets. Their sorting " +
                    "bands overlap (oars hull+1/+2 vs the motor's lower layer hull+1/+2 over the hull), so " +
                    "the engine leg and the port oar would z-fight — and only at some headings, because the " +
                    "lower band flips across the stern-away arc.");
        }
    }
}
