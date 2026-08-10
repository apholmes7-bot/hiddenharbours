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

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A BOAT LYING AT HER BERTH</b> — the three claims <see cref="MooredBoat"/> exists to make, none
    /// of which an EditMode test can reach, because all three are about what happens at RUNTIME.
    ///
    /// <para><b>Why these are PlayMode and had to be.</b> The region builder deliberately does not skin
    /// these hulls: <c>IsoFacetHullPresentationService</c> registers the mesh path at
    /// <c>RuntimeInitializeOnLoadMethod</c> and says in its own words that edit-time builders must not, so
    /// that a built scene never bakes a renderer whose setup is runtime-owned. The consequence is that
    /// <b>every interesting thing about a moored boat happens on wake</b> — and an EditMode test of the
    /// placement (which exists, in <c>NineMileCreekPhotographTests</c>) would pass in full while the
    /// fleet drew as flat sprites on a sea of its own.</para>
    ///
    /// <para>These load the REAL committed owner assets rather than a mirror, the
    /// <c>BoatLoadTimePresentationPlayTests</c> convention: a fixture that invents its own owner would
    /// stop testing the register the game actually ships.</para>
    /// </summary>
    public class MooredBoatPlayTests
    {
        const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";

        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();       // no environment → a dead-flat sea, which is what we want here
            MooringCleats.Clear();
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
                .Where(o => o != null)
                .OrderBy(o => o.Id, System.StringComparer.Ordinal)
                .ToList();
            Assert.IsNotEmpty(owners, $"no BoatOwnerDef assets under {OwnersFolder}");
            return owners;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed register, not a mirror.");
            return null;
#endif
        }

        MooredBoat Moor(BoatOwnerDef owner, Vector2 at, float heading = 90f)
        {
            // Built INACTIVE and configured before waking, exactly as the region builder does it — a
            // MooredBoat presents in OnEnable, so a boat woken before she is told whose she is would warn
            // and draw nothing, and the test would be measuring the wrong thing.
            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, heading);
            go.SetActive(true);
            return moored;
        }

        /// <summary>
        /// ⭐ Every boat on the register actually draws. This is the one that catches a re-bake, a renamed
        /// visual, or an owner pointed at a hull whose art never landed — each of which leaves a berth
        /// silently empty on a wharf whose whole point is that it looks worked.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryOwnerOnTheRegister_ActuallyPresentsAHull()
        {
            var owners = LoadOwners();
            var moored = owners.Select((o, i) => Moor(o, new Vector2(i * 20f, 0f))).ToList();
            yield return null;

            for (int i = 0; i < owners.Count; i++)
                Assert.IsTrue(moored[i].IsPresented,
                    $"'{owners[i].Id}' keeps '{owners[i].Boat.Id}' and nothing was drawn for her. Her " +
                    "berth is empty on a wharf that is supposed to look worked.");
        }

        /// <summary>
        /// ⭐⭐ <b>AND SHE IS MESH, not the sprite fallback.</b> ADR 0022: the whole fleet is mesh. The
        /// reason the region builder does not skin these hulls is precisely that an edit-time build would
        /// bake the SPRITE rig into the committed scene — and the failure is silent, because the sprite
        /// fallback draws a perfectly good boat in exactly the right place.
        ///
        /// <para>Discriminated on the component that only the SPRITE branch installs:
        /// <c>DirectionalBoatSprite</c> is the compass that counter-rotates a sprite child, and the mesh
        /// branch returns <c>Directional = null</c> and drives the hull through <c>MeshHullDriver</c>
        /// instead. So a hull carrying that component came down the fallback — which, for a visual whose
        /// own asset says <c>Variant = Mesh</c>, means the mesh path was refused at runtime (an unusable
        /// mesh def, or no presentation service) and nobody was told.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AMeshHulledOwner_IsDrawnAsMesh_NotAsTheSpriteFallback()
        {
            var owners = LoadOwners()
                .Where(o => o.Boat != null && o.Boat.Visual != null &&
                            o.Boat.Visual.Variant == BoatHullVariant.Mesh)
                .ToList();
            Assert.IsNotEmpty(owners,
                "no owner on the register keeps a mesh-variant hull — ADR 0022 says the whole fleet is mesh");

            var moored = owners.Select((o, i) => Moor(o, new Vector2(i * 20f, 0f))).ToList();
            yield return null;

            for (int i = 0; i < owners.Count; i++)
            {
                Assert.IsTrue(moored[i].IsPresented, $"'{owners[i].Id}' was not drawn at all");
                Assert.IsNull(moored[i].GetComponent<DirectionalBoatSprite>(),
                    $"'{owners[i].Id}' keeps '{owners[i].Boat.Visual.Id}', whose asset says Variant = Mesh, " +
                    "and she came down the SPRITE fallback instead. That is the exact failure the builder " +
                    "refuses to skin these hulls in order to avoid, and it is invisible on screen.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>THE WAVE TRAP.</b> A floating hull must ride the PUBLISHED wave field. A private smoother
        /// agrees only with itself — two boats on two smoothers sit on two different seas, and this repo
        /// has paid for that twice (#331's wavelength half, #460's phase half). <see cref="BoatWaveMotion"/>
        /// is the component that reads <c>SharedWaveField</c>, and the ONE line in
        /// <see cref="MooredBoat"/> that keeps it is <c>SkipWaveMotion</c> staying false.
        ///
        /// <para>So this asserts the rider is THERE. It is a wiring test and deliberately not a physics
        /// one: what the field does once she has it is <c>BoatWaveMotion</c>'s own business and is tested
        /// where that lives. What nothing else would notice is somebody adding
        /// <c>SkipWaveMotion = true</c> here to quiet a warning, after which the fleet sits dead still on
        /// a moving sea and no test fails.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryMooredHull_RidesTheSharedWaveField()
        {
            var owners = LoadOwners();
            var moored = owners.Select((o, i) => Moor(o, new Vector2(i * 20f, 0f))).ToList();
            yield return null;

            for (int i = 0; i < owners.Count; i++)
            {
                if (!moored[i].IsPresented) continue;   // her art is missing; the test above says so
                Assert.IsNotNull(moored[i].GetComponent<BoatWaveMotion>(),
                    $"'{owners[i].Id}' has no BoatWaveMotion, so she does not ride the published wave " +
                    "field. She will sit dead still on a moving sea — or worse, somebody will give her a " +
                    "rider of her own and she will heave on a sea nobody else is on.");
            }
        }

        /// <summary>
        /// ⭐ <b>A stranger's boat is not something you may tie to.</b> The skinner writes the hull's own
        /// <c>BoatCleats</c> from her deck sidecar, and those register into the GLOBAL
        /// <see cref="MooringCleats"/> registry that the player's mooring searches BY POSITION. Left alone,
        /// a basin of moored NPC hulls would put dozens of tie-off points around the wharf and a rope
        /// could be made fast to a boat nothing simulates.
        /// </summary>
        [UnityTest]
        public IEnumerator AMooredStrangersBoat_OffersNothingToTieTo()
        {
            var owners = LoadOwners();
            Assert.That(MooringCleats.Count, Is.Zero, "the fixture must start with an empty registry");

            foreach (var (owner, i) in owners.Select((o, i) => (o, i)))
                Moor(owner, new Vector2(i * 20f, 0f));
            yield return null;

            Assert.That(MooringCleats.Count, Is.Zero,
                $"{MooringCleats.Count} tie-off point(s) were registered by the moored fleet. The player " +
                "could make a line fast to somebody else's boat — a hull nothing simulates — on the one " +
                "wharf whose guarantee is that the thing you can see is the thing you can use.");
        }

        /// <summary>
        /// Her skipper stands on her, not beside her: the figure is parented into the hull's visual child,
        /// which is the transform the wave rider writes roll, pitch and heave onto. Parented anywhere else
        /// she would be a person standing on the water next to a boat that moves without her.
        /// </summary>
        [UnityTest]
        public IEnumerator ASkipperRidesTheHullSheIsStandingOn()
        {
            var owners = LoadOwners();
            var withCrew = owners.Where(o => o.Skipper != null).ToList();
            Assert.IsNotEmpty(withCrew, "the owner asked for captains aboard; nobody on the register has one");

            foreach (var (owner, i) in withCrew.Select((o, i) => (o, i)))
            {
                var moored = Moor(owner, new Vector2(i * 20f, 0f));
                yield return null;
                if (!moored.IsPresented) continue;

                var skipper = moored.GetComponentsInChildren<IsoCharacterSprite>(true).FirstOrDefault();
                Assert.IsNotNull(skipper, $"'{owner.Id}' has a skipper authored and none was drawn");

                Transform visual = moored.transform.Find(BoatHullSkinner.VisualChildName);
                Assert.IsNotNull(visual, $"'{owner.Id}': no visual child to stand on");
                Assert.AreEqual(visual, skipper.transform.parent,
                    $"'{owner.Id}': her skipper hangs off {skipper.transform.parent?.name} rather than " +
                    "the visual child the wave rider moves — she would stand still while the boat heaved");
            }
        }
    }
}
