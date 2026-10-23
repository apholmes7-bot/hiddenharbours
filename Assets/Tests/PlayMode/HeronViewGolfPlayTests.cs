#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class HeronViewGolfPlayTests : CumberlandSceneFixture
    {
        const int Width = 1920, Height = 1080;
        const int CourseDay = 6;
        const float TeeHour = 7f + 20f / 60f;
        const float BackHour = 14f + 56f / 60f;
        const float FrameHeight = 14f;
        Camera _view;
        RenderTexture _target;
        bool _shadersSaved, _asyncShaders;

        VillagerRoutine[] Golfers() => RegionScene.GetRootGameObjects()
            .Single(g => g.name == CumberlandRoutines.RootName).GetComponentsInChildren<VillagerRoutine>(true);

        static bool Draws(VillagerRoutine golfer)
        {
            var figure = golfer.GetComponent<CharacterFigurePresenter>();
            return golfer.GetComponent<SpriteRenderer>().enabled || (figure != null && figure.DrawsInsteadOfSprite);
        }

        static void CheckColours(VillagerRoutine golfer)
        {
            var presenter = golfer.GetComponent<CharacterFigurePresenter>();
            Assert.That(presenter, Is.Not.Null, golfer.name);
            Assert.That(presenter.AshoreFigure, Is.Not.Null, golfer.name);
            var figure = presenter.AshoreFigure;
            Assert.That(figure.Recoloured, Is.True, golfer.name);
            Assert.That(HeronViewGolferColours.TryLook(golfer.Routine.Npc.Build, out var look, out string problem), Is.True, problem);
            Assert.That(figure.ShownRamps.Length, Is.EqualTo(look.Colours.Length), golfer.name);
            for (int m = 0; m < look.Colours.Length; m++)
                if (look.Colours[m].Length > 0)
                    Assert.That(figure.ShownRamps[m], Is.EqualTo(look.Colours[m]), golfer.name + " material " + m);
        }

        [UnityTest]
        public IEnumerator HeronViewGolfer_StandsOnTheFirstTee_AtItsHour()
        {
            yield return LoadDirect();
            SetHour(CourseDay * 24d + TeeHour);
            yield return new WaitForSecondsRealtime(1);
            var golfers = Golfers();
            Assert.That(golfers.Length, Is.EqualTo(8));
            var course = CumberlandCourse.Read();
            foreach (var person in HeronViewRoster.Visitors.Where(p => p.Party == 0))
            {
                var golfer = golfers.Single(g => g.Routine.Id == person.RoutineId);
                Assert.That(Vector2.Distance(golfer.transform.position,
                    course.Station(CumberlandCourse.SlotId(true, 1, person.Slot)).Position), Is.LessThan(.02f), person.Name);
                Assert.That(golfer.Pose.Walking, Is.False, person.Name);
                Assert.That(golfer.GetComponent<Interactable>().enabled, Is.True, person.Name);
                Assert.That(Draws(golfer), Is.True, person.Name);
                CheckColours(golfer);
            }
            SetHour(CourseDay * 24d + 19.1);
            yield return new WaitForSecondsRealtime(1);
            foreach (var golfer in golfers)
            {
                Assert.That(golfer.Pose.Shelter, Is.EqualTo(RoutineShelter.OutOfSight), golfer.name);
                Assert.That(Draws(golfer), Is.False, golfer.name);
                Assert.That(golfer.GetComponent<Interactable>().enabled, Is.False, golfer.name);
            }
        }

        [UnityTest]
        public IEnumerator HeronViewGolf_FiveCoursePlates()
        {
            _asyncShaders = ShaderUtil.allowAsyncCompilation; _shadersSaved = true;
            ShaderUtil.allowAsyncCompilation = false;
            yield return LoadDirect();
            string folder = "artifacts/golf/b/plates";
            Directory.CreateDirectory(folder);
            DateTime floor = DateTime.Parse(File.ReadAllText("artifacts/golf/b/plate-floor.txt"),
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
            _view = Camera.main;
            Assert.That(_view, Is.Not.Null);
            foreach (var component in _view.GetComponents<Behaviour>())
                if (component is HiddenHarbours.App.CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create(); _view.targetTexture = _target; _view.aspect = (float)Width / Height;
            var rows = new StringBuilder("file,day_index,hour,centre_x,centre_y,world_height,utc\n");
            var frames = new[] {
                ("first-tee-0930", new Vector2(214, 94), 9.5f),
                ("green-18-0930", new Vector2(289, 96), 9.5f),
                ("clubhouse-0930", new Vector2(238, 60), 9.5f),
                ("first-tee-party", new Vector2(214, 94), TeeHour),
                ("green-18-back-pair", new Vector2(289, 96), BackHour)
            };
            foreach (var frame in frames)
            {
                SetHour(CourseDay * 24d + frame.Item3);
                Assert.That(GameServices.Clock.DayIndex, Is.EqualTo(CourseDay));
                _view.transform.position = new Vector3(frame.Item2.x, frame.Item2.y, -10);
                _view.orthographicSize = FrameHeight / 2f;
                yield return new WaitForSecondsRealtime(2);
                for (int i = 0; i < 30; i++) yield return null;
                if (frame.Item1 == "green-18-back-pair")
                {
                    var pair = Golfers().Where(g => HeronViewRoster.Visitors.Any(p => p.Party == 1 && p.RoutineId == g.Routine.Id)).ToArray();
                    Assert.That(pair.Length, Is.EqualTo(2));
                    Assert.That(pair.All(g => !g.Pose.Walking && Draws(g)), Is.True);
                    foreach (var golfer in pair) CheckColours(golfer);
                }
                if (frame.Item1 == "first-tee-party")
                    foreach (var golfer in Golfers().Where(g => HeronViewRoster.Visitors.Any(p => p.Party == 0 && p.RoutineId == g.Routine.Id)))
                        CheckColours(golfer);
                string path = Path.Combine(folder, frame.Item1 + ".png");
                Capture(path);
                Assert.That(File.GetLastWriteTimeUtc(path), Is.GreaterThan(floor));
                rows.AppendLine(FormattableString.Invariant(
                    $"{frame.Item1}.png,{CourseDay},{frame.Item3:F6},{frame.Item2.x},{frame.Item2.y},{FrameHeight},{DateTime.UtcNow:O}"));
            }
            File.WriteAllText(Path.Combine(folder, "plates.csv"), rows.ToString());
            SetHour(CourseDay * 24d + 17d + 26d / 60d);
            yield return new WaitForSecondsRealtime(2);
            var latePair = Golfers().Where(g => HeronViewRoster.Visitors.Any(p => p.Party == 2 && p.RoutineId == g.Routine.Id)).ToArray();
            Assert.That(latePair.Length, Is.EqualTo(2));
            Assert.That(latePair.All(g => !g.Pose.Walking && Draws(g)), Is.True);
            foreach (var golfer in latePair) CheckColours(golfer);
            File.WriteAllText(Path.Combine(folder, "colours.txt"), "All eight drawn palettes equal their resolved build colours.\n");
            Debug.Log("[HeronViewGolf] FIVE PLATES OK: " + Path.GetFullPath(folder));
        }

        void Capture(string path)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                _view.Render(); RenderTexture.active = _target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        [TearDown]
        public void ReleaseGolfPlates()
        {
            if (_view != null) _view.targetTexture = null;
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            if (_shadersSaved) ShaderUtil.allowAsyncCompilation = _asyncShaders;
        }
    }
}
#endif
