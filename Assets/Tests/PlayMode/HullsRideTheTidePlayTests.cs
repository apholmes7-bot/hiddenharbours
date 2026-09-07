using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>HULLS RIDE THE TIDE</b> (owner playtest 2026-09-06, at the Nine Mile Creek north wall on a
    /// spring ebb — <i>"boats dont ride the tide"</i>). The float rode; the fleet tied to the wall beside
    /// her, the two small craft on her own fingers, and the player's cape islander alongside all sat at
    /// the same place on a 4.6 m face at every state of the tide.
    ///
    /// <para><b>Why this had to be PlayMode.</b> Everything in the chain is installed on WAKE and nowhere
    /// else: the region builder deliberately does not skin these hulls (the mesh path is chosen live, per
    /// run), <see cref="BoatHullSkinner"/> installs the rider beside the wave motion, and
    /// <see cref="BoatWaveMotion"/> composes it into the one transform write it owns. An EditMode test of
    /// the arithmetic — which exists, and is where the numbers are pinned — passes in full while nothing
    /// is wired to anything.</para>
    ///
    /// <para><b>The float is the ORACLE, and that is the point of the fixture.</b> Nothing here asserts a
    /// number: a real <see cref="FloatingPlatform"/> and her <see cref="FloatingPlatformVisual"/> stand in
    /// the same scene, on the same water, at the same three states of tide, and every hull is measured
    /// against how far SHE moved. A boat lying at a float has to go up and down with the planks she is
    /// tied to; two arithmetics for one rise is how they part company.</para>
    ///
    /// <para>Owners come from the REAL committed register (the <c>MooredBoatPlayTests</c> convention) —
    /// a fixture that invents its own would stop testing the fleet the game ships.</para>
    /// </summary>
    public class HullsRideTheTidePlayTests
    {
        const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";

        /// <summary>Spring low, mean and spring high on this coast — the three states, and the range the
        /// owner was watching the fleet fail to cross.</summary>
        static readonly float[] TideStates = { -2.2f, 0f, 2.2f };

        /// <summary>Far below anything that floats here, so every hull under test is unambiguously AFLOAT
        /// at all three states and the grounding arm (which has its own EditMode tests) cannot quietly
        /// become the thing being measured.</summary>
        const float DeepBedElevation = -50f;

        readonly List<Object> _spawned = new();
        TideEnv _sea;

        sealed class TideEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 20260906;
            public TideProfile ActiveTideProfile { get; set; }
            /// <summary>Glass calm on purpose: at sea state 0 the wave field's amplitudes are exactly 0,
            /// so the ONLY thing left that can move a hull's picture up the screen is the tide.</summary>
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>A clock that does not advance. The sea has to be still for two frames to be
        /// comparable at all (the wave animator integrates on the clock's own delta), and a frozen clock
        /// is how this fixture measures the TIDE rather than the swell.</summary>
        sealed class StillClock : IGameClock
        {
            public double TotalSeconds => 0.0;
            public GameTime Now => new GameTime(0.0);
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; }
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0.5f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) { }
        }

        sealed class DeepBed : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => DeepBedElevation;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            MooringCleats.Clear();
            _sea = new TideEnv { Level = 0f };
            GameServices.Environment = _sea;
            GameServices.Clock = new StillClock();
            GameServices.TidalTerrain = new DeepBed();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            MooringCleats.Clear();
            GameServices.Reset();
        }

        static List<BoatOwnerDef> LoadOwners()
        {
#if UNITY_EDITOR
            var owners = AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { OwnersFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(o => o != null && o.IsPresentable())
                .OrderBy(o => o.Id, System.StringComparer.Ordinal)
                .ToList();
            Assert.IsNotEmpty(owners, $"no presentable BoatOwnerDef assets under {OwnersFolder}");
            return owners;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed register, not a mirror.");
            return null;
#endif
        }

        MooredBoat Moor(BoatOwnerDef owner, Vector2 at)
        {
            // Built INACTIVE and configured before waking, exactly as the region builder does it.
            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, 90f);
            go.SetActive(true);
            return moored;
        }

        /// <summary>The player's own hull: a piloted boat, skinned by the very call
        /// <c>PersistentCoreBuilder</c> makes for the dory, so she comes down the same path the fleet
        /// does and gets whatever the fleet gets.</summary>
        Transform PilotedHull(BoatHullDef hull, Vector2 at)
        {
            var go = new GameObject($"Player_{hull.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var controller = go.AddComponent<BoatController>();
            controller.SetHull(hull);
            go.SetActive(true);
            BoatHullSkinner.Apply(go, hull.Visual, controller);
            return go.transform.Find(BoatHullSkinner.VisualChildName);
        }

        /// <summary>
        /// A real float, drawn, on the same water — the oracle. Her draught and freeboard are the
        /// harbour's own; the baked deck she is measured from is arbitrary (it cancels in a difference)
        /// but is written as the real one so the fixture reads like the wharf it stands for.
        /// </summary>
        FloatingPlatformVisual Float(Vector2 at)
        {
            var raft = new GameObject("Float");
            _spawned.Add(raft);
            var platform = raft.AddComponent<FloatingPlatform>();
            platform.Configure("float.oracle", new Rect(at.x - 3f, at.y - 1.2f, 6f, 2.4f),
                               DeepBedElevation, draughtMetres: 0.31f, freeboardMetres: 0.40f);

            var picture = new GameObject("FloatDeck");
            _spawned.Add(picture);
            picture.transform.position = new Vector3(at.x, at.y, 0f);
            var visual = picture.AddComponent<FloatingPlatformVisual>();
            visual.Configure(platform, bakedDeckElevation: 2.82f, planY: at.y);
            return visual;
        }

        /// <summary>Set the tide and let the frame wear it. Two frames: one for every LateUpdate in the
        /// chain to run against the new level, one to be sure nothing is a frame behind.</summary>
        IEnumerator SetTide(float metres)
        {
            _sea.Level = metres;
            yield return null;
            yield return null;
        }

        static Transform VisualOf(MooredBoat moored) =>
            moored.transform.Find(BoatHullSkinner.VisualChildName);

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>THE ACCEPTANCE.</b> Every hull on the register, moored, across three states of tide:
        /// her picture moves by exactly what the float's picture moves by, both ways.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryMooredHullRidesTheTide_ByExactlyWhatTheFloatRides()
        {
            var owners = LoadOwners();
            var fleet = owners.Select((o, i) => Moor(o, new Vector2(i * 30f, 0f))).ToList();
            FloatingPlatformVisual oracle = Float(new Vector2(-40f, 0f));

            yield return SetTide(TideStates[0]);

            for (int i = 0; i < fleet.Count; i++)
                Assert.IsTrue(fleet[i].IsPresented,
                    $"'{owners[i].Id}' drew nothing, so there is no picture to ride anything");

            var visuals = fleet.Select(VisualOf).ToList();
            for (int i = 0; i < visuals.Count; i++)
                Assert.IsNotNull(visuals[i],
                    $"'{owners[i].Id}' has no '{BoatHullSkinner.VisualChildName}' child — the skinner's " +
                    "own visual, which is the transform the sea moves");

            float[] wasHull = visuals.Select(v => v.position.y).ToArray();
            float wasFloat = oracle.transform.position.y;

            for (int s = 1; s < TideStates.Length; s++)
            {
                yield return SetTide(TideStates[s]);

                float floatRose = oracle.transform.position.y - wasFloat;
                Assert.That(floatRose, Is.GreaterThan(0.1f),
                    "the ORACLE did not move, so this fixture proved nothing about anything — the float " +
                    "is the thing every hull below is measured against");

                for (int i = 0; i < visuals.Count; i++)
                {
                    float rose = visuals[i].position.y - wasHull[i];
                    Assert.That(rose, Is.EqualTo(floatRose).Within(1e-3f),
                        $"between {TideStates[s - 1]:0.0} m and {TideStates[s]:0.0} m of tide the float " +
                        $"rose {floatRose:0.000} units and '{owners[i].Id}' rose {rose:0.000}. A boat " +
                        "tied to a dock that leaves without her is the defect this whole change is.");
                    wasHull[i] = visuals[i].position.y;
                }
                wasFloat = oracle.transform.position.y;
            }
        }

        /// <summary>
        /// ⭐ <b>THE PLAYER'S HULL ALONGSIDE RISES WITH THE FLEET.</b> She is skinned by the same call and
        /// therefore gets the same rider — but she is the one hull in the game with a physics body under
        /// her, and "no region special-cases" is only true if somebody checks.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlayersOwnHullRidesTheTideToo()
        {
            var owners = LoadOwners();
            BoatOwnerDef owner = owners.First(o => o.Boat != null && o.Boat.Visual != null);

            MooredBoat neighbour = Moor(owner, new Vector2(0f, 0f));
            Transform player = PilotedHull(owner.Boat, new Vector2(30f, 0f));
            Assert.IsNotNull(player, "the player's hull was not skinned at all");

            yield return SetTide(TideStates[0]);
            float wasPlayer = player.position.y;
            float wasNeighbour = VisualOf(neighbour).position.y;

            yield return SetTide(TideStates[2]);

            float neighbourRose = VisualOf(neighbour).position.y - wasNeighbour;
            Assert.That(neighbourRose, Is.GreaterThan(0.1f), "the moored fleet did not ride");
            Assert.That(player.position.y - wasPlayer, Is.EqualTo(neighbourRose).Within(1e-3f),
                "the player's boat lay still against a wall the fleet beside her was climbing");
        }

        /// <summary>
        /// ⚠️⚠️ <b>AND HER PLAN DOES NOT MOVE.</b> The root transform is her position on the GROUND
        /// plane — her body, her berth, the outline a swimmer reaches for — and a metre of tide is not a
        /// metre of northing. Riding the root would walk a moored boat about five metres north over a
        /// spring tide, into the wall she is tied to. It is the picture that rides, which is exactly the
        /// distinction the float already draws between her walkable surface and her sprite.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTideMovesHerPictureAndNeverHerPlan()
        {
            var owners = LoadOwners();
            MooredBoat moored = Moor(owners.First(), new Vector2(12f, 34f));

            yield return SetTide(TideStates[0]);
            Vector3 plan = moored.transform.position;
            float picture = VisualOf(moored).position.y;

            yield return SetTide(TideStates[2]);

            Assert.That(moored.transform.position, Is.EqualTo(plan).Using(new Vector3Comparer(1e-4f)),
                "the tide moved the boat across the ground — over a 4.4 m range that is about five " +
                "metres of northing, and she is tied to a wall two metres away");
            Assert.That(VisualOf(moored).position.y - picture, Is.GreaterThan(0.1f),
                "…and her picture did not move at all, which is the original defect");
        }

        sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            readonly float _tolerance;
            public Vector3Comparer(float tolerance) => _tolerance = tolerance;
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= _tolerance * _tolerance;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }
    }
}
