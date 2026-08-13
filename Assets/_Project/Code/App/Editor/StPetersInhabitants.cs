#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.World;               // NpcDef, Interactable
using HiddenHarbours.Art;                 // YSortSprite — islanders layer with the player by world Y

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE PEOPLE ON THE ISLAND</b> — M1 §7.1's "4–6 named inhabitants with a line of dialogue with
    /// an opinion and a fixed spot". #353 stood the buildings up; this is who is in them. The exit
    /// condition it serves is the section's own: <i>walk the island in two minutes, meet everyone, and
    /// know what each building is for</i> — which is why the storekeeper stands at the store and the
    /// teacher at the school rather than anywhere prettier. A building you can't attach a person to is
    /// scenery; a person standing at its door is a label you can talk to.
    ///
    /// <para><b>⭐ THIS FILE STILL ONLY PLACES THEM — AND THEY NOW WALK.</b> It used to say "anchored, not
    /// scheduled … nothing here plans a path, ticks, or reads the clock", and the first two thirds of that
    /// are still exactly true of THIS file: every position below is a fixed spot, nothing here reads a
    /// clock, and a scene built without routine content stands these five people up precisely as it always
    /// did. What changed is that <see cref="StPetersRoutines"/> now runs after this pass and gives whoever
    /// has a <c>RoutineDef</c> authored a day to keep (M2-23, <c>design/npcs-and-routines.md</c> §2.6). The
    /// spots below are therefore the villagers' <b>starting marks and their working stations</b> — the
    /// routine layer derives its own stations from them rather than from a copy — and an islander with no
    /// routine still simply waits here.</para>
    ///
    /// <para><b>Nothing new is built here.</b> The plumbing already shipped: an <see cref="NpcDef"/> per
    /// person and a <see cref="DialogueDef"/> per conversation under <c>Data/NPCs</c>, placed onto an
    /// <see cref="Interactable"/> that <c>WorldInteractor</c> finds by proximity and
    /// <c>DialoguePresenter</c> draws. This file is authoring and placement, and the words live in assets
    /// the owner can edit without opening C# (CLAUDE.md rule 2).</para>
    ///
    /// <para><b>⭐ The dooryard is DERIVED, not typed in.</b> Three of the five stand at a building, and
    /// their positions are computed from that building's own site constant on
    /// <see cref="StPetersBuilder"/> — outward toward <see cref="StPetersBuilder.VillageGreen"/>, the same
    /// point every door in #353 is already turned toward. So the person stands in front of the door
    /// rather than beside it or through it, and if the owner moves a building its keeper moves with it.
    /// A hand-typed coordinate would have been a second copy of the village's geometry, and #345 is the
    /// standing lesson on what happens when a copy stops agreeing with the original.</para>
    ///
    /// <para><b>⚠️ Two of them do not stand on the plateau, on purpose.</b> Basil is on the WHARF, whose
    /// deck is planks over −1 m of water — checked against <see cref="StPetersWharf.DeckFootprint"/>,
    /// never against the ground, because a pier that stood on dry land would not be a pier (#350 made the
    /// deck standable floor for exactly this). Junior is at the HEAD OF THE FLATS, past the plateau edge
    /// on the beach falloff where a clam digger actually works — dry at every tide, but it is beach, not
    /// village, so the plateau test that guards the houses would be the wrong test for him.</para>
    /// </summary>
    public static class StPetersInhabitants
    {
        /// <summary>The root every islander hangs under.</summary>
        public const string RootName = "IslandInhabitants";

        const string DataNpcs   = "Assets/_Project/Data/NPCs";
        const string ArtChars   = "Assets/_Project/Art/Characters";
        const string ArtPortrait = "Assets/_Project/Art/Portraits";

        /// <summary>
        /// How far out from a building's footprint CENTRE its keeper stands, in metres.
        ///
        /// <para>It has to clear the largest footprint in the kit at any facing. The kit's own half-
        /// diagonals run to ~6.3 m (the white farmhouse, 7.68 × 9.94 m), so 7.5 m clears every one of
        /// them and leaves the person a stride or two out from the wall — standing in the dooryard, not
        /// embedded in the clapboard. <see cref="StPetersVillage.FootprintRadiusMetres"/> is the same
        /// half-diagonal circle the village reserves its ground with, and StPetersInhabitantsTests
        /// re-derives this margin from the CONTRACT rather than trusting this number, so a re-bake with
        /// bigger buildings fails with the figure to use instead of quietly swallowing a shopkeeper.</para>
        /// </summary>
        public const float DoorstepMetres = 7.5f;

        /// <summary>What a person is standing ON — which decides what "is this a legal spot" even means.</summary>
        public enum Footing
        {
            /// <summary>Ground that is dry at every tide. Checked against the authored terrain.</summary>
            DryGround,
            /// <summary>A built structure over water (the wharf deck). Checked against its footprint.</summary>
            Deck,
        }

        /// <summary>
        /// One anchored islander: which asset speaks, where they stand, what they are standing on, and one
        /// line on why there — the same shape <see cref="StPetersVillage.Site"/> uses, for the same reason.
        /// </summary>
        public readonly struct Person
        {
            /// <summary>Asset stem under <c>Data/NPCs</c> — also the NpcDef this places.</summary>
            public readonly string AssetName;
            /// <summary>Art stem under <c>Art/Characters</c> / <c>Art/Portraits</c> (the Ginny/Ned convention).</summary>
            public readonly string ArtStem;
            public readonly Vector2 Position;
            public readonly Footing Ground;
            public readonly Color GreyboxTint;
            public readonly string Reason;

            /// <summary>
            /// Which way they are turned, in degrees (0 = North, CW) — used only when the person has a
            /// baked body, since a greybox rectangle has no front. <b>180 (South, toward the camera) is
            /// the default and the right one for almost everybody:</b> a face is how you tell a person
            /// from scenery. Turn someone away only when the turning is the point.
            /// </summary>
            public readonly float HeadingDegrees;

            public Person(string assetName, string artStem, Vector2 position, Footing ground,
                          Color greyboxTint, string reason, float headingDegrees = 180f)
            {
                AssetName = assetName;
                ArtStem = artStem;
                Position = position;
                Ground = ground;
                GreyboxTint = greyboxTint;
                Reason = reason;
                HeadingDegrees = headingDegrees;
            }
        }

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the cast without either)
        // =====================================================================================

        /// <summary>
        /// The spot in front of a building's door: <see cref="DoorstepMetres"/> out from its site,
        /// toward <see cref="StPetersBuilder.VillageGreen"/>. #353 turns every door toward the green, so
        /// "toward the green" IS "out of the door" without this file needing to know a facing index —
        /// and it stays right if the owner re-bakes the kit with a different number of facings.
        /// </summary>
        public static Vector2 Dooryard(Vector3 buildingSite)
        {
            Vector2 site = new Vector2(buildingSite.x, buildingSite.y);
            Vector2 d = StPetersBuilder.VillageGreen - site;
            if (d.sqrMagnitude < 1e-6f) return site;
            return site + d.normalized * DoorstepMetres;
        }

        /// <summary>
        /// The five, in the order you meet them walking out from the spawn: the green, the lane, then the
        /// flats to the west and the wharf to the east.
        /// </summary>
        public static IReadOnlyList<Person> People => new[]
        {
            new Person("RoseMacIsaac", "Rose",
                       Dooryard(StPetersBuilder.RedSaltboxPos), Footing.DryGround,
                       new Color(0.76f, 0.52f, 0.52f),
                       "the dooryard of the red saltbox, on the green's east side — the neighbour you " +
                       "cross the green past, so the first face you meet is a friendly one"),

            new Person("MargueriteLeBlanc", "Marguerite",
                       Dooryard(StPetersBuilder.GeneralStorePos), Footing.DryGround,
                       new Color(0.72f, 0.60f, 0.44f),
                       "the store's door, at the lane's centre where the path up from the spawn meets it " +
                       "— #353 calls this 'the first door you have a reason to open', and she is the reason"),

            new Person("EileenDoiron", "Eileen",
                       Dooryard(StPetersBuilder.SchoolPos), Footing.DryGround,
                       new Color(0.55f, 0.60f, 0.72f),
                       "the schoolhouse door at the west end of the lane — a one-room school reads as a " +
                       "school the moment its teacher is standing at it"),

            new Person("JuniorPoirier", "Junior",
                       new Vector2(StPetersBuilder.WetBucketPos.x + 4f,
                                   StPetersBuilder.WetBucketPos.y + 2.5f), Footing.DryGround,
                       new Color(0.70f, 0.66f, 0.46f),
                       "the head of the flats, a few strides off the wet bucket — where you come up off " +
                       "the sand with a bucket, which is where the man who digs for a living would be"),

            new Person("BasilSamson", "Basil",
                       new Vector2(StPetersWharf.HeadCellX - 5.5f,
                                   StPetersWharf.MaxCellY - 0.5f), Footing.Deck,
                       new Color(0.58f, 0.62f, 0.58f),
                       "up the wharf from the slip head, watching the water — the dock is the east end " +
                       "(§5.1a), so he is who is there when you come home under power",
                       // The one person NOT turned to the camera: his whole reason for standing there is
                       // that he is looking out at the water, and a wharf with everyone facing inland
                       // reads as a stage set.
                       headingDegrees: 90f),
        };

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the islanders up and return their <see cref="Interactable"/>s so the builder can hand
        /// them to the <c>WorldInteractor</c> (which resolves the nearest by proximity — the list is the
        /// only wiring an NPC needs).
        ///
        /// <para>Null-tolerant like every other layer here: a person whose NpcDef has not imported yet is
        /// SKIPPED with a warning rather than placed mute, because a silent interactable that opens an
        /// empty panel is worse than an absence you can see in the log.</para>
        ///
        /// <para>Art is greybox — a tinted standee at the Ginny marker's size — but the loader looks for
        /// <c>Art/Characters/&lt;stem&gt;.png</c> and <c>Art/Portraits/&lt;stem&gt;.png</c> first, so the
        /// day art-pipeline drops those files in, these people wear them with no code change. The
        /// portrait slot ships EMPTY and legal: <c>DialoguePresenter</c> hides the portrait box when the
        /// sprite is null and draws name + text alone.</para>
        /// </summary>
        public static List<Interactable> Place(ITidalTerrain terrain, Sprite greyboxSquare)
        {
            var placed = new List<Interactable>();
            var root = new GameObject(RootName);
            var report = new List<string>();

            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;

            foreach (var person in People)
            {
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{person.AssetName}.asset");
                if (npc == null)
                {
                    Debug.LogWarning(
                        $"[StPetersInhabitants] '{person.AssetName}' has no NpcDef at {DataNpcs} — the " +
                        $"island is missing the one who stands {person.Reason}. Re-run after the Data/NPCs " +
                        "assets import rather than placing a mute standee.");
                    continue;
                }

                // ⚠️ Checked against the AUTHORED WORLD, not against the constants above — the #345 lesson,
                // and the reason each Person carries what it is standing ON. A villager in the sea and a
                // fisherman floating beside his wharf are both authoring bugs, but they are not the same
                // bug and one test cannot catch both.
                switch (person.Ground)
                {
                    case Footing.DryGround when terrain != null:
                    {
                        float ground = terrain.ElevationAt(person.Position);
                        if (ground <= springHigh)
                            Debug.LogError(
                                $"[StPetersInhabitants] {person.AssetName} stands at {person.Position} " +
                                $"where the ground is {ground:0.00} m — at or below spring high water " +
                                $"({springHigh:0.00} m). People do not stand in the sea. Move the spot; " +
                                "do not lower the tide.");
                        break;
                    }
                    case Footing.Deck:
                    {
                        var deck = StPetersWharf.DeckFootprint();
                        if (!deck.Contains(person.Position))
                            Debug.LogError(
                                $"[StPetersInhabitants] {person.AssetName} stands at {person.Position}, " +
                                $"which is OFF the wharf deck ({deck}). He would be standing on open " +
                                "water — the deck is his floor, so his spot has to be on it.");
                        break;
                    }
                }

                var go = MakeStandee(person, npc, greyboxSquare);
                go.transform.SetParent(root.transform, worldPositionStays: true);

                var interactable = go.AddComponent<Interactable>();
                ConfigureNpc(interactable, npc, LoadSprite($"{ArtPortrait}/{person.ArtStem}.png"));

                placed.Add(interactable);
                report.Add($"{npc.DisplayName} at ({person.Position.x:0.#},{person.Position.y:0.#})" +
                           (npc.HasBakedBody ? $" as {npc.Build.Preset}" : " (greybox)"));
            }

            Debug.Log(
                $"[StPetersInhabitants] Placed {placed.Count} of {People.Count} islander(s) on their " +
                $"starting marks: {string.Join(" · ", report)}. Whether any of them WALKS is " +
                "StPetersRoutines' answer, a few passes later — this pass only stands them up.");

            return placed;
        }

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>
        /// The person's body in the world, best available first:
        ///
        /// <list type="number">
        ///   <item>a BAKED BODY (their <see cref="NpcDef.Build"/>) — an eight-facing iso character
        ///   breathing through its idle cycle, driven by <see cref="IsoCharacterSprite"/>, the same
        ///   component the player uses. They are anchored, so it never measures any motion and simply
        ///   idles; the day a routine walks them the walk cycle plays with no code change;</item>
        ///   <item>a static sprite at the conventional <c>Art/Characters/&lt;stem&gt;.png</c>;</item>
        ///   <item>the tinted greybox rectangle.</item>
        /// </list>
        ///
        /// <para>Sorting order 9 either way, so the five sit in the same layer they always did.
        /// <b>Scale stays 1 for a real body</b> — the sheets are PPU 32 with the pivot on ground
        /// contact, so the metre size is already right and the greybox's 1 × 2 stretch would make a
        /// nine-foot islander.</para>
        /// </summary>
        static GameObject MakeStandee(Person person, NpcDef npc, Sprite greyboxSquare)
        {
            var go = new GameObject(person.AssetName);
            go.transform.position = new Vector3(person.Position.x, person.Position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;   // pre-Play default only; the YSortSprite below OWNS the order

            NpcBodyDresser.Dress(go, sr, npc, person.ArtStem, greyboxSquare, person.GreyboxTint,
                                 person.HeadingDegrees);

            // An islander layers with the player by world Y like every other thing you can walk past.
            // Without this they held a FIXED order 9 — which read correctly only while the player's own
            // Y-sort resolved (the old band covered −7.5…+2.0 m, and the village stands at Y +8…+33), so
            // up in the village the player drew BEHIND every islander, even face to face.
            //
            // Added STATIC here, and turned DYNAMIC by StPetersRoutines for whoever gets a day to keep — a
            // walker has to re-sort as they go or they draw in front of the house they just walked behind,
            // and an islander who never moves should not pay for a per-frame dispatch (rule 7). The flip
            // lives there because that is the pass that knows which of them walks.
            go.AddComponent<YSortSprite>();
            return go;
        }

        static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>
        /// Wire an <see cref="Interactable"/> to its <see cref="NpcDef"/> (+ portrait) through the
        /// builder's persist-the-refs SerializedObject convention, so the references survive into the
        /// saved scene rather than being lost when the build finishes.
        /// </summary>
        static void ConfigureNpc(Interactable it, NpcDef npc, Sprite portrait)
        {
            var so = new SerializedObject(it);
            var npcProp = so.FindProperty("_npc");
            if (npcProp != null) npcProp.objectReferenceValue = npc;
            var portraitProp = so.FindProperty("_portrait");
            if (portraitProp != null) portraitProp.objectReferenceValue = portrait;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
