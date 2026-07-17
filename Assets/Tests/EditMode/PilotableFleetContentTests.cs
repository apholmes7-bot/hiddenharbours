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

        /// <summary>
        /// The COMMITTED hulls the picker cycles, in cycle order.
        ///
        /// <para><b>"Punt" is absent on purpose, and it is not an oversight.</b> The picker's real roster is
        /// seven — the basic punt sits between FishingSkiff and PuntUpgraded — but <c>Data/Boats/Punt.asset</c>
        /// is BUILDER-GENERATED AND HAS NEVER BEEN COMMITTED: it exists only in a checkout where someone has
        /// run the cove builder. Listing her here would fail on a clean clone for a reason with nothing to do
        /// with the code under test. Every existing test in this repo mirrors the punt in memory for the same
        /// reason. What CAN be asserted about her from disk lives behind <see cref="OptionalHull"/>.</para>
        /// </summary>
        static string[] FleetFiles => new[]
        {
            "Dory", "FishingSkiff", "PuntUpgraded", "ConsoleSkiff", "SportSkiff", "SportSkiffTwin",
        };

        /// <summary>
        /// A hull that may legitimately not be on disk (the basic Punt — see <see cref="FleetFiles"/>).
        /// Returns null rather than failing, so a clean clone skips instead of going red on a missing
        /// generated asset. Everything reached through this is ALSO covered by an in-memory mirror elsewhere,
        /// so a skip never leaves a BEHAVIOUR unguarded — it only skips "is the owner's local asset in step
        /// with the C# that writes it", which is meaningless without the asset.
        /// </summary>
        static BoatHullDef OptionalHull(string file)
            => AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");

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
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Work, console.MotorVariant,
                "the console is the workboat: the graphite-cowl paint build");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, console.MotorFit,
                "the console workboat is single-engine — the twin is sport only");

            var sport = Visual("SportSkiffSingle");
            Assert.IsTrue(sport.HasMotor());
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Sport, sport.MotorVariant,
                "the sport skiff takes the white-cowl build");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, sport.MotorFit);

            var twin = Visual("SportSkiffTwin");
            Assert.IsTrue(twin.HasMotor());
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Sport, twin.MotorVariant);
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Twin, twin.MotorFit, "…and this one hangs two of them");
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
                Assert.AreEqual(OutboardMotorMath.SteerColumns, v.MotorColumnCount,
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

        // ---- the punt: her skin, her two engines ----------------------------------------------

        [Test]
        public void BothPuntSkins_WearHerTillerOutboard_InTheRightPaint()
        {
            var basic = Visual("PuntIsoBasic");
            Assert.IsTrue(basic.HasMotor(),
                "the punt's outboard is unwired — she would sail with no engine drawn. Re-run Build Boat " +
                "Visual Defs.");
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Basic, basic.MotorVariant,
                "the starter engine: weathered grey/black");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, basic.MotorFit,
                "the punt is single-engine — her kit ships NO twin at all");

            var upgraded = Visual("PuntIsoUpgraded");
            Assert.IsTrue(upgraded.HasMotor());
            Assert.AreEqual(OutboardMotorLayer.MotorVariant.Upgraded, upgraded.MotorVariant,
                "the upgrade: domed cowl, gloss pan, red wrap stripe");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, upgraded.MotorFit);

            Assert.IsFalse(basic.HasOarSheets(), "she is powered, not rowed");
            Assert.IsFalse(upgraded.HasOarSheets());
        }

        [Test]
        public void ThePuntSteers32Degrees_AndTheSkiffsStillSteer30()
        {
            // The reason MotorMaxSteerDegrees had to become data. Her rig bakes angle(f) = -32 + 64*f/8 (8°
            // steps) where the skiffs bake -30 + 60*f/8 (7.5°) — across the SAME 9 columns, which is exactly
            // why a shared column count is not a shared authority.
            foreach (var name in new[] { "PuntIsoBasic", "PuntIsoUpgraded" })
            {
                var v = Visual(name);
                Assert.AreEqual(32f, v.MotorMaxSteerDegrees, 0.001f, $"{name}: the punt's sheets bake ±32°");
                Assert.AreEqual(OutboardMotorMath.SteerColumns, v.MotorColumnCount,
                    $"{name}: still 9 steer columns — it is the ARC that differs, not the column count");
            }

            foreach (var name in new[] { "ConsoleSkiff", "SportSkiffSingle", "SportSkiffTwin" })
                Assert.AreEqual(30f, Visual(name).MotorMaxSteerDegrees, 0.001f,
                    $"{name}: the skiffs keep ±30 — authoring the punt must not have moved them");
        }

        [Test]
        public void TheUpgradedPuntSkin_IsTheSameBoat_WearingDifferentSheets()
        {
            // The art README: "Both builds share the SAME cell, pivot, steer cols and grip JSON — the sheets
            // are drop-in swaps." So the upgrade must differ in the MOTOR sheets and in nothing else. If the
            // hull or the rock ever diverge, someone has re-baked art that nobody needed.
            var basic = Visual("PuntIsoBasic");
            var upgraded = Visual("PuntIsoUpgraded");

            CollectionAssert.AreEqual(basic.Facings, upgraded.Facings, "the SAME hull…");
            CollectionAssert.AreEqual(basic.RockGrid, upgraded.RockGrid, "…rocking the same way…");
            Assert.AreEqual(basic.MotorMaxSteerDegrees, upgraded.MotorMaxSteerDegrees, 0.001f,
                "…through the same steer arc…");
            Assert.AreEqual(basic.MotorRockRollDegrees, upgraded.MotorRockRollDegrees, 0.001f,
                "…and leaning her engine identically");

            CollectionAssert.AreNotEqual(basic.MotorLower, upgraded.MotorLower,
                "…but the ENGINE really is a different paint build, or the upgrade is invisible");
            CollectionAssert.AreNotEqual(basic.MotorUpper, upgraded.MotorUpper);
        }

        [Test]
        public void ThePuntSkins_CarryHerRockGrid_AndHerOwnMotorLean()
        {
            foreach (var name in new[] { "PuntIsoBasic", "PuntIsoUpgraded" })
            {
                var v = Visual(name);
                Assert.IsTrue(v.HasRockGrid(),
                    $"{name}: no rock grid — she would sit dead still on a moving sea");
                Assert.AreEqual(8, v.RockFrameCount, $"{name}: her rock sheet ships 8 frames");
                Assert.AreEqual(64, v.RockGrid.Length, $"{name}: 8 headings × 8 frames");

                // Read STRAIGHT off her rig now — all three, no conversion. The pitch used to be pushed
                // through a borrowed "0.02 m / 3.0°" rate off the dory; that rate ignored the mount's lever
                // arm, which is the only thing that actually decides the answer, and shipped an engine rocking
                // in anti-phase with its own transom. MountedRockPoseMath derives the travel from the MOUNT.
                Assert.AreEqual(4.2f, v.MotorRockRollDegrees, 0.001f, $"{name}: her rig's rollA");
                Assert.AreEqual(1.5f, v.MotorRockHeavePixels, 0.001f, $"{name}: her rig's heaveA");
                Assert.AreEqual(2.4f, v.MotorRockPitchDegrees, 0.001f,
                    $"{name}: her rig's pitchA, in DEGREES — transcribed, not converted");

                // Her MOUNT is her own: a 5.2 m boat clamps her engine 2.63 m aft, not the 7 m skiffs' 3.53.
                // Copying theirs across would give her ~34% too much pitch travel at every heading.
                Assert.AreEqual(0f, v.MotorMountLocalMeters.x, 0.001f, $"{name}: single engine, on the centreline");
                Assert.AreEqual(-2.63f, v.MotorMountLocalMeters.y, 0.001f, $"{name}: her rig's MOUNT.y − 0.03");
                Assert.AreEqual(0.56f, v.MotorMountLocalMeters.z, 0.001f, $"{name}: her rig's MOUNT.z");
                Assert.AreEqual(40f, v.ArtBakeElevationDegrees, 0.001f, $"{name}: her rig's DEFAULT_ELEV");
            }

            // The rock ladder must read like the boats do: beamier than the dory (so stiffer than her), but a
            // smaller, livelier hull than either 7 m skiff.
            float punt = Visual("PuntIsoBasic").MotorRockRollDegrees;
            Assert.Less(punt, 5f, "beamier than the dory → a stiffer roll than the dory's 5°");
            Assert.Greater(punt, Visual("SportSkiffSingle").MotorRockRollDegrees,
                "…but livelier than the 7 m sport skiff");
            Assert.Greater(punt, Visual("ConsoleSkiff").MotorRockRollDegrees,
                "…and far livelier than the heavy console");
        }

        [Test]
        public void TheUpgradedPunt_IsThePuntHull_WithAStrongerEngine()
        {
            var upgraded = Hull("PuntUpgraded");
            Assert.AreEqual("boat.punt_upgraded", upgraded.Id, "ids are stable and append-only");
            Assert.AreEqual(PropulsionType.Engine, upgraded.Propulsion);
            Assert.AreEqual(5.2f, upgraded.LengthMeters, 0.001f, "the SAME drawn hull as the punt");

            // Her speed is the design contract: ~20–30% over the basic punt, and still a workboat.
            float v = TerminalSpeed(upgraded);
            Assert.AreEqual(2.89f, v, 0.1f,
                "the upgraded punt's target is ≈2.9 m/s — MEASURED to terminal on real physics, never taken " +
                "from an EnginePower/ForwardDrag ratio (that drops the rigidbody's own linearDamping, which " +
                "is half her resistance)");
            Assert.Less(v, TerminalSpeed(Hull("SportSkiff")),
                "she is a workboat with a better engine, not a sports car — she stays under the sport " +
                "skiff's 4.64 m/s");
            Assert.Greater(v, 1.7f,
                "…but over BowSprayGrading's 1.7 m/s floor, or she would never throw spray");

            // …and she must be the punt in every way that is not the engine.
            var basic = OptionalHull("Punt");
            if (basic == null)
                Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed — run the cove " +
                              "builder to assert the pair against each other.");

            Assert.AreEqual(basic.LengthMeters, upgraded.LengthMeters, 0.001f, "the same hull…");
            Assert.AreEqual(basic.ForwardDrag, upgraded.ForwardDrag, 0.001f, "…the same slipperiness…");
            Assert.AreEqual(basic.LateralDrag, upgraded.LateralDrag, 0.001f, "…the same tracking…");
            Assert.AreEqual(basic.HoldUnits, upgraded.HoldUnits, "…and the same hold: only the engine differs");
            Assert.Greater(upgraded.MassKg, basic.MassKg, "…carrying a heavier block");
            Assert.Greater(upgraded.EnginePower, basic.EnginePower, "…which pushes harder");

            float gain = TerminalSpeed(upgraded) / TerminalSpeed(basic) - 1f;
            Assert.GreaterOrEqual(gain, 0.20f, $"the upgrade is worth {gain:P0} — under the 20% asked for");
            Assert.LessOrEqual(gain, 0.30f, $"the upgrade is worth {gain:P0} — over the 30% asked for");
        }

        [Test]
        public void ThePunt_IsAuthoredAtTheLengthSheIsDrawn()
        {
            var punt = OptionalHull("Punt");
            if (punt == null) Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed.");

            // NOT cosmetic. WakeGrading.SternAnchor puts the plume at LengthMeters*0.5, so the 6 this used to
            // say threw her wake ~40 cm astern of a transom that was never drawn there — and LengthMeters
            // also feeds the wake/spray size term. The art director draws her at ~5.2 m and the slicer cuts
            // her at that scale; the asset now agrees with the picture.
            Assert.AreEqual(5.2f, punt.LengthMeters, 0.001f,
                "the punt is DRAWN at ~5.2 m — authoring 6 lied to the wake");
            Assert.Greater(punt.LengthMeters, Hull("Dory").LengthMeters,
                "…still longer than the dory (she is the upgrade from her)");
            Assert.Less(punt.LengthMeters, Hull("ConsoleSkiff").LengthMeters,
                "…and well short of the 7 m skiffs");
        }

        [Test]
        public void ThePunt_KeepsHerTunedFeel_AndFinallyHasAPicture()
        {
            var punt = OptionalHull("Punt");
            if (punt == null) Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed.");

            // She is NOT a dev hull: she is the first boat the owner BUYS (PuntOffer, ₲1800) and he already
            // knows how she handles. Giving her a picture must not have retuned her. These are the values she
            // has carried since VS-16 — if a later change wants to move them, that is a FEEL decision for the
            // owner to take, not a side effect of a skin.
            Assert.AreEqual("boat.punt", punt.Id);
            Assert.AreEqual(650f, punt.EnginePower, 0.001f, "her thrust is untouched");
            Assert.AreEqual(140f, punt.ForwardDrag, 0.001f);
            Assert.AreEqual(360f, punt.LateralDrag, 0.001f);
            Assert.AreEqual(700f, punt.MassKg, 0.001f);
            Assert.AreEqual(14, punt.HoldUnits);
            Assert.AreEqual(2.32f, TerminalSpeed(punt), 0.05f, "…so she still settles at ≈2.3 m/s");

            Assert.IsNotNull(punt.Visual, "…but she finally HAS a picture");
            Assert.IsTrue(punt.Visual.HasFullCompass(), "…a full 8-way one");
            Assert.IsTrue(punt.Visual.HasMotor(), "…with her outboard drawn on it");
        }

        [Test]
        public void ThePunt_SeakeepsBetweenTheFishingSkiffAndTheSport_AndTheArtAgrees()
        {
            var punt = OptionalHull("Punt");
            if (punt == null) Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed.");

            // She predates the Seakeeping* fields and had been sitting on the raw defaults (1/1/0). Placed by
            // mass between the fishing skiff (450 kg) and the sport (950 kg). The check that matters is that
            // the ART agrees independently: her rig's rollA is 4.2 against the dory's 5.0 and the sport's
            // 3.8, so she must cork about LESS than the dory and MORE than the sport — and she lands there.
            var fishing = Hull("FishingSkiff");
            var sport = Hull("SportSkiff");

            Assert.Less(punt.SeakeepingLiveliness, fishing.SeakeepingLiveliness,
                "heavier than the little fishing skiff → she corks about less");
            Assert.Greater(punt.SeakeepingLiveliness, sport.SeakeepingLiveliness,
                "…but lighter than the 7 m sport → livelier than her");
            Assert.Greater(punt.SeakeepingMassFactor, fishing.SeakeepingMassFactor);
            Assert.Less(punt.SeakeepingMassFactor, sport.SeakeepingMassFactor);
            Assert.Greater(punt.SeakeepingDamping, fishing.SeakeepingDamping);
            Assert.Less(punt.SeakeepingDamping, sport.SeakeepingDamping);

            Assert.AreNotEqual(1f, punt.SeakeepingLiveliness,
                "the raw default (1/1/0) means UNAUTHORED — she is a real boat and must sit on the ladder");
        }

        [Test]
        public void EveryVisual_DeclaresTheCameraItsArtWasBakedAt()
        {
            // THE #212 TRAP, GUARDED. A committed .asset that predates a field deserialises it to ZERO — which
            // is how #212 nearly shipped a no-op fix. ArtBakeElevationDegrees at 0 would mean sin(0) = 0 and
            // fold every hull-anchored effect onto the boat's own middle, so it must be present and sane in
            // the ASSETS, not merely correct in the builder that writes them.
            foreach (var name in new[] { "DoryIso", "ConsoleSkiff", "SportSkiffSingle", "SportSkiffTwin",
                                         "PuntIsoBasic", "PuntIsoUpgraded" })
                Assert.AreEqual(40f, Visual(name).ArtBakeElevationDegrees, 0.001f,
                    $"{name}: an iso rig bake — its DEFAULT_ELEV is 40. If this reads 0, the asset predates " +
                    "the field and the wake fix is a silent no-op: re-run Hidden Harbours ▸ Art ▸ Build Boat " +
                    "Visual Defs, or the committed asset is wrong.");

            // The odd one out, deliberately: 8 hand-drawn files, no camera, no bake. 90 = a plan view = do not
            // foreshorten = its wake (and the whole ambient fleet's, which wears these facings) stays put.
            Assert.AreEqual(90f, Visual("FishingBoat").ArtBakeElevationDegrees, 0.001f,
                "FishingBoat is NOT a rig bake — foreshortening it would invent a camera it never had, the " +
                "same trap the per-artwork mirror flag avoids");
        }

        [Test]
        public void EveryPoweredHull_CarriesItsOwnClampPointAndItsRigsPitchInDegrees()
        {
            // The pitch used to be screen-METRES, produced by pushing each rig's pitchA through a rate borrowed
            // from the DORY. It ignored the mount's lever arm, which is the whole quantity — 8x too small and
            // wrong-signed, so the engine lifted on the wave its hull dropped into. Both halves are asserted:
            // the degrees are transcription, the mount is the thing that was missing.
            foreach (var name in new[] { "ConsoleSkiff", "SportSkiffSingle", "SportSkiffTwin" })
            {
                var v = Visual(name);
                Assert.AreEqual(-3.53f, v.MotorMountLocalMeters.y, 0.001f,
                    $"{name}: a 7 m skiff clamps at MOUNT.y − 0.03 = −3.53 (her rig's L/2, just aft)");
                Assert.AreEqual(0.72f, v.MotorMountLocalMeters.z, 0.001f, $"{name}: her rig's transom top");
                Assert.Greater(v.MotorRockPitchDegrees, 1f,
                    $"{name}: this field is DEGREES now (1.9/2.2), not the old ~0.013 screen-metres fudge — a " +
                    "value under 1 means a stale asset and an engine with ~1/100th of its pitch");
                Assert.Less(v.MotorRockPitchDegrees, 5f, $"{name}: …and no rig bakes more than a few degrees");
            }

            Assert.AreEqual(1.9f, Visual("ConsoleSkiff").MotorRockPitchDegrees, 0.001f, "consoleIsoRig's pitchA");
            Assert.AreEqual(2.2f, Visual("SportSkiffSingle").MotorRockPitchDegrees, 0.001f, "sportSkiffIsoRig's pitchA");

            // The punt is a SHORTER boat and her clamp is her own. Copying the skiffs' across would give her
            // ~34% too much pitch travel at every heading — the exact class of mistake the fudge institutionalised.
            Assert.AreEqual(-2.63f, Visual("PuntIsoBasic").MotorMountLocalMeters.y, 0.001f,
                "the punt's own MOUNT — 5.2 m of boat, not 7");
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
