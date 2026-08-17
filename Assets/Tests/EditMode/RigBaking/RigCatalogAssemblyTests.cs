using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The rig catalog is no longer one dictionary — it is assembled at static init from a
    /// <c>[RigContribution]</c> method in each <c>RigCatalog.&lt;Kit&gt;.cs</c> partial. This fixture
    /// is what makes that split safe.
    ///
    /// <para><b>⭐ WHY IT EXISTS.</b> The old single initializer put every kit's registration in one
    /// block, so two art-kit PRs in flight always conflicted on its tail — and a CONFLICTING PR never
    /// starts CI, so the collision was found by hand every time (three in one day, 2026-08-12,
    /// #494/#495/#496). Splitting the file removes the conflict but introduces two failures that would
    /// otherwise be SILENT, and both are pinned here:</para>
    /// <list type="number">
    ///   <item><b>An entry lost or altered in the move.</b> <see cref="Baseline"/> is the exact
    ///     registration table <c>main</c> declared at 18b67d1c — key, script path, global name,
    ///     declared convention and prerequisites, all 33 of them. Nothing about a rig's identity is
    ///     checked by the C# compiler: a path typo, a dropped prerequisite or a flipped convention
    ///     compiles and bakes the wrong art. This is the "no behaviour change" proof that does not
    ///     need a bake to run.</item>
    ///   <item><b>Two kit files claiming one key.</b> A dictionary initializer given the same key
    ///     twice keeps the LAST one and says nothing — with the entries spread over twelve files that
    ///     would be a merge away. <see cref="NoTwoKitFilesRegisterTheSameKey"/> proves each key has
    ///     exactly one owner, and the assembler itself throws rather than picking.</item>
    /// </list>
    ///
    /// <para>⚠️ <b>ADDING A KIT?</b> Add its file, and add its row to <see cref="Baseline"/> below —
    /// the table is sorted by key, so the row goes at its sorted position, not at the tail. That is
    /// the whole point: two kits landing at once touch different lines of a sorted list instead of
    /// the same last line of a shared block.</para>
    /// </summary>
    public class RigCatalogAssemblyTests
    {
        /// <summary>One registration exactly as <c>main</c> declared it before the split.</summary>
        readonly struct Snapshot
        {
            public readonly string Key;
            public readonly string ScriptPath;
            public readonly string GlobalName;
            public readonly AzimuthConvention Convention;
            public readonly string[] Prerequisites;

            public Snapshot(string key, string scriptPath, string globalName,
                            AzimuthConvention convention, params string[] prerequisites)
            {
                Key = key;
                ScriptPath = scriptPath;
                GlobalName = globalName;
                Convention = convention;
                Prerequisites = prerequisites;
            }
        }

        /// <summary>
        /// The catalog as it stood at 18b67d1c, read out of the single dictionary this fixture's
        /// refactor replaced, PLUS every kit registered since — the table is a running ledger, not a
        /// frozen snapshot, and <see cref="TheCatalogHoldsExactlyTheKeysMainDeclared"/> says so in its
        /// own failure message ("if a NEW kit landed, add its row"). Later rows carry the drop that
        /// brought them.
        ///
        /// <para>Sorted by key; prerequisites are IN ORDER, because the order is the depth-first
        /// install order (<c>shopBuilding</c> → <c>shopInterior</c>, <c>shopfront</c>) and swapping it
        /// changes what a bake produces.</para>
        /// </summary>
        static readonly Snapshot[] Baseline =
        {
            new Snapshot("bobber", "docs/art/rigs/bobberRig.js",
                         "RodBobber", AzimuthConvention.Clockwise),
            new Snapshot("bucket", "docs/art/rigs/bucketRig.js",
                         "BucketIso", AzimuthConvention.CounterClockwise),
            new Snapshot("buoyIso", "docs/art/rigs/deck-loop-kit/Art/buoyIsoRig.js",
                         "BuoyIso", AzimuthConvention.Clockwise, "deckIsoSolid"),
            // Added by the camper iso kit (drop 2026-08-16, imported #552, baked here), not present
            // at 18b67d1c. Standalone — the kit's README is explicit that the rig carries its own
            // loft and needs no isoSolid.js. Convention MEASURED two ways rather than inherited: the
            // hitch projects 107 px (bantam) / 158 px (clipper) WEST of the pivot at cell 2, which
            // the rig's own order labels 'E', and the ground-plane bearing of +Y steps +45.000°/dir —
            // the same figure to three decimals as utilityIso and houseIso in one host. ⚠️ The DOOR
            // cannot answer that question on this rig: it is on the CURB side, 5.8 px (bantam) /
            // 2.6 px (clipper) off the pivot at a quarter turn, under BuildingRigAzimuthProbe's own
            // MinDoorOffsetPx. That probe would refuse, correctly — hence CamperRigAzimuthProbe.
            new Snapshot("camper", "docs/art/rigs/camper-iso-kit/camperIsoRig.js",
                         "CamperIso", AzimuthConvention.CounterClockwise),
            new Snapshot("catchKit", "docs/art/rigs/catchKit.js",
                         "CatchKit", AzimuthConvention.Clockwise),
            new Snapshot("character", "docs/art/rigs/characterIsoRig6.js",
                         "CharacterIso6", AzimuthConvention.Clockwise, "characterHead"),
            new Snapshot("characterEye", "docs/art/rigs/eyeIsoRig.js",
                         "EyeIso", AzimuthConvention.Clockwise),
            // Added by the rev-6.4 carry drop (2026-08-14), not present at 18b67d1c. The prerequisite
            // is the BODY and the order is load-bearing: this layer reads the body's camera basis and
            // anchors(), and loaded first it does not throw — every pin() silently returns null.
            // Convention is inherited from the body it annotates rather than measured; the table holds
            // no pixels of its own to measure.
            new Snapshot("characterHands", "docs/art/rigs/characterIsoRig6.hands.js",
                         "CharacterHands6", AzimuthConvention.Clockwise, "character"),
            new Snapshot("characterHead", "docs/art/rigs/headIsoRig3.js",
                         "HeadIso3", AzimuthConvention.Clockwise, "characterEye"),
            new Snapshot("crustacean", "docs/art/rigs/crustaceanRig.js",
                         "Crustacean", AzimuthConvention.Clockwise),
            new Snapshot("deckGear", "docs/art/rigs/deck-loop-kit/Art/deckGearRig.js",
                         "DeckGear", AzimuthConvention.Clockwise, "deckIsoSolid"),
            new Snapshot("deckIsoSolid", "docs/art/rigs/deck-loop-kit/Art/isoSolid.js",
                         "IsoSolid", AzimuthConvention.Clockwise),
            // Added by the dialogue bubble kit (#561), not present at 18b67d1c. NOT DIRECTIONAL —
            // flat screen-space stamps on the 32 px/m grid; the Clockwise convention is the
            // placeholder every non-directional entry carries (shellfish, catchKit) and nothing
            // probes it. It DRAWS (fillStyle/fillRect), so BubbleKitBaker installs a real canvas
            // shim before it loads; no prerequisites — a bare host with the shim renders every piece.
            new Snapshot("dialogueBubble", "docs/art/rigs/dialogue-bubble-kit/Art/dialogueBubbleRig.js",
                         "BubbleKit", AzimuthConvention.Clockwise),
            new Snapshot("fish", "docs/art/rigs/fishIsoRig.js",
                         "FishIso", AzimuthConvention.Clockwise),
            new Snapshot("fishTote", "docs/art/rigs/fishToteRig.js",
                         "FishTote", AzimuthConvention.Clockwise),
            new Snapshot("fishTray2", "docs/art/rigs/deck-loop-kit/Art/trayIsoRig.js",
                         "FishTray2", AzimuthConvention.Clockwise, "deckIsoSolid"),
            new Snapshot("fuel", "docs/art/rigs/fuel-storage-kit/fuelRig.js",
                         "FuelIso", AzimuthConvention.Clockwise, "deckIsoSolid"),
            new Snapshot("house", "docs/art/rigs/houseIsoRig.js",
                         "HouseIso", AzimuthConvention.CounterClockwise),
            new Snapshot("interior", "docs/art/rigs/interiorIsoRig.js",
                         "InteriorIso", AzimuthConvention.CounterClockwise),
            new Snapshot("interiorProp", "docs/art/rigs/interiorPropRig.js",
                         "PropIso", AzimuthConvention.CounterClockwise),
            new Snapshot("lobsterBoat", "docs/art/rigs/lobsterBoatIsoRig.js",
                         "LobsterBoatIso", AzimuthConvention.CounterClockwise),
            // Eighteen hulls off one generator (fleet rig pack). CCW re-measured against
            // lobsterBoat in one host — +45.000° per step at all 8 headings, un-squashed.
            new Snapshot("lobsterBoatVariants", "docs/art/rigs/lobsterBoatVariantsIsoRig.js",
                         "LobsterBoatVariantsIso", AzimuthConvention.CounterClockwise),
            new Snapshot("navBuoy", "docs/art/rigs/nav-buoy-kit/navBuoyRig.js",
                         "NavBuoy", AzimuthConvention.Clockwise, "deckIsoSolid"),
            // The player's notebook — the main UI surface (drop 2026-08-17, imported here).
            // TWO prerequisites, in install order, and both are load-bearing for different reasons:
            // dialogueBubble because this kit ships NO FACE OF ITS OWN (probeType asserts literal
            // object identity with BubbleKit.FONT), and deckIsoSolid because without it the rig
            // still loads and every piece of furniture still draws while NotebookIsoKit — the closed
            // book — silently never registers. A missing global that costs nothing today.
            new Snapshot("notebook", "docs/art/rigs/notebook-kit/Art/notebookRig3.js",
                         "NotebookKit", AzimuthConvention.Clockwise, "dialogueBubble", "deckIsoSolid"),
            new Snapshot("punt", "docs/art/rigs/puntIsoRig.js",
                         "PuntIso", AzimuthConvention.CounterClockwise),
            new Snapshot("rod", "docs/art/rigs/rodIsoRig.js",
                         "RodIso", AzimuthConvention.Clockwise),
            new Snapshot("shellfish", "docs/art/rigs/shellfishRig.js",
                         "Shellfish", AzimuthConvention.Clockwise),
            new Snapshot("shipyardIso", "docs/art/rigs/shipyard-iso-kit/shipyardIsoRig.js",
                         "ShipyardIso", AzimuthConvention.CounterClockwise),
            new Snapshot("shopBuilding", "docs/art/rigs/shop-building-kit/shopBuildingRig.js",
                         "ShopBuilding", AzimuthConvention.CounterClockwise, "shopInterior", "shopfront"),
            new Snapshot("shopInterior", "docs/art/rigs/shop-building-kit/shopInteriorRig.js",
                         "ShopInterior", AzimuthConvention.CounterClockwise),
            new Snapshot("shopfront", "docs/art/rigs/shop-building-kit/shopfrontRig.js",
                         "Shopfront", AzimuthConvention.CounterClockwise, "shopInterior"),
            new Snapshot("shoreFinds", "docs/art/rigs/iso-rig-pack/shoreline-finds-iso/shoreFindsRig.js",
                         "ShoreFinds", AzimuthConvention.CounterClockwise),
            // Added by the dialogue bubble kit (#561). ⚠️ The prerequisite is the SHIPPING character
            // rig, not the kit's harness copy — the kit's characterIsoRig6/headIsoRig3/eyeIsoRig
            // duplicates are byte-identical today and a test keeps them that way; depending on
            // "character" means the mouth probe adjudicates the rig the game actually runs, and it
            // drags characterHead/characterEye in through character's own declared prerequisites.
            // TalkCue draws its emote markers, so it wants the same canvas shim the bubble rig does.
            new Snapshot("talkCue", "docs/art/rigs/dialogue-bubble-kit/Art/talkCueRig.js",
                         "TalkCue", AzimuthConvention.Clockwise, "character"),
            new Snapshot("trap", "docs/art/rigs/deck-loop-kit/Art/trapIsoRig.js",
                         "TrapIso", AzimuthConvention.Clockwise, "deckIsoSolid"),
            new Snapshot("trapFauna", "docs/art/rigs/deck-loop-kit/Art/trapFaunaRig.js",
                         "TrapFauna", AzimuthConvention.Clockwise, "deckIsoSolid", "catchKit", "crustacean"),
            new Snapshot("utilityIso", "docs/art/rigs/iso-rig-pack/utility-iso/utilityIsoRig.js",
                         "UtilityIso", AzimuthConvention.CounterClockwise),
            new Snapshot("wharfBuilding", "docs/art/rigs/wharfBuildingRig.js",
                         "WharfBuilding", AzimuthConvention.CounterClockwise),
            new Snapshot("wharfDecor", "docs/art/rigs/iso-rig-pack/wharf-decor-iso/wharfDecorRig.js",
                         "WharfDecor", AzimuthConvention.CounterClockwise),
            new Snapshot("wharfIso", "docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js",
                         "WharfIso", AzimuthConvention.CounterClockwise),
        };

        // ---- the registration table ------------------------------------------------------------

        [Test]
        public void TheCatalogHoldsExactlyTheKeysMainDeclared()
        {
            var expected = Baseline.Select(s => s.Key).ToArray();
            var actual = RigCatalog.Entries.Keys.ToArray();

            var lost = expected.Except(actual, StringComparer.Ordinal).ToArray();
            Assert.That(lost, Is.Empty,
                $"The split DROPPED {lost.Length} rig(s): {string.Join(", ", lost)}. Every key here " +
                "was registered on main — a kit file was left out of the move, lost its " +
                "[RigContribution] attribute, or never compiled into the assembly.");

            var added = actual.Except(expected, StringComparer.Ordinal).ToArray();
            Assert.That(added, Is.Empty,
                $"The catalog registers {added.Length} key(s) this table does not know: " +
                $"{string.Join(", ", added)}. If a NEW kit landed, add its row to Baseline (sorted by " +
                "key). If it did not, a key was mistyped in the move and the old one is gone.");
        }

        [Test]
        public void EveryEntryCarriesTheValuesMainDeclared()
        {
            foreach (var want in Baseline)
            {
                var got = RigCatalog.Get(want.Key);   // throws on an unknown key

                Assert.That(got.ScriptPath, Is.EqualTo(want.ScriptPath),
                    $"'{want.Key}' points at a different rig source than it did on main. Nothing " +
                    "downstream checks this path against the key — it bakes whatever it loads.");
                Assert.That(got.GlobalName, Is.EqualTo(want.GlobalName),
                    $"'{want.Key}' installs a different global than it did on main.");
                Assert.That(got.DeclaredConvention, Is.EqualTo(want.Convention),
                    $"'{want.Key}' changed handedness. Every one of these was MEASURED from rendered " +
                    "pixels; flipping one mirrors all eight cells of every piece in the kit.");
                CollectionAssert.AreEqual(want.Prerequisites, got.Prerequisites,
                    $"'{want.Key}' declares different prerequisites, or declares them in a different " +
                    "order. Order is the depth-first install order, and a missing prerequisite mostly " +
                    "does not throw — it renders the wrong art.");
            }
        }

        [Test]
        public void EveryPrerequisiteResolvesToARegisteredKey()
        {
            foreach (var pair in RigCatalog.Entries)
            {
                foreach (string prerequisite in pair.Value.Prerequisites)
                {
                    Assert.That(RigCatalog.Entries.ContainsKey(prerequisite), Is.True,
                        $"'{pair.Key}' names '{prerequisite}' as a prerequisite, but no kit file " +
                        "registers that key. Prerequisites cross file boundaries by design (fuel and " +
                        "navBuoy both ride the deck loop's turntable), so a kit file removed or " +
                        "renamed on its own breaks a sibling — at bake time, not here.");
                }
            }
        }

        // ---- the assembly itself ---------------------------------------------------------------

        [Test]
        public void TheCatalogIsAssembledFromSeveralKitFiles()
        {
            var contributions = Contributions();

            Assert.That(contributions, Is.Not.Empty,
                "No [RigContribution] methods were found on RigCatalog at all. Either the reflection " +
                "below stopped matching the attribute, or the catalog was folded back into one file.");
            Assert.That(contributions.Count, Is.GreaterThan(1),
                $"Only {contributions.Count} contribution(s). The catalog is split per kit precisely " +
                "so two kit PRs cannot collide on one block; one file means the collision is back.");
        }

        [Test]
        public void NoTwoKitFilesRegisterTheSameKey()
        {
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);
            int registered = 0;

            foreach (var contribution in Contributions())
            {
                foreach (var pair in contribution.Value)
                {
                    registered++;
                    if (owner.TryGetValue(pair.Key, out string first))
                    {
                        Assert.Fail(
                            $"'{pair.Key}' is registered by BOTH {first}() and " +
                            $"{contribution.Key}(). A dictionary initializer would have kept the last " +
                            "one silently and baked one kit's art for the other.");
                    }

                    owner[pair.Key] = contribution.Key;
                }
            }

            Assert.That(registered, Is.EqualTo(RigCatalog.Entries.Count),
                $"The kit files register {registered} entries but the catalog holds " +
                $"{RigCatalog.Entries.Count}. A registration is being swallowed on the way in.");
        }

        [Test]
        public void TheAssembledCatalogDoesNotDependOnContributionOrder()
        {
            // Fold the same contributions in REVERSE — if any part of the result depended on the
            // order reflection happens to return methods in, this is where it shows.
            var reversed = new Dictionary<string, RigEntry>(StringComparer.Ordinal);
            var contributions = Contributions();
            for (int i = contributions.Count - 1; i >= 0; i--)
            {
                foreach (var pair in contributions[i].Value) reversed[pair.Key] = pair.Value;
            }

            CollectionAssert.AreEquivalent(RigCatalog.Entries.Keys, reversed.Keys,
                "Folding the kit files in the opposite order produced a different key set.");

            foreach (var pair in reversed)
            {
                var live = RigCatalog.Get(pair.Key);
                Assert.That(live.ScriptPath, Is.EqualTo(pair.Value.ScriptPath), pair.Key);
                Assert.That(live.GlobalName, Is.EqualTo(pair.Value.GlobalName), pair.Key);
                Assert.That(live.DeclaredConvention, Is.EqualTo(pair.Value.DeclaredConvention), pair.Key);
            }

            // And the catalog reads back in one fixed order regardless, which is what keeps the
            // "Known: …" list in RigCatalog.Get's exception the same on every machine.
            var keys = RigCatalog.Entries.Keys.ToArray();
            CollectionAssert.AreEqual(keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), keys,
                "Entries enumerates in an order that depends on how the kit files were folded.");
        }

        // ---- reflection over the kit files -------------------------------------------------------

        /// <summary>
        /// Every <c>[RigContribution]</c> method on <see cref="RigCatalog"/>, invoked, keyed by method
        /// name and sorted by it.
        ///
        /// <para>The methods and the attribute are private — deliberately, since the attribute is
        /// wiring and not API. Reading them by reflection is the only way to see what each FILE
        /// contributed rather than what the assembled catalog ended up with, and "what each file
        /// contributed" is exactly what the duplicate-key test needs. <c>CustomAttributeData</c> reads
        /// the metadata without instantiating the attribute, so a private attribute type is fine.</para>
        /// </summary>
        static List<KeyValuePair<string, IReadOnlyList<KeyValuePair<string, RigEntry>>>> Contributions()
        {
            var found = new List<KeyValuePair<string, IReadOnlyList<KeyValuePair<string, RigEntry>>>>();

            foreach (var method in typeof(RigCatalog).GetMethods(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                bool marked = CustomAttributeData.GetCustomAttributes(method)
                    .Any(d => d.AttributeType.Name == "RigContributionAttribute");
                if (!marked) continue;

                var entries = method.Invoke(null, null) as IEnumerable<KeyValuePair<string, RigEntry>>;
                Assert.That(entries, Is.Not.Null,
                    $"RigCatalog.{method.Name}() is marked [RigContribution] but did not return its " +
                    "entries.");

                found.Add(new KeyValuePair<string, IReadOnlyList<KeyValuePair<string, RigEntry>>>(
                    method.Name, entries.ToList()));
            }

            found.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return found;
        }
    }
}
