using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE REGISTER AT NINE MILE CREEK</b> — content validation over the actual
    /// <see cref="BoatOwnerDef"/> assets in <c>Data/</c>, in the <see cref="ContentValidationTests"/>
    /// tradition: these run over the shipped data rather than a fixture, so they keep catching authoring
    /// errors as the register grows.
    ///
    /// <para><b>The owner's ruling (2026-08-10) is what these hold to:</b> each moored boat has its own
    /// owner network, owners differ in success, and a more successful owner has a larger building. The
    /// first is an identity problem (nobody shares a mark, a berth or a lot); the second and third are one
    /// ordering.</para>
    /// </summary>
    public class BoatOwnerContentTests
    {
        /// <summary>
        /// ⭐ <b>THE CAP IS THE ART'S, NOT A PREFERENCE.</b> The deck-loop kit bakes exactly eight buoy
        /// paint schemes, and a scheme is an OWNERSHIP MARK — two fishers painting the same cannot be told
        /// apart on the water, which is the only job the mark has. So a region's authored register cannot
        /// exceed eight without a re-bake. Shared with the photograph tests, which size the shed row from
        /// it, so the two cannot disagree about how many fishers this wharf holds.
        /// </summary>
        public const int MaxOwners = 8;

        /// <summary>The eight the kit bakes, by the key each cell carries. ⚠ They are named after HULLS
        /// because that is what each scheme was drawn against; they are paint schemes, and an owner whose
        /// mark is <c>Tanker</c> does not keep a tanker (and could not — see the draught test).</summary>
        private static readonly string[] BakedSchemes =
        {
            "Dory", "Punt", "CapeIslander", "LobsterBoat",
            "SideDragger", "SternTrawler", "CoastalPacket", "Tanker",
        };

        private static List<BoatOwnerDef> Owners() => NineMileCreekMooredFleet.LoadOwners();

        /// <summary>How many berths the table <paramref name="moorage"/> names actually holds. Read off
        /// the region rather than restated, so growing either table grows this check with it.</summary>
        private static int BerthsAt(BoatMoorage moorage) => moorage == BoatMoorage.Float
            ? NineMileCreekWharf.FloatBerthCount
            : NineMileCreekMainland.BerthCount;

        private static string Where(BoatMoorage moorage) =>
            moorage == BoatMoorage.Float ? "the floating dock" : "the quay wall";

        [Test]
        public void TheCreekShipsARegister()
        {
            Assert.IsNotEmpty(Owners(),
                $"the slice must ship at least one BoatOwnerDef in {NineMileCreekMooredFleet.OwnersFolder} " +
                "— the owner's ruling is that every moored boat has an owner network behind it");
        }

        [Test]
        public void EveryOwnerHasABoat_AMark_ALotAndABerth()
        {
            foreach (var o in Owners())
            {
                Assert.IsNotEmpty(o.Id, $"{o.name}: an owner with no id cannot be referred to");
                Assert.IsNotEmpty(o.DisplayName, $"{o.Id}: nobody on a register is nameless");
                Assert.IsNotNull(o.Boat, $"{o.Id}: keeps no boat, so there is nothing to moor");
                Assert.IsNotNull(o.Boat.Visual,
                    $"{o.Id}: '{o.Boat.Id}' has no visual, so she cannot be drawn at her berth");
                Assert.IsNotEmpty(o.BuoyScheme, $"{o.Id}: has no mark, so their gear is anonymous");
                Assert.That(o.ShedFootprintMetres, Is.GreaterThan(0f),
                    $"{o.Id}: a shed that reserves no ground is not a building");
                Assert.That(o.LotIndex, Is.GreaterThanOrEqualTo(0), $"{o.Id}: negative lot");
                int berths = BerthsAt(o.Moorage);
                Assert.That(o.BerthIndex, Is.InRange(0, berths - 1),
                    $"{o.Id}: berth {o.BerthIndex} is not one of the {berths} at {Where(o.Moorage)}. " +
                    "⚠ The wall's table and the float's are different LENGTHS, so moving an owner " +
                    "between them is a Moorage edit AND a BerthIndex edit.");
                Assert.IsTrue(o.IsPresentable(), $"{o.Id}: cannot be presented");
            }
        }

        [Test]
        public void EveryIdIsWellFormedAndUnique()
        {
            var seen = new HashSet<string>();
            foreach (var o in Owners())
            {
                Assert.IsTrue(o.Id.StartsWith("owner."),
                    $"'{o.Id}' must be type.snake_case with the 'owner.' type (CLAUDE.md §5)");
                Assert.IsTrue(o.Id.ToLowerInvariant() == o.Id,
                    $"'{o.Id}' must be snake_case — ids are append-only and stable, so case matters once");
                Assert.IsTrue(seen.Add(o.Id), $"'{o.Id}' is used by two owner assets");
            }
        }

        // =============================================================================================
        //  IDENTITY — nobody shares what makes them recognisable
        // =============================================================================================

        [Test]
        public void NoTwoOwnersShareABuoyScheme()
        {
            var byScheme = new Dictionary<string, string>();
            foreach (var o in Owners())
            {
                Assert.IsFalse(byScheme.TryGetValue(o.BuoyScheme, out string other),
                    $"'{o.Id}' and '{other}' both paint their gear '{o.BuoyScheme}'. The scheme is the " +
                    "ownership MARK — two fishers marked the same cannot be told apart on the water, " +
                    "which is the only job it has.");
                byScheme[o.BuoyScheme] = o.Id;
            }
        }

        [Test]
        public void EverySchemeIsOneTheKitActuallyBakes()
        {
            foreach (var o in Owners())
                Assert.Contains(o.BuoyScheme, BakedSchemes,
                    $"'{o.Id}' paints their gear '{o.BuoyScheme}', which the deck-loop kit does not bake. " +
                    "A mark with no pixels is a mark nobody can read.");
        }

        [Test]
        public void TheRegisterFitsTheMarksTheKitBakes()
        {
            var owners = Owners();
            Assert.That(owners.Count, Is.LessThanOrEqualTo(MaxOwners),
                $"{owners.Count} owners are authored and the kit bakes {MaxOwners} buoy schemes. The " +
                "ninth fisher cannot be told apart from one of the first eight. This is the real cap on " +
                "the owner's ~16-boat vision and it is an ART ask (more schemes), not a data one.");
        }

        /// <summary>
        /// ⚠ <b>A BERTH IS A PLACE, AND A PLACE IS (MOORING, INDEX).</b> Keyed on the bare index this
        /// would call wall berth 4 and float berth 4 the same berth and refuse a boat that is thirty
        /// metres away from her supposed neighbour — and, worse, would have passed silently while the
        /// register only used one table.
        /// </summary>
        [Test]
        public void NoTwoOwnersShareABerthOrALot()
        {
            var berths = new Dictionary<(BoatMoorage, int), string>();
            var lots = new Dictionary<int, string>();

            foreach (var o in Owners())
            {
                var place = (o.Moorage, o.BerthIndex);
                Assert.IsFalse(berths.TryGetValue(place, out string b),
                    $"'{o.Id}' and '{b}' are both moored in berth {o.BerthIndex} at {Where(o.Moorage)}");
                berths[place] = o.Id;

                Assert.IsFalse(lots.TryGetValue(o.LotIndex, out string l),
                    $"'{o.Id}' and '{l}' both claim shed lot {o.LotIndex}");
                lots[o.LotIndex] = o.Id;
            }
        }

        /// <summary>The exclusion is a WALL berth: the dock zone is a distance test against the wall's
        /// own berth line, so float berth 0 is not the player's berth and never was.</summary>
        [Test]
        public void NobodyIsMooredInThePlayersBerth()
        {
            int player = NineMileCreekMooredFleet.PlayerBerthIndex();
            foreach (var o in Owners().Where(o => o.Moorage == BoatMoorage.QuayWall))
                Assert.AreNotEqual(player, o.BerthIndex,
                    $"'{o.Id}' is authored into wall berth {player}, which is where the player's own " +
                    "boat docks (derived from the region's dock zone). She would arrive on top of him.");
        }

        // =============================================================================================
        //  THE TWO MOORINGS (S3) — the photograph is small craft on the float, working boats on the wall
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE DEFAULT IS THE WALL, IN BOTH READINGS OF "UNSET".</b> The seven owners on this
        /// register predate <c>Moorage</c>, and the ones that still do not mention it in their YAML must
        /// read as wall-moored — Unity leaves an unmentioned field at whatever the C# constructor gave
        /// it, so the field initializer AND <c>default(BoatMoorage)</c> both have to be
        /// <see cref="BoatMoorage.QuayWall"/>. Get this wrong and a register edit nobody made moves the
        /// whole working fleet onto a 2.4 m pontoon.
        /// </summary>
        [Test]
        public void AnOwnerWhoSaysNothingAboutHerMooringLiesAtTheWall()
        {
            var fresh = ScriptableObject.CreateInstance<BoatOwnerDef>();
            try
            {
                Assert.AreEqual(BoatMoorage.QuayWall, fresh.Moorage,
                    "a newly constructed BoatOwnerDef must default to the quay wall — that is the value " +
                    "every owner asset written before this field existed will read back as");
                Assert.AreEqual(BoatMoorage.QuayWall, default(BoatMoorage),
                    "default(BoatMoorage) must also be the wall: renumbering the enum would silently " +
                    "re-moor anything deserialised without a field initializer");
                Assert.IsFalse(fresh.LiesAtTheFloat);
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        /// <summary>The photograph, as an assertion about the register: both moorings are used. A wharf
        /// with an empty float is the picture this slice exists to replace.</summary>
        [Test]
        public void BothMooringsAreActuallyUsed()
        {
            var owners = Owners();
            Assert.That(owners.Count(o => o.Moorage == BoatMoorage.Float), Is.GreaterThan(0),
                "nobody is authored at the float, so the finger in the middle of the bullpen lies empty");
            Assert.That(owners.Count(o => o.Moorage == BoatMoorage.QuayWall), Is.GreaterThan(0),
                "nobody is authored at the wall, so the tall quay faces stand over open water");
        }

        /// <summary>
        /// ⭐ <b>THE FLEET SORTS ITSELF, and nobody typed the list.</b> Whether a boat belongs at the
        /// float is a measurement — her half-beam against the standoff a boat lies off the finger — and
        /// this asserts the register agrees with it. A working hull authored onto the float fails HERE,
        /// by name, before the builder refuses her on the same number.
        ///
        /// <para>⚠ A sprite-only boat has no hull mesh and therefore no measured beam (Bernard's skiff is
        /// the standing example). She is exempt and the exemption is NAMED, the same way
        /// <c>NineMileCreekChannelTests</c> names her when it pins the widest-beam constant.</para>
        /// </summary>
        [Test]
        public void EveryBoatAtTheFloatIsSmallEnoughToLieAlongsideIt()
        {
            float widest = NineMileCreekWharf.WidestHalfBeamAFloatBerthCarries;

            foreach (var o in Owners().Where(o => o.Moorage == BoatMoorage.Float))
            {
                float half = NineMileCreekMooredFleet.HalfBeamOf(o);
                if (half <= 0f) continue;    // sprite-only: no beam to measure — see the note above
                Assert.That(half, Is.LessThanOrEqualTo(widest),
                    $"'{o.Id}' keeps '{o.Boat.Id}', {half * 2f:0.00} m in the beam, and is authored at " +
                    $"the float. A boat lies {widest:0.00} m off the finger's edge, so she would be " +
                    "drawn lying ON the dock. The float is for small craft; her place is the wall.");
            }
        }

        // =============================================================================================
        //  THE HARBOUR AND THE FLEET AGREE
        // =============================================================================================

        /// <summary>
        /// ⭐ The shoal IS the gate. Nine Mile Creek's basin bed is the ruled −1.6 m, so a hull deeper than
        /// that plus the tide's rise cannot lie at this wharf at all — and a moored fleet is exactly where
        /// that would show, because she is drawn floating at a berth she could not reach.
        /// </summary>
        [Test]
        public void EveryOwnersBoatCanActuallyFloatAtThisWharf()
        {
            float bed = NineMileCreekMainland.BasinBedElevation;
            float high = NineMileCreekMainland.SpringHighWater;
            float deepest = high - bed;   // the most water there ever is over the berth

            foreach (var o in Owners())
                Assert.That(o.Boat.DraughtMeters, Is.LessThan(deepest),
                    $"'{o.Id}' keeps '{o.Boat.Id}', which draws {o.Boat.DraughtMeters:0.0} m. This basin " +
                    $"has at most {deepest:0.0} m over the bed at spring high, so she could never lie " +
                    "here — and she would be drawn floating in a berth she cannot reach.");
        }

        /// <summary>
        /// <b>⚠ THE OTHER HALF OF THE GATE, AND THE ONE THAT BOUNDS THE LOBSTER SPREAD.</b> The basin
        /// bed above says whether a hull can LIE here; this says whether she can LEAVE. Nine Mile
        /// Creek's channel is cut for <see cref="NineMileCreekMainland.DeepestResidentDraughtMetres"/>
        /// — today the 1.40 m of Marie Gallant's Cape Islander, the deepest thing this creek was ever
        /// dredged for — and every station on the way out carries exactly that plus her keel clearance
        /// at spring low.
        ///
        /// <para><b>So a deeper boat is not a repaint, it is a dredging.</b> Berthing one moves the
        /// constant, which moves the thalweg, which re-bakes the committed seabed the region ships and
        /// re-opens the berth-deepening ruling that is still owed. That is a drop of its own and a
        /// call the owner makes — never a side effect of handing somebody a different boat.</para>
        ///
        /// <para>This is the bound that decided the 2026-08-20 lobster spread: eighteen lobster
        /// variants are committed, but the six OFFSHORE hulls draw 1.45 m and were refused on exactly
        /// this line. <see cref="NineMileCreekChannelTests"/> re-measures the constant off the register
        /// from the CHANNEL's end and would fail if it moved; this asserts the same fact from the
        /// FLEET's end, so the reason a variant is refused sits where the next person choosing one
        /// will look rather than only in a merged PR body.</para>
        /// </summary>
        [Test]
        public void NobodysBoatOutdrawsTheChannelTheCreekIsDredgedFor()
        {
            float dredgedFor = NineMileCreekMainland.DeepestResidentDraughtMetres;

            foreach (var o in Owners().Where(o => o.Boat != null))
                Assert.That(o.Boat.DraughtMeters, Is.LessThanOrEqualTo(dredgedFor),
                    $"'{o.Id}' keeps '{o.Boat.Id}', which draws {o.Boat.DraughtMeters:0.00} m, against " +
                    $"the {dredgedFor:0.00} m Nine Mile Creek's channel is cut for. She would lie at " +
                    "her berth and be shut in by the lane out of it. Deepening the cut for one boat " +
                    "re-bakes the region's seabed and moves the thalweg — a ruling and a drop of its " +
                    "own, not a side effect of a register edit.");
        }

        // =============================================================================================
        //  PROSPERITY — the owner's "a more successful owner has a LARGER building", as an ordering
        // =============================================================================================

        [Test]
        public void AMoreSuccessfulOwnerNeverHasASmallerShed()
        {
            var owners = Owners();
            foreach (var a in owners)
                foreach (var b in owners)
                {
                    if (a.Prosperity <= b.Prosperity) continue;
                    Assert.That(a.ShedFootprintMetres, Is.GreaterThanOrEqualTo(b.ShedFootprintMetres),
                        $"'{a.Id}' is {a.Prosperity} and '{b.Id}' is {b.Prosperity}, but their sheds are " +
                        $"{a.ShedFootprintMetres:0.#} m and {b.ShedFootprintMetres:0.#} m. The owner's " +
                        "ruling is that a more successful owner has a LARGER building — the tier is a " +
                        "label and the metres are the tunable, so this is the one thing holding them together.");
                }
        }

        [Test]
        public void TheRegisterActuallyDiffersInSuccess()
        {
            var tiers = Owners().Select(o => o.Prosperity).Distinct().ToList();
            Assert.That(tiers.Count, Is.GreaterThan(1),
                "the owner's ruling is that owners DIFFER in success. A register where everyone is the " +
                "same tier draws a wharf where every shed is the same size, which is the thing the " +
                "ruling exists to prevent.");
        }

        // =============================================================================================
        //  THE CREW ABOARD (ruled 2026-08-10: figures, no routines)
        // =============================================================================================

        [Test]
        public void NobodyPutsMoreAboardThanTheirBoatBerths()
        {
            foreach (var o in Owners())
                Assert.That(o.AboardCount(), Is.LessThanOrEqualTo(Mathf.Max(0, o.Boat.CrewSlots)),
                    $"'{o.Id}' has more people aboard '{o.Boat.Id}' than she has crew slots");
        }

        [Test]
        public void TheWharfIsNotDeserted()
        {
            var owners = Owners();
            int crewed = owners.Count(o => o.AboardCount() > 0);
            Assert.That(crewed, Is.GreaterThan(0),
                "the owner asked for NPC workers/captains aboard the moored fleet. Every boat empty is " +
                "the picture that ruling was meant to replace.");
        }

        [Test]
        public void EveryAssetLivesInTheRegisterFolderOnePerFile()
        {
            foreach (var o in Owners())
            {
                string path = AssetDatabase.GetAssetPath(o);
                Assert.IsTrue(path.StartsWith(NineMileCreekMooredFleet.OwnersFolder),
                    $"'{o.Id}' is at {path}, outside {NineMileCreekMooredFleet.OwnersFolder}");
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<BoatOwnerDef>().Count(),
                    Is.EqualTo(1),
                    $"{path} holds more than one owner — content is one entity per file (rule 2)");
            }
        }
    }
}
