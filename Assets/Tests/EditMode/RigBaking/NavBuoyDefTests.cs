using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The nav-buoy DATA and the one prefab that wears it.
    ///
    /// <para><b>⭐ What these guard.</b> Three of the numbers on every size rung are MEASUREMENTS
    /// (<c>SpriteHeightMeters</c>, <c>FloatLineFraction</c>, <c>SlopeFollow</c>) fed straight into
    /// <see cref="BuoyWaveVisual"/>. Wrong, they do not throw — the mark just floats oddly, which is
    /// indistinguishable from art nobody has tuned yet. So each is re-derived here from the committed
    /// contract and the live rig rather than trusted.</para>
    /// </summary>
    public class NavBuoyDefTests
    {
        static NavBuoyContract _contract;
        static List<NavBuoyDef> _defs;

        [OneTimeSetUp]
        public void Load()
        {
            _contract = NavBuoyContract.Load();
            _defs = AssetDatabase.FindAssets($"t:{nameof(NavBuoyDef)}")
                                 .Select(AssetDatabase.GUIDToAssetPath)
                                 .Select(AssetDatabase.LoadAssetAtPath<NavBuoyDef>)
                                 .Where(d => d != null)
                                 .ToList();
        }

        [Test]
        public void OneDefExistsForEveryBakedMark()
        {
            var byType = _defs.ToDictionary(d => d.MarkType, StringComparer.Ordinal);
            foreach (string type in NavBuoyKit.BakeTypes)
                Assert.That(byType.ContainsKey(type), Is.True,
                    $"No NavBuoyDef for baked mark '{type}'. Run " +
                    "\"Hidden Harbours/Art/Build Nav Buoy Defs (from the bake)\".");
        }

        [Test]
        public void IdsAreStableUniqueAndCorrectlyShaped()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in _defs)
            {
                Assert.That(Regex.IsMatch(d.Id, "^buoy\\.[a-z0-9_]+$"), Is.True,
                    $"'{d.Id}' is not type.snake_case (CLAUDE.md §5).");
                Assert.That(seen.Add(d.Id), Is.True, $"Duplicate nav-buoy id '{d.Id}'.");

                // The builder's reserved id must be what the asset carries — ids are append-only, so a
                // drift here means someone re-ided a placed asset.
                Assert.That(d.Id, Is.EqualTo(NavBuoyDefBuilder.IdFor(d.MarkType)),
                    $"'{d.MarkType}' carries id '{d.Id}' but the reserved id is " +
                    $"'{NavBuoyDefBuilder.IdFor(d.MarkType)}'. Ids are APPEND-ONLY.");
            }
        }

        [Test]
        public void EverySizeRungCarriesEightRealFacings()
        {
            foreach (var d in _defs)
            {
                Assert.That(d.Sizes, Is.Not.Empty, $"{d.Id} has no size rungs.");
                foreach (var s in d.Sizes)
                {
                    Assert.That(s.Facings, Is.Not.Null.And.Length.EqualTo(NavBuoyKit.Facings),
                        $"{d.Id}/{s.SizeId}: expected {NavBuoyKit.Facings} facings.");
                    for (int i = 0; i < s.Facings.Length; i++)
                        Assert.That(s.Facings[i], Is.Not.Null,
                            $"{d.Id}/{s.SizeId} facing {i} is null — the sheet was Multiple but not " +
                            "SLICED (a fresh import has an empty rect list), or the slicer did not run.");
                }
            }
        }

        /// <summary>
        /// ⚠️ The nav sheets pivot ON the waterline, so the float line IS the sprite's normalised
        /// pivot y. Re-derived from the contract rather than trusted: get it wrong and every mark sits
        /// with its keel on the surface — wrong, but plausible enough to ship.
        /// </summary>
        [Test]
        public void FloatGeometryIsDerivedFromTheContractNotGuessed()
        {
            foreach (var d in _defs)
            {
                foreach (var s in d.Sizes)
                {
                    var cell = _contract.Get(NavBuoyKit.CellKey(d.MarkType, s.SizeId));

                    float wantFloat = (cell.cellH - cell.pivotY) / (float)cell.cellH;
                    Assert.That(s.FloatLineFraction, Is.EqualTo(wantFloat).Within(1e-5f),
                        $"{d.Id}/{s.SizeId}: float line {s.FloatLineFraction} != the sprite's " +
                        $"normalised pivot y {wantFloat} (ADR 0026 over the contract's cell).");

                    float wantHeight = cell.cellH / (float)NavBuoyKit.PxPerM;
                    Assert.That(s.SpriteHeightMeters, Is.EqualTo(wantHeight).Within(1e-5f),
                        $"{d.Id}/{s.SizeId}: height {s.SpriteHeightMeters} m != cellH/{NavBuoyKit.PxPerM} " +
                        $"= {wantHeight} m.");

                    Assert.That(s.FloatLineFraction, Is.InRange(0.01f, 0.99f),
                        $"{d.Id}/{s.SizeId}: a waterline at the very top or bottom of the sprite means " +
                        "the pivot is not the waterline any more.");
                }
            }
        }

        /// <summary>
        /// <c>SlopeFollow</c> is the rig's own <c>BM/(BM+BG)</c>. It must be REAL per-hull data, not a
        /// constant copied across every rung — a flat can rolls its guts out (~0.6) and a
        /// counterweighted steel buoy stands up (~0.13). If these ever collapse to one number, the
        /// bob has stopped being hull-specific and nothing else would say so.
        /// </summary>
        [Test]
        public void SlopeFollowIsRealPerHullDataNotAConstant()
        {
            var can = _defs.FirstOrDefault(d => d.MarkType == "PortCan");
            var steel = _defs.FirstOrDefault(d => d.MarkType == "StbdLit");
            Assert.That(can, Is.Not.Null); Assert.That(steel, Is.Not.Null);

            var canRung = can.Size("s20");
            var steelRung = steel.Size("s20");
            Assert.That(canRung, Is.Not.Null); Assert.That(steelRung, Is.Not.Null);

            Assert.That(canRung.SlopeFollow, Is.GreaterThan(0.4f),
                "A flat can follows the wave slope hard — it should be well above 0.4.");
            Assert.That(steelRung.SlopeFollow, Is.LessThan(0.3f),
                "A counterweighted steel hull stands up in a sea — it should be well under 0.3.");
            Assert.That(canRung.SlopeFollow, Is.GreaterThan(steelRung.SlopeFollow * 2f),
                "The can must follow at least twice as much as the steel buoy; if these converge the " +
                "per-hull hydrostatics have stopped reaching the def.");

            foreach (var d in _defs)
                foreach (var s in d.Sizes)
                    Assert.That(s.SlopeFollow, Is.InRange(0.0001f, 1f),
                        $"{d.Id}/{s.SizeId}: follow {s.SlopeFollow} is outside BM/(BM+BG)'s range.");
        }

        [Test]
        public void LightDataIsCarriedButNothingClaimsToRenderIt()
        {
            foreach (var d in _defs)
            {
                // Mooring carries no light in the kit; the lit laterals and cardinals do.
                if (d.MarkType == "Mooring")
                    Assert.That(d.IsLit, Is.False, "The mooring mark is unlit in IALA Region B.");
                else
                    Assert.That(d.IsLit, Is.True, $"{d.Id} should carry a light character as DATA.");

                if (d.IsLit)
                    Assert.That(d.LightText, Is.Not.Empty,
                        $"{d.Id} has a light id but no readable character — the pair is the data.");
            }
        }

        /// <summary>
        /// ⚠️ <b>Scene-wired is not builder-wired.</b> The prefab's SpriteRenderer must live on a
        /// CHILD, never the root: <see cref="BuoyWaveVisual"/> samples the wave field at the root and
        /// bobs the child, so a prefab whose visual IS the root samples at a point that moves with its
        /// own bob. That reads as jitter, not as a wiring bug, and no other check would catch it.
        /// </summary>
        [Test]
        public void ThePrefabSamplesAtTheRootAndBobsAChild()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NavBuoyDefBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null,
                $"No prefab at {NavBuoyDefBuilder.PrefabPath}. Run " +
                "\"Hidden Harbours/Art/Build Nav Buoy Prefab\".");

            Assert.That(prefab.GetComponent<BuoyWaveVisual>(), Is.Not.Null,
                "The prefab must reuse BuoyWaveVisual — the marks share ONE sea with the trap buoys.");
            Assert.That(prefab.GetComponent<NavBuoyVisual>(), Is.Not.Null);

            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Null,
                "The renderer is on the ROOT. The bob would then move the field's own sample point.");

            var sr = prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            Assert.That(sr, Is.Not.Null, "No SpriteRenderer on a child — nothing would draw.");
            Assert.That(sr.transform, Is.Not.EqualTo(prefab.transform));
            Assert.That(sr.transform.parent, Is.EqualTo(prefab.transform));
        }

        /// <summary>The marks must not have grown a second bob. The whole point of reusing
        /// <see cref="BuoyWaveVisual"/> is that two buoys 30 m apart are on the same wave.</summary>
        [Test]
        public void NavBuoyVisualDoesNotImplementItsOwnMotion()
        {
            var t = typeof(NavBuoyVisual);
            foreach (string perFrame in new[] { "Update", "LateUpdate", "FixedUpdate" })
                Assert.That(t.GetMethod(perFrame,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public),
                    Is.Null,
                    $"NavBuoyVisual declares {perFrame} — it is WIRING. The bob belongs to " +
                    "BuoyWaveVisual; a second one puts the marks on a different sea from the boats.");
        }
    }
}
