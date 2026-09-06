using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Does the register's paint actually reach the water?</b>
    ///
    /// <para>Two halves, and this fixture is the DATA half: who wears what, and whether that
    /// assignment could ever be drawn. <b>Scene-wired is not builder-wired</b> — a moored boat
    /// wearing navy in a banked scene proves nothing about the next re-build, because
    /// <c>NineMileCreekMooredFleet</c> deliberately does not draw hulls; it places boats, and
    /// <c>MooredBoat</c> skins them on wake so the committed scene never bakes the sprite fallback.
    /// The wake half therefore cannot be judged here and lives in
    /// <c>NineMileCreekFleetPaintPlayTests</c>; see the note at the foot of this file.</para>
    /// </summary>
    public class NineMileCreekFleetPaintTests
    {
        const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";

        static BoatOwnerDef[] Owners() => AssetDatabase
            .FindAssets($"t:{nameof(BoatOwnerDef)}", new[] { OwnersFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BoatOwnerDef>)
            .Where(o => o != null)
            .OrderBy(o => o.Id)
            .ToArray();

        /// <summary>Owners whose boat is drawn from a hull mesh that HAS baked schemes — the only
        /// ones this kit can paint. Derived from the assets, never listed here: the day the art
        /// director paints another hull, these tests cover her without an edit.</summary>
        static BoatOwnerDef[] PaintableOwners() => Owners()
            .Where(o => o.IsPresentable() && o.Boat.Visual.HullMesh != null)
            .Where(o => SchemesFor(o.Boat.Visual.HullMesh).Length > 0)
            .ToArray();

        static HullPaintSchemeDef[] AllSchemes() => AssetDatabase
            .FindAssets($"t:{nameof(HullPaintSchemeDef)}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>)
            .Where(s => s != null)
            .ToArray();

        static HullPaintSchemeDef[] SchemesFor(HullMeshDef hull) =>
            AllSchemes().Where(s => s.IsUsableFor(hull)).ToArray();

        // ---- the register ------------------------------------------------------------------------

        [Test]
        public void EveryOwnerWhoseHullCanBePaintedIsPainted()
        {
            var paintable = PaintableOwners();
            Assert.IsNotEmpty(paintable,
                "No owner keeps a hull with baked paint schemes. Either the schemes are unbaked " +
                "(Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake hull PAINT SCHEMES…) or the register changed.");

            foreach (var o in paintable)
                Assert.IsNotNull(o.HullPaint,
                    $"'{o.Id}' keeps '{o.Boat.Id}', whose hull HAS paint schemes, but wears none — so " +
                    "she lies at the wharf in the same white gelcoat as her neighbour. Assign a " +
                    "HullPaintSchemeDef, or say in the PR why this owner is deliberately unpainted.");
        }

        [Test]
        public void NoTwoOwnersInTheRegisterShareAHullScheme()
        {
            var seen = new Dictionary<string, string>();
            foreach (var o in Owners().Where(x => x.HullPaint != null))
            {
                Assert.IsFalse(seen.TryGetValue(o.HullPaint.Id, out string other),
                    $"'{o.Id}' and '{other}' both keep a '{o.HullPaint.Id}' hull. Paint is not the " +
                    "ownership MARK — that is the buoy scheme, and a real harbour does hold two navy " +
                    "boats — but on a seven-berth wharf where every hull is in frame at once, two the " +
                    "same reads as a bug rather than as a coincidence. Kept distinct deliberately.");
                seen[o.HullPaint.Id] = o.Id;
            }
        }

        /// <summary>
        /// ⚠️ <see cref="NoTwoOwnersInTheRegisterShareAHullScheme"/> compares ASSET ids, and those are
        /// hull-qualified — so it cannot see two owners in the same COLOUR on two different hulls.
        /// That hole opened the moment the register held more than one painted hull, and it is a real
        /// one: <c>paint.punt_squall_grey</c> and a hypothetical <c>paint.console_squall_grey</c> are
        /// two different tables that read as the same boat from the wharf, which is the whole thing
        /// the sibling test is trying to prevent.
        ///
        /// <para><see cref="HullPaintSchemeDef.RigPaintId"/> is the rig's own name for the colourway
        /// and is deliberately shared across hulls — the console skiff's first six schemes are the
        /// punt's "value for value, so a harbour of mixed boats still reads as one fleet". That makes
        /// it exactly the right key for this claim, and it is why #497 kept the field.</para>
        /// </summary>
        [Test]
        public void NoTwoOwnersWearTheSameColourway()
        {
            var seen = new Dictionary<string, string>();
            foreach (var o in Owners().Where(x => x.HullPaint != null))
            {
                string colour = o.HullPaint.RigPaintId;
                if (string.IsNullOrEmpty(colour)) continue;
                Assert.IsFalse(seen.TryGetValue(colour, out string other),
                    $"'{o.Id}' ({o.HullPaint.Id}) and '{other}' both wear the rig colourway " +
                    $"'{colour}'. Different hulls, different tables — but the same paint, and every " +
                    "berth is in one frame. Pick another; they are one-line data edits.");
                seen[colour] = o.Id;
            }
        }

        /// <summary>
        /// Paint is no longer a one-hull feature, and this says so in data. Before the small-craft
        /// drop every painted boat at the wharf was a lobster boat off one mesh, so "the register
        /// wears its colours" was true of four berths and silently false of three.
        ///
        /// <para><b>SIX hulls now, and four of them are lobster boats that used to be one.</b> Until
        /// the spread, Arsenault, Campbell, Doiron and MacDonald all kept the same
        /// <c>boat.lobster_boat</c>: four berths of one mesh in four colours, which reads down the
        /// north wall as a repeating texture rather than as four working boats. They now keep four
        /// different variants off the fleet rig pack — see
        /// <see cref="TheLobsterOwnersEachKeepADifferentLobsterBoat"/>, which owns that claim; this
        /// test only records what the painted SET became.</para>
        ///
        /// <para><b>⭐ The console skiff's nine schemes stopped being an unspent bake on 2026-09-04.</b>
        /// This note used to read "the one hull that is still missing is not an art gap" — the schemes
        /// were baked and proven and nobody at Nine Mile Creek owned a boat drawn from
        /// <c>hullmesh.console_iso</c> — and it named the fix as a world-content call rather than an
        /// art one. <b>Alma Boudreau is that call taken:</b> the register's eighth name keeps
        /// <c>boat.console_skiff</c> on the float fingers in <c>paint.console_cranberry</c>, so the
        /// seventh mesh here is a hull that was drawn but never owned.</para>
        ///
        /// <para><b>⚠ One berth stays plain, and the reason CHANGED on 2026-09-06.</b> Celeste Bernard
        /// used to keep the hand-drawn skiff (retired 2026-09-06 — see Core RetiredContentIds), a
        /// SPRITE-only visual with no hull mesh at all: paint exists only on the mesh path, so it could
        /// never reach her whatever anyone drew.
        /// The owner retired that hull (Core RetiredContentIds) and she now keeps
        /// <c>boat.dory_outboard</c>, which IS a mesh. So her berth is one bake away rather than a
        /// world-content call: <c>doryIsoRig.js</c> has no SCHEMES axis, so zero schemes are baked for
        /// <c>hullmesh.dory_iso</c>. That is the last unpainted keeper at this wharf.</para>
        ///
        /// <para><b>⚠️ It pins the SET, not a floor, for the reason its sibling below spells out.</b>
        /// A <c>GreaterOrEqual(2)</c> would have gone on passing when the Cape Islander gained her
        /// axis and Marie Gallant put paint on — i.e. it would have survived the very change it
        /// exists to notice. The set moves the moment a hull gains or loses a painted keeper.</para>
        ///
        /// <para>⚠️ Ordered ORDINALLY rather than by the current culture: the pin is a literal array,
        /// and with seven ids sharing long prefixes the sort is load-bearing enough that it should not
        /// depend on which machine ran the suite.</para>
        /// </summary>
        [Test]
        public void ThePaintedRegisterSpansMoreThanOneHull()
        {
            var hulls = Owners()
                .Where(o => o.HullPaint != null && o.IsPresentable() && o.Boat.Visual.HullMesh != null)
                .Select(o => o.Boat.Visual.HullMesh.Id)
                .Distinct()
                .OrderBy(s => s, System.StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "hullmesh.cape_islander_iso",
                    "hullmesh.console_iso",
                    "hullmesh.lobster_inshore_hardtop_newfoundland_iso",
                    "hullmesh.lobster_inshore_open_northumberland_iso",
                    "hullmesh.lobster_standard_hardtop_northumberland_iso",
                    "hullmesh.lobster_standard_open_fundy_iso",
                    "hullmesh.punt_iso",
                },
                hulls,
                "The set of hull meshes wearing paint at Nine Mile Creek has moved — found " +
                $"[{string.Join(", ", hulls)}]. Four lobster variants, the punt, the Cape Islander " +
                "and the console skiff all have owners and all have paint axes; if one has stopped " +
                "being drawn painted, a bake or an assignment was lost. If an owner was moved onto " +
                "another variant that is " +
                "a one-line edit here — but check she still has a table baked for the hull she moved to.");
        }

        /// <summary>
        /// <b>Four owners, four different lobster boats.</b> Four of the names on this register
        /// kept the same <c>boat.lobster_boat</c> until the spread, so the money shot down the north
        /// wall was one hull drawn four times. The fleet rig pack ships eighteen lobster variants;
        /// this asserts the register actually spends them.
        ///
        /// <para><b>The set is DERIVED, never listed.</b> "A lobster owner" is anyone whose hull mesh
        /// id sits in the <c>hullmesh.lobster_</c> family, so the day an eighth fisher joins the wharf
        /// with a lobster boat she is covered here with no edit. What IS pinned is the COUNT — a bare
        /// distinctness check would go on passing if three of the four collapsed back onto one hull
        /// and the others left the register, which is the exact shape of the bug this test exists to
        /// catch.</para>
        ///
        /// <para>Each hull must also have a paint table of its OWN. Schemes are baked per hull id
        /// (<see cref="HullPaintSchemeDef.HullMeshId"/>) and matched to materials BY INDEX, so an
        /// owner moved onto a variant whose family was never baked is not drawn in the wrong colours
        /// — <c>IsUsableFor</c> refuses the scheme and she falls back to the def's own ramps, i.e.
        /// she silently stops being painted at all. That failure is invisible in a screenshot of one
        /// boat and obvious in a row of four, which is why it is asserted here.</para>
        /// </summary>
        [Test]
        public void TheLobsterOwnersEachKeepADifferentLobsterBoat()
        {
            const string Family = "hullmesh.lobster_";

            var lobster = Owners()
                .Where(o => o.IsPresentable() && o.Boat.Visual.HullMesh != null)
                .Where(o => o.Boat.Visual.HullMesh.Id.StartsWith(Family, System.StringComparison.Ordinal))
                .ToArray();

            Assert.AreEqual(4, lobster.Length,
                $"Nine Mile Creek's register holds FOUR lobster owners; found {lobster.Length}: " +
                $"[{string.Join(", ", lobster.Select(o => o.Id))}]. A fisher joining or leaving this " +
                "wharf is a register change worth stating in the PR — the count is pinned so that a " +
                "collapse back onto one hull cannot hide behind a shrinking set.");

            var byHull = lobster
                .GroupBy(o => o.Boat.Visual.HullMesh.Id)
                .OrderBy(g => g.Key, System.StringComparer.Ordinal)
                .ToArray();

            foreach (var g in byHull)
                Assert.AreEqual(1, g.Count(),
                    $"{string.Join(" and ", g.Select(o => o.DisplayName))} all keep '{g.Key}'. Boats " +
                    "off one mesh is what this wall looked like before the spread: same sheer, same " +
                    "house, same length, four times in a row, and paint cannot rescue it because the " +
                    "silhouette is the thing that repeats. Eighteen lobster variants are committed — " +
                    "give her one of her own.");

            Assert.AreEqual(4, byHull.Length,
                $"Expected four DISTINCT lobster hulls, found {byHull.Length}: " +
                $"[{string.Join(", ", byHull.Select(g => g.Key))}].");

            foreach (var g in byHull)
            {
                HullMeshDef hull = g.First().Boat.Visual.HullMesh;
                Assert.IsNotEmpty(SchemesFor(hull),
                    $"'{hull.Id}' has no baked paint scheme, so {g.First().DisplayName} cannot be " +
                    "painted at all: a table baked against another hull is refused by IsUsableFor and " +
                    "she falls back to her def's own ramps. Bake her family (Hidden Harbours ▸ Dev ▸ " +
                    "3D Hulls ▸ Bake hull PAINT SCHEMES…) or move her onto a variant that has one.");
            }
        }

        /// <summary>A scheme is a table of ramps matched to a hull's materials BY INDEX, so one baked
        /// against another hull recolours the wrong things. The renderer refuses those — this catches
        /// them in the data, where the fix is cheap.</summary>
        [Test]
        public void NoOwnerCarriesASchemeBakedForADifferentHull()
        {
            foreach (var o in Owners().Where(x => x.HullPaint != null))
            {
                Assert.IsTrue(o.IsPresentable(), $"'{o.Id}' has paint but no presentable boat.");
                var hull = o.Boat.Visual.HullMesh;
                Assert.IsNotNull(hull,
                    $"'{o.Id}' wears '{o.HullPaint.Id}' but '{o.Boat.Visual.Id}' has no hull mesh — " +
                    "paint only exists on the mesh path, so this scheme could never be drawn.");
                Assert.IsTrue(o.HullPaint.IsUsableFor(hull),
                    $"'{o.Id}': {o.HullPaint.ExplainUnusableFor(hull)}");
            }
        }

        /// <summary>
        /// The unpainted owners are unpainted for a REASON, and this pins what the reason now is.
        ///
        /// <para><b>⚠️ This test was FLIPPED by the small-craft drop, and it is worth knowing that it
        /// did not go red on its own.</b> Its previous form only asserted that an unpainted owner's
        /// hull has zero baked schemes — which stayed true when the punt and the console skiff became
        /// paintable, because by then both their keepers had been painted. So the assertion would
        /// have gone on passing while the fact it was written to record ("three of seven are plain,
        /// because this kit paints one hull") had quietly become false. A test that survives the
        /// change it was meant to notice is worse than no test, so it now pins the COUNT and the
        /// identity, both of which move the moment another hull gains an axis or another owner
        /// joins the register.</para>
        ///
        /// <para><b>ONE owner is unpainted now, and since 2026-09-06 it is an ART gap rather than a
        /// world-content one.</b> Celeste Bernard used to keep a hand-drawn skiff — a SPRITE-only visual
        /// with no hull mesh, which paint could never reach at all. The owner retired that hull on
        /// 2026-09-06 (Core RetiredContentIds); she keeps <c>boat.dory_outboard</c> now, whose
        /// <c>visual.dory_iso</c> DOES carry a mesh. What is missing is the paint AXIS:
        /// <c>doryIsoRig.js</c> ships no SCHEMES block, so nothing is baked for
        /// <c>hullmesh.dory_iso</c> to wear. That is the same shape of gap Marie Gallant's Cape
        /// Islander had before her axis landed, and it closes the same way.</para>
        ///
        /// <para><b>⚠️ The Cape Islander's half of this test was the reason it was written, and it
        /// has now fired.</b> Marie Gallant was the OTHER unpainted owner, for a different reason:
        /// her hull had a mesh but her rig had no paint axis (a plain <c>MATS</c> const) — an art
        /// gap, and the last one at this wharf. The gap is closed, so the list below is one name
        /// shorter and the Cape Islander's own assertion has flipped from "she is unpainted BECAUSE
        /// nothing is baked for her" to "she is painted, off a hull that has schemes". Six of seven
        /// berths wear colours; the seventh needs a boat, not a paint job.</para>
        /// </summary>
        [Test]
        public void TheUnpaintedOwnersAreUnpaintedForStatedReasons()
        {
            var unpainted = Owners().Where(x => x.HullPaint == null && x.IsPresentable())
                                    .OrderBy(o => o.Id).ToArray();

            foreach (var o in unpainted)
            {
                var hull = o.Boat.Visual.HullMesh;
                int available = hull == null ? 0 : SchemesFor(hull).Length;
                Assert.Zero(available,
                    $"'{o.Id}' keeps '{o.Boat.Id}', for which {available} scheme(s) ARE baked, yet she " +
                    "wears none. If that is deliberate, say so; otherwise assign one.");
            }

            CollectionAssert.AreEqual(
                new[] { "owner.bernard_celeste" },
                unpainted.Select(o => o.Id).ToArray(),
                "The unpainted set has moved. Expected exactly Celeste Bernard, whose dory has a hull " +
                "mesh but no paint schemes baked for it. Found: [" +
                string.Join(", ", unpainted.Select(o => o.Id)) + "]. If a hull just gained an axis, " +
                "paint her keeper and update this list; if an owner joined the register unpainted, " +
                "say in the PR why.");

            // ⭐ HER GAP IS NOW THE SAME KIND AS EVERY OTHER: a missing bake, not a missing mesh. She
            // kept the hand-drawn fishing skiff until 2026-09-06 (Core RetiredContentIds) — a SPRITE-only
            // visual that paint could never reach whatever the art director drew. Asserting the mesh
            // POSITIVELY is what keeps that distinction from quietly regressing: if she is ever moved
            // back onto a mesh-less hull, this goes red rather than her simply staying in the list above
            // for a reason nobody re-read.
            var bernard = unpainted.Single(o => o.Id == "owner.bernard_celeste");
            Assert.IsNotNull(bernard.Boat.Visual.HullMesh,
                $"'{bernard.Boat.Visual.Id}' has NO hull mesh — Celeste Bernard is back on a sprite-only " +
                "hull, which paint can never reach. The fleet is mesh (ADR 0022); put her on a mesh hull.");
            Assert.IsEmpty(SchemesFor(bernard.Boat.Visual.HullMesh),
                $"Schemes ARE baked for '{bernard.Boat.Visual.HullMesh.Id}' now, so nothing is blocking " +
                "Celeste Bernard any more: assign her one and move her out of this list.");

            // The other direction, and the half that just flipped: Marie Gallant's Cape Islander had
            // a mesh all along and was held out only by a missing paint AXIS. Asserted positively so
            // that losing the axis again cannot show up merely as "the list grew".
            var gallant = Owners().Single(o => o.Id == "owner.gallant_marie");
            Assert.IsNotNull(gallant.Boat.Visual.HullMesh, "Marie Gallant's boat has lost its hull mesh.");
            Assert.IsNotEmpty(SchemesFor(gallant.Boat.Visual.HullMesh),
                "No paint schemes are baked for Marie Gallant's Cape Islander. Her rig gained an axis " +
                "(capeIslanderIsoRig.js SCHEMES); if this is empty the bake was lost — run Hidden " +
                "Harbours ▸ Dev ▸ 3D Hulls ▸ Bake hull PAINT SCHEMES…");
            Assert.IsNotNull(gallant.HullPaint,
                "Marie Gallant keeps the best Cape Islander on this wall and her hull now HAS schemes, " +
                "yet she wears none — she is back in the same stock sage as any other Cape Islander.");
        }

        // ---- the builder path ---------------------------------------------------------------
        //
        // It is NOT tested here, and that is deliberate. MooredBoat draws herself in OnEnable and
        // nowhere else, and that lifecycle does not run in an EditMode fixture — measured both ways
        // (inactive-then-activated, and enable-toggled): the component logged nothing and installed
        // nothing. An assertion here would have passed because nothing happened, which is the exact
        // shape of a false green this repo has shipped before. It lives in
        // NineMileCreekFleetPaintPlayTests, where the wake is real and the fixture asserts the
        // install COUNT before it believes anything about which scheme was handed over.

    }
}
