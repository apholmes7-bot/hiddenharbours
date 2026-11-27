using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The player's camera, first slice (owner 2026-11-26): how a picture is judged, what the shutter keeps,
    /// the keys' gates, Odile's request through flags and the album, the album page, and the shipped data.
    /// All headless: the camera's model, the scoring, the request talk and the page are pure.
    /// </summary>
    public class PlayerCameraTests
    {
        const string Seal = "wildlife.grey_seal";
        const string Gull = "wildlife.herring_gull";
        const string Region = "region.st_peters";
        const string Local = SavePlayerKeys.Local;
        const string SpeciesDefTypeName = "WildlifeSpeciesDef";

        PhotoCameraDef _def;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<PhotoCameraDef>();
            _def.Id = "photo_camera.test";
            _def.DisplayName = "test camera";
            _def.Subjects = new List<PhotoSubjectEntry>
            {
                new PhotoSubjectEntry { SubjectId = Seal, DisplayName = "grey seal", PluralName = "grey seals" },
            };
            _def.Places = new List<PhotoPlaceEntry> { new PhotoPlaceEntry { RegionId = Region, Name = "St Peters" } };
            _made.Add(_def);
            PhotoRequestTalk.RequestsSource = null;
            WildlifePresences.Reset();
            PhotoViewfinder.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            PhotoRequestTalk.RequestsSource = null;
            WildlifePresences.Reset();
            PhotoViewfinder.Reset();
        }

        static WildlifeSighting S(string id, float x, float y, WildlifePose pose, int key = 0)
            => new WildlifeSighting(id, new Vector2(x, y), pose, key);

        // ---- scoring ------------------------------------------------------------------------------

        [Test]
        public void TheFrameIsCentredOnTheAimAndSizedByTheDef()
        {
            Rect f = PhotoScoring.FrameAt(_def, new Vector2(10f, 20f));
            Assert.That(f.center, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(f.width, Is.EqualTo(_def.FrameWidthMetres));
            Assert.That(f.height, Is.EqualTo(_def.FrameHeightMetres));
        }

        [Test]
        public void TheAimIsHeldInsideTheReach()
        {
            Vector2 far = PhotoScoring.ClampAim(_def, new Vector2(100f, 0f));
            Assert.That(far.magnitude, Is.EqualTo(_def.MaxReachMetres).Within(1e-4f));
            Assert.That(PhotoScoring.ClampAim(_def, new Vector2(3f, 4f)), Is.EqualTo(new Vector2(3f, 4f)));
        }

        [Test]
        public void SharpnessFallsWithDistanceToTheFarFloor()
        {
            Assert.That(PhotoScoring.DistanceQuality(_def, 0f), Is.EqualTo(1f));
            Assert.That(PhotoScoring.DistanceQuality(_def, _def.FullQualityDistanceMetres), Is.EqualTo(1f));
            Assert.That(PhotoScoring.DistanceQuality(_def, 1000f), Is.EqualTo(_def.FarQuality));
            float mid = PhotoScoring.DistanceQuality(_def,
                (_def.FullQualityDistanceMetres + _def.FarDistanceMetres) * 0.5f);
            Assert.That(mid, Is.LessThan(1f).And.GreaterThan(_def.FarQuality));
        }

        [Test]
        public void TheMiddleOfTheFrameBeatsItsEdge()
        {
            Rect f = PhotoScoring.FrameAt(_def, Vector2.zero);
            Assert.That(PhotoScoring.CentreQuality(_def, f, Vector2.zero), Is.EqualTo(1f));
            Assert.That(PhotoScoring.CentreQuality(_def, f, new Vector2(f.xMax, 0f)),
                        Is.EqualTo(1f - _def.CentreWeight).Within(1e-5f));
        }

        [Test]
        public void ARestingSealIsABetterPictureThanASwimmingOneAndADiverIsNone()
        {
            Assert.That(_def.QualityOf(WildlifePose.Resting), Is.GreaterThan(_def.QualityOf(WildlifePose.Swimming)));
            Assert.That(_def.QualityOf(WildlifePose.Diving), Is.EqualTo(0f));
        }

        [Test]
        public void ExtraAnimalsAddACappedBonus()
        {
            Assert.That(PhotoScoring.ScoreOf(_def, 0.5f, 1), Is.EqualTo(50));
            Assert.That(PhotoScoring.ScoreOf(_def, 0.5f, 2), Is.EqualTo(50 + _def.CountBonusPerExtra));
            Assert.That(PhotoScoring.ScoreOf(_def, 0.5f, 99), Is.EqualTo(50 + _def.CountBonusMax));
            Assert.That(PhotoScoring.ScoreOf(_def, 1f, 99), Is.EqualTo(100));
        }

        [Test]
        public void EvaluateGroupsByKindCountsAndKeepsTheBestPose()
        {
            var sightings = new[]
            {
                S(Seal, 1f, 0f, WildlifePose.Alert, 1),
                S(Seal, 0f, 0f, WildlifePose.Resting, 2),
                S(Seal, -1f, 0f, WildlifePose.Swimming, 3),
            };
            var into = new PhotoSubjectResult[4];
            int n = PhotoScoring.Evaluate(_def, Vector2.zero, PhotoScoring.FrameAt(_def, Vector2.zero),
                                          sightings, sightings.Length, into);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(into[0].SubjectId, Is.EqualTo(Seal));
            Assert.That(into[0].Count, Is.EqualTo(3));
            Assert.That(into[0].Pose, Is.EqualTo(WildlifePose.Resting));
            Assert.That(into[0].Score, Is.EqualTo(100));
        }

        [Test]
        public void EvaluateLeavesOutWhatIsOutsideUnknownOrUnder()
        {
            var sightings = new[]
            {
                S(Seal, 100f, 100f, WildlifePose.Resting),   // outside the frame
                S(Gull, 0f, 0f, WildlifePose.Resting),       // a kind the camera cannot name
                S(Seal, 0f, 0f, WildlifePose.Diving),        // under the water
            };
            var into = new PhotoSubjectResult[4];
            Assert.That(PhotoScoring.Evaluate(_def, Vector2.zero, PhotoScoring.FrameAt(_def, Vector2.zero),
                                              sightings, sightings.Length, into), Is.EqualTo(0));
        }

        [Test]
        public void EvaluateIsTheSameWhateverOrderTheAnimalsAreListedIn()
        {
            _def.Subjects.Add(new PhotoSubjectEntry { SubjectId = Gull, DisplayName = "gull", PluralName = "gulls" });
            var a = new[]
            {
                S(Seal, 2f, 1f, WildlifePose.Alert, 1), S(Gull, -2f, 0f, WildlifePose.Resting, 2),
                S(Seal, 0f, 0f, WildlifePose.Resting, 3),
            };
            var b = a.Reverse().ToArray();
            Rect f = PhotoScoring.FrameAt(_def, Vector2.zero);
            var ra = new PhotoSubjectResult[4];
            var rb = new PhotoSubjectResult[4];
            int na = PhotoScoring.Evaluate(_def, Vector2.zero, f, a, a.Length, ra);
            int nb = PhotoScoring.Evaluate(_def, Vector2.zero, f, b, b.Length, rb);
            Assert.That(nb, Is.EqualTo(na));
            for (int i = 0; i < na; i++)
            {
                Assert.That(rb[i].SubjectId, Is.EqualTo(ra[i].SubjectId));
                Assert.That(rb[i].Count, Is.EqualTo(ra[i].Count));
                Assert.That(rb[i].Pose, Is.EqualTo(ra[i].Pose));
                Assert.That(rb[i].Score, Is.EqualTo(ra[i].Score));
            }
        }

        [Test]
        public void ThePicturesScoreIsItsBestKinds()
        {
            var r = new[] { new PhotoSubjectResult { Score = 40 }, new PhotoSubjectResult { Score = 85 } };
            Assert.That(PhotoScoring.PhotoScore(r, 2), Is.EqualTo(85));
            Assert.That(PhotoScoring.PhotoScore(r, 0), Is.EqualTo(0));
        }

        // ---- the wildlife seam's in-frame copy -----------------------------------------------------

        sealed class FakePresence : IWildlifePresence
        {
            public WildlifeSighting[] Drawn = new WildlifeSighting[0];
            public int Count => Drawn.Length;
            public int CopySightings(WildlifeSighting[] into)
            {
                if (into == null) return 0;
                int n = Mathf.Min(into.Length, Drawn.Length);
                for (int i = 0; i < n; i++) into[i] = Drawn[i];
                return n;
            }
        }

        [Test]
        public void CopyInFrameTakesEveryVisibleKindInsideTheFrameFromEveryProducer()
        {
            var a = new FakePresence { Drawn = new[] { S(Seal, 0f, 0f, WildlifePose.Resting), S(Seal, 50f, 0f, WildlifePose.Resting) } };
            var b = new FakePresence { Drawn = new[] { S(Gull, 1f, 1f, WildlifePose.Alert), S(Seal, 1f, 0f, WildlifePose.Diving) } };
            WildlifePresences.Register(a);
            WildlifePresences.Register(b);
            var scratch = new WildlifeSighting[8];
            var into = new WildlifeSighting[8];
            int n = WildlifePresences.CopyInFrame(new Rect(-5f, -5f, 10f, 10f), scratch, into);
            Assert.That(n, Is.EqualTo(2));
            Assert.That(into.Take(n).Select(s => s.SubjectId), Is.EquivalentTo(new[] { Seal, Gull }));
        }

        [Test]
        public void CopyInFrameStopsAtTheBuffer()
        {
            var a = new FakePresence { Drawn = new[] { S(Seal, 0f, 0f, WildlifePose.Resting), S(Seal, 1f, 0f, WildlifePose.Resting), S(Seal, 2f, 0f, WildlifePose.Resting) } };
            WildlifePresences.Register(a);
            var into = new WildlifeSighting[2];
            Assert.That(WildlifePresences.CopyInFrame(new Rect(-5f, -5f, 10f, 10f), new WildlifeSighting[8], into),
                        Is.EqualTo(2));
            Assert.That(WildlifePresences.CopyInFrame(new Rect(0f, 0f, 1f, 1f), null, into), Is.EqualTo(0));
        }

        // ---- the camera's model -------------------------------------------------------------------

        [Test]
        public void RaisingPutsTheFrameAheadOfTheWayHeFaced()
        {
            var cam = new PhotoCameraModel(_def);
            cam.NoteFacing(Vector2.up);
            cam.NoteFacing(Vector2.zero);   // on deck: keeps the last facing
            cam.Raise();
            Assert.That(cam.IsUp, Is.True);
            Assert.That(cam.Aim, Is.EqualTo(Vector2.up * _def.StartReachMetres));
            Assert.That(cam.FrameFor(new Vector2(5f, 5f)).center, Is.EqualTo(new Vector2(5f, 5f + _def.StartReachMetres)));
            cam.Lower();
            Assert.That(cam.IsUp, Is.False);
        }

        [Test]
        public void SteeringSlidesTheFrameAndHoldsItInsideTheReach()
        {
            var cam = new PhotoCameraModel(_def);
            cam.NoteFacing(Vector2.right);
            cam.Raise();
            cam.Steer(Vector2.up, 1f);
            Assert.That(cam.Aim.y, Is.EqualTo(_def.AimSpeedMetresPerSecond).Within(1e-4f));
            for (int i = 0; i < 100; i++) cam.Steer(Vector2.right, 1f);
            Assert.That(cam.Aim.magnitude, Is.EqualTo(_def.MaxReachMetres).Within(1e-3f));
        }

        [Test]
        public void SteeringDoesNothingWhileTheCameraIsDown()
        {
            var cam = new PhotoCameraModel(_def);
            cam.Steer(Vector2.up, 1f);
            Assert.That(cam.Aim, Is.EqualTo(Vector2.zero));
        }

        PhotoCameraModel RaisedAt(Vector2 facing)
        {
            var cam = new PhotoCameraModel(_def);
            cam.NoteFacing(facing);
            cam.Raise();
            return cam;
        }

        WildlifeSighting[] SealsAhead(int n, WildlifePose pose)
        {
            var s = new WildlifeSighting[n];
            for (int i = 0; i < n; i++) s[i] = S(Seal, _def.StartReachMetres + i * 0.5f, 0f, pose, i);
            return s;
        }

        [Test]
        public void TheShutterKeepsAPictureOfSealsInTheAlbum()
        {
            var save = new SaveData();
            var cam = RaisedAt(Vector2.right);
            var seals = SealsAhead(3, WildlifePose.Resting);
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 3600d, Region, 10f, seals, seals.Length),
                        Is.EqualTo(PhotoShotOutcome.Kept));
            Assert.That(save.Photos.Count, Is.EqualTo(1));
            PhotoRecordDto p = save.Photos[0];
            Assert.That(p.Number, Is.EqualTo(1));
            Assert.That(p.RegionId, Is.EqualTo(Region));
            Assert.That(p.GameTimeSeconds, Is.EqualTo(3600d));
            Assert.That(p.Subjects[0].SubjectId, Is.EqualTo(Seal));
            Assert.That(p.Subjects[0].Count, Is.EqualTo(3));
            Assert.That(p.Subjects[0].Pose, Is.EqualTo((int)WildlifePose.Resting));
            Assert.That(cam.LineFor(PhotoShotOutcome.Kept), Is.EqualTo("3 grey seals: " + _def.GoodWord + "."));
        }

        [Test]
        public void TheSamePictureScoresTheSameEveryTime()
        {
            var seals = SealsAhead(4, WildlifePose.Alert);
            var s1 = new SaveData();
            var s2 = new SaveData();
            RaisedAt(Vector2.right).Shoot(s1, Local, Vector2.zero, 10d, Region, 0f, seals, seals.Length);
            RaisedAt(Vector2.right).Shoot(s2, Local, Vector2.zero, 10d, Region, 99f, seals, seals.Length);
            Assert.That(JsonUtility.ToJson(s2.Photos[0]), Is.EqualTo(JsonUtility.ToJson(s1.Photos[0])));
        }

        [Test]
        public void APictureOfNothingIsNotKept()
        {
            var save = new SaveData();
            var cam = RaisedAt(Vector2.right);
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 0d, Region, 0f, new WildlifeSighting[0], 0),
                        Is.EqualTo(PhotoShotOutcome.NothingInFrame));
            Assert.That(save.Photos, Is.Empty);
            Assert.That(cam.LineFor(PhotoShotOutcome.NothingInFrame), Is.EqualTo(_def.NothingInFrameLine));
        }

        [Test]
        public void TheShutterWaitsOutItsCooldownAndNeedsTheCameraUp()
        {
            var save = new SaveData();
            var seals = SealsAhead(2, WildlifePose.Resting);
            var down = new PhotoCameraModel(_def);
            Assert.That(down.Shoot(save, Local, Vector2.zero, 0d, Region, 0f, seals, 2), Is.EqualTo(PhotoShotOutcome.Cooling));

            var cam = RaisedAt(Vector2.right);
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 0d, Region, 5f, seals, 2), Is.EqualTo(PhotoShotOutcome.Kept));
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 0d, Region, 5f + _def.ShutterCooldownSeconds * 0.5f, seals, 2),
                        Is.EqualTo(PhotoShotOutcome.Cooling));
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 0d, Region, 5f + _def.ShutterCooldownSeconds, seals, 2),
                        Is.EqualTo(PhotoShotOutcome.Kept));
            Assert.That(save.Photos.Select(p => p.Number), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void AFullAlbumTakesNoMore()
        {
            _def.AlbumCapacity = 1;
            var save = new SaveData();
            var seals = SealsAhead(2, WildlifePose.Resting);
            var cam = RaisedAt(Vector2.right);
            cam.Shoot(save, Local, Vector2.zero, 0d, Region, 0f, seals, 2);
            Assert.That(cam.Shoot(save, Local, Vector2.zero, 0d, Region, 10f, seals, 2), Is.EqualTo(PhotoShotOutcome.AlbumFull));
            Assert.That(save.Photos.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheShutterReadsTheLiveWildlifeSeam()
        {
            WildlifePresences.Register(new FakePresence { Drawn = SealsAhead(2, WildlifePose.Resting) });
            var save = new SaveData();
            Assert.That(RaisedAt(Vector2.right).Shoot(save, Local, Vector2.zero, 0d, Region, 0f),
                        Is.EqualTo(PhotoShotOutcome.Kept));
            Assert.That(save.Photos[0].Subjects[0].Count, Is.EqualTo(2));
        }

        // ---- the player's gates -------------------------------------------------------------------

        [Test]
        public void TheCameraAnswersOnFootAndOnDeckOnly()
        {
            Assert.That(PlayerPhotoCamera.Answers(InteractContext.OnFoot), Is.True);
            Assert.That(PlayerPhotoCamera.Answers(InteractContext.OnDeck), Is.True);
            Assert.That(PlayerPhotoCamera.Answers(InteractContext.AtHelm), Is.False);
            Assert.That(PlayerPhotoCamera.Answers(InteractContext.Driving), Is.False);
        }

        [Test]
        public void TheCameraIsHisOnlyOnceItsFlagIsSet()
        {
            _def.OwnedWhileFlag = "";
            Assert.That(PlayerPhotoCamera.Owned(_def, null), Is.True, "no flag: always his");
            _def.OwnedWhileFlag = "st_peters.odile_seals.asked";
            Assert.That(PlayerPhotoCamera.Owned(_def, null), Is.False, "no save: not his");
            Assert.That(PlayerPhotoCamera.Owned(null, null), Is.False);
        }

        [Test]
        public void AStoppedWorldOrCapturedKeysTakeTheCamerasKeys()
        {
            CameraIntents live = DeviceCameraIntentSource.Map(true, true, Vector2.one, false, false);
            Assert.That(live.Raise && live.Shutter, Is.True);
            Assert.That(live.Aim, Is.EqualTo(Vector2.one));
            CameraIntents paused = DeviceCameraIntentSource.Map(true, true, Vector2.one, true, false);
            Assert.That(paused.Raise || paused.Shutter, Is.False);
            Assert.That(paused.Aim, Is.EqualTo(Vector2.zero));
            CameraIntents captured = DeviceCameraIntentSource.Map(true, true, Vector2.one, false, true);
            Assert.That(captured.Raise || captured.Shutter, Is.False);
        }

        // ---- the viewfinder's pixels ----------------------------------------------------------------

        [Test]
        public void TheViewfinderSnapsEveryCornerToAWholeKitPixel()
        {
            Assert.That(ViewfinderOverlay.SnapToKit(10f, 3), Is.EqualTo(9f));
            Assert.That(ViewfinderOverlay.SnapToKit(10.6f, 3), Is.EqualTo(12f));
            Assert.That(ViewfinderOverlay.SnapToKit(7.4f, 0), Is.EqualTo(7f), "a zero kit is treated as one pixel");
            Assert.That(ViewfinderOverlay.KitPixels(1f), Is.EqualTo(DialogueBubbleKit.ArtScale));
            Assert.That(ViewfinderOverlay.KitPixels(2f / DialogueBubbleKit.ArtScale), Is.EqualTo(2));
            Assert.That(ViewfinderOverlay.KitPixels(0f), Is.EqualTo(1));
            Assert.That(ViewfinderOverlay.ThickKitPixels, Is.EqualTo(1), "one pixel size on screen");
        }

        [Test]
        public void TheViewfinderStateShowsHidesAndCountsShots()
        {
            PhotoViewfinder.Show(new Rect(0f, 0f, 10f, 7f), "hint");
            Assert.That(PhotoViewfinder.IsUp, Is.True);
            PhotoViewfinder.Say("words", 5f);
            Assert.That(PhotoViewfinder.Shots, Is.EqualTo(0));
            PhotoViewfinder.Shot("picture", 1f, 3f);
            Assert.That(PhotoViewfinder.Shots, Is.EqualTo(1));
            Assert.That(PhotoViewfinder.Line, Is.EqualTo("picture"));
            PhotoViewfinder.Hide();
            Assert.That(PhotoViewfinder.IsUp, Is.False);
        }

        // ---- Odile's request ----------------------------------------------------------------------

        sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        sealed class FakeSave : ISaveService
        {
            public SaveData Data = new SaveData();
            public SaveData Current => Data;
            public bool LoadedExistingSave => false;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        PhotoRequestDef Request()
        {
            var r = ScriptableObject.CreateInstance<PhotoRequestDef>();
            _made.Add(r);
            r.Id = "photo_request.test_seals";
            r.GiverNpcId = "npc.odile_marchand";
            r.SubjectId = Seal;
            r.MinCount = 2;
            r.WorstPose = WildlifePose.Alert;
            r.MinScore = 30;
            r.AskedFlag = "t.asked";
            r.PicturedFlag = "t.pictured";
            r.DoneFlag = "t.done";
            r.RewardMoney = 15;
            r.AskLines = new[] { "ask" };
            r.ReminderLines = new[] { "remind" };
            r.ThanksLines = new[] { "thanks" };
            r.AfterLines = new[] { "after" };
            PhotoRequestTalk.RequestsSource = () => new[] { r };
            return r;
        }

        static void AddSealPicture(SaveData save, int count, WildlifePose pose, int score)
        {
            var subjects = new[] { new PhotoSubjectResult { SubjectId = Seal, Count = count, Pose = pose, Score = score } };
            PhotoAlbumRows.Add(save, Local, 0d, Region, subjects, 1, 120);
        }

        [Test]
        public void SheAsksFirstAndOnlyAHeardAskSetsTheFlag()
        {
            Request();
            var flags = new InMemoryFlagStore();
            string[] lines = PhotoRequestTalk.WithLines("npc.odile_marchand", new[] { "hello" }, new FakeSave(), flags,
                                                        out System.Action heard);
            Assert.That(lines, Is.EqualTo(new[] { "hello", "ask" }));
            Assert.That(flags.Get("t.asked"), Is.False, "nothing is set before the talk is heard");
            heard();
            Assert.That(flags.Get("t.asked"), Is.True);
        }

        [Test]
        public void SomebodyElseHearsNothingOfIt()
        {
            Request();
            var flags = new InMemoryFlagStore();
            string[] mine = { "hello" };
            Assert.That(PhotoRequestTalk.WithLines("npc.ginny", mine, new FakeSave(), flags, out System.Action heard),
                        Is.SameAs(mine));
            Assert.That(heard, Is.Null);
        }

        [Test]
        public void ARequiredFlagHoldsTheAsking()
        {
            PhotoRequestDef r = Request();
            r.RequiresFlag = "t.met";
            var flags = new InMemoryFlagStore();
            Assert.That(PhotoRequestTalk.StageOf(r, null, flags), Is.EqualTo(PhotoRequestStage.None));
            flags.Set("t.met", true);
            Assert.That(PhotoRequestTalk.StageOf(r, null, flags), Is.EqualTo(PhotoRequestStage.Ask));
        }

        [Test]
        public void SheRemindsUntilThePictureWillDo()
        {
            PhotoRequestDef r = Request();
            var flags = new InMemoryFlagStore();
            flags.Set("t.asked", true);
            var save = new FakeSave();
            Assert.That(PhotoRequestTalk.StageOf(r, save.Data, flags), Is.EqualTo(PhotoRequestStage.Waiting));

            AddSealPicture(save.Data, 1, WildlifePose.Resting, 90);      // one seal: too few
            AddSealPicture(save.Data, 3, WildlifePose.Sliding, 90);      // sliding in: worse than alert
            AddSealPicture(save.Data, 3, WildlifePose.Resting, 10);      // too poor
            Assert.That(PhotoRequestTalk.StageOf(r, save.Data, flags), Is.EqualTo(PhotoRequestStage.Waiting));
            Assert.That(PhotoRequestTalk.WithLines(r.GiverNpcId, null, save, flags, out _), Is.EqualTo(new[] { "remind" }));

            AddSealPicture(save.Data, 2, WildlifePose.Alert, 60);
            Assert.That(PhotoRequestTalk.StageOf(r, save.Data, flags), Is.EqualTo(PhotoRequestStage.Thank));
        }

        [Test]
        public void TheCamerasMessageMarksItPicturedOnlyOnceAsked()
        {
            Request();
            var flags = new InMemoryFlagStore();
            var save = new SaveData();
            AddSealPicture(save, 3, WildlifePose.Resting, 90);
            Assert.That(PhotoRequestTalk.MarkPictured(save, flags), Is.EqualTo(0), "not asked yet");
            flags.Set("t.asked", true);
            Assert.That(PhotoRequestTalk.MarkPictured(save, flags), Is.EqualTo(1));
            Assert.That(flags.Get("t.pictured"), Is.True);
            Assert.That(PhotoRequestTalk.MarkPictured(save, flags), Is.EqualTo(0), "marked once");
        }

        [Test]
        public void HerThanksHeardToItsEndCompletesTheRequestAndPaysOnce()
        {
            PhotoRequestDef r = Request();
            var flags = new InMemoryFlagStore();
            flags.Set("t.asked", true);
            var save = new FakeSave();
            AddSealPicture(save.Data, 3, WildlifePose.Resting, 90);

            var wallet = new FakeWallet();
            Assert.That(PhotoRequestTalk.StageOf(r, save.Data, flags), Is.EqualTo(PhotoRequestStage.Thank));
            PhotoRequestTalk.Thank(r, flags, wallet);
            PhotoRequestTalk.Thank(r, flags, wallet);
            Assert.That(flags.Get("t.done") && flags.Get("t.pictured"), Is.True);
            Assert.That(wallet.Money, Is.EqualTo(15));
            Assert.That(PhotoRequestTalk.WithLines(r.GiverNpcId, null, save, flags, out System.Action heard),
                        Is.EqualTo(new[] { "after" }));
            Assert.That(heard, Is.Null);
        }

        [Test]
        public void AGuestsPictureDoesNotAnswerHerRequest()
        {
            PhotoRequestDef r = Request();
            var flags = new InMemoryFlagStore();
            flags.Set("t.asked", true);
            var save = new SaveData();
            var subjects = new[] { new PhotoSubjectResult { SubjectId = Seal, Count = 3, Pose = WildlifePose.Resting, Score = 90 } };
            PhotoAlbumRows.Add(save, "player.guest.1", 0d, Region, subjects, 1, 120);
            Assert.That(PhotoRequestTalk.StageOf(r, save, flags), Is.EqualTo(PhotoRequestStage.Waiting));
        }

        // ---- the quest view -----------------------------------------------------------------------

        [Test]
        public void AQuestWithAnOpeningFlagStaysOffTheBoardUntilItIsSet()
        {
            var q = ScriptableObject.CreateInstance<QuestDef>();
            _made.Add(q);
            q.Id = "quest.test_seals";
            q.Title = "Seals";
            q.Steps = new List<QuestStep> { new QuestStep { Label = "picture them", Flag = "t.pictured" } };
            q.OpensWithFlag = "t.asked";
            var flags = new InMemoryFlagStore();
            var active = new List<QuestDef>();
            var archived = new List<QuestDef>();
            Assert.That(QuestBoard.IsOpen(q, flags), Is.False);
            QuestBoard.Sort(new[] { q }, flags, active, archived);
            Assert.That(active.Count + archived.Count, Is.EqualTo(0));
            flags.Set("t.asked", true);
            Assert.That(QuestBoard.IsOpen(q, flags), Is.True);
            QuestBoard.Sort(new[] { q }, flags, active, archived);
            Assert.That(active.Count, Is.EqualTo(1));

            q.OpensWithFlag = "";
            Assert.That(QuestBoard.IsOpen(q, new InMemoryFlagStore()), Is.True, "no opening flag: always open");
        }

        // ---- the album page -----------------------------------------------------------------------

        [Test]
        public void TheAlbumPageListsHisPicturesNewestFirst()
        {
            var save = new SaveData();
            AddSealPicture(save, 1, WildlifePose.Resting, 90);
            AddSealPicture(save, 3, WildlifePose.Alert, 50);
            save.Photos[1].GameTimeSeconds = 1.5d * 86400d + 3600d * 2.5d;
            var subjects = new[] { new PhotoSubjectResult { SubjectId = Seal, Count = 1, Pose = WildlifePose.Resting, Score = 90 } };
            PhotoAlbumRows.Add(save, "player.guest.1", 0d, Region, subjects, 1, 120);

            List<NotebookPhotoEntry> page = NotebookPhotoPage.Entries(save, Local, _def, 86400f);
            Assert.That(page.Select(e => e.Number), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(page[0].Head, Is.EqualTo("Picture 2"));
            Assert.That(page[0].Lines, Is.EqualTo(new[]
            {
                "3 grey seals, " + _def.PoseWords[1],
                "A fair picture",
                "Day 2, 14:30, St Peters",
            }));
        }

        [Test]
        public void TheAlbumTabAppearsOnlyOnceThereIsAPicture()
        {
            var flags = new InMemoryFlagStore();
            var none = NotebookContent.Build(new QuestDef[0], new KnowledgePageDef[0], new NotebookNote[0], flags, 31);
            Assert.That(none.Any(t => t.Key == NotebookContent.TabPhotos), Is.False);

            var photos = new[] { new NotebookPhotoEntry(1, "Picture 1", new[] { "Grey seal, resting" }) };
            var some = NotebookContent.Build(new QuestDef[0], new KnowledgePageDef[0], new NotebookNote[0], flags, 31,
                                             photos: photos);
            NotebookTab tab = some.Single(t => t.Key == NotebookContent.TabPhotos);
            Assert.That(tab.Kind, Is.EqualTo(NotebookPageKind.Photos));
            Assert.That(tab.Label.Length, Is.LessThanOrEqualTo(NotebookKit.ChipChars));
            Assert.That(tab.Blocks, Is.Not.Empty);
        }

        [Test]
        public void TheWhenLineCountsDaysFromOne()
        {
            Assert.That(NotebookPhotoPage.WhenLine(_def, 0d, Region, 86400f), Is.EqualTo("Day 1, 00:00, St Peters"));
            Assert.That(NotebookPhotoPage.WhenLine(_def, 86400d - 1d, "region.nowhere", 86400f),
                        Does.StartWith("Day 1, 23:59, "));
        }

        // ---- the shipped data -------------------------------------------------------------------

        static List<T> LoadAll<T>(string root) where T : Object
            => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                            .Where(a => a != null).ToList();

        [Test]
        public void TheShippedCameraIsWholeAndNamesOnlyRealAnimals()
        {
            PhotoCameraDef cam = PhotoCameraDef.Load();
            Assert.That(cam, Is.Not.Null, "Resources/" + PhotoCameraDef.ResourcesPath + " is missing");
            Assert.That(cam.ValidationError(), Is.Null);
            // The species defs arrive with the seal haul-outs, on their own branch. Read them by type name
            // and serialized id so the camera depends on the seals only through data ids.
            var species = new HashSet<string>(
                AssetDatabase.FindAssets("t:" + SpeciesDefTypeName, new[] { "Assets/_Project/Data" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null)
                    .Select(a => new SerializedObject(a).FindProperty("Id"))
                    .Where(p => p != null)
                    .Select(p => p.stringValue));
            if (species.Count == 0)
                Assert.Ignore("No " + SpeciesDefTypeName + " assets yet: they come with the seal haul-outs.");
            foreach (PhotoSubjectEntry s in cam.Subjects)
                Assert.That(species, Does.Contain(s.SubjectId), $"the camera names {s.SubjectId}, which no species def has");
            Assert.That(cam.Knows(Seal), Is.True);
        }

        [Test]
        public void EveryShippedPhotoRequestIsWholeAndPointsAtRealThings()
        {
            List<PhotoRequestDef> reqs = LoadAll<PhotoRequestDef>("Assets/_Project/Data");
            Assert.That(reqs.Select(r => r.Id), Does.Contain("photo_request.odile_grey_seals"));
            Assert.That(PhotoRequestTalk.Requests().Count, Is.EqualTo(reqs.Count),
                        "every request must be under Resources/" + PhotoRequestDef.ResourcesFolder);
            var npcs = new HashSet<string>(LoadAll<NpcDef>("Assets/_Project/Data").Select(n => n.Id));
            PhotoCameraDef cam = PhotoCameraDef.Load();
            foreach (PhotoRequestDef r in reqs)
            {
                Assert.That(r.ValidationError(), Is.Null, r.Id);
                Assert.That(npcs, Does.Contain(r.GiverNpcId), r.Id);
                Assert.That(cam.Knows(r.SubjectId), Is.True, $"{r.Id} asks for a subject the camera cannot name");
                Assert.That(r.MinCount, Is.LessThanOrEqualTo(_def.MaxSightingsRead));
            }
            Assert.That(reqs.Select(r => r.Id).Distinct().Count(), Is.EqualTo(reqs.Count), "duplicate request ids");
        }

        [Test]
        public void TheCameraIsAlwaysHisAndHerQuestOpensWhenSheAsks()
        {
            PhotoCameraDef cam = PhotoCameraDef.Load();
            PhotoRequestDef odile = LoadAll<PhotoRequestDef>("Assets/_Project/Data")
                .Single(r => r.Id == "photo_request.odile_grey_seals");
            Assert.That(cam.OwnedWhileFlag, Is.Empty, "owner 2026-11-13: the camera is always on the player, on P");
            QuestDef quest = NotebookContentSource.Quests().Single(q => q.Id == "quest.a_picture_of_the_seals");
            Assert.That(quest.OpensWithFlag, Is.EqualTo(odile.AskedFlag));
            Assert.That(quest.Steps.Select(s => s.Flag), Is.EqualTo(new[] { odile.PicturedFlag, odile.DoneFlag }));
        }

        [Test]
        public void HerWordsNeverNameAKey()
        {
            string[] keys = { "press", "key", " p ", "space", "button", "trigger", "click" };
            foreach (PhotoRequestDef r in LoadAll<PhotoRequestDef>("Assets/_Project/Data"))
                foreach (string line in r.AskLines.Concat(r.ReminderLines).Concat(r.ThanksLines).Concat(r.AfterLines))
                    foreach (string k in keys)
                        Assert.That((" " + line.ToLowerInvariant() + " ").Contains(k), Is.False,
                                    $"{r.Id}: '{line}' names a key ({k.Trim()}); she hints, never instructs");
        }
    }
}
