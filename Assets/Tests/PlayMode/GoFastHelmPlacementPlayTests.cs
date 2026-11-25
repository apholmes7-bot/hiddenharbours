using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class GoFastHelmPlacementPlayTests
    {
        static readonly Vector3 StoredWalkStand = new Vector3(0.516405f, -1.035f, 1.556f);
        static readonly Vector3 PublishedHelmAnchor = new Vector3(0.42f, -1.86f, 0.62f);
        readonly List<Object> _spawned = new List<Object>();
        BoatController _boat;
        DeckWalkController _walk;
        DeckRiderVisual _rider;
        BoatHullDef _source;

        static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        Vector3 Stand() => (Vector3)typeof(DeckRiderVisual)
            .GetMethod("StandPointRigLocal", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_rider, null);

        T Keep<T>(T value) where T : Object { _spawned.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _source = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/GoFast.asset");
            Assert.IsNotNull(_source);
            var boatRoot = Keep(new GameObject("HelmBoat"));
            _boat = boatRoot.AddComponent<BoatController>();
            _boat.SetHull(_source);
            _boat.enabled = false;
            var player = Keep(new GameObject("HelmPlayer"));
            player.AddComponent<SpriteRenderer>();
            _walk = player.AddComponent<DeckWalkController>();
            _walk.enabled = false;
            Set(_walk, "_deckLocal", new Vector2(StoredWalkStand.x, StoredWalkStand.y));
            Set(_walk, "_deckHeight", StoredWalkStand.z);
            _rider = player.AddComponent<DeckRiderVisual>();
            _rider.enabled = false;
            Set(_rider, "_boat", _boat);
            Set(_rider, "_mode", ControlMode.Aboard);
#else
            Assert.Ignore("Requires the committed asset database.");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        BoatHullDef WithVisualCopy()
        {
            var hull = Keep(Object.Instantiate(_source));
            hull.Visual = Keep(Object.Instantiate(_source.Visual));
            hull.Visual.Deck = Keep(Object.Instantiate(_source.Visual.Deck));
            _boat.SetHull(hull);
            return hull;
        }

        [Test]
        public void GoFastAtTheHelm_ReadsThePublishedInteriorAnchor()
        {
            Assert.AreEqual(PublishedHelmAnchor, Stand());
            Assert.AreEqual(new Vector2(StoredWalkStand.x, StoredWalkStand.y), _walk.DeckLocalPosition,
                "the walking stand must not be moved");
            Assert.AreEqual(StoredWalkStand.z, _walk.DeckHeightMeters);
        }

        [Test]
        public void OnDeck_KeepsTheStoredWalkingStand()
        {
            Set(_rider, "_mode", ControlMode.OnDeck);
            Assert.AreEqual(StoredWalkStand, Stand());
        }

        [Test]
        public void AStationOnReachableDeck_KeepsTheExistingStandRead()
        {
            var hull = WithVisualCopy();
            hull.Visual.Deck.HelmStationLocalMeters = Vector3.zero;
            hull.Visual.Deck.Areas = new[] { DeckArea.From("sole", DeckAreaKind.Deck, new[] {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) }) };
            Assert.AreEqual(StoredWalkStand, Stand());
        }

        [Test]
        public void WithNoPublishedStation_KeepsTheExistingStandRead()
        {
            var hull = WithVisualCopy();
            hull.Visual.Deck.HasHelmStation = false;
            Assert.AreEqual(StoredWalkStand, Stand());
        }

        [Test]
        public void WithNoHelmAnchor_KeepsTheExistingStandRead()
        {
            var hull = WithVisualCopy();
            hull.Visual.Interior = null;
            Assert.AreEqual(StoredWalkStand, Stand());
        }

        [Test]
        public void AHullSwap_ReadsTheNewHullsInteriorAnchor()
        {
            var hull = WithVisualCopy();
            hull.Visual.Interior = Keep(ScriptableObject.CreateInstance<BoatInteriorDef>());
            hull.Visual.Interior.Anchors = new[] { new BoatInteriorAnchor {
                Id = "helm", ReachPoint = new Vector3(0.7f, -2.1f, 0.6f) } };
            Assert.AreEqual(new Vector3(0.7f, -2.1f, 0.6f), Stand());
        }

        [Test]
        public void ACarriedStand_RemainsTheCarriersPoint()
        {
            Set(_rider, "_carried", true);
            Set(_rider, "_carriedStand", new Vector3(2f, 3f, 4f));
            Assert.AreEqual(new Vector3(2f, 3f, 4f), Stand());
        }
    }
}
