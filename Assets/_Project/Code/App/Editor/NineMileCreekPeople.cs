#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;               // NpcDef, Interactable
using HiddenHarbours.Art;                 // YSortSprite — the creek's two layer with the player by world Y

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE THREE PEOPLE AT THE CREEK</b> — the buyer who takes your catch, the man with the used
    /// outboards, and the woman who keeps the store. §7.2 asks for a working creek, and a working creek
    /// is people who are busy and one of whom will give you an honest price for a bucket of clams without
    /// making a fuss about it (<c>design/nine-mile-creek-wharf.md</c> §2).
    ///
    /// <para><b>ANCHORED, NOT SCHEDULED</b> — §7.1's rule, and <see cref="NpcDef"/>'s own. Fixed spots,
    /// no routines, nothing here reads the clock, and the storekeeper is anchored with the other two by
    /// the owner's 2026-08-27 ruling rather than by default: <b>this region has no routine engine at
    /// all</b>. There is no NMC station table, no lane graph and no indoor stand-point — the
    /// <c>RoutineDef</c>/<c>RoutineStations</c> machinery is St Peters-only. Standing one up here is its
    /// own world-content lane, and it is not this one. So the creek's store has no hours: she is simply
    /// there, which is the shipped convention of this file and not a shortcut taken inside it.</para>
    ///
    /// <para>⚠ <b>The header used to say the creek "does not get a cast".</b> That was true when this
    /// was a two-person creek and <c>design/settlement-population.md</c> ruling 5 has since overturned
    /// it — the mainland has more residents than the island. Rewriting this region's cast to that ruling
    /// is the S6 roster slice; the storekeeper lands early because the wares book needs somebody to hold
    /// it out, not because that slice has started.</para>
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

        /// <summary>
        /// One anchored person: which asset speaks, where they stand, and one line on why there — the
        /// same shape the island's cast uses, for the same reason.
        /// </summary>
        public readonly struct Person
        {
            /// <summary>Asset stem under <c>Data/NPCs</c> — also the NpcDef this places.</summary>
            public readonly string AssetName;
            /// <summary>Art stem under <c>Art/Characters</c> (the Ginny/Ned convention).</summary>
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

            new Person("ClaudetteBoudreau", "Claudette",
                       Toward(NineMileCreekBuilder.ChandleryPos, TownRoadAt(NineMileCreekBuilder.ChandleryPos),
                              ShopStepMetres),
                       new Color(0.70f, 0.62f, 0.66f),
                       "on the step of the general store, between her own door and Route 19 — the town " +
                       "end of the walk up from the wharf, and the other end of the line the island's " +
                       "storekeeper has an opinion about",
                       headingDegrees: BearingFrom(NineMileCreekBuilder.ChandleryPos,
                                                   TownRoadAt(NineMileCreekBuilder.ChandleryPos))),
        };

        /// <summary>
        /// How far the storekeeper stands out from the store's own centre, toward the road.
        ///
        /// <para><b>MEASURED, not chosen.</b> The general store's <c>BoxCollider2D</c> in the shipped
        /// scene is 5 × 5.5 m centred on the lot, so its wall is 2.5 m out and she walks almost due east
        /// off it — anything shorter stands her inside her own building, where nobody can reach her and
        /// the bug looks like a broken <c>Interactable</c> rather than a number. This is a metre clear of
        /// that wall, well inside the interactor's 1.8 m radius from the step in front of her, and 25 m
        /// short of Route 19's <see cref="NineMileCreekMainland.RoadHalfWidth"/> corridor.</para>
        /// </summary>
        public const float ShopStepMetres = 3.5f;

        /// <summary>
        /// The point on the through-road (Route 19) that a town lot fronts onto — the road's own
        /// geometry, sampled, never a number typed beside the lot.
        ///
        /// <para>The town is "strung along the through-road", with the lots in two columns either side of
        /// it, so "which way does a shopfront face?" has one honest answer: the road. Deriving it means
        /// the storekeeper turns when the road is re-cut, which is #345's lesson applied to a facing
        /// rather than to a position.</para>
        /// </summary>
        public static Vector2 TownRoadAt(Vector3 lot)
        {
            Vector2 from = new Vector2(lot.x, lot.y);
            Vector2[] road = NineMileCreekMainland.ThroughRoad;
            Vector2 best = road[0];
            float bestSq = float.MaxValue;

            for (int i = 0; i < road.Length - 1; i++)
            {
                Vector2 a = road[i], b = road[i + 1];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(from - a, ab) / len2);
                Vector2 p = a + ab * t;
                float d = (p - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        /// <summary>Which way to turn to look from <paramref name="site"/> at <paramref name="target"/>,
        /// in the rig's own convention: degrees, 0 = North, clockwise. Same convention
        /// <c>StPetersRoutines.HeadingTo</c> uses, so the two coasts aim people the same way.</summary>
        public static float BearingFrom(Vector3 site, Vector2 target)
        {
            Vector2 d = target - new Vector2(site.x, site.y);
            if (d.sqrMagnitude < 1e-6f) return 180f;
            float deg = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;   // atan2(x, y) => 0 at +Y (north), CW
            return deg < 0f ? deg + 360f : deg;
        }

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
                ConfigureNpc(interactable, npc);

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


        /// <summary>Wire an <see cref="Interactable"/> to its <see cref="NpcDef"/> through the builder's
        /// persist-the-refs SerializedObject convention, so the reference survives into the saved scene
        /// rather than being lost when the build finishes.</summary>
        static void ConfigureNpc(Interactable it, NpcDef npc)
        {
            var so = new SerializedObject(it);
            var npcProp = so.FindProperty("_npc");
            if (npcProp != null) npcProp.objectReferenceValue = npc;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
