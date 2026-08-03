using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Reads a boat's EFFECTIVE helm equipment fit (<see cref="HelmFit"/>): the hull's default console fit
    /// with the player's owned upgrades layered on. The single place that answers "what instruments does
    /// this boat's helm actually show right now?" — so the sim (heading/depth reads) and the console UI
    /// agree, the same discipline <c>PlayerGear</c> uses for on-foot capabilities.
    ///
    /// <para><b>Recompute, never store the resolved fit</b> (rule 5, the <c>PotLocker</c> discipline): the
    /// hull's default lives in its <see cref="HelmConsoleDef"/> (authored data), and only the player's
    /// <i>deviations</i> — the instruments they bought for THIS hull — are save state. This keeps the save
    /// sparse (absent = ships default) and the per-boat "Only the watch is carried" model honest: every
    /// instrument except the watch is owned against a specific hull.</para>
    ///
    /// <para><b>Pure &amp; dormant.</b> These are pure functions over an owned-id set — no Unity runtime
    /// needed to test them. The owned-per-hull set is fed in by the caller; the save schema that persists
    /// it (v4→v5, its own ADR) and the shop that grants the ids are deferred to their milestone. Nothing
    /// gates on this yet — it is the model, tested, waiting.</para>
    /// </summary>
    public static class BoatEquipment
    {
        // Stable instrument ids (authored as GearOffer-style priced assets; append-only, rule 2).
        /// <summary>The basic flush-mount depth sounder (<c>DepthRig</c>) — the entry-level brow
        /// instrument, ADR 0025 S2. Owning it fits <see cref="SounderKind.Depth"/> on any hull with a
        /// console; <see cref="FishFinderId"/> supersedes it in the same cutout.</summary>
        public const string DepthSounderId  = "instrument.depth_sounder";
        public const string FishFinderId   = "instrument.fish_finder";
        public const string RadarId        = "instrument.radar";
        public const string GpsId          = "instrument.gps";
        public const string CompassDomeId  = "instrument.compass_dome";
        public const string CompassFlushId = "instrument.compass_flush";

        /// <summary>
        /// The effective fit for a hull: its <paramref name="console"/> default, with any owned upgrade in
        /// <paramref name="ownedForHull"/> applied — but only where the console actually supports that slot
        /// (buying a fish-finder does nothing on a helm whose brow can't take it). A null console (the dory)
        /// resolves to <see cref="HelmFit.None"/>.
        /// </summary>
        public static HelmFit EffectiveFit(HelmConsoleDef console, ICollection<string> ownedForHull)
        {
            if (console == null) return HelmFit.None;

            var sounder = console.DefaultSounder;
            var compass = console.DefaultCompass;
            bool radar  = console.DefaultRadar;
            bool gps    = console.DefaultGps;

            if (ownedForHull != null && ownedForHull.Count > 0)
            {
                // The brow cutout, in tier order. FISH WINS: owning the colour finder on a helm that can
                // take it upgrades the cutout even when the plain sounder is also owned (it is the same
                // hole, and the finder does everything the sounder does). Otherwise a bought depth sounder
                // lights a bare brow.
                //
                // ⚠ There is deliberately no `SupportsSounder` flag to gate the basic unit on. A helm
                // console is BY DEFINITION a dash with a brow, and the depth sounder is the smallest
                // instrument in the set — the flag would be a knob no authored console would ever set
                // false (rule 6: don't add a dial that never moves). `SupportsFishFinder` earns its
                // existence because a small brow genuinely may not take the bigger colour unit. If a
                // console ever does need to refuse the basic sounder, adding the flag then is additive.
                if (console.SupportsFishFinder && ownedForHull.Contains(FishFinderId))
                    sounder = SounderKind.Fish;
                else if (sounder == SounderKind.None && ownedForHull.Contains(DepthSounderId))
                    sounder = SounderKind.Depth;

                // Flush wins over dome if both are somehow owned (it is the higher-tier fitted unit).
                if (console.SupportsFlushCompass && ownedForHull.Contains(CompassFlushId))
                    compass = CompassMount.Flush;
                else if (console.SupportsDomeCompass && ownedForHull.Contains(CompassDomeId))
                    compass = CompassMount.Dome;

                if (console.SupportsRadar && ownedForHull.Contains(RadarId)) radar = true;
                if (console.SupportsGps   && ownedForHull.Contains(GpsId))   gps   = true;
            }

            return new HelmFit(console.Rig, sounder, compass, radar, gps);
        }

        /// <summary>Convenience for a hull that carries a console pointer.</summary>
        public static HelmFit EffectiveFit(BoatHullDef hull, ICollection<string> ownedForHull)
            => EffectiveFit(hull != null ? hull.Helm : null, ownedForHull);
    }
}
