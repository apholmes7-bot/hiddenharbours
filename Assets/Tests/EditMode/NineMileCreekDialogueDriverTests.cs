using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CREEK'S DIALOGUE DRIVER IS NOT BUILT INSIDE THE GRAVE.</b> Nine Mile Creek's
    /// <see cref="WorldInteractor"/> and <see cref="DialoguePresenter"/> used to be parented into the
    /// scene's DEV CORE, because an interactor needs a player to measure from and the dev stand-in is
    /// the only player a region scene can name at build time. <c>DevRegionBootstrap</c> destroys that
    /// core the moment a real core travels in, and it took the driver and the panel with it: Wendell and
    /// Hector were mute for every player who arrived by sea, and perfect for the owner pressing Play.
    ///
    /// <para><b>Why this test exists next to the PlayMode one.</b> <c>RegionDialogueTravelPlayTests</c>
    /// proves that a driver standing on a region root survives a hop and finds the player who arrived —
    /// but it BUILDS that shape itself, so it would keep passing while the builder quietly went back to
    /// parenting the driver into the dev core. The runtime claim needs a static claim about the scene's
    /// actual construction underneath it, and this is it: the parenting is the fix, so the parenting is
    /// what is pinned. The committed <c>.unity</c> is builder OUTPUT; the builder is the rule, and the
    /// rule is what a test may assert against.</para>
    ///
    /// <para>Reads the wiring back through <see cref="SerializedObject"/>, the same channel the builder
    /// writes it on, because these are private serialized fields — the region-builder-test convention.</para>
    /// </summary>
    public class NineMileCreekDialogueDriverTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>The dev core with its stand-in inside it — the thing the travel path destroys.</summary>
        private Transform MakeDevCoreStandIn(out GameObject devCore)
        {
            devCore = Spawn("DevCore");
            var player = new GameObject("Player");
            player.transform.SetParent(devCore.transform, worldPositionStays: false);
            return player.transform;
        }

        /// <summary>A person to talk to, as <c>NineMileCreekPeople.Place</c> hands them over.</summary>
        private Interactable MakePerson(string name)
        {
            var go = Spawn(name);
            return go.AddComponent<Interactable>();
        }

        private WorldInteractor Place(IReadOnlyList<Interactable> people, Transform reviewPlayer)
        {
            var interactor = NineMileCreekBuilder.PlaceDialogueDriver(people, reviewPlayer);
            if (interactor != null)
            {
                _spawned.Add(interactor.gameObject);
                var presenter = Presenter(interactor);
                if (presenter != null) _spawned.Add(presenter.gameObject);
            }
            return interactor;
        }

        private static DialoguePresenter Presenter(WorldInteractor interactor)
            => Ref(interactor, "_presenter") as DialoguePresenter;

        private static Object Ref(Component c, string field)
        {
            var prop = new SerializedObject(c).FindProperty(field);
            return prop != null ? prop.objectReferenceValue : null;
        }

        private static Object[] RefArray(Component c, string field)
        {
            var prop = new SerializedObject(c).FindProperty(field);
            if (prop == null || !prop.isArray) return new Object[0];
            var values = new Object[prop.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue;
            return values;
        }

        // =============================================================================================

        [Test]
        public void TheDriverAndItsPanel_AreSceneRoots_NotDevCoreChildren()
        {
            // ⭐ THE PIN. Not "somewhere outside the dev core" — a ROOT, which is what the travel
            // coordinator toggles per region and what therefore makes this live exactly while you are
            // standing in the creek.
            Transform standIn = MakeDevCoreStandIn(out _);
            var interactor = Place(new[] { MakePerson("Wendell") }, standIn);

            Assert.IsNotNull(interactor, "a creek with people in it gets a driver");
            Assert.IsNull(interactor.transform.parent,
                "the interact driver must be a scene ROOT — parented into the dev core it is destroyed " +
                "on arrival, which is exactly how the creek's two people ended up mute for anyone who " +
                "sailed in");

            var presenter = Presenter(interactor);
            Assert.IsNotNull(presenter, "the driver has a panel to speak through");
            Assert.IsNull(presenter.transform.parent, "…and the panel is a root for the same reason");
        }

        [Test]
        public void TheDriverAndItsPanel_SurviveTheDevCoresDestruction()
        {
            // The same claim stated as the EVENT that used to break it: DevRegionBootstrap.Start does
            // exactly this when a persistent core travels in.
            Transform standIn = MakeDevCoreStandIn(out GameObject devCore);
            var interactor = Place(new[] { MakePerson("Wendell") }, standIn);
            var presenter = Presenter(interactor);

            Object.DestroyImmediate(devCore);            // DevRegionBootstrap.cs: Destroy(_devCoreRoot)

            Assert.IsTrue(interactor != null,            // Unity's overload: a destroyed object is fake-null
                "the driver outlives the dev core — it is region content, not review kit");
            Assert.IsTrue(presenter != null, "and so does the panel");
            Assert.IsTrue(Ref(interactor, "_player") == null,
                "…while the stand-in it was pointed at is gone, which is precisely why WorldInteractor " +
                "falls back to GameServices.PlayerTransform instead of measuring from a corpse");
        }

        [Test]
        public void TheDriver_IsWiredToTheWholeCast_AndToTheReviewStandIn()
        {
            // The review affordance is the reason the stand-in is handed over at all: press Play in this
            // scene with no persistent core and NOTHING publishes the Core relay, so a driver wired to
            // nobody would be mute for the owner even though it is now correct for the player.
            Transform standIn = MakeDevCoreStandIn(out _);
            var wendell = MakePerson("Wendell");
            var hector = MakePerson("Hector");
            var interactor = Place(new[] { wendell, hector }, standIn);

            Assert.AreSame(standIn, Ref(interactor, "_player"),
                "the dev stand-in is the review player, and WorldInteractor prefers it while it lives");

            var wired = RefArray(interactor, "_interactables");
            CollectionAssert.AreEquivalent(new Object[] { wendell, hector }, wired,
                "everybody NineMileCreekPeople placed is somebody you can walk up to — a cast member " +
                "missing from this list is silently unspeakable-to, with no error anywhere");
        }

        [Test]
        public void WithNobodyToTalkTo_NoDriverIsBuiltAtAll()
        {
            // NineMileCreekPeople skips a person whose NpcDef has not imported, so an empty cast is a
            // real state. A driver for it would be a prompt-less canvas built on every scene load.
            Transform standIn = MakeDevCoreStandIn(out _);

            Assert.IsNull(Place(new Interactable[0], standIn), "no cast, no driver");
            Assert.IsNull(Place(null, standIn), "…and null is the same answer, not an exception");
        }
    }
}
