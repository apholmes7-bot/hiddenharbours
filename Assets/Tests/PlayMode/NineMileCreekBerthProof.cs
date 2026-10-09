#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>Real NMC scene, including its serialized bed and hull placements. An optional external
    /// scene copy permits red controls without replacing any project asset.</summary>
    public abstract class NineMileCreekBerthProof
    {
        protected MooredBoat[] Wall;
        protected MooredBoat[] Fleet;
        protected FloatingPlatform Float;
        protected IGameClock Clock;
        protected Scene Loaded;
        protected static readonly double[] Tides = {15300, 6300, 49500, 24840};
        double oldSeconds;
        float oldClockScale, oldEngineScale;
        bool oldIgnore;
        BoatWaveMotion[] waves;
        float[] waveStrengths;

        [UnitySetUp]
        public IEnumerator LoadCreek()
        {
            oldIgnore = LogAssert.ignoreFailingMessages;
            oldEngineScale = Time.timeScale;
            Time.timeScale = 1;
            LogAssert.ignoreFailingMessages = true; // existing decor-import errors, as other NMC fixtures
#if UNITY_EDITOR
            string copy = System.Environment.GetEnvironmentVariable("HH_NMC_PROOF_SCENE");
            if (!string.IsNullOrEmpty(copy))
                Loaded = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(copy,
                    new LoadSceneParameters(LoadSceneMode.Single));
            else
#endif
            {
                yield return SceneManager.LoadSceneAsync("NineMileCreek", LoadSceneMode.Single);
                Loaded = SceneManager.GetSceneByName("NineMileCreek");
            }
            for (int i = 0; i < 8; i++) yield return null;
            var liveClock = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
            Assert.IsNotNull(liveClock);
            GameServices.Clock = Clock = liveClock;
            GameServices.Config = liveClock.Config;
            oldSeconds = Clock.TotalSeconds;
            oldClockScale = Clock.TimeScale;
            Clock.TimeScale = 0;
            Fleet = Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None).Where(b => b.gameObject.scene == Loaded && b.Owner != null).ToArray();
            Wall = Fleet.Where(b => !b.Owner.LiesAtTheFloat).OrderBy(b => b.Owner.BerthIndex).ToArray();
            Assert.AreEqual(5, Wall.Length, "The real five wall hulls must all be present.");
            Float = Object.FindObjectsByType<FloatingPlatform>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.scene == Loaded);
            Assert.IsNotNull(Float, "The real harbour float is the tide oracle.");
            waves = Wall.SelectMany(b => b.GetComponentsInChildren<BoatWaveMotion>()).ToArray();
            waveStrengths = waves.Select(w => w.MasterStrength).ToArray();
        }

        protected void TideOnly()
        {
            foreach (var wave in waves) wave.MasterStrength = 0;
        }
        protected IEnumerator Seek(double time, int frames = 8)
        {
            Clock.SeekTo(time);
            for (int i = 0; i < frames; i++) yield return null;
        }
        protected static Transform Visual(MooredBoat b) => b.transform.Find(BoatHullSkinner.VisualChildName);
        protected static Vector2 DrawnSpan(MooredBoat b)
        {
            var facet = b.GetComponentInChildren<IsoFacetHullRenderer>();
            Assert.IsNotNull(facet, b.Owner.Id + " did not draw a mesh hull");
            Transform posed = facet.PosedMesh;
            Assert.IsNotNull(posed);
            var mesh = posed.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            // Geometry actually sent to the renderer, after the live pose. The overlay quad includes
            // transparent padding, and a BoatHullDef length cannot prove where the picture ends.
            var x = mesh.vertices.Select(v => posed.TransformPoint(v).x).ToArray();
            return new Vector2(x.Min(), x.Max());
        }

        [UnityTearDown]
        public IEnumerator RestoreCreek()
        {
            if (waves != null)
                for (int i = 0; i < waves.Length; i++) if (waves[i] != null) waves[i].MasterStrength = waveStrengths[i];
            if (Clock is Object clockObject && clockObject != null) { Clock.SeekTo(oldSeconds); Clock.TimeScale = oldClockScale; }
            Time.timeScale = oldEngineScale;
            LogAssert.ignoreFailingMessages = oldIgnore;
            var empty = SceneManager.CreateScene("BerthProofCleanup");
            SceneManager.SetActiveScene(empty);
            if (Loaded.IsValid() && Loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(Loaded);
        }
    }
}

#endif
