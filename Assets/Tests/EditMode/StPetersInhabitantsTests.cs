using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Economy;             // StallGate — the reach every counter interaction reads
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PEOPLE ON ST PETERS.</b> M1 §7.1 asks for "4–6 named inhabitants with a line of dialogue
    /// with an opinion and a fixed spot", anchored rather than scheduled, and its exit condition is that
    /// you can walk the island, meet everyone, and know what each building is for.
    ///
    /// <para>These assert that against the AUTHORED WORLD — the real terrain, the real wharf geometry and
    /// the building kit's own baked footprints — rather than against the constants in
    /// <see cref="StPetersInhabitants"/>. A position test that re-states the number it is checking proves
    /// nothing (the standing lesson of #345, where every village position was a comment claiming dry
    /// ground and three of them were wrong at once).</para>
    ///
    /// <para>Content validation for the underlying assets (ids namespaced + unique, dialogue resolves,
    /// no blank lines) is already generic over everything in <c>Data/</c> in
    /// <see cref="NpcContentValidationTests"/>, so the five new NpcDefs are covered there the moment they
    /// import. What is here is what is SPECIFIC to this cast: that it is the right size, that each person
    /// is where their job is, and that the re-greet they were written with can actually play.</para>
    /// </summary>
    public class StPetersInhabitantsTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // 2.2
        const string DataNpcs = "Assets/_Project/Data/NPCs";

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_InhabitantsTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static IReadOnlyList<StPetersInhabitants.Person> People => StPetersInhabitants.People;

        static NpcDef Load(StPetersInhabitants.Person p) =>
            AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{p.AssetName}.asset");

        // =================================================================================
        //  THE CAST ITSELF
        // =================================================================================

        [Test]
        public void TheIslandHasFourToSixNamedInhabitants_AndEveryOneOfThemHasAnAsset()
        {
            Assert.GreaterOrEqual(People.Count, 4,
                "§7.1 asks for 4–6 named inhabitants — fewer and the village is a set of empty buildings");
            Assert.LessOrEqual(People.Count, 6,
                "§7.1 caps the opening cast at 6; more people than that is M2's job, with routines to match");

            foreach (var p in People)
                Assert.IsNotNull(Load(p),
                    $"{p.AssetName} has no NpcDef at {DataNpcs}/{p.AssetName}.asset — the builder would " +
                    $"skip the one who stands {p.Reason}");
        }

        [Test]
        public void EveryInhabitantIsNamespacedAndUnique_IncludingAgainstTheOpeningCast()
        {
            // The opening cast SHIPPED (npc.aunt_ginny / npc.ned_letter). Ids are append-only and stable
            // (CLAUDE.md §5), so a new islander colliding with one of those is a permanent mistake, not a
            // rename away — which is exactly why this checks the whole of Data/NPCs and not just the five.
            var byId = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:NpcDef", new[] { DataNpcs }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>(path);
                if (npc == null) continue;

                Assert.IsTrue(npc.Id.StartsWith("npc."),
                    $"{path}: '{npc.Id}' must be namespaced 'npc.snake_case'");
                Assert.IsFalse(byId.ContainsKey(npc.Id),
                    $"duplicate NpcDef id '{npc.Id}' in '{path}' and " +
                    $"'{(byId.TryGetValue(npc.Id, out var other) ? other : "?")}'");
                byId[npc.Id] = path;
            }

            foreach (var p in People)
            {
                var npc = Load(p);
                if (npc == null) continue;
                Assert.IsFalse(string.IsNullOrWhiteSpace(npc.DisplayName),
                    $"{p.AssetName} has no DisplayName — the nameplate would be blank");
                Assert.AreEqual(InteractKind.Talk, npc.Kind,
                    $"{p.AssetName} is a PERSON: you talk to them, you don't read them");
            }
        }

        [Test]
        public void EveryInhabitantSpeaks_AndTheirWarmerRegreetCanActuallyPlay()
        {
            foreach (var p in People)
            {
                var npc = Load(p);
                if (npc == null) continue;

                Assert.IsTrue(npc.HasDialogue,
                    $"{p.AssetName} points at no DialogueDef — walking up to them would open nothing");
                Assert.IsNotEmpty(npc.Dialogue.FirstLines,
                    $"{p.AssetName}'s conversation has no first line, so it can never play");

                // §7.1 asks for "a line of dialogue with an opinion". An opinion needs room to be stated:
                // a single clause is a greeting. This is a floor, not a style guide.
                Assert.GreaterOrEqual(npc.Dialogue.FirstLines.Length, 2,
                    $"{p.AssetName} says one line — §7.1 asks for a line with an OPINION, which needs " +
                    "more than a hello");

                // The re-greet is only REACHABLE if a completion flag exists to flip metBefore: the
                // interactor reads flags.Get(CompletionFlag), and an empty key always returns false
                // (OnboardingFlags.Get). Repeat lines behind an empty flag are dead content that would
                // never be seen and never be caught.
                if (npc.Dialogue.RepeatLines != null && npc.Dialogue.RepeatLines.Length > 0)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(npc.CompletionFlag),
                        $"{p.AssetName} has RepeatLines but no CompletionFlag — metBefore could never " +
                        "become true, so the warmer re-greet would never play");
            }
        }

        [Test]
        public void NobodyIsOnARoutine_BecauseRoutinesAreM2()
        {
            // The guard is structural: NpcDef is deliberately the minimal anchored shape (its own remarks
            // say so), so there is no schedule field to fill in by accident. If someone adds one to the
            // Def before the M2 routine engine lands, this is the test that says "not yet, and here is
            // where the boundary was written down".
            var fields = typeof(NpcDef).GetFields()
                .Select(f => f.Name.ToLowerInvariant()).ToArray();

            foreach (string forbidden in new[] { "schedule", "routine", "anchor", "waypoint" })
                Assert.IsFalse(fields.Any(f => f.Contains(forbidden)),
                    $"NpcDef grew a '{forbidden}' field — the St Peters cast is ANCHORED, not scheduled " +
                    "(npcs-and-routines.md §2 keeps the schedule engine in M2). Land the routine engine " +
                    "first, then give them routines.");
        }

        // =================================================================================
        //  EVERYONE IS SOMEWHERE THAT MAKES SENSE
        // =================================================================================

        [Test]
        public void EveryoneOnGroundStandsWhereItIsDryAtEveryTide()
        {
            foreach (var p in People)
            {
                if (p.Ground != StPetersInhabitants.Footing.DryGround) continue;

                float e = _terrain.ElevationAt(p.Position);
                Assert.Greater(e, SpringHighWater,
                    $"{p.AssetName} stands at {p.Position} where the ground is {e:0.00} m — at or below " +
                    $"spring high water ({SpringHighWater:0.00} m). They would be standing in the sea " +
                    "twice a day.");
            }
        }

        [Test]
        public void TheRetiredFishermanStandsOnTheWharfDeck_WhichIsPlanksOverWater()
        {
            var onDeck = People.Where(p => p.Ground == StPetersInhabitants.Footing.Deck).ToArray();
            Assert.IsNotEmpty(onDeck, "somebody should be down at the one dock — it is a named site in §7.1");

            var deck = StPetersWharf.DeckFootprint();
            foreach (var p in onDeck)
            {
                Assert.IsTrue(deck.Contains(p.Position),
                    $"{p.AssetName} at {p.Position} is off the deck rectangle {deck} — he would be " +
                    "standing on open water");

                // ...and this is WHY he needs the deck test rather than the ground test: the sea floor
                // under him is well below spring high water. A pier stands over water; #350 made the deck
                // standable floor precisely so a person can be up there. If this ever stops being true the
                // wharf has moved onto dry land and something is badly wrong with the terrain.
                float seabed = _terrain.ElevationAt(p.Position);
                Assert.Less(seabed, SpringHighWater,
                    $"{p.AssetName} is on the pier, so the ground under the planks should be tidal " +
                    $"water — it measures {seabed:0.00} m, which is dry land. Is the wharf still a wharf?");

                // Not standing in the doorway: the disembark point is where you step ashore off the boat.
                Assert.Greater(Vector2.Distance(p.Position, StPetersBuilder.DisembarkPos), 2f,
                    $"{p.AssetName} is standing on the disembark spot — you would walk through him " +
                    "every time you came home");
            }
        }

        [Test]
        public void TheClamDiggerIsAtTheHeadOfTheFlats_WhereTheDiggingIs()
        {
            var junior = People.First(p => p.AssetName == "JuniorPoirier");

            // Close enough to the wet bucket to read as "the man who works these flats", not a stray
            // villager who wandered west.
            float toBucket = Vector2.Distance(junior.Position, StPetersBuilder.WetBucketPos);
            Assert.Less(toBucket, 10f,
                $"the clam digger is {toBucket:0.0} m from the wet bucket — he belongs AT the head of the " +
                "flats, where you come up off the sand with a bucket");

            // He is past the plateau edge, on the beach — which is the point. The plateau test that guards
            // the houses would be the wrong test for him, and asserting it here documents that difference
            // rather than leaving the next person to wonder whether it was an oversight.
            float d = TidalTerrain.IslandDistance(junior.Position, StPetersBuilder.IslandCenter,
                                                  StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY);
            Assert.Greater(d, StPetersBuilder.IslandRadius,
                "the clam digger works the foreshore, not the village green — he should be off the plateau");
        }

        [Test]
        public void TheKeepersStandInTheVillage_InsideTheClearingTheWoodsLeaveForIt()
        {
            // The three who belong to buildings are all in the village clearing, which is what §7.1's
            // "walk the island in two minutes" is really about: the village core is one small place.
            foreach (var p in People)
            {
                if (p.AssetName == "JuniorPoirier" || p.AssetName == "BasilSamson") continue;

                float d = Vector2.Distance(p.Position, StPetersBuilder.CottagePos);
                Assert.Less(d, StPetersWoods.VillageClearingRadius,
                    $"{p.AssetName} is {d:0.0} m from the hearth, outside the {StPetersWoods.VillageClearingRadius:0.0} m " +
                    "village clearing — they would be standing in the woods, and the planter would be " +
                    "planting trees around them");
            }
        }

        // =================================================================================
        //  NOBODY IS INSIDE A WALL, OR INSIDE ANYBODY ELSE
        // =================================================================================

        [Test]
        public void EachKeeperStandsAtTheirOwnBuildingsDoor_AndClearOfItsFootprint()
        {
            // Derived from the CONTRACT's own footprints, not from DoorstepMetres — so a re-bake with
            // bigger buildings fails here with the number to use, instead of quietly swallowing a
            // shopkeeper into a wall. Same rule StPetersVillageTests applies to the buildings themselves.
            var pairs = new[]
            {
                ("MargueriteLeBlanc", "generalStore",   (Vector2)StPetersBuilder.GeneralStorePos),
                ("EileenDoiron",      "school",         (Vector2)StPetersBuilder.SchoolPos),
                ("RoseMacIsaac",      "redSaltbox",     (Vector2)StPetersBuilder.RedSaltboxPos),
            };

            foreach (var (assetName, buildingKey, site) in pairs)
            {
                var person = People.First(p => p.AssetName == assetName);
                var placement = VillageBuildingCatalog.Find(buildingKey);
                if (!placement.IsValid) continue;   // kit not baked in this checkout — the village test owns that

                float radius = StPetersVillage.FootprintRadiusMetres(placement);
                float d = Vector2.Distance(person.Position, site);

                Assert.Greater(d, radius,
                    $"{assetName} stands {d:0.00} m from the {buildingKey} centre, inside its " +
                    $"{radius:0.00} m footprint — they would be standing IN the building. Raise " +
                    $"StPetersInhabitants.DoorstepMetres above {radius:0.00}.");

                // ...but still at the door, not across the green from it. A keeper you have to hunt for
                // does not tell you what the building is.
                Assert.Less(d, radius + 6f,
                    $"{assetName} stands {d:0.00} m out from the {buildingKey} — far enough that they no " +
                    "longer read as belonging to it");
            }
        }

        [Test]
        public void NobodyStandsInsideAnyOfTheFiveBuildings()
        {
            // Not just their own: Rose's dooryard must also clear the farmhouse next door.
            foreach (var person in People)
            foreach (var site in StPetersVillage.Sites)
            {
                var placement = VillageBuildingCatalog.Find(site.Key);
                if (!placement.IsValid) continue;

                float radius = StPetersVillage.FootprintRadiusMetres(placement);
                float d = Vector2.Distance(person.Position, site.Position);
                Assert.Greater(d, radius,
                    $"{person.AssetName} at {person.Position} is inside the {site.Key} footprint " +
                    $"({d:0.00} m from its centre, radius {radius:0.00} m)");
            }
        }

        /// <summary>
        /// The same test for the SHOPS, and it is not a copy for its own sake. The general store left
        /// <c>StPetersVillage.Sites</c> for <c>StPetersShops.Sites</c> on 2026-08-11, and its real shell
        /// is <b>8.00 × 10.00 m</b> against the house stand-in's 7.08 × 8.89 — a footprint radius of
        /// <b>6.40 m</b> where it used to be 5.68. Marguerite stands at that building's dooryard. Had
        /// this loop not followed her building into the other kit, the one person whose clearance
        /// actually got tighter would have quietly stopped being checked.
        /// </summary>
        [Test]
        public void NobodyStandsInsideAShop()
        {
            foreach (var person in People)
            foreach (var site in StPetersShops.Sites)
            {
                var shell = ShopCatalog.FindShell(site.Key);
                if (!shell.IsValid) continue;

                float radius = ShopCatalog.FootprintRadiusMetres(shell);
                float d = Vector2.Distance(person.Position, site.Position);
                Assert.Greater(d, radius,
                    $"{person.AssetName} at {person.Position} is inside the {site.Key} footprint " +
                    $"({d:0.00} m from its centre, radius {radius:0.00} m — the SHOP shell, which is " +
                    "bigger than the house that used to stand there)");
            }
        }

        [Test]
        public void NobodyIsStandingOnAnybodyElse_IncludingTheOpeningCastAndTheProps()
        {
            const float Personal = 3f;

            // Each NEW islander against every other interactable spot on the island. Deliberately not
            // shipped-against-shipped: Aunt Ginny already stands 1.8 m from her own freezer, which is fine
            // (one is a person and one is an appliance, and the opening was tuned that way). This wave owns
            // where the five stand, not where the opening props do.
            var alsoThere = new List<(string Name, Vector2 At)>
            {
                ("AuntGinny",    (Vector2)StPetersBuilder.GinnyPos),
                ("NedsLetter",   (Vector2)StPetersBuilder.NedsLetterPos),
                ("GinnyFreezer", (Vector2)StPetersBuilder.FreezerPos),
                ("WetBucket",    (Vector2)StPetersBuilder.WetBucketPos),
            };
            foreach (var p in People) alsoThere.Add((p.AssetName, p.Position));

            foreach (var person in People)
            foreach (var other in alsoThere)
            {
                if (other.Name == person.AssetName) continue;
                float d = Vector2.Distance(person.Position, other.At);
                Assert.Greater(d, Personal,
                    $"{person.AssetName} and {other.Name} are {d:0.00} m apart — close enough that the " +
                    "interactor's nearest-target pick would be a coin toss and you could not reliably " +
                    "talk to the one you walked up to");
            }
        }

        // =================================================================================
        //  THE COUNTER SHE STANDS AT (plan-to-m1 §7.5's placement half)
        // =================================================================================

        /// <summary>
        /// A shop and a shopkeeper have to be ONE thing to walk up to. Every stall interaction in the game
        /// gates on <see cref="StallGate.DefaultRange"/> from the stall's own transform, so if the counter
        /// drifts further than that from Marguerite, the player ends up standing at the person and being
        /// told they are too far from the till — the failure
        /// <c>NineMileCreekPeople.ByHisTruckMetres</c> was written to prevent at the creek.
        ///
        /// <para>Note this is the OPPOSITE requirement to
        /// <see cref="NobodyIsStandingOnAnybodyElse_IncludingTheOpeningCastAndTheProps"/>, which keeps
        /// people 3 m apart, and the counter is deliberately not in that test's list: two Interactables
        /// close together make the <c>WorldInteractor</c>'s nearest-target pick a coin toss, but a counter
        /// is not an Interactable — it is a stall, on a different driver and a different key. Talking to
        /// her and trading over her counter never compete, so they SHOULD be within arm's reach.</para>
        /// </summary>
        [Test]
        public void TheStoreCounterStandsWithinReachOfItsKeeper_SoTheyAreOneThingToWalkUpTo()
        {
            var marguerite = People.First(p => p.AssetName == "MargueriteLeBlanc");
            Vector2 counter = (Vector2)StPetersBuilder.GeneralStoreCounterPos;

            float d = Vector2.Distance(counter, marguerite.Position);
            Assert.LessOrEqual(d, StallGate.DefaultRange,
                $"the counter is {d:0.00} m from the storekeeper, past the {StallGate.DefaultRange:0.0} m " +
                "stall reach — you could stand at her and be told you are too far from the till");
        }

        [Test]
        public void TheStoreCounterIsOutsideTheStoresWalls_AndOnGroundThatNeverFloods()
        {
            Vector2 counter = (Vector2)StPetersBuilder.GeneralStoreCounterPos;
            Vector2 site = (Vector2)StPetersBuilder.GeneralStorePos;

            // Against the CONTRACT's own footprint, like every other clearance on this island.
            var placement = VillageBuildingCatalog.Find("generalStore");
            if (placement.IsValid)
            {
                float radius = StPetersVillage.FootprintRadiusMetres(placement);
                Assert.Greater(Vector2.Distance(counter, site), radius,
                    $"the counter is inside the store's {radius:0.00} m footprint — it would be behind the " +
                    "wall, and you cannot walk up to a counter you cannot reach");
            }

            // And against the AUTHORED TERRAIN, not against the constant that placed it (#345's lesson).
            float ground = _terrain.ElevationAt(counter);
            Assert.Greater(ground, SpringHighWater,
                $"the counter sits where the ground is {ground:0.00} m, at or below spring high water " +
                $"({SpringHighWater:0.00} m) — a shop counter does not go under twice a day");
        }

        [Test]
        public void TheCounterIsOnTheGreenSideOfTheStore_OutItsDoorAndPastItsKeeper()
        {
            Vector2 site = (Vector2)StPetersBuilder.GeneralStorePos;
            Vector2 counter = (Vector2)StPetersBuilder.GeneralStoreCounterPos;
            var marguerite = People.First(p => p.AssetName == "MargueriteLeBlanc");

            // #353 turns every door toward the green, so "out of the door" is "toward the green".
            Vector2 toGreen = StPetersBuilder.VillageGreen - site;
            Vector2 toCounter = counter - site;
            Assert.Greater(Vector2.Dot(toGreen.normalized, toCounter.normalized), 0.99f,
                "the counter is round the back of the store — it belongs on the side the door is on");

            // …and a stride FURTHER out than she is, so the player meets the counter and then her behind
            // it, rather than walking through the shopkeeper to reach the till.
            Assert.Greater(Vector2.Distance(counter, site), Vector2.Distance(marguerite.Position, site),
                "the storekeeper should stand behind her own counter, not in front of it");
        }

        [Test]
        public void TheDooryardIsDerivedFromTheBuilding_SoAKeeperMovesWithIt()
        {
            // The property that makes these positions maintainable rather than a second copy of #353's
            // geometry: move the store, and the storekeeper moves with it. Asserted by construction on a
            // site that does not exist, so it tests the RULE and not the current constants.
            var madeUpSite = new Vector3(-90f, 60f, 0f);
            Vector2 spot = StPetersInhabitants.Dooryard(madeUpSite);

            Assert.AreEqual(StPetersInhabitants.DoorstepMetres,
                            Vector2.Distance(spot, madeUpSite), 0.01f,
                "the dooryard sits exactly DoorstepMetres out from its building");

            // And it is out of the DOOR: #353 turns every door toward the green, so the keeper must be on
            // the green side of the building, never behind it.
            Vector2 toGreen = StPetersBuilder.VillageGreen - (Vector2)madeUpSite;
            Vector2 toKeeper = spot - (Vector2)madeUpSite;
            Assert.Greater(Vector2.Dot(toGreen.normalized, toKeeper.normalized), 0.99f,
                "the keeper stands on the green side of their building — the side the door is on");
        }
    }
}
