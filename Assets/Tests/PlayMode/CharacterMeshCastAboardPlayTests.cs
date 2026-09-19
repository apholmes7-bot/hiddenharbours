using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A skipper aboard their moored boat draws as their skinned mesh</b> (ADR 0044, amendment 2026-09-17:
    /// the owner's "yes make everyone a mesh now", Phase B, option (b)).
    ///
    /// <para>Production's whole road, with nothing faked and nothing pre-registered. <see cref="MooredBoat"/>
    /// stands the skipper, claims their deck slot and asks Core's <see cref="CharacterFigurePresentation"/>
    /// for a figure. Art's service, registered before the first scene loads, attaches the presenter. The
    /// presenter hides the sprite through <c>forceRenderingOff</c> and draws the mesh inside the hull's posed
    /// mesh, where the hull's facet pass draws it.</para>
    ///
    /// <para>⚠ <b>Until Phase C bakes the cast, no NPC's art def links a skin.</b> The first three tests
    /// therefore dress a CLONE of Leo Arsenault's skipper in their own skin if it exists, and otherwise in
    /// the player's committed skin, the one def on disk a presenter has already drawn. No committed asset is
    /// written. <see cref="EverySkipperOnTheRegister_DrawsAsTheirOwnBakedMesh"/> reads the real register
    /// instead, and is RED until the bake lands, by design.</para>
    /// </summary>
    public class CharacterMeshCastAboardPlayTests
    {
        private const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";
        private const string SkipperOwnerPath = "Assets/_Project/Data/Boats/Owners/ArsenaultLeo.asset";
        private const string StandInSkinPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        private const string ShippedConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SkipperOwnerId = "owner.arsenault_leo";
        private const string CastSkinIdPrefix = "charskin.";

        /// <summary>Off every axis on purpose. At a heading of 0 a figure turned by the COMPASS heading
        /// and one turned by the DECK bearing look the same, so a lost subtraction would hide.</summary>
        private const float SkipperHeadingDegrees = 135f;

        /// <summary>Far enough apart that no two hulls' pictures or slots could be confused.</summary>
        private const float BerthSpacingMetres = 40f;

        private const int SettleFrames = 3;

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
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

        // ------------------------------------------------------------------ the tests

        [UnityTest]
        public IEnumerator AMooredSkipperWithASkin_DrawsAsTheirMesh_AndTheSpriteStandsDown()
        {
            AssertTheRealServicesAreRegistered();
            CharacterSkinDef skin = SkinForTheSkipper();
            UseConfig(meshCast: true);
            BoatOwnerDef owner = SkipperOwnerWearing(skin);

            MooredBoat moored = Moor(owner, Vector2.zero);
            yield return Settle();

            Assert.IsTrue(moored.IsPresented, $"harness: '{owner.Id}' drew no hull at all");
            IsoCharacterSprite skipper = SkipperOf(moored, owner.Id);
            var sprite = skipper.GetComponent<SpriteRenderer>();
            var presenter = skipper.GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(presenter,
                $"'{owner.Id}': their art def links '{skin.Id}' and no figure presenter was attached. Either " +
                "MooredBoat never asked Core for a figure, or Art's service never answered.");

            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot,
                $"'{owner.Id}' is not drawing as their mesh: {presenter.NotDrawingReason}");
            Assert.IsTrue(presenter.DrawsInsteadOfSprite);
            Assert.AreSame(skin, presenter.Skin, $"'{owner.Id}' draws '{presenter.Skin?.Id}', not '{skin.Id}'");

            // The sprite stands down by forceRenderingOff and never by enabled: enabled belongs to whoever
            // stands the character (a villager's shelter, a slot hiding someone), and the presenter only reads it.
            Assert.IsTrue(presenter.HidesSprite);
            Assert.IsTrue(sprite.forceRenderingOff,
                $"'{owner.Id}': the mesh draws and the sprite still draws too, so the skipper is on screen twice");
            Assert.IsTrue(sprite.enabled, $"'{owner.Id}': the sprite was DISABLED. The presenter hides it with " +
                                          "forceRenderingOff and must never take enabled from its owner.");

            IsoFacetHullRenderer[] hulls = moored.GetComponentsInChildren<IsoFacetHullRenderer>(true);
            Assert.AreEqual(1, hulls.Length, $"harness: '{owner.Id}' carries {hulls.Length} facet hull renderers");
            IsoFacetHullRenderer hull = hulls[0];
            Assert.IsNotNull(hull.PosedMesh, $"harness: '{owner.Id}''s facet hull built no posed mesh");
            Assert.AreSame(hull, presenter.Hull, $"'{owner.Id}': the figure stands on a hull that is not their boat's");

            List<Transform> figures = FiguresUnder(moored.transform);
            Assert.AreEqual(1, figures.Count,
                $"'{owner.Id}': {figures.Count} '{CharacterFigurePresenter.FigureObjectName}' objects on one boat " +
                "with one skipper");
            Transform figure = figures[0];
            Assert.AreSame(presenter.Figure.transform, figure);
            Assert.AreSame(hull.PosedMesh, figure.parent,
                $"'{owner.Id}': the figure hangs off '{figure.parent?.name}', not the hull's posed mesh, so the " +
                "hull's facet pass (its depth, its light and the cabin cut) never draws it");
            Assert.AreEqual(hull.PosedMesh.gameObject.layer, figure.gameObject.layer,
                $"'{owner.Id}': the figure is on another layer than its hull, so the facet pass never sees it");
            Assert.IsTrue(presenter.Figure.Visible, $"'{owner.Id}': the figure is built and hidden");

            Assert.AreEqual(CharacterSkinStateMap.Idle, presenter.DrawnStateKey,
                $"'{owner.Id}' stands still (a held speed of 0), so the sprite would show idle and the mesh must too");

            // Where their feet are: the point the boat gave the deck-occupant slot, so the mesh and the cabin
            // that must hide it agree. Read on the transform that draws, not only on the presenter's readout.
            Vector3 stand = moored.OccupantStandRigMeters;
            Assert.That(Vector3.Distance(stand, figure.localPosition), Is.LessThan(1e-4f),
                $"'{owner.Id}': the figure stands at {figure.localPosition} in the hull's rig metres and the " +
                $"deck slot was told {stand}. The cabin would cut a skipper who is not where it thinks.");

            // Which way they face: the skipper holds the boat's own heading, so their bearing against the deck
            // is zero whatever the compass says.
            Assert.That(Quaternion.Angle(Quaternion.identity, figure.localRotation), Is.LessThan(0.5f),
                $"'{owner.Id}' holds their boat's heading ({SkipperHeadingDegrees}°) and the figure is turned " +
                $"{presenter.FigureYawDegrees}° against the deck. It is facing by the compass, not the deck.");
        }

        [UnityTest]
        public IEnumerator TheSwitch_OffAtWake_KeepsTheSprite_AndFlippingItHandsTheDrawBothWays()
        {
            AssertTheRealServicesAreRegistered();
            CharacterSkinDef skin = SkinForTheSkipper();
            GameConfig config = UseConfig(meshCast: false);
            BoatOwnerDef owner = SkipperOwnerWearing(skin);

            MooredBoat moored = Moor(owner, Vector2.zero);
            yield return Settle();

            IsoCharacterSprite skipper = SkipperOf(moored, owner.Id);
            var sprite = skipper.GetComponent<SpriteRenderer>();
            var presenter = skipper.GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(presenter,
                "the presenter is attached for the SKIN, not the switch: with none attached, MeshCast turned on " +
                "mid-session would find nobody to draw");

            Assert.AreEqual(CharacterFigurePresenter.Refusal.SwitchOff, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsFalse(presenter.DrawsInsteadOfSprite);
            Assert.IsFalse(sprite.forceRenderingOff, "MeshCast is off and the skipper's sprite is hidden: they draw as nothing");
            Assert.IsTrue(sprite.enabled);
            Assert.IsNull(presenter.Figure, "the switch was off from wake and a figure was built anyway (rule 7)");
            Assert.IsEmpty(FiguresUnder(moored.transform), "a figure exists under a boat whose switch never opened");

            config.MeshCast = true;
            yield return Settle();

            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot,
                $"MeshCast turned on and the skipper did not take the mesh: {presenter.NotDrawingReason}");
            Assert.IsTrue(sprite.forceRenderingOff);
            IsoCharacterFigureRenderer built = presenter.Figure;
            Assert.IsNotNull(built);
            Assert.IsTrue(built.Visible);

            config.MeshCast = false;
            yield return null;

            Assert.AreEqual(CharacterFigurePresenter.Refusal.SwitchOff, presenter.WhyNot, presenter.NotDrawingReason);
            Assert.IsFalse(presenter.DrawsInsteadOfSprite);
            Assert.IsFalse(sprite.forceRenderingOff,
                "MeshCast turned off and the sprite was not given back: the skipper draws as nothing");
            Assert.IsTrue(sprite.enabled);
            Assert.AreSame(built, presenter.Figure,
                "turning the switch off threw the figure away. It is hidden and kept, so turning it back on does " +
                "not rebuild a mesh, a material and two ramps (rule 7).");
            Assert.IsFalse(built.Visible, "MeshCast is off and the figure still shows, beside the sprite");
        }

        [UnityTest]
        public IEnumerator ASkipperWhoseArtDefNamesNoSkin_GetsNoFigureAtAll()
        {
            AssertTheRealServicesAreRegistered();
            CharacterSkinDef skin = SkinForTheSkipper();
            UseConfig(meshCast: true);
            BoatOwnerDef dressed = SkipperOwnerWearing(skin);
            BoatOwnerDef bare = SkipperOwnerWearing(null);

            MooredBoat dressedBoat = Moor(dressed, Vector2.zero);
            MooredBoat bareBoat = Moor(bare, new Vector2(BerthSpacingMetres, 0f));
            yield return Settle();

            // The positive first, in the same world, so the negative below cannot pass on a dead service.
            var dressedPresenter = SkipperOf(dressedBoat, "the dressed skipper").GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(dressedPresenter, "harness: the dressed skipper got no presenter, so nothing here can fail");
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, dressedPresenter.WhyNot,
                $"harness: the dressed skipper is not drawing, so nothing here can fail: {dressedPresenter.NotDrawingReason}");

            IsoCharacterSprite skipper = SkipperOf(bareBoat, "the bare skipper");
            Assert.IsNull(skipper.GetComponent<CharacterFigurePresenter>(),
                "a skipper whose art def links no skin was given a figure presenter; they stand exactly as before " +
                "the cast, as a sprite and nothing else");
            System.Reflection.Assembly art = typeof(CharacterFigurePresenter).Assembly;
            string[] artComponents = skipper.GetComponents<Component>()
                .Where(c => c != null && c.GetType().Assembly == art)
                .Select(c => c.GetType().Name).ToArray();
            Assert.IsEmpty(artComponents,
                $"a skipper with no skin carries Art's [{string.Join(", ", artComponents)}]");
            Assert.IsFalse(skipper.GetComponent<SpriteRenderer>().forceRenderingOff,
                "a skipper with no skin has their sprite hidden, so they draw as nothing");
            Assert.IsEmpty(FiguresUnder(bareBoat.transform), "a figure exists on a boat whose skipper has no skin");
        }

        /// <summary>
        /// <b>RED UNTIL PHASE C.</b> Every skipper on the real register draws as the skin Phase C bakes for
        /// them, under the switch as it ships. Until the cast bake is committed only the one skipper who
        /// wears the player's def can pass. The failure lists every other owner by name, which is the list the
        /// bake must empty.
        /// </summary>
        [UnityTest]
        public IEnumerator EverySkipperOnTheRegister_DrawsAsTheirOwnBakedMesh()
        {
            AssertTheRealServicesAreRegistered();
            GameConfig shipped = Load<GameConfig>(ShippedConfigPath);
            Assert.IsTrue(shipped.MeshCast,
                $"{ShippedConfigPath} ships MeshCast OFF. The owner ruled 09-17 that the cast follows the player " +
                "onto skinned meshes.");
            GameServices.Config = shipped;   // read, never written

            List<BoatOwnerDef> owners = LoadOwners()
                .Where(o => o.Skipper != null && o.AboardCount() > 0)
                .ToList();
            Assert.IsTrue(owners.Any(o => o.Id == SkipperOwnerId),
                $"harness: '{SkipperOwnerId}' no longer has a skipper aboard, so the register lost the case the " +
                "other tests here are built on");

            var berths = owners
                .Select((o, i) => (owner: o, boat: Moor(o, new Vector2(i * BerthSpacingMetres, 0f))))
                .ToList();
            yield return Settle();

            var failures = new List<string>();
            foreach (var berth in berths)
            {
                BoatOwnerDef owner = berth.owner;
                CharacterVisualDef def = owner.Skipper;
                string who = $"'{owner.Id}' ({def.name})";
                CharacterSkinDef skin = def.Skin;
                if (skin == null)
                {
                    failures.Add($"{who}: the art def links no CharacterSkinDef");
                    continue;
                }
                if (!skin.Id.StartsWith(CastSkinIdPrefix, System.StringComparison.Ordinal))
                    failures.Add($"{who}: links '{skin.Id}', which is not a {CastSkinIdPrefix}* id");

                if (!berth.boat.IsPresented)
                {
                    failures.Add($"{who}: the boat drew no hull");
                    continue;
                }

                IsoCharacterSprite[] skippers = berth.boat.GetComponentsInChildren<IsoCharacterSprite>(true);
                if (skippers.Length != 1)
                {
                    failures.Add($"{who}: {skippers.Length} characters stand aboard, not the one skipper");
                    continue;
                }

                var presenter = skippers[0].GetComponent<CharacterFigurePresenter>();
                if (presenter == null)
                {
                    failures.Add($"{who}: links '{skin.Id}' and was given no figure presenter");
                    continue;
                }
                if (presenter.WhyNot != CharacterFigurePresenter.Refusal.None)
                    failures.Add($"{who}: {presenter.WhyNot}, {presenter.NotDrawingReason}");
                else if (!ReferenceEquals(presenter.Skin, skin))
                    failures.Add($"{who}: draws '{presenter.Skin?.Id}', not their own '{skin.Id}'");
                if (!skippers[0].GetComponent<SpriteRenderer>().forceRenderingOff)
                    failures.Add($"{who}: the sprite still draws");
            }

            Assert.IsEmpty(failures,
                $"{failures.Count} problem(s) across {berths.Count} skippers aboard. Every one should draw as the " +
                "skin the cast bake links (ADR 0044 §7):\n  " + string.Join("\n  ", failures));
        }

        // ------------------------------------------------------------------ the harness

        private static void AssertTheRealServicesAreRegistered()
        {
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service,
                "harness: Art registers CharacterFigurePresentation.Service before the first scene loads, and " +
                $"it is {(CharacterFigurePresentation.Service == null ? "null" : CharacterFigurePresentation.Service.GetType().Name)}. " +
                "No stand could ask for a figure.");
            Assert.IsNotNull(HullMeshPresentation.Service,
                "harness: no hull mesh service is registered, so no hull draws as a facet mesh to stand a figure in");
        }

        private static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these load the REAL committed defs, not a mirror.");
            return null;
