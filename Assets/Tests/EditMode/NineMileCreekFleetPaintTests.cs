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

        /// <summary>The unpainted owners are unpainted for a REASON, and the reason is that this kit
        /// paints one hull. Stated as a test so that "three of seven are plain" is a recorded fact
        /// rather than something a reader has to notice.</summary>
        [Test]
        public void OwnersWithNoPaintKeepAHullThatHasNone()
        {
            foreach (var o in Owners().Where(x => x.HullPaint == null && x.IsPresentable()))
            {
                var hull = o.Boat.Visual.HullMesh;
                int available = hull == null ? 0 : SchemesFor(hull).Length;
                Assert.Zero(available,
                    $"'{o.Id}' keeps '{o.Boat.Id}', for which {available} scheme(s) ARE baked, yet she " +
                    "wears none. If that is deliberate, say so; otherwise assign one.");
            }
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
