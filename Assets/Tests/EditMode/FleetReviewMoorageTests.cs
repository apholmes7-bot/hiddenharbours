using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The review anchorage — twenty-three hulls the owner asked to be able to SEE.</b>
    ///
    /// <para><b>What can go wrong here is not subtle, it is just invisible until somebody sails out.</b>
    /// A berth on land, two boats in the same water, a hull whose art never baked, or the roster
    /// quietly covering twenty-one of twenty-three: every one of those builds a scene that looks
    /// finished. So the layout is re-derived here against the region's OWN terrain rather than trusted
    /// as authored — this fixture samples the same <see cref="MainlandTidalTerrain"/> the region is
    /// built from, at every position in the table.</para>
    ///
    /// <para>CPU-only apart from the AssetDatabase, so CI adjudicates all of it.</para>
    /// </summary>
    public class FleetReviewMoorageTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_MoorageTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // ---- 1. the roster is the whole fleet pack, and nothing else ------------------------------

        /// <summary>
        /// <b>Every hull the fleet pack added is at anchor, and every hull at anchor is one of them.</b>
        ///
        /// <para>Derived from <see cref="HullMeshFleet"/> rather than counted to 23, because a count is
        /// exactly the guard that survives a hull being added and never noticed. The eleven baked
        /// before the pack are deliberately NOT here — they are already in the game.</para>
        /// </summary>
        [Test]
        public void TheAnchorage_IsExactlyTheFleetPacksHulls()
        {
            // Read from the fleet catalog, which is a different source from the authored layout —
            // see FleetReviewMoorage.FleetPackVisualIds for why that separation is the whole check.
            var packVisualIds = new HashSet<string>(FleetReviewMoorage.FleetPackVisualIds());
            Assert.AreEqual(23, packVisualIds.Count,
                "The fleet pack is 23 hulls (18 lobster + 2 zodiac + the Mk2 + 2 sport fishers). A " +
                "different number means the catalog moved and this anchorage has not been told.");

            var moored = FleetReviewMoorage.Anchorage.Select(b => b.VisualId).ToList();

            CollectionAssert.AllItemsAreUnique(moored, "a hull is anchored twice");
            CollectionAssert.AreEquivalent(packVisualIds, moored,
                "The anchorage is no longer exactly the hulls the 2026-08-12 fleet pack added. A hull " +
                "baked and not anchored is a boat the owner cannot see, which is the whole point of " +
                "this arc; a hull anchored and not baked draws nothing.");
        }

        [Test]
        public void EveryAnchoredHull_HasCommittedArtThatCanActuallySkin()
        {
            foreach (var berth in FleetReviewMoorage.Anchorage)
            {
                BoatVisualDef visual = FleetReviewMoorage.Resolve(berth);
                Assert.IsNotNull(visual, $"{berth.Label}: no BoatVisualDef with id '{berth.VisualId}'");
                Assert.IsNotNull(visual.HullMesh,
                    $"{berth.Label}: her visual carries no HullMesh, and she has no sprite compass " +
                    "either — she would anchor as an empty patch of water.");
                Assert.IsTrue(visual.HullMesh.IsUsable(),
                    $"{berth.Label}: her committed HullMeshDef is not usable, so the skinner will " +
                    "refuse her at runtime and the berth draws empty.");
            }
        }

        // ---- 2. the layout is on water, and the water is deep enough ------------------------------

        /// <summary>
        /// <b>Every hull is afloat</b> — re-sampled from the region's own terrain, not trusted.
        ///
        /// <para>This is the assert that would have caught the layout being written against a guess.
        /// The measured water here is a flat −6.00 m shelf east of the wharf basin; the basin itself is
        /// −1.60 m and there is LAND at x 120–160 / y 35–40, so "somewhere off the wharf" is not
        /// automatically wet.</para>
        /// </summary>
        [Test]
        public void EveryHull_LiesInWaterDeepEnoughToFloatHer()
        {
            var report = new StringBuilder();
            var aground = new List<string>();

            foreach (var berth in FleetReviewMoorage.Anchorage)
            {
                float seabed = _terrain.ElevationAt(berth.At);
                BoatVisualDef visual = FleetReviewMoorage.Resolve(berth);
                float draft = visual != null && visual.HullMesh != null
                    ? visual.HullMesh.RestingDraftMeters
                    : 0f;

                // Seabed elevation is negative below the datum; the hull's keel sits `draft` below it.
                float clearance = -seabed - draft;
                report.AppendLine($"  {berth.Label} at {berth.At}: seabed {seabed:0.00} m, " +
                                  $"draft {draft:0.00} m, clearance {clearance:0.00} m");

                if (clearance < FleetReviewMoorage.MinClearanceMetres)
                    aground.Add($"{berth.Label} ({clearance:0.00} m under her keel)");
            }

            CollectionAssert.IsEmpty(aground,
                $"Hull(s) anchored where the water cannot float them:\n{report}" +
                $"Each needs {FleetReviewMoorage.MinClearanceMetres:0.0} m under the keel — the tide " +
                "swings ±2.2 m at springs and a boat sitting on the bottom reads as a bug, not a boat.");
        }

        /// <summary>
        /// <b>No two hulls occupy the same water.</b> Every hull lies bow-east, so her LENGTH runs along
        /// x and her beam across y — which means the spacing rule is not symmetric and cannot be one
        /// radius.
        /// </summary>
        [Test]
        public void NoTwoHulls_Overlap()
        {
            var report = new StringBuilder();
            var fouled = new List<string>();
            var all = FleetReviewMoorage.Anchorage;

            for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
            {
                var a = all[i];
                var b = all[j];
                float dx = Mathf.Abs(a.At.x - b.At.x);
                float dy = Mathf.Abs(a.At.y - b.At.y);

                // Clear if EITHER axis separates them: along-length (x) needs half of each LOA plus a
                // berth's grace; across (y) needs only beam, and no hull in this pack is beamier than 6 m.
                float needAlong = (a.LoaMetres + b.LoaMetres) * 0.5f + 4f;
                const float needAcross = 8f;

                if (dx >= needAlong || dy >= needAcross) continue;

                fouled.Add($"{a.Label} and {b.Label}");
                report.AppendLine($"  {a.Label} {a.At} vs {b.Label} {b.At}: " +
                                  $"dx {dx:0.0} (needs {needAlong:0.0}), dy {dy:0.0} (needs {needAcross:0.0})");
            }

            CollectionAssert.IsEmpty(fouled,
                $"Hull(s) anchored on top of each other:\n{report}");
        }

        /// <summary>
        /// <b>The anchorage keeps out of the working fleet's basin.</b> The seven owned boats lie at
        /// authored berths along the wall and this change must not crowd them — two fleets in one basin
        /// is how a review display becomes a regression to region content.
        /// </summary>
        [Test]
        public void TheAnchorage_IsClearOfTheWorkingBerths()
        {
            var tooClose = new List<string>();

            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
            {
                Vector2 berth = NineMileCreekMainland.BerthPos(i);
                foreach (var review in FleetReviewMoorage.Anchorage)
                {
                    float d = Vector2.Distance(berth, review.At);
                    if (d < 30f) tooClose.Add($"{review.Label} is {d:0} m from working berth {i}");
                }
            }

            CollectionAssert.IsEmpty(tooClose,
                "The review anchorage crowds the working wharf: " + string.Join("; ", tooClose));
        }

        // ---- 3. the thing that makes the whole display worth building -----------------------------

        /// <summary>
        /// <b>The hulls lie BROADSIDE, and the sport fishers get the clearest look.</b>
        ///
        /// <para>The owner owes a ruling on the two battlewagons' waterlines (0.67 m and 1.13 m are an
        /// authored judgment call, not a derivation), and he can only give it if he can SEE the boot
        /// top along the whole hull. Bow-on would hide exactly that. This pins the heading, and that
        /// the two of them are the nearest hulls to the wharf he sails from.</para>
        /// </summary>
        [Test]
        public void TheSportFishers_AreBroadsideAndNearestTheWharf()
        {
            Assert.AreEqual(90f, FleetReviewMoorage.HeadingDegrees, 1e-4f,
                "Bow east is what puts a hull's waterline across the screen instead of end-on.");

            var wharf = NineMileCreekMainland.BerthPos(0);
            var byDistance = FleetReviewMoorage.Anchorage
                .OrderBy(b => Vector2.Distance(b.At, wharf))
                .ToList();

            var nearestTwo = byDistance.Take(2).Select(b => b.VisualId).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "visual.sport_fisher_skybridge_iso", "visual.sport_fisher_convertible_iso" },
                nearestTwo,
                "The two hulls whose waterline the owner owes a ruling on are no longer the first he " +
                "reaches. That is the one placement decision in this table with a REASON behind it — " +
                "if the layout changed for a better one, say so and move this assert deliberately.");
        }

        /// <summary>
        /// Every anchored hull is mesh-only art, and the anchorage MECHANISM is display-only.
        ///
        /// <para><b>This used to assert that no <c>BoatHullDef</c> anywhere in the project referenced an
        /// anchored visual, and that half has been overtaken by a ruling.</b> It was written under the
        /// owner's 2026-08-14 call — every model visible, moored, NOT pilotable — and read the ABSENCE of
        /// hull defs as the proof of it. On 2026-08-15 he ruled the pack dev-picker sailable (#545), so all
        /// 23 hulls now have one and the old scan would have failed on the ruling itself rather than on a
        /// defect. Its own message asked for exactly that case to be met by reconsidering the anchorage
        /// rather than letting it drift into the next phase; this is that reconsideration, and the
        /// anchorage stays — it is where the owner rules on the sport fishers' waterlines.</para>
        ///
        /// <para><b>The claim worth keeping is the narrower one, and it is also the stronger one.</b> "No
        /// hull def exists" was a fact about the whole project, and the project has moved. "This anchorage
        /// cannot make a boat pilotable" is a fact about this anchorage, it is what actually keeps a review
        /// display from quietly becoming the fleet roster, and no ruling about the picker touches it. So
        /// the guard now reads the <see cref="FleetReviewMoorage.Berth"/> TYPE: a berth may name a picture
        /// and a place to put it, and may carry no route to a hull def or an owner. Reading the type rather
        /// than the instances matters — a table that merely happens to leave such a field null would
        /// satisfy an instance check and still have opened the door.</para>
        /// </summary>
        [Test]
        public void NothingHere_IsPilotableOrPurchasable()
        {
            foreach (var berth in FleetReviewMoorage.Anchorage)
            {
                BoatVisualDef visual = FleetReviewMoorage.Resolve(berth);
                Assert.IsNotNull(visual, berth.Label);
                Assert.IsFalse(visual.HasFullCompass(),
                    $"{berth.Label}: a full sprite compass here would mean she is drawn from sheets " +
                    "rather than from the mesh this arc baked.");
            }

            // A BERTH IS A PICTURE AND A PLACE. Read off the TYPE, so this cannot be satisfied by a
            // table that merely happens to leave a field null.
            var berthFields = typeof(FleetReviewMoorage.Berth)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotEmpty(berthFields,
                "reflection found no fields on Berth — the type moved, and this guard is now inert");

            var routes = berthFields
                .Where(f => typeof(BoatHullDef).IsAssignableFrom(f.FieldType)
                         || typeof(BoatOwnerDef).IsAssignableFrom(f.FieldType))
                .Select(f => $"{f.FieldType.Name} {f.Name}")
                .ToList();

            CollectionAssert.IsEmpty(routes,
                "A berth has grown a route to a hull def or an owner: " + string.Join(", ", routes) +
                ". This anchorage is a DISPLAY — he sails out and looks at it. The moment a berth can name " +
                "the boat you drive or the person who owns her, it has stopped being a display and become " +
                "the fleet roster, which is M2's job (rule 8). Hulls being sailable ELSEWHERE is expected " +
                "since the 2026-08-15 ruling; hulls being sailable FROM HERE is what this guards.");

            // …and the one thing a berth resolves is ART. Pinned separately so a Resolve that starts
            // handing back hull defs cannot slip past a field check that only sees the struct.
            Assert.AreEqual(typeof(BoatVisualDef),
                typeof(FleetReviewMoorage).GetMethod(nameof(FleetReviewMoorage.Resolve)).ReturnType,
                "Resolve hands back a PICTURE. If it ever returns a hull def, the anchorage has become " +
                "the fleet by another route and the field check above would not have seen it.");
        }
    }
}