#endif
        }

        private static List<BoatOwnerDef> LoadOwners()
        {
#if UNITY_EDITOR
            var owners = AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { OwnersFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(o => o != null)
                .OrderBy(o => o.Id, System.StringComparer.Ordinal)
                .ToList();
            Assert.IsNotEmpty(owners, $"harness: no BoatOwnerDef assets under {OwnersFolder}");
            return owners;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed register, not a mirror.");
            return null;
#endif
        }

        /// <summary>The skin a clone of the skipper wears: their own once Phase C links it, and until then
        /// the player's, which a presenter has drawn on every build since #839.</summary>
        private static CharacterSkinDef SkinForTheSkipper()
        {
            CharacterVisualDef committed = Load<BoatOwnerDef>(SkipperOwnerPath).Skipper;
            Assert.IsNotNull(committed, $"harness: {SkipperOwnerPath} names no skipper");
            CharacterSkinDef skin = committed.Skin != null ? committed.Skin : Load<CharacterSkinDef>(StandInSkinPath);
            Assert.IsTrue(skin.IsUsable(), $"harness: '{skin.Id}' is not usable, so no figure could be built from it");
            Assert.IsTrue(skin.DrawsAsMesh(CharacterSkinStateMap.Idle),
                $"harness: '{skin.Id}' does not switch on '{CharacterSkinStateMap.Idle}', the state a moored " +
                "skipper stands in");
            return skin;
        }

        private GameConfig UseConfig(bool meshCast)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.MeshCast = meshCast;
            _spawned.Add(config);
            GameServices.Config = config;
            return config;
        }

        /// <summary>A clone of Leo Arsenault's register entry whose skipper is a clone of their committed
        /// art def wearing <paramref name="skin"/> (null for none). The committed assets are never written;
        /// the boat is the committed one, read as it is.</summary>
        private BoatOwnerDef SkipperOwnerWearing(CharacterSkinDef skin)
        {
            BoatOwnerDef committed = Load<BoatOwnerDef>(SkipperOwnerPath);
            Assert.AreEqual(SkipperOwnerId, committed.Id, $"harness: {SkipperOwnerPath} changed id");
            Assert.IsNotNull(committed.Skipper, $"harness: {SkipperOwnerPath} names no skipper");
            Assert.Greater(committed.AboardCount(), 0, $"harness: '{committed.Id}' has no room aboard for a skipper");
            Assert.IsTrue(committed.Boat != null && committed.Boat.Visual != null,
                $"harness: '{committed.Id}' has no boat art");
            Assert.AreEqual(BoatHullVariant.Mesh, committed.Boat.Visual.Variant,
                $"harness: '{committed.Id}''s boat is a sprite hull, which has no facet pass to draw a figure in");

            var visual = Object.Instantiate(committed.Skipper);
            visual.name = committed.Skipper.name;
            visual.Skin = skin;
            _spawned.Add(visual);

            var owner = Object.Instantiate(committed);
            owner.name = committed.name;
            owner.Skipper = visual;
            _spawned.Add(owner);
            return owner;
        }

        private MooredBoat Moor(BoatOwnerDef owner, Vector2 at)
        {
            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, SkipperHeadingDegrees);
            go.SetActive(true);
            return moored;
        }

        private static IEnumerator Settle()
        {
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        private static IsoCharacterSprite SkipperOf(MooredBoat moored, string who)
        {
            IsoCharacterSprite[] characters = moored.GetComponentsInChildren<IsoCharacterSprite>(true);
            Assert.AreEqual(1, characters.Length, $"harness: '{who}' stands {characters.Length} characters, not one skipper");
            Assert.AreEqual(MooredBoat.SkipperChildName, characters[0].name, $"harness: '{who}''s character is not the skipper");
            return characters[0];
        }

        private static List<Transform> FiguresUnder(Transform root) =>
            root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == CharacterFigurePresenter.FigureObjectName)
                .ToList();
    }
}
