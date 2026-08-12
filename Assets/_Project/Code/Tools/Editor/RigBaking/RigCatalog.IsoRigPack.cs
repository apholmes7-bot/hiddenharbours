using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The owner's iso rig pack of 2026-08-06: wharf structure, wharf dressing, village services
        /// and shoreline finds — four rigs that share a drop but declare no order dependency.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> IsoRigPackRigs() =>
            new RigRegistration
            {
                // ---- the ISO RIG PACK (owner drop of 2026-08-06) — FOUR INDEPENDENT RIGS ---------
                //
                // wharf structure · wharf dressing · village services · shoreline finds. Unlike the
                // pass-6 character kit and the shop kit, these declare NO prerequisites, and that is
                // MEASURED rather than taken from the pack README's "no order dependency" claim: 83
                // probe keys — including the full RGBA buffer of 24 representative renders across all
                // four rigs — are byte-identical whether each rig is loaded alone, in the README's
                // order, in reverse, or shuffled. The shop kit's README made the same claim and was
                // wrong three ways (#437), so the claim was re-measured here rather than inherited.
                //
                // ⚠️ ALL THREE DIRECTIONAL RIGS ARE COUNTER-CLOCKWISE BY MEASUREMENT, not by label.
                // Each turns its +X axis −46.75° of screen rotation per dir step — the same figure,
                // to the digit, as houseIsoRig / wharfBuildingRig / interiorIsoRig, all of which are
                // registered CCW here after their own probes measured them. Their project() calls
                // also agree to 0.000000000 px relative to each rig's own origin, so the three share
                // ONE turntable rather than three that happen to look alike. The bake must still
                // probe from rendered pixels like every sibling; this entry is the prior it is
                // cross-checked against.

                // Structure: quay/pier/crib/float/gangway/slipway/riprap, 7 families and 17 presets.
                // NO standard W/H/pivot triple — the cell sizes itself from the projected bbox and
                // the rig reports px,py PER BAKE (both fractional), so this must load with
                // InstallModule; Install would throw on the missing pivot.
                //
                // ⚠️ It does NOT replace the near-plan wharf tile kit. Its own README is explicit
                // ("the baked kit is untouched — WharfAtlas.png / WharfOverlays.png stay where they
                // are; this rig sits beside them"), so WharfKitCatalog, StPetersWharf and
                // NineMileCreekWharf are untouched by this import and their migration is a separate
                // decision, not a side effect of registering a rig.
                ["wharfIso"] = new RigEntry($"{RigFolder}/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js",
                                            "WharfIso", AzimuthConvention.CounterClockwise),

                // Dressing that sits ON the deck: 61 pieces in 7 categories. FIXED 420×520 sheet
                // with the pivot at (210,420), so unlike its structural sibling this one does expose
                // the W/H/pivot triple and loads with Install.
                ["wharfDecor"] = new RigEntry($"{RigFolder}/iso-rig-pack/wharf-decor-iso/wharfDecorRig.js",
                                              "WharfDecor", AzimuthConvention.CounterClockwise),

                // Village services — power, light, water, sewer, fuel, telecom. 42 pieces, FIXED
                // 440×620 sheet, pivot (220,520); the sheet is sized for a 10.10 m radio mast, so
                // most pieces sit in a lot of transparent space and the bake crops to ink.
                // Spans are NEVER baked: ties() hands out wire/lamp/drop points in metres and the
                // catenary between two poles is drawn at runtime, so there is no sprite per gap.
                ["utilityIso"] = new RigEntry($"{RigFolder}/iso-rig-pack/utility-iso/utilityIsoRig.js",
                                              "UtilityIso", AzimuthConvention.CounterClockwise),

                // NOT DIRECTIONAL, and not in the same way anything else here is not directional.
                // The finds lie FLAT on the sand, so there is no facing and no project() at all —
                // only a lie angle, 8 canonical steps of the object's long axis in the ground plane,
                // rotated in the GENERATOR before projection (never a pixel rotate). The convention
                // below is a placeholder nothing reads and nothing probes, exactly as for the bobber.
                //
                // ⚠️ TWO SHAPE TRAPS. Its DIRS is a string ARRAY (["N","NE",…]), not the number the
                // other rigs expose, so Install's `typeof DIRS === 'number'` reports 0 rather than 8;
                // and it declares no pivot, which Install would throw on. Load it with InstallModule
                // and read cellOf(key) for the per-find cell and pivot.
                //
                // ⚠️ Its ground foreshorten is Q = 0.72, NOT the 0.6428 (= sin 40°) every other rig
                // in this pack projects with. Carrying that constant across the two families is the
                // un-squash mistake this repo keeps making; the value is per family and measured.
                ["shoreFinds"] = new RigEntry($"{RigFolder}/iso-rig-pack/shoreline-finds-iso/shoreFindsRig.js",
                                              "ShoreFinds", AzimuthConvention.CounterClockwise),
            };
    }
}
