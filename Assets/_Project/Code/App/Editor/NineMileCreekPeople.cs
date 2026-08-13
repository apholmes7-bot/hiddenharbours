#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;               // NpcDef, Interactable
using HiddenHarbours.Art;                 // YSortSprite — the creek's two layer with the player by world Y

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE TWO PEOPLE AT THE CREEK</b> — the buyer who takes your catch, and the man with the used
    /// outboards. Nine Mile Creek is not a town and does not get a cast: §7.2 asks for a working creek,
    /// and a working creek is two people who are busy and one of whom will give you an honest price for
    /// a bucket of clams without making a fuss about it (<c>design/nine-mile-creek-wharf.md</c> §2).
    ///
    /// <para><b>ANCHORED, NOT SCHEDULED</b> — §7.1's rule, and <see cref="NpcDef"/>'s own. Fixed spots,
    /// no routines, nothing here reads the clock. Routines are M2.</para>
    ///
    /// <para><b>Nothing new is built.</b> An <see cref="NpcDef"/> + <see cref="DialogueDef"/> per person
    /// under <c>Data/NPCs</c>, placed onto an <see cref="Interactable"/> — the plumbing #354 already
    /// shipped for the island. This file is authoring and placement; the words are assets the owner can
    /// edit without opening C# (CLAUDE.md rule 2).</para>
    ///
    /// <para><b>THE DIALOGUE DRIVER USED TO BE A KNOWN SEAM. IT IS CLOSED.</b> A
    /// <c>WorldInteractor</c> needs a player to measure proximity from, and in a region scene the real
    /// player lives in the PERSISTENT core — a different scene, which Unity will not let anything here
    /// serialize a reference to. The driver is therefore wired to the DEV bootstrap's stand-in, which is
    /// what makes these two live the moment the owner presses Play in Nine Mile Creek; when a real core
    /// travels in and that stand-in is destroyed, the interactor falls through to Core's
    /// <c>GameServices.PlayerTransform</c> instead (the same relay the shops' rooms resolve through).
    /// The driver is built by <c>NineMileCreekBuilder.PlaceDialogueDriver</c> as a SCENE ROOT rather
    /// than inside the dev core, so it survives the arrival that destroys the stand-in — it did not, and
    /// these two were mute for every player who came by sea. The people, their words and their spots
    /// were real throughout; nobody could hear them.</para>
    /// </summary>
    public static class NineMileCreekPeople
    {
        /// <summary>The root both of them hang under.</summary>
        public const string RootName = "CreekPeople";

        const string DataNpcs    = "Assets/_Project/Data/NPCs";
        const string ArtChars    = "Assets/_Project/Art/Characters";
        const string ArtPortrait = "Assets/_Project/Art/Portraits";

        /// <summary>
        /// One anchored person: which asset speaks, where they stand, and one line on why there — the
        /// same shape the island's cast uses, for the same reason.
        /// </summary>
        public readonly struct Person
        {
            /// <summary>Asset stem under <c>Data/NPCs</c> — also the NpcDef this places.</summary>
            public readonly string AssetName;
            /// <summary>Art stem under <c>Art/Characters</c> / <c>Art/Portraits</c> (the Ginny/Ned convention).</summary>
            public readonly string ArtStem;
            public readonly Vector2 Position;
            public readonly Color GreyboxTint;
            public readonly string Reason;

            /// <summary>Which way they are turned, degrees (0 = N, CW) — used only when the person has
            /// a baked body, since a greybox rectangle has no front. 180 (South, toward the camera) is
            /// the default: a face is how you tell a person from scenery.</summary>
            public readonly float HeadingDegrees;

            public Person(string assetName, string artStem, Vector2 position, Color greyboxTint,
                          string reason, float headingDegrees = 180f)
            {
                AssetName = assetName;
                ArtStem = artStem;
                Position = position;
                GreyboxTint = greyboxTint;
                Reason = reason;
                HeadingDegrees = headingDegrees;
            }
        }

        /// <summary>
        /// How far the buyer stands OUT from his truck, toward the quay — close enough that he and his
        /// stall are one thing to walk up to. <see cref="HiddenHarbours.Economy.StallGate.DefaultRange"/>
        /// is 4 m from the STALL, so he must not stand further from it than that or the man and the till
        /// come apart.
        /// </summary>
        public const float ByHisTruckMetres = 1.5f;

        /// <summary>
        /// The two, in the order you meet them walking west off the planks.
        ///
        /// <para>Both spots are DERIVED from the site constants the builder places their stall at, not
        /// typed in beside them — so if the owner moves the buyer's truck the buyer moves with it. The
        /// island's cast learned that one the expensive way (#345).</para>
        /// </summary>
        public static IReadOnlyList<Person> People => new[]
        {
            new Person("WendellArsenault", "Wendell",
                       Toward(NineMileCreekBuilder.FishBuyerPos, NineMileCreekWharf.DeckFootprint().center,
                              ByHisTruckMetres),
                       new Color(0.66f, 0.58f, 0.48f),
                       "at his truck where the planks meet the yard — the first money in the game is a " +
                       "man on a wharf, not a market, and he is standing between you and everything else"),

            new Person("HectorBernard", "Hector",
                       Toward(NineMileCreekBuilder.DoryYardPos, NineMileCreekWharf.DeckFootprint().center,
                              OutboardStallMetres),
                       new Color(0.52f, 0.56f, 0.60f),
                       "down the yard beside the old dory, with three tired outboards on a barrel — the " +
                       "man who sells you the hull is the man who sells you what pushes her"),
        };

        /// <summary>How far the outboard man stands out from the yard, toward the water. Further than the
        /// buyer because he has a yard to stand in rather than a truck to stand at.</summary>
        public const float OutboardStallMetres = 2.5f;

        /// <summary>A spot <paramref name="metres"/> out from <paramref name="site"/> in the direction of
        /// <paramref name="target"/> — how a person ends up in front of their own counter rather than
        /// inside it.</summary>
        public static Vector2 Toward(Vector3 site, Vector2 target, float metres)
        {
            Vector2 from = new Vector2(site.x, site.y);
            Vector2 d = target - from;
            if (d.sqrMagnitude < 1e-6f) return from;
            return from + d.normalized * metres;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the two of them up and return their <see cref="Interactable"/>s so the builder can hand
        /// them to a <c>WorldInteractor</c>.
        ///
        /// <para>Null-tolerant: a person whose NpcDef has not imported is SKIPPED with a warning rather
        /// than placed mute, because a silent interactable that opens an empty panel is worse than an
        /// absence you can see in the log.</para>
        /// </summary>
        public static List<Interactable> Place(Sprite greyboxSquare)
        {
            var placed = new List<Interactable>();
            var root = new GameObject(RootName);
            var report = new List<string>();

            foreach (var person in People)
            {
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{person.AssetName}.asset");
                if (npc == null)
                {
                    Debug.LogWarning(
                        $"[NineMileCreekPeople] '{person.AssetName}' has no NpcDef at {DataNpcs} — the creek " +
                        $"is missing the one who stands {person.Reason}. Re-run after the Data/NPCs assets " +
                        "import rather than placing a mute standee.");
                    continue;
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
                $"[NineMileCreekPeople] Placed {placed.Count} of {People.Count} — anchored and unscheduled: " +
                $"{string.Join(" · ", report)}. Nobody here walks anywhere; routines are M2.");

            return placed;
        }

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>The person's body, through the shared <see cref="NpcBodyDresser"/>: their baked
        /// build if they have one (an eight-facing iso character breathing through its idle cycle),
        /// else the conventional static sprite, else the tinted greybox marker. Sorting order 9 either
        /// way, so the coast reads at one scale — and the island uses the same ladder, from the same
        /// code, so the two coasts cannot drift apart.</summary>
        static GameObject MakeStandee(Person person, NpcDef npc, Sprite greyboxSquare)
        {
            var go = new GameObject(person.AssetName);
            go.transform.position = new Vector3(person.Position.x, person.Position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;   // pre-Play default only; the YSortSprite below OWNS the order

            NpcBodyDresser.Dress(go, sr, npc, person.ArtStem, greyboxSquare, person.GreyboxTint,
                                 person.HeadingDegrees);

            // Layer with the player by world Y like every other thing you can walk past — the island's cast
            // gets the same treatment from the same ladder, so the two coasts cannot drift apart. Static:
            // routines are M2, nobody here walks, so it sorts once on enable and stands its dispatch down.
            go.AddComponent<YSortSprite>();
            return go;
        }

        static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>Wire an <see cref="Interactable"/> to its <see cref="NpcDef"/> (+ portrait) through
        /// the builder's persist-the-refs SerializedObject convention, so the references survive into the
        /// saved scene rather than being lost when the build finishes.</summary>
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
