using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class DockLadderContactPlatePlayTests
    {
        private AshoreFigurePlatePlayTests _plate;

        [UnityTest]
        public IEnumerator NineMileCreekLadder_HighAndLow_BothWays()
        {
            WharfNightStage.RequireAGraphicsDevice();
            Assert.AreNotEqual(SaveStore.DefaultPath, SaveStore.ActivePath);
            _plate = new AshoreFigurePlatePlayTests();
            yield return _plate.SetUpRegion();
            yield return _plate.RunDockLadderContactPlate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_plate != null) yield return _plate.TearDownRegion();
        }
    }

    public partial class AshoreFigurePlatePlayTests
    {
        [Serializable] private sealed class DockContactPaper { public DockContactClips clips; }
        [Serializable] private sealed class DockContactClips
        {
            public DockContactClip ladderDown, board, boardDown, idle, walk, run, balance;
            public DockContactClip For(string state) => state switch
            {
                "ladderDown" => ladderDown, "board" => board, "boardDown" => boardDown,
                "idle" => idle, "walk" => walk, "run" => run, "balance" => balance, _ => null,
            };
        }
        [Serializable] private sealed class DockContactClip { public DockContactPin[] pins; }
        [Serializable] private sealed class DockContactPin
        {
            public float[] handL, handR, footL, footR;
        }

        private static Vector3 DockContactPoint(Transform frame, float[] pin)
            => frame.TransformPoint(new Vector3(pin[0], pin[1], pin[2]));

        private static float DockContactRailError(Vector3 hand, float x, float bottom, float top, float width)
        {
            float left = Mathf.Abs(hand.x - (x - width * .5f));
            float right = Mathf.Abs(hand.x - (x + width * .5f));
            float dy = hand.y - Mathf.Clamp(hand.y, bottom, top);
            return Mathf.Sqrt(Mathf.Min(left * left, right * right) + dy * dy);
        }

        internal IEnumerator RunDockLadderContactPlate()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence~/dock-lines/pr1/plates-ruling-10"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "_floor.txt"), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            yield return Arrive(NineMileCreekScene, Noon);
            Vector2 shore = NineMileCreekGroundPlan.DisembarkPos;
            Vector2 head = NineMileCreekGroundPlan.BerthLadderPos;
            yield return StandHer(shore, head + Vector2.down, 12f,
                "the banked berth ladder with the shipped dory placed alongside", plateFigure: false);
            // This contact fixture needs two IDs, independent of the rest of the town's population.
            // Preserve everything in the camera and release only off-camera facet components at runtime.
            int hullsBefore = IsoFacetHullRegistry.Count;
            int outside = 0;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_stage.Camera);
            foreach (var facet in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None))
            {
                bool inFrame = false;
                foreach (var renderer in facet.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy
                        && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) inFrame = true;
                if (!inFrame && facet.enabled) { facet.enabled = false; outside++; }
            }
            File.WriteAllText(Path.Combine(output, "nmc-fixture.txt"),
                $"Runtime berth fixture: {outside} off-camera facet components disabled; " +
                $"registered hulls {hullsBefore} -> {IsoFacetHullRegistry.Count}. " +
                "No banked scene asset edited; camera-visible geometry retained.\n");
            SetTheSwitch(true);
            if (_player.GetComponent<DeckWalkController>() == null)
                _player.gameObject.AddComponent<DeckWalkController>();
            if (_player.GetComponent<CharacterClipPlayer>() == null)
                _player.gameObject.AddComponent<CharacterClipPlayer>();
            var clip = _player.GetComponent<CharacterClipPlayer>();
            clip.Configure(_iso.Visual);
            File.WriteAllText(Path.Combine(output, "clip-clock.txt"),
                $"Board ClipFrameCount={_iso.Visual.ClipFrameCount(CharacterClip.Board)}; " +
                $"BoardDown ClipFrameCount={_iso.Visual.ClipFrameCount(CharacterClip.BoardDown)}.\n" +
                "The stage freezes Time.timeScale. This fixture advances non-driven clips by its measured tick, " +
                "and advances the game clock for the actual idle/walk/balance figure poses.\n" +
                "The rider keeps the mode written by ControlSwitcher, including after Complete.\n");
            if (_player.GetComponent<DeckRiderVisual>() == null)
            {
                var child = new GameObject("Plate deck rider");
                child.transform.SetParent(_player.transform, false);
                var riderSprite = child.AddComponent<SpriteRenderer>();
                riderSprite.enabled = false;
                _player.gameObject.AddComponent<DeckRiderVisual>().Configure(riderSprite, _sprite, _iso);
            }
            yield return FindHerPresenter();

            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Dory.asset");
            Assert.IsNotNull(hull);
            var boatGo = _stage.Track(new GameObject("Contact plate dory"));
            var boat = boatGo.AddComponent<BoatController>();
            boat.SetHull(hull);
            boat.enabled = false;
            var boatBody = boatGo.GetComponent<Rigidbody2D>();
            boatBody.simulated = false;
            float halfBeam = hull.Visual.HullMesh.WatertightHalfBeamMeters;
            Vector2 berth = new Vector2(head.x, NineMileCreekGroundPlan.FenderLineY - halfBeam);
            boatGo.transform.position = berth;
            boatGo.transform.rotation = NineMileCreekGroundPlan.ArrivalRotation;
            var rig = BoatHullSkinner.ApplyHull(boatGo, boatGo.AddComponent<SpriteRenderer>(), hull, boat,
                new BoatHullSkinner.Options { SkipOars = true });
            var tideRide = boatGo.GetComponent<HullTideRide>();
            Assert.IsNotNull(tideRide, "the fixture must use the live hull's tide publisher");
            Assert.IsNotNull(rig.Wave, "the live picture must apply the tide ride");
            Assert.IsNotNull(rig.Visual);
            var controls = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.IsNotNull(controls);
            var landing = _stage.Track(new GameObject("Contact plate landing"));
            landing.transform.position = shore;
            controls.Configure(_player, boat, null, landing.transform, 12f, landing.transform);
            controls.ConfigureHelm(new Vector2(10f, 10f), .05f);
            controls.ConfigureBoardingMove(true, 3f, .8f, .55f, .35f);
            controls.enabled = false;
            var sea = new MovePlateSea { Source = GameServices.Environment };
            GameServices.Environment = sea;
            var begin = typeof(ControlSwitcher).GetMethod("BeginBoardingMove", BindingFlags.Instance | BindingFlags.NonPublic);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type kind = begin.GetParameters()[0].ParameterType;
            var rows = new StringBuilder("tide,direction,elapsed,sequence_frame,leg,clip,frame,root_x,root_y,root_step_m,gap,rungs,loops,water,waterline,is_aground,rise,hull_picture_root_y,foot_y,foot_minus_hull_root_y,sole_l_y,sole_r_y,hand_rail_error_m,post_complete_frame,drawn_x,drawn_y,drawn_state,drawn_frame,balance_arms_out,drawn_step_m,entry_drawn_x,entry_drawn_y,picture,drawn_z,mesh_heave_m,mesh_ride_m,deck_x,deck_y,deck_height\n");
            var hullPose = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(hullPose);
            string pinPath = Path.Combine(Application.dataPath,
                "../docs/art/rigs/character/rig10/gameplay/characterIsoRig10.fisher.gameplay.json");
            var paper = JsonUtility.FromJson<DockContactPaper>(File.ReadAllText(pinPath));
            Assert.AreEqual(10, paper.clips.ladderDown.pins.Length);
            var drawnFrameField = typeof(IsoCharacterFigureRenderer).GetField("_meshChild", flags);
            Assert.IsNotNull(drawnFrameField);
            var isoTick = typeof(IsoCharacterSprite).GetMethod("LateUpdate", flags);
            var riderTick = typeof(DeckRiderVisual).GetMethod("LateUpdate", flags);
            Assert.IsNotNull(isoTick);
            Assert.IsNotNull(riderTick);
            IGameClock clock = GameServices.Clock;
            double savedClock = clock.TotalSeconds;
            Vector3 DrawnPoint()
            {
                var figure = _presenter.DrawsAshore ? _presenter.AshoreFigure : _presenter.Figure;
                Assert.IsNotNull(figure);
                return ((Transform)drawnFrameField.GetValue(figure)).TransformPoint(Vector3.zero);
            }
            Vector3 cameraPosition = _stage.Camera.transform.position;
            float cameraSize = _stage.Camera.orthographicSize;
            File.WriteAllText(Path.Combine(output, "nmc-camera.txt"), FormattableString.Invariant(
                $"camera_x={cameraPosition.x}\ncamera_y={cameraPosition.y}\northo={cameraSize}\nwidth={_stage.Width}\nheight={_stage.Height}\nladder_x={head.x}\nladder_y={head.y}\nhull={hull.Id}\nskin={_iso.Visual.Skin.Id}\n"));
            // The fixture just supplied a boat and freed off-camera ids. Reassert the actual mode once
            // as a fresh arrival, rather than overriding it on every measured frame.
            _rider.SetMode(controls.Mode, controls.Mode == ControlMode.OnDeck ? boatGo.transform : null);
            yield return null;
            riderTick.Invoke(_rider, null);
            try
            {
                foreach (float water in new[] { 2.2f, -2.2f })
                {
                    sea.Water = water;
                    Assert.AreEqual(water, HullTideRide.WaterLevelNow(), .0001f,
                        "the hull must read this plate's GameServices.Environment.WaterLevelAt");
                    // Let the runtime span's slow tick and the live hull's LateUpdate consume this tide.
                    yield return new WaitForSecondsRealtime(.6f);
                    PutHerAt(shore, true);
                    foreach (string direction in new[] { "Boarding", "Disembarking" })
                    {
                        Vector3 entryDrawn = DrawnPoint();
                        _iso.ReleaseHeading();
                        Assert.IsTrue((bool)begin.Invoke(controls, new[] { Enum.Parse(kind, direction) }));
                        Assert.IsTrue(controls.IsLadderBoarding);
                        float gap = (float)typeof(ControlSwitcher).GetField("_moveGapMetres", flags).GetValue(controls);
                        double loops = (double)typeof(ControlSwitcher).GetField("_moveEndPhase", flags).GetValue(controls);
                        var ladder = (IBoardingLadder)typeof(ControlSwitcher).GetField("_moveLadder", flags).GetValue(controls);
                        float approach = (float)typeof(ControlSwitcher).GetField("_moveApproachSeconds", flags).GetValue(controls);
                        float transition = (float)typeof(ControlSwitcher).GetField("_moveTransitionSeconds", flags).GetValue(controls);
                        float climbingSeconds = (float)typeof(ControlSwitcher).GetField("_moveClimbSeconds", flags).GetValue(controls);
                        var seat = (Vector3)typeof(ControlSwitcher).GetField("_moveLadderSeat", flags).GetValue(controls);
                        // Foot OF THE CLIMB (root waypoint), separate from the two published sole pins below.
                        float foot = seat.y - LadderBoardingMath.ProjectHeight(gap, IsoGround.CameraElevationDegrees);
                        int shots = 0, lastFrame = -1, sequenceFrame = 0;
                        CharacterClip lastClip = CharacterClip.None;
                        string lastLeg = "";
                        var rungFrames = new HashSet<int>();
                        bool visibleClimberProved = false;
                        Vector3 previous = _player.transform.position;
                        Vector3 previousDrawn = entryDrawn;
                        Vector3? lastOffDrawn = null;
                        int lastOffSequence = -1, afterComplete = -1, armsOutAfterComplete = 0;
                        float elapsed = 0f;
                        while ((controls.IsBoardingMove || afterComplete < 12) && elapsed < 60f)
                        {
                            Vector3 beforeTick = _player.transform.position;
                            bool moving = controls.IsBoardingMove;
                            controls.TickBoardingMove(.055f);
                            clip.Advance(.055f); // LateUpdate sees dt=0 on this deliberately frozen stage
                            clock.SeekTo(clock.TotalSeconds + .055);
                            Vector2 stepVelocity = (Vector2)(_player.transform.position - beforeTick) / .055f;
                            _iso.HoldSpeed(controls.IsBoardingMove && clip.Clip == CharacterClip.None ? stepVelocity.magnitude : 0f);
                            if (controls.IsBoardingMove && clip.Clip == CharacterClip.None && stepVelocity.sqrMagnitude > 0f)
                                _iso.HoldHeading(IsoCharacterMath.GroundHeadingFor(stepVelocity, 0f, _iso.HeadingDegrees));
                            elapsed += .055f;
                            isoTick.Invoke(_iso, null);
                            yield return null;
                            riderTick.Invoke(_rider, null); // publish the real handoff's mode; never force OnFoot
                            Assert.IsTrue(_presenter.DrawsInsteadOfSprite, _presenter.NotDrawingReason);
                            if (!controls.IsBoardingMove) afterComplete = moving ? 0 : afterComplete + 1;
                            if (clip.Clip == CharacterClip.LadderDown)
                            {
                                Assert.IsTrue(_presenter.DrawsAshore, _presenter.NotDrawingReason);
                                Assert.AreEqual(clip.Frame, _presenter.AshoreFigure.DrawnFrame);
                            }
                            float climbing = elapsed - approach - transition;
                            string leg = afterComplete > 0 ? "AfterComplete" : !controls.IsBoardingMove ? "Complete" : elapsed < approach ? "Approach"
                                : climbing < 0f ? "Mount" : climbing < climbingSeconds ? "Rungs" : "Off";
                            bool newPose = clip.Frame != lastFrame || clip.Clip != lastClip;
                            bool capture = leg != lastLeg || leg == "Mount" || leg == "Off" || afterComplete >= 0
                                || (leg == "Rungs" && newPose && (rungFrames.Count < 10
                                    || climbingSeconds - climbing < LadderBoardingMath.RigLoopSeconds));
                            if (leg == "Rungs") rungFrames.Add(clip.Frame);
                            lastFrame = clip.Frame;
                            lastClip = clip.Clip;
                            lastLeg = leg;
                            _stage.Camera.transform.position = cameraPosition;
                            _stage.Camera.orthographicSize = cameraSize;
                            string name = "";
                            if (capture)
                            {
                                name = $"nmc-{(water > 0f ? "high" : "low")}-{direction.ToLowerInvariant()}-{sequenceFrame:0000}-{leg.ToLowerInvariant()}-f{clip.Frame:00}.png";
                                byte[] image = _stage.Capture();
                                shots++;
                                if (!visibleClimberProved && clip.Clip == CharacterClip.LadderDown)
                                {
                                    _presenter.AshoreFigure.Visible = false;
                                    byte[] absent = _stage.Capture();
                                    _presenter.AshoreFigure.Visible = true;
                                    Assert.Greater(WharfNightStage.Footprint(image, absent), 5,
                                        "the plate must contain visible pixels from the actual mesh climber");
                                    visibleClimberProved = true;
                                }
                                var tex = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
                                tex.LoadRawTextureData(image);
                                tex.Apply();
                                File.WriteAllBytes(Path.Combine(output, name), tex.EncodeToPNG());
                                Object.DestroyImmediate(tex);
                            }
                            Vector3 root = _player.transform.position;
                            var drawnFigure = _presenter.DrawsAshore ? _presenter.AshoreFigure : _presenter.Figure;
                            var drawnTransform = (Transform)drawnFrameField.GetValue(drawnFigure);
                            Vector3 drawn = DrawnPoint();
                            string drawnState = drawnFigure.DrawnStateKey;
                            int drawnFrame = drawnFigure.DrawnFrame;
                            bool armsOut = drawnState == "balance"; // all eight published balance poses hold both arms out
                            if (afterComplete >= 0 && armsOut) armsOutAfterComplete++;
                            if (leg == "Off") { lastOffDrawn = drawn; lastOffSequence = sequenceFrame; }
                            float soleL = float.NaN, soleR = float.NaN, hands = float.NaN;
                            var drawnPins = paper.clips.For(drawnState)?.pins;
                            if (drawnPins != null && drawnFrame >= 0 && drawnFrame < drawnPins.Length)
                            {
                                var pin = drawnPins[drawnFrame];
                                soleL = DockContactPoint(drawnTransform, pin.footL).y;
                                soleR = DockContactPoint(drawnTransform, pin.footR).y;
                                float top = head.y + ladder.ClimbTopScreenOffset;
                                float bottom = top - LadderBoardingMath.ProjectHeight(ladder.TopElevationMeters - water,
                                    IsoGround.CameraElevationDegrees);
                                if (drawnState == "ladderDown")
                                    hands = Mathf.Max(DockContactRailError(DockContactPoint(drawnTransform, pin.handL), head.x, bottom, top, .45f),
                                        DockContactRailError(DockContactPoint(drawnTransform, pin.handR), head.x, bottom, top, .45f));
                            }
                            float hullY = rig.Visual.position.y;
                            float step = Vector3.Distance(root, previous);
                            var deckWalk = _player.GetComponent<DeckWalkController>();
                            rows.AppendLine(FormattableString.Invariant($"{water},{direction},{elapsed},{sequenceFrame},{leg},{clip.Clip},{clip.Frame},{root.x},{root.y},{step},{gap},{gap / ladder.RungMetres},{loops},{HullTideRide.WaterLevelNow()},{tideRide.WaterlineNow()},{tideRide.IsAgroundNow()},{tideRide.ScreenRiseNow()},{hullY},{foot},{foot - hullY},{soleL},{soleR},{hands},{afterComplete},{drawn.x},{drawn.y},{drawnState},{drawnFrame},{armsOut},{Vector3.Distance(drawn, previousDrawn)},{entryDrawn.x},{entryDrawn.y},{name},{drawn.z},{hullPose.HeavePixels / hull.Visual.HullMesh.PxPerMetre},{hullPose.RidePixels / hull.Visual.HullMesh.PxPerMetre},{deckWalk.DeckLocalPosition.x},{deckWalk.DeckLocalPosition.y},{deckWalk.DeckHeightMeters}"));
                            previous = root;
                            previousDrawn = drawn;
                            File.WriteAllText(Path.Combine(output, "nmc-contact.csv"), rows.ToString());
                            // Write the failing row and picture first so a STOP has exact frame numbers.
                            if (direction == "Disembarking" && sequenceFrame == 0)
                                Assert.LessOrEqual(Vector3.Distance(entryDrawn, drawn), .25f, "the first ashore frame must start on the ridden deck");
                            if (afterComplete >= 0)
                            {
                                Assert.IsTrue(lastOffDrawn.HasValue, "retain the last Off frame");
                                Assert.LessOrEqual(Vector3.Distance(lastOffDrawn.Value, drawn), .25f,
                                    $"drawn handoff moved from Off seq {lastOffSequence} to seq {sequenceFrame}, post {afterComplete}");
                                if (direction == "Boarding")
                                {
                                    // Undo the actual posed hull transform, including its heave and rock.
                                    // A tide-only subtraction mistakes a correctly ridden figure for off-deck.
                                    Vector3 stand = hullPose.PosedMesh.InverseTransformPoint(drawn);
                                    Vector3 unridden = boatGo.transform.position + (Vector3)DeckAreaMath.DeckToWorld(
                                        new Vector2(stand.x, stand.y), stand.z,
                                        DeckWalkController.DrawnHeadingDegreesOf(boatGo.transform),
                                        DeckWalkController.BakeElevationDegreesOf(boatGo.transform));
                                    Assert.IsTrue(deckWalk.TryDeckPointWorldAtHeight(unridden - boatGo.transform.position,
                                        ControlSwitcher.BoardSpotHeightMeters, out Vector3 held));
                                    Assert.LessOrEqual(Vector3.Distance(held, unridden), 1f / 32f, "the drawn figure must stay on her deck");
                                }
                                else Assert.LessOrEqual(Vector2.Distance(drawn, shore), .25f, "the drawn figure stays on the planks");
                            }
                            sequenceFrame++;
                        }
                        Assert.IsFalse(controls.IsBoardingMove);
                        Assert.AreEqual(direction == "Boarding" ? ControlMode.OnDeck : ControlMode.OnFoot, controls.Mode);
                        Assert.GreaterOrEqual(rungFrames.Count, 8, direction + ": at least eight distinct rung frames");
                        Assert.IsTrue(visibleClimberProved, "prove actual climber pixels in a rung frame");
                        Assert.AreEqual(12, afterComplete, "capture all twelve frames past Complete");
                        File.AppendAllText(Path.Combine(output, "post-complete.txt"),
                            FormattableString.Invariant($"water={water} direction={direction} frames=Complete+12 arms_out_balance_frames={armsOutAfterComplete}\n"));
                        Debug.Log(FormattableString.Invariant($"[dock-contact] {water} {direction}: gap {gap}, rungs {gap / ladder.RungMetres}, loops {loops}, duration {elapsed}, shots {shots}"));
                    }
                }
            }
            finally
            {
                _iso.ReleaseSpeed();
                _iso.ReleaseHeading();
                clock.SeekTo(savedClock);
                GameServices.Environment = sea.Source;
            }
        }
    }
}
