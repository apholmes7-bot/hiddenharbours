using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A boat presents as her DATA says the moment she loads — no key press (ADR 0022).</b>
    ///
    /// <para><b>The defect these pin.</b> A built scene carries a SERIALISED rig, banked by an edit-time
    /// builder that deliberately registers no mesh presentation service, so the builder always writes the
    /// SPRITE compass and the mesh path was meant to be chosen live, per run, by the skinner. Nothing was
    /// making that per-run choice for the player's boat — <c>OwnedFleet</c> only re-skinned on a purchase or
    /// a save-restore — so every session opened on the banked sprite rig and stayed there until the owner
    /// pressed V (the dev A/B toggle) or F. <c>OwnedFleet.PresentWornHull</c> is the load-time pass that
    /// closes it.</para>
    ///
    /// <para><b>Why these cannot be EditMode tests.</b> The claim is about what happens WITHOUT anyone
    /// calling anything — the <c>Start</c> edge — and the fix hangs off that lifecycle, which never fires
    /// outside play mode. An EditMode test that called <see cref="OwnedFleet.PresentWornHull"/> by hand
    /// would pass against a build with the fix deleted. So the fixture below adds the component and then
    /// gets out of the way, and the ONE frame it waits is a lifecycle edge, not a duration.</para>
    ///
    /// <para><b>And why they assert against the ASSET, not against "Mesh".</b> Content is data (rule 2):
    /// the guard is <c>presented variant == visual.Variant</c>, so it holds if the owner re-authors a hull
    /// either way. <c>SpriteAuthoredHull_LoadsAsSprite</c> is the other half of that — a real committed
    /// hull whose visual carries no <c>Variant</c> key at all must still load sprite, which a fix that
    /// simply forced Mesh would break.</para>
    ///
    /// <para>Headless-safe by construction, exactly as <c>MeshHullPlayTests</c> is: no camera is created,
    /// so nothing renders (CI has no graphics device and a render there CRASHES the editor). These prove
    /// the components pick the right path over real frames; the pixels are the EditMode GPU fixture's
    /// business.</para>
    /// </summary>
    public class BoatLoadTimePresentationPlayTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();          // no environment → zero wind / current / tide
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        static BoatHullDef LoadHull(string file)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
            Assert.IsNotNull(asset, $"{DataBoats}/{file}.asset is missing.");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed hulls, not a mirror.");
            return null;
