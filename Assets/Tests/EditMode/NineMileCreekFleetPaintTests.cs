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
        /// <para><b>Two, not three, and the missing third is not an art gap.</b> The console skiff's
        /// nine schemes ARE baked and proven, but no owner at Nine Mile Creek keeps a boat drawn from
        /// <c>hullmesh.console_iso</c>: Celeste Bernard's <c>boat.fishing_skiff</c> resolves to
        /// <c>visual.fishing_boat</c>, a legacy SPRITE-only visual (eight hand-drawn top-down facings,
        /// no hull mesh), and paint exists only on the mesh path. <c>boat.console_skiff</c> is a
        /// separate def that nobody on this wharf owns. Which boat Celeste Bernard keeps is a
        /// world-content call, not an art one — see the PR body.</para>
        /// </summary>
        [Test]
        public void ThePaintedRegisterSpansMoreThanOneHull()
        {
            var hulls = Owners()
                .Where(o => o.HullPaint != null && o.IsPresentable() && o.Boat.Visual.HullMesh != null)
                .Select(o => o.Boat.Visual.HullMesh.Id)
                .Distinct()
                .OrderBy(s => s)
                .ToArray();

            Assert.GreaterOrEqual(hulls.Length, 2,
                "Fewer than two distinct hull meshes wear paint at Nine Mile Creek — found " +
                $"[{string.Join(", ", hulls)}]. The lobster boat and the punt both have owners and " +
                "both have paint axes; if one has stopped being drawn painted, a bake or an " +
                "assignment was lost.");
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
        /// <para><b>TWO owners are unpainted, for two DIFFERENT reasons, and conflating them is how
        /// this stays broken quietly.</b> Marie Gallant's Cape Islander has a hull mesh but her RIG
        /// has no paint axis (a plain <c>MATS</c> const) — an art gap. Celeste Bernard's
        /// <c>boat.fishing_skiff</c> resolves to <c>visual.fishing_boat</c>, a legacy SPRITE-only
        /// visual with no hull mesh at all — so paint, which exists only on the mesh path, can never
        /// reach her whatever the art director draws. Her console-skiff schemes ARE baked; nobody on
        /// this wharf owns a boat that uses them. Each reason is asserted separately so that fixing
        /// one cannot make the other look fixed.</para>
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
                new[] { "owner.bernard_celeste", "owner.gallant_marie" },
                unpainted.Select(o => o.Id).ToArray(),
                "The unpainted set has moved. Expected exactly Celeste Bernard (her boat is drawn " +
                "from a sprite visual with NO hull mesh, so paint cannot reach her at all) and Marie " +
                "Gallant (her Cape Islander rig has no paint axis to bake). Found: [" +
                string.Join(", ", unpainted.Select(o => o.Id)) + "]. If a hull just gained an axis, " +
                "paint her keeper and update this list; if an owner joined the register unpainted, " +
                "say in the PR why.");

            // The two reasons, pinned apart. Gallant HAS a mesh; Bernard has none.
            var gallant = unpainted.Single(o => o.Id == "owner.gallant_marie");
            Assert.IsNotNull(gallant.Boat.Visual.HullMesh,
                "Marie Gallant's boat has lost its hull mesh. Her reason for being unpainted is " +
                "supposed to be a missing paint AXIS on a mesh hull, not a missing mesh.");

            var bernard = unpainted.Single(o => o.Id == "owner.bernard_celeste");
            Assert.IsNull(bernard.Boat.Visual.HullMesh,
                $"'{bernard.Boat.Visual.Id}' now HAS a hull mesh — so Celeste Bernard's boat is on " +
                "the mesh path and can wear paint. That was the whole thing blocking her: assign " +
                "her a scheme baked for that hull and move her out of this list.");
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
