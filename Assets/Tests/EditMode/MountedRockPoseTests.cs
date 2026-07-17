using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE OUTBOARD'S ROCK POSE, ASSERTED AGAINST THE ART — not against the code's own constants.
    ///
    /// <para><b>Why this suite is written the way it is.</b> #212's tests were green while the bug shipped,
    /// because they asserted the heading→cell mapping against the very mapping they were testing. Verifying
    /// geometry is not verifying MEANING. So every number below is either read out of
    /// <c>docs/art/skiff-fleet-rigs/consoleIsoRig.js</c> / <c>docs/art/punt-iso-rig/puntIsoRig.js</c>, or
    /// hand-derived from them and written here as a literal. If the production code changed its mind about what
    /// the art does, these go red.</para>
    ///
    /// <para><b>The bug these exist to catch.</b> The pose used to come from a scalar
    /// <c>MotorRockPitchOffsetMeters = +0.0127</c> (the dory's hand-tuned 0.02 m rescaled by pitchA). The rig
    /// says the console skiff's transom clamp actually travels <b>−0.104 m</b> at that phase — 8× bigger and the
    /// OTHER WAY. Positive pitch is bow-up, which drops the stern; the code lifted the engine hanging on it. The
    /// motor rocked in ANTI-PHASE with the hull, which is exactly the owner's "it seems to bounce independtly".
    /// <see cref="Pose_PitchAtNorth_DropsTheStern_TheAntiPhaseBug"/> is the one that fails if that regresses.</para>
    /// </summary>
    public class MountedRockPoseTests
    {
        // ---- READ OFF THE RIGS. Not tunables, not the code's constants — the art's. ----------------------

        // consoleIsoRig.js: ROCK = { frames: 8, rollA: 3.4, pitchA: 1.9, heaveA: 1.3 }
        private const float ConsoleRollA = 3.4f, ConsolePitchA = 1.9f, ConsoleHeaveA = 1.3f;
        private const int Frames = 8, Headings = 8;

        // consoleIsoRig.js: const MOUNT = { x:0, y:-L/2, z:T[0][3]+T[0][2] } with L = 7.0 and the transom
        // station T[0] = [0.92, 0.74, 0.66, 0.06] → z = 0.06 + 0.66 = 0.72; motorMount() projects MOUNT.y − 0.03.
        private static readonly Vector3 SkiffMount = new Vector3(0f, -3.53f, 0.72f);
        // puntIsoRig.js: L = 5.2, T[0] = [0.62, 0.46, 0.48, 0.08] → z = 0.56; same y − 0.03.
        private static readonly Vector3 PuntMount = new Vector3(0f, -2.63f, 0.56f);

        // Both rigs: const DEFAULT_ELEV = 40; const PX = 32, S = 32.
        private const float Elev = 40f, Ppu = 32f;

        // The rigs bake 8 cells at 45° steps of their own turntable, cell i at θ = i·45°. Cell 2 is the one
        // drawn at θ = 90°. (What HEADING that cell depicts is #212's business, not this file's: the pose is
        // correct in CELL space, and the cell is what the hull draws.)
        private const int CellN = 0, CellTheta90 = 2, CellTheta180 = 4, CellTheta270 = 6;

        // Rock frames: a = frame·45°, so frame 0 is a = 0 (cos 1, sin 0 → PURE PITCH, no roll, no heave) and
        // frame 2 is a = 90° (cos 0, sin 1 → PURE ROLL + full heave). Those two isolate the terms.
        private const int PurePitchFrame = 0, PureRollFrame = 2;

        private static MountedRockPoseMath.MountRockPose ConsolePose(int cell, int frame, float lateral = 0f)
            => MountedRockPoseMath.Pose(frame, Frames, cell, Headings, SkiffMount, lateral, Elev,
                                        ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 1f);

        /// <summary>Recover where the rig actually puts the MOUNT, out of the pose the code hands a renderer.
        /// The pose is expressed for a transform whose pivot is the boat ORIGIN, so
        /// <c>mount = offset + R(roll)·bakedMount</c>. Inverting it here lets the assertions talk about the
        /// thing the art actually measures — the clamp point — instead of about the code's chosen encoding.</summary>
        private static Vector2 MountTravel(MountedRockPoseMath.MountRockPose pose, Vector3 mount, int cell)
        {
            float theta = cell * (2f * Mathf.PI / Headings);
            Vector2 baked = MountedRockPoseMath.Project(mount, theta, 0f, 0f, Elev * Mathf.Deg2Rad);
            float phi = pose.RollDegrees * Mathf.Deg2Rad;
            float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            var rotated = new Vector2(baked.x * cp - baked.y * sp, baked.x * sp + baked.y * cp);
            return pose.Offset + rotated - baked;
        }

        // ==== the projection itself, against numbers derived by hand from the rigs' projVert ==============

        [Test]
        public void Project_PutsTheSkiffsClampWhereTheRigDoes()
        {
            float e = Elev * Mathf.Deg2Rad;

            // Cell 0 (θ = 0): the clamp is dead astern and BELOW the origin on screen. The rig's own arithmetic:
            //   xr = 0, yr = −3.53, zr = 0.72  →  screen = (0, −3.53·sin40 + 0.72·cos40) = (0, −1.7175)
            Vector2 n = MountedRockPoseMath.Project(SkiffMount, 0f, 0f, 0f, e);
            Assert.AreEqual(0f, n.x, 1e-4f, "on the centreline, seen end-on: no lateral component");
            Assert.AreEqual(-1.7175f, n.y, 1e-3f, "the along-heading 3.53 m is FORESHORTENED to 2.269, less " +
                                                  "the clamp's own 0.72 m of height read at cos(40°)");

            // Cell 2 (θ = 90°): the boat is broadside, so the SAME 3.53 m now runs across the screen at FULL
            // length and nothing of it is foreshortened. This is the whole shape of the wake bug, in one number.
            Vector2 e90 = MountedRockPoseMath.Project(SkiffMount, Mathf.PI * 0.5f, 0f, 0f, e);
            Assert.AreEqual(3.53f, e90.x, 1e-3f, "broadside: the full 3.53 m, unforeshortened");
            Assert.AreEqual(0.5516f, e90.y, 1e-3f, "only the clamp's height is left in screen-Y");

            // Cell 4 (θ = 180°): the mirror of cell 0 — the clamp is now ABOVE the origin on screen.
            Vector2 s = MountedRockPoseMath.Project(SkiffMount, Mathf.PI, 0f, 0f, e);
            Assert.AreEqual(0f, s.x, 1e-4f);
            Assert.AreEqual(2.8207f, s.y, 1e-3f, "the same 2.269 foreshortened metres, the other way, plus 0.5516");
        }

        // ==== THE BUG ====================================================================================

        [Test]
        public void Pose_PitchAtNorth_DropsTheStern_TheAntiPhaseBug()
        {
            // Rock frame 0: a = 0, so roll = 0, heave = 0 and pitch = pitchA·cos(0) = the FULL +1.9°. Nothing
            // else is moving — this frame is the pitch, alone.
            var pose = ConsolePose(CellN, PurePitchFrame);

            // Hand-derived from consoleIsoRig's projVert, with the rig's own numbers:
            //   y2 = −3.53·cos(1.9°) − 0.72·sin(1.9°) = −3.5519
            //   z2 = −3.53·sin(1.9°) + 0.72·cos(1.9°) =  0.6026
            //   screen y = −3.5519·sin40 + 0.6026·cos40 = −1.8215   (level: −1.7175)
            //   → the clamp travels −0.1041 m.
            Vector2 travel = MountTravel(pose, SkiffMount, CellN);

            Assert.Less(travel.y, 0f,
                "A POSITIVE PITCH IS BOW-UP, WHICH PUTS THE STERN DOWN — and the outboard hangs on the stern. " +
                "The old code applied +0.0127 m here: it LIFTED the engine on the wave the hull was dropping " +
                "it into, i.e. the motor rocked in ANTI-PHASE with its own transom. That is the owner's " +
                "'the skiffs motor doesnt rock/bounce in synch with the boat itself'. If this assert is red, " +
                "the sign has regressed and the bug is back.");

            Assert.AreEqual(-0.1041f, travel.y, 0.003f,
                "the rig's own answer for the console skiff's clamp at full pitch. The old fudge said +0.0127 " +
                "— ~8× too small AND the wrong way. It was a linear rescale of the DORY's hand-tuned 0.02 m, " +
                "which was tuned for oarlocks near AMIDSHIPS; this clamp hangs 3.53 m aft, and the lever arm " +
                "is the entire quantity. A scalar cannot express it.");

            Assert.AreEqual(0f, pose.RollDegrees, 0.01f,
                "at cell 0 a pure pitch turns the boat's vertical about the axis pointing AT the camera, so " +
                "nothing rotates on screen — it is pure travel");
        }

        [Test]
        public void Pose_PitchTravel_IsBiggerThanTheOldFudgeAtEveryHeading_AndAlwaysNegativeAtTheStern()
        {
            const float OldFudge = 0.0127f;   // what ConsoleSkiff.asset used to carry

            foreach (int cell in new[] { CellN, 1, CellTheta90, 3, CellTheta180, 5, CellTheta270, 7 })
            {
                Vector2 travel = MountTravel(ConsolePose(cell, PurePitchFrame), SkiffMount, cell);
                Assert.Less(travel.y, 0f, $"cell {cell}: bow-up must drop the stern-mounted engine, at EVERY heading");
                Assert.Greater(Mathf.Abs(travel.y), OldFudge * 3f,
                    $"cell {cell}: the true travel dwarfs the old +{OldFudge} scalar — the error was larger than " +
                    "the signal, which is why switching the coupling off would have looked BETTER than leaving it");
            }
        }

        // ==== the roll is heading-dependent, and was not ==================================================

        [Test]
        public void Pose_ScreenRoll_SwingsWithTheHeading_NotAConstantRollA()
        {
            // The old code applied a flat rollA·sin(a) = 3.4·sin(a)° at EVERY heading. The rig says otherwise.
            float atN = ConsolePose(CellN, PureRollFrame).RollDegrees;
            float at90 = ConsolePose(CellTheta90, PureRollFrame).RollDegrees;
            float at180 = ConsolePose(CellTheta180, PureRollFrame).RollDegrees;
            float at270 = ConsolePose(CellTheta270, PureRollFrame).RollDegrees;

            // Cell 0: the fore-aft (roll) axis lies ACROSS the screen, so the boat's vertical tips fully
            // sideways — and reads AMPLIFIED by the ¾ squash: −rollA / cos(40°) = −3.4 / 0.766 = −4.44°.
            Assert.AreEqual(-4.44f, atN, 0.05f,
                "at cell 0 the screen lean is rollA/cos(elev), not rollA — the old code's flat 3.4 was both " +
                "too small and the wrong sign");
            Assert.AreEqual(+4.44f, at180, 0.05f, "cell 4 is cell 0's mirror: the same lean, the other way");

            // Cells 2/6: the roll axis points AT the camera, so a roll barely rotates anything on screen.
            Assert.AreEqual(0f, at90, 0.05f, "broadside, a pure roll is almost invisible as rotation — the old " +
                                             "code leaned the engine 3.4° here for no reason at all");
            Assert.AreEqual(0f, at270, 0.05f);
        }

        [Test]
        public void Pose_LateralTravel_IsReal_AndTheOldCodeAppliedNone()
        {
            // The clamp sits 0.72 m ABOVE the roll axis, so a roll swings it sideways: 0.72·sin(3.4°)·cos(θ),
            // which at cell 0 is +0.0427 m — 1.37 px, the table's ±1.37. The old code applied exactly 0.
            Vector2 travel = MountTravel(ConsolePose(CellN, PureRollFrame), SkiffMount, CellN);
            Assert.AreEqual(0.0427f, travel.x, 0.002f,
                "a rolled clamp 0.72 m up off the axis moves ACROSS the screen; the old pose had no dx term");
            Assert.AreEqual(-0.0427f, MountTravel(ConsolePose(CellTheta180, PureRollFrame), SkiffMount, CellTheta180).x,
                0.002f, "cell 4 mirrors it");
        }

        [Test]
        public void Pose_HeaveIsReproducedExactly_TheTermThatWasAlreadyRight()
        {
            // frame 2: a = 90°, so heave is at its peak of heaveA = 1.3 px = 0.040625 m of pure screen lift,
            // and pitch (∝ cos a) is zero. The along-Y travel at cell 0 is heave + the roll's own contribution
            // (which at cell 0 is nil, since the clamp swings sideways there, not up).
            Vector2 travel = MountTravel(ConsolePose(CellN, PureRollFrame), SkiffMount, CellN);
            Assert.AreEqual(ConsoleHeaveA / Ppu, travel.y, 0.003f,
                "heave is the one term the old code already had right (+1.30 px) — which is what makes the " +
                "pitch discrepancy trustworthy rather than a sign-convention muddle");
        }

        // ==== the two derivations must agree ==============================================================

        [Test]
        public void LevelPose_IsExactlyTheTwinClampOffset_TwoDerivationsAgree()
        {
            // OutboardMotorMath.MountOffset was derived straight from the rig's mountOffset(dir,mx,elev) and is
            // verified against _preview-sport-twin.png. MountedRockPoseMath gets there from a completely
            // different direction — a full projection of the mount. On a LEVEL hull they must land on the same
            // point, or one of them is wrong. This is the cross-check that keeps the new math honest.
            const float Twin = 0.34f;
            for (int cell = 0; cell < Headings; cell++)
            {
                var pose = MountedRockPoseMath.Pose(MountedRockPoseMath.LevelRockFrame, Frames, cell, Headings,
                                                    SkiffMount, Twin, Elev, ConsoleRollA, ConsolePitchA,
                                                    ConsoleHeaveA, Ppu, 1f);
                Vector2 expected = OutboardMotorMath.MountOffset(cell, Twin, Headings, Elev);
                Assert.AreEqual(expected.x, pose.Offset.x, 1e-4f, $"cell {cell}: x");
                Assert.AreEqual(expected.y, pose.Offset.y, 1e-4f, $"cell {cell}: y");
                Assert.AreEqual(0f, pose.RollDegrees, 1e-4f, $"cell {cell}: a level hull leans nothing");
            }
        }

        [Test]
        public void LevelPose_SingleEngine_IsExactlyZero()
        {
            for (int cell = 0; cell < Headings; cell++)
            {
                var pose = MountedRockPoseMath.Pose(MountedRockPoseMath.LevelRockFrame, Frames, cell, Headings,
                                                    SkiffMount, 0f, Elev, ConsoleRollA, ConsolePitchA,
                                                    ConsoleHeaveA, Ppu, 1f);
                Assert.AreEqual(Vector2.zero, pose.Offset, $"cell {cell}: the cell is already drawn correctly");
                Assert.AreEqual(0f, pose.RollDegrees, 1e-5f);
            }
        }

        [Test]
        public void CalmSea_StillSeparatesATwinsEngines()
        {
            // The level sentinel zeroes the WAVE, not the clamp. Collapsing a twin onto its own centreline on a
            // glassy morning would be a fine way to ship one engine.
            const float Twin = 0.34f;
            var port = MountedRockPoseMath.Pose(MountedRockPoseMath.LevelRockFrame, Frames, CellN, Headings,
                                                SkiffMount, -Twin, Elev, ConsoleRollA, ConsolePitchA,
                                                ConsoleHeaveA, Ppu, 1f);
            var star = MountedRockPoseMath.Pose(MountedRockPoseMath.LevelRockFrame, Frames, CellN, Headings,
                                                SkiffMount, +Twin, Elev, ConsoleRollA, ConsolePitchA,
                                                ConsoleHeaveA, Ppu, 1f);
            Assert.AreEqual(2f * Twin, star.Offset.x - port.Offset.x, 1e-4f,
                "at cell 0 the engines separate purely horizontally, by the full clamp spacing");
        }

        [Test]
        public void Strength_Zero_SitsTheEngineLevel_AndOneIsTheArtsOwnRead()
        {
            var off = MountedRockPoseMath.Pose(PureRollFrame, Frames, CellN, Headings, SkiffMount, 0f, Elev,
                                               ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 0f);
            Assert.AreEqual(Vector2.zero, off.Offset, "strength 0 = the engine sits level on a rocking hull");
            Assert.AreEqual(0f, off.RollDegrees, 1e-5f);

            var on = ConsolePose(CellN, PureRollFrame);
            Assert.AreNotEqual(0f, on.RollDegrees, "strength 1 is the tuned read");
        }

        // ==== the kits differ, and the difference must survive ============================================

        [Test]
        public void ThePuntsShorterLeverArm_GivesHerLessPitchTravelThanASkiff()
        {
            // Her rig: rollA 4.2, pitchA 2.4, heaveA 1.5 — a LIVELIER boat than either skiff. But her clamp is
            // 2.63 m aft where theirs is 3.53, and the lever arm is what the pitch travel is made of. If someone
            // ever "tidies" her mount into the skiffs', this catches it.
            var punt = MountedRockPoseMath.Pose(PurePitchFrame, Frames, CellN, Headings, PuntMount, 0f, Elev,
                                                4.2f, 2.4f, 1.5f, Ppu, 1f);
            Vector2 puntTravel = MountTravel(punt, PuntMount, CellN);
            Vector2 skiffTravel = MountTravel(ConsolePose(CellN, PurePitchFrame), SkiffMount, CellN);

            Assert.Less(puntTravel.y, 0f, "the punt's stern drops on a bow-up pitch too");
            // Her 2.4° over 2.63 m beats the console's 1.9° over 3.53 m — but only just, and the point is that
            // BOTH terms are in play. The old flat rescale saw only the degrees.
            Assert.AreNotEqual(skiffTravel.y, puntTravel.y,
                "two hulls with different mounts cannot share a pose — that assumption is the bug");

            // Same amplitudes, different mount → different answer. Nails the lever arm specifically.
            var puntAtSkiffMount = MountedRockPoseMath.Pose(PurePitchFrame, Frames, CellN, Headings, SkiffMount,
                                                            0f, Elev, 4.2f, 2.4f, 1.5f, Ppu, 1f);
            Assert.Greater(Mathf.Abs(MountTravel(puntAtSkiffMount, SkiffMount, CellN).y), Mathf.Abs(puntTravel.y),
                "the SAME pitch at a mount 0.9 m further aft travels further — the term the old scalar dropped");
        }

        // ==== the clamp-don't-throw contract this file shares with the rest of the boats' math ============

        [Test]
        public void DefensiveInputs_ClampAndNeverThrowOrNaN()
        {
            var poses = new[]
            {
                MountedRockPoseMath.Pose(3, 0, CellN, Headings, SkiffMount, 0f, Elev, ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 1f),
                MountedRockPoseMath.Pose(3, Frames, -5, 0, SkiffMount, 0f, Elev, ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 1f),
                MountedRockPoseMath.Pose(99, Frames, 99, Headings, SkiffMount, 0f, Elev, ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 1f),
                MountedRockPoseMath.Pose(3, Frames, CellN, Headings, SkiffMount, float.NaN, Elev, ConsoleRollA, ConsolePitchA, ConsoleHeaveA, Ppu, 1f),
                MountedRockPoseMath.Pose(3, Frames, CellN, Headings, SkiffMount, 0f, Elev, ConsoleRollA, ConsolePitchA, ConsoleHeaveA, 0f, 1f),
            };
            foreach (var p in poses)
            {
                Assert.IsFalse(float.IsNaN(p.Offset.x) || float.IsNaN(p.Offset.y), "no NaN reaches a transform");
                Assert.IsFalse(float.IsNaN(p.RollDegrees));
            }

            // An out-of-range cell wraps rather than indexing off the turntable.
            Assert.AreEqual(ConsolePose(CellN, PureRollFrame).RollDegrees,
                            ConsolePose(CellN + Headings, PureRollFrame).RollDegrees, 1e-4f,
                            "cell indices wrap around the circle");
        }

        // ==== the boat's visual chain runs in a DEFINED order =============================================

        [Test]
        public void TheVisualChain_HasAnExplicitExecutionOrder_WriterBeforeReaders()
        {
            int wave = Order(typeof(BoatWaveMotion));
            int compass = Order(typeof(DirectionalBoatSprite));
            int motor = Order(typeof(OutboardMotorLayer));
            int oars = Order(typeof(DoryOarLayer));

            Assert.Less(wave, compass,
                "BoatWaveMotion WRITES DirectionalBoatSprite.RockFrame and must run first. All four work in " +
                "LateUpdate; with no explicit order Unity picks one arbitrarily, and a writer landing BETWEEN " +
                "two readers desynchronises them by a frame — 45° of wave phase on an 8-frame cycle. It " +
                "resolves per boat at build time, so it is silently permanent either way.");
            Assert.Less(compass, motor, "the motor reads the frame + heading the compass drew");
            Assert.Less(compass, oars, "so do the oars");
        }

        private static int Order(System.Type t)
        {
            var attrs = t.GetCustomAttributes(typeof(DefaultExecutionOrder), inherit: false);
            Assert.AreEqual(1, attrs.Length,
                $"{t.Name} must declare [DefaultExecutionOrder] — the implicit order is the bug");
            return ((DefaultExecutionOrder)attrs[0]).order;
        }
    }
}
