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

        /// <summary>Where the life guard's stepped clock starts, in game seconds.</summary>
        private const double LifeClockOrigin = 1000.0;

        /// <summary>The life guard's clock step while it waits for a beat: under a quarter of the slowest
        /// clip frame the cast plays (rig 9's idle, 5.88 fps).</summary>
        private const double BeatStepSeconds = 0.04;

        /// <summary>A bound on that wait: many clip frames' worth of steps.</summary>
        private const int MaxBeatSteps = 200;

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

        [UnityTest]
        public IEnumerator AMooredSkipper_BlinksOnTheirOwnClock_AndLooksAtThePlayerNearby()
        {
            // Character PR 2a (A2) down production's whole road: the skipper's figure is keyed by the boat's
            // one identity, the owner id; it blinks as the rig's BLINK says, on the clock the clips play on;
            // and it turns toward a player standing within the radius, and not toward one past it. The skin
            // is the skipper's own as committed, so this is RED until the cast is re-baked with the face, the
            // blink and the look (PR 2a Phase B), by design.
            AssertTheRealServicesAreRegistered();
            CharacterSkinDef skin = SkinForTheSkipper();
            Assert.IsTrue(skin.HasBlink, $"'{skin.Id}' carries no blink. Re-bake the cast (character PR 2a, Phase B).");
            Assert.IsTrue(skin.HasLook, $"'{skin.Id}' carries no look. Re-bake the cast (character PR 2a, Phase B).");
            GameConfig config = UseConfig(meshCast: true);
            Assert.IsTrue(config.CharacterBlink && config.CharacterHeadLook && config.CharacterEyeLook,
                "harness: a config built in code must start with the figure's life on");
            var clock = new SteppedClock { TotalSeconds = LifeClockOrigin };
            GameServices.Clock = clock;
            BoatOwnerDef owner = SkipperOwnerWearing(skin);

            MooredBoat moored = Moor(owner, Vector2.zero);
            yield return Settle();

            var presenter = SkipperOf(moored, owner.Id).GetComponent<CharacterFigurePresenter>();
            Assert.IsNotNull(presenter, $"'{owner.Id}': no figure presenter was attached");
            Assert.AreEqual(CharacterFigurePresenter.Refusal.None, presenter.WhyNot,
                $"'{owner.Id}' is not drawing as their mesh: {presenter.NotDrawingReason}");
            IsoCharacterFigureRenderer figure = presenter.Figure;
            Assert.AreEqual(owner.Id, presenter.FigureLife.Key,
                "the skipper's life must be keyed by their boat's one identity, the owner id");
            Assert.AreEqual(CharacterFigurePresenter.KeyHash(owner.Id), presenter.FigureLife.KeyHash,
                "the skipper's life must be handed the ONE hash of that identity, the one the presenter took");
            Assert.AreEqual(CharacterFigureBlink.SeedFor(skin.Id, CharacterFigurePresenter.KeyHash(owner.Id)),
                presenter.FigureLife.Blink.Seed, "the skipper's blink must be seeded by their skin and that one hash");
            Assert.IsTrue(skin.TryGetClip(presenter.DrawnStateKey, out CharacterSkinDef.SkinClip clip),
                $"harness: '{skin.Id}' has no clip '{presenter.DrawnStateKey}'");

            // ---- the blink: its first step shows over the clip's own eyes, and the eyes come back after it.
            CharacterFigureBlink blink = presenter.FigureLife.Blink;
            double start = blink.NextStart;
            Assert.That(start, Is.InRange(LifeClockOrigin, LifeClockOrigin + skin.BlinkIntervalSeconds.y + 1e-6),
                "the first blink must fall inside the first wait");
            Assert.Greater(skin.BlinkDoubleGapSeconds, 0f, "harness: the rig's blink has a gap before its second");
            CharacterSkinDef.BlinkStep first = skin.BlinkSteps[0];
            clock.TotalSeconds = start + first.Seconds * 0.5;
            yield return Settle();
            Assert.IsFalse(CharacterFigureFace.Skips(skin, OwnEyes(skin, clip, figure.DrawnFrame)),
                "harness: the frame shown must be one a blink shows over");
            Assert.AreEqual(first.Group, figure.DrawnFace.x,
                $"'{owner.Id}' did not blink {first.Seconds * 0.5:F3} s into the blink their clock scheduled");

            clock.TotalSeconds = start + blink.Length + skin.BlinkDoubleGapSeconds * 0.5;
            yield return Settle();
            Assert.AreEqual(OwnEyes(skin, clip, figure.DrawnFrame), figure.DrawnFace.x,
                $"'{owner.Id}''s eyes did not come back to the clip's own after the blink");

            // ---- the look: a player a step ahead and to the skipper's right is looked at; one past the
            // radius is not. The look is sampled on the clip's beat, so each read waits for the next one.
            var player = new GameObject("LookedAtPlayer");
            _spawned.Add(player);
            player.transform.position = WorldAtFigureGround(figure, new Vector2(0.8f, 1.5f));
            GameServices.PlayerTransform = player.transform;
            yield return ToTheNextBeat(clock, figure);
            Assert.That(figure.DrawnLookYaw, Is.GreaterThan(0.0).And.LessThanOrEqualTo(skin.LookYawLimits.y),
                $"'{owner.Id}' did not turn toward a player a step ahead and to their right");

            float pastTheRadius = config.CharacterLookRadiusMetres + 1f;
            player.transform.position = WorldAtFigureGround(figure, new Vector2(pastTheRadius, 1.5f));
            yield return ToTheNextBeat(clock, figure);
            Assert.AreEqual(0.0, figure.DrawnLookYaw, $"'{owner.Id}' turned toward a player past the radius");
            Assert.AreEqual(0.0, figure.DrawnLookPitch, $"'{owner.Id}' tipped toward a player past the radius");
            Assert.AreEqual(CharacterFigureLook.GazeOpen, figure.DrawnGaze,
                $"'{owner.Id}''s eyes followed a player past the radius");
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

        /// <summary>The clock the skipper's clips and blink play on in the life guard: stepped by the test
        /// alone, so a blink and a beat land where the test puts them.</summary>
        private sealed class SteppedClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>The eyes a frame shows of its own: its face track's, else the def's rest face.</summary>
        private static int OwnEyes(CharacterSkinDef skin, in CharacterSkinDef.SkinClip clip, int frame)
        {
            int eyes = clip.FaceGroupOf(frame, CharacterSkinDef.EyesSlot);
            if (eyes != CharacterSkinDef.NoFaceGroup) return eyes;
            return skin.RestFace != null && skin.RestFace.Length > CharacterSkinDef.EyesSlot
                ? skin.RestFace[CharacterSkinDef.EyesSlot]
                : CharacterSkinDef.NoFaceGroup;
        }

        /// <summary>The world point that stands at <paramref name="ground"/> on the figure's own ground, in
        /// its rig metres. <see cref="IsoCharacterFigureRenderer.TryFigureGround"/> is affine in the world
        /// point, so three probes give its inverse; the answer is checked back through it.</summary>
        private static Vector3 WorldAtFigureGround(IsoCharacterFigureRenderer figure, Vector2 ground)
        {
            Vector3 o = figure.transform.position;
            bool seen = figure.TryFigureGround(o, out Vector3 g0);
            seen &= figure.TryFigureGround(o + Vector3.right, out Vector3 gx);
            seen &= figure.TryFigureGround(o + Vector3.up, out Vector3 gy);
            Assert.IsTrue(seen, "harness: the figure's ground is seen edge-on");
            double a = gx.x - g0.x, b = gy.x - g0.x, c = gx.y - g0.y, d = gy.y - g0.y;
            double det = a * d - b * c;
            double rx = ground.x - g0.x, ry = ground.y - g0.y;
            var world = o + new Vector3((float)((d * rx - b * ry) / det), (float)((a * ry - c * rx) / det), 0f);
            Assert.IsTrue(figure.TryFigureGround(world, out Vector3 back), "harness: the probe was refused");
            Assert.That(Vector2.Distance(ground, back), Is.LessThan(1e-3f),
                $"harness: the player was meant to stand at {ground} on the figure's ground and stands at {back}");
            return world;
        }

        /// <summary>Step the clock a quarter of a clip frame at a time until the figure is asked for a new
        /// frame, its next beat, and let the presenter pose it once more.</summary>
        private static IEnumerator ToTheNextBeat(SteppedClock clock, IsoCharacterFigureRenderer figure)
        {
            int was = figure.RequestedFrame;
            for (int i = 0; i < MaxBeatSteps && figure.RequestedFrame == was; i++)
            {
                clock.TotalSeconds += BeatStepSeconds;
                yield return null;
            }
            Assert.AreNotEqual(was, figure.RequestedFrame,
                "harness: the clip never asked for a new frame, so the look was never sampled again");
            yield return null;
        }

        private static List<Transform> FiguresUnder(Transform root) =>
            root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == CharacterFigurePresenter.FigureObjectName)
                .ToList();
    }
}