#endif
        }

        /// <summary>
        /// A boat rigged exactly as a BUILT SCENE hands her to the running game: the plain hull renderer,
        /// the controller wearing her hull — and a skin banked under the BUILDER'S conditions, i.e. with no
        /// mesh presentation service registered, which is what makes the skinner write the sprite compass.
        /// Clearing the live service for that one call is the honest way to reproduce the serialised state
        /// in-process; it is restored immediately, so the load-time pass runs against the real service the
        /// shipped game has.
        ///
        /// <para><see cref="OwnedFleet"/> goes on LAST and is then left alone — its <c>Start</c> is the
        /// thing under test.</para>
        /// </summary>
        (GameObject go, SpriteRenderer plain) NewBoatAsASceneSerialisesHer(BoatHullDef hull)
        {
            var go = new GameObject(hull.Id);
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + CapsuleCollider2D
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, Mathf.Max(1f, hull.LengthMeters));
            col.offset = Vector2.zero;
            _spawned.Add(go);
            boat.SetHull(hull);

            var plain = go.AddComponent<SpriteRenderer>();  // the plain rotating hull picture the skin hides

            var live = HullMeshPresentation.Service;
            HullMeshPresentation.Service = null;
            try { BoatHullSkinner.ApplyHull(go, plain, hull, boat); }
            finally { HullMeshPresentation.Service = live; }

            var fleet = go.AddComponent<OwnedFleet>();
            fleet.Configure(new[] { hull }, boat, hold: null, spriteRenderer: plain);
            return (go, plain);
        }

        static BoatHullVariant PresentedVariant(GameObject root)
        {
            var presenter = BoatHullPresenterHost.Resolve(root);
            Assert.IsNotNull(presenter, "the boat must be presenting SOMETHING — an unskinned boat is invisible");
            return presenter.Variant;
        }

        /// <summary>
        /// THE DEFECT, and its fix. The dory the owner starts in wears <c>visual.dory_iso</c>, which is the
        /// MESH variant with a baked hull mesh — but the scene banks her as a sprite. One frame of ordinary
        /// lifecycle, no input, and she must be presenting as her asset says.
        /// </summary>
        [UnityTest]
        public IEnumerator MeshAuthoredHull_LoadsAsMesh_WithNoKeyPress()
        {
            var hull = LoadHull("Dory");
            var visual = hull.Visual;
            Assert.IsNotNull(visual, "boat.dory must bind a visual");
            Assert.AreEqual(BoatHullVariant.Mesh, visual.Variant,
                "fixture assumption: the committed dory visual is authored MESH (ADR 0022 — the whole " +
                "fleet is mesh). If the owner re-authors her, retarget this test, do not weaken it.");
            Assert.IsNotNull(HullMeshPresentation.Service,
                "the Art presentation service must self-register at runtime load — without it every mesh " +
                "hull silently falls back to sprite in the shipped game");

            var (go, _) = NewBoatAsASceneSerialisesHer(hull);

            // The precondition, asserted rather than assumed: the fixture really does reproduce the state a
            // built scene hands us. Without this the test could pass on a boat that was never sprite.
            Assert.AreEqual(BoatHullVariant.Sprite, PresentedVariant(go),
                "fixture must start from the SERIALISED sprite rig — that is the defect's precondition");

            yield return null;      // the Start edge — the only thing that happens between the asserts

            Assert.AreEqual(visual.Variant, PresentedVariant(go),
                "on load, a hull must present as her DATA says with no key press — this going red means a " +
                "session opens on the sprite hull again and only V or F fixes it");

            // The concrete evidence behind the seam, so a presenter that lied would not slip through.
            Assert.IsNotNull(go.GetComponent<MeshHullDriver>(),
                "the mesh path drives from MeshHullDriver on the physics root");
            var child = go.transform.Find(BoatHullSkinner.VisualChildName);
            Assert.IsNotNull(child, "the visual child survives the swap (BoatSpotlight finds it BY NAME)");
            Assert.IsNull(child.GetComponent<SpriteRenderer>(),
                "the sprite compass must be torn off, or two hulls draw at once");
        }

        /// <summary>
        /// The other half of "the data wins": <c>boat.fishing_skiff</c> wears <c>visual.fishing_boat</c>, the
        /// legacy hand-drawn compass, whose asset carries NO <c>Variant</c> key and no hull mesh. She must
        /// still load as a sprite. A fix that just forced Mesh at load would turn her invisible.
        /// </summary>
        [UnityTest]
        public IEnumerator SpriteAuthoredHull_LoadsAsSprite()
        {
            var hull = LoadHull("FishingSkiff");
            var visual = hull.Visual;
            Assert.IsNotNull(visual, "boat.fishing_skiff must bind a visual");
            Assert.AreEqual(BoatHullVariant.Sprite, visual.Variant,
                "fixture assumption: visual.fishing_boat is authored SPRITE (no Variant key at all)");

            var (go, _) = NewBoatAsASceneSerialisesHer(hull);

            yield return null;      // the same Start edge

            Assert.AreEqual(visual.Variant, PresentedVariant(go),
                "a sprite-authored hull must stay a sprite — the load-time pass reads the asset, it does " +
                "not impose a variant");
            Assert.IsNull(go.GetComponent<MeshHullDriver>(),
                "no mesh driver belongs on a hull whose data never asked for one");
        }

        /// <summary>
        /// ORDER-INDEPENDENCE against the save-restore. <see cref="OwnedFleet.OnGameLoaded"/> can land before
        /// the load-time pass, and when it does it has already re-pointed the controller — so the pass must
        /// present the hull actually WORN, not the one the scene happened to serialise. Getting this wrong
        /// would silently undo a restore's picture while leaving its feel and hold on the restored boat,
        /// which is the #208 divergence in a new coat.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadTimePass_PresentsTheWornHull_NotTheSceneDefault()
        {
            var scenic = LoadHull("FishingSkiff");     // what the scene banked: the sprite compass
            var restored = LoadHull("Dory");           // what a save-restore swaps her to: the mesh dory

            var (go, _) = NewBoatAsASceneSerialisesHer(scenic);
            var fleet = go.GetComponent<OwnedFleet>();
            fleet.Configure(new[] { scenic, restored }, go.GetComponent<BoatController>(),
                            hold: null, spriteRenderer: go.GetComponent<SpriteRenderer>());

            // The restore arrives BEFORE Start — it re-points feel, hold and picture through the swap path.
            fleet.RestoreFromSave(new SaveData { ActiveHullId = restored.Id });

            yield return null;      // ...and now the load-time pass runs on top of it

            Assert.AreEqual(restored, go.GetComponent<BoatController>().Hull,
                "the restore owns which hull is worn — the load-time pass must not re-point it");
            Assert.AreEqual(restored.Visual.Variant, PresentedVariant(go),
                "the load-time pass must present the WORN hull, not re-present the scene's serialised one");
        }
    }
}
