using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The water and ground effects that used to be parked at fixed orders just above the decor floor
    /// (spray at 5, splashes at 4 and 6, buoys and emergent shore plants at 3, the NPC catch at 30, the rod
    /// line and splash at 50 and 51). ADR 0059 step 2 re-anchors each one, so none of them is a fixed number
    /// in the band any more. See <see cref="EffectSorting"/> for the rule and the table.
    /// </summary>
    public enum SortedEffect
    {
        /// <summary>Wind-torn spray off the crests (Art <c>SprayEmitter</c>). Anchored at the wisp's spawn point.</summary>
        WindSpray = 0,
        /// <summary>Surf spray off a breaking lip (Art <c>SurfSprayEmitter</c>). Anchored at the puff's spawn point.</summary>
        SurfSpray = 1,
        /// <summary>A wading or swimming figure's splash (Art <c>WadeSplashEmitter</c>). Anchored at the splash's spawn point.</summary>
        WadeSplash = 2,
        /// <summary>The landing splash and cast rings (Art <c>MomentBurstEmitter</c>). Anchored at the cue's point.
        /// The dig's dirt is <see cref="DigDirt"/>.</summary>
        MomentBurst = 3,
        /// <summary>The trap haul-break and set splash (Fishing <c>TrapSplashFx</c>). Anchored at the splash's point.</summary>
        TrapSplash = 4,
        /// <summary>A trap buoy: the player's, the ambient fleet's and an NPC pot boat's. Anchored at the buoy's root.</summary>
        Buoy = 5,
        /// <summary>An emergent shore plant on the old sprite path (Art <c>ShorePlantTideView</c>). Anchored at its root.</summary>
        EmergentShorePlant = 6,
        /// <summary>The NPC pot boat's catch in crates and in hand, when it has no skipper to take its order from.
        /// Anchored at the boat's waterline pivot.</summary>
        NpcCatch = 7,
        /// <summary>The fishing line (Player <c>RodFightPresenter</c>). Carried: the fisher's own order.</summary>
        RodLine = 8,
        /// <summary>The rod fight's surface splash (Player <c>RodFightPresenter</c>). Carried: the fisher's own order.</summary>
        RodSplash = 9,
        /// <summary>An NPC pot boat's gear while it is at her rail: the pot aboard, the haul rope, the buoy
        /// being gaffed or tossed (App <c>NpcPotBoats</c>). Carried: the boat's own order. The gear is not her
        /// child, so without this it would sort under the hull it is being worked over.</summary>
        NpcGearAtRail = 10,
        /// <summary>The dirt a shovel throws out of a clam hole (Art <c>MomentBurstEmitter</c>, the dig impact and
        /// its toss). Anchored at the hole, which the digger stands beside, never on.</summary>
        DigDirt = 11,
        /// <summary>The hooked fish at the surface, through the lift to the deck lip (Player
        /// <c>SurfacedFishDrawer</c>, for the player and for a villager angler). Carried: the fisher's own
        /// order, on the side of him she is on (<see cref="EffectSorting.FishOnLineOrder"/>). In his hands she
        /// is the held catch, which is drawn in front of him by its own rule.</summary>
        FishOnLine = 12,
    }

    /// <summary>
    /// <b>Where a free effect sorts (ADR 0059 decision 3, rollout step 2).</b> No effect holds a fixed order
    /// in the decor band's low floor. Each one is either <b>anchored</b>: the decor band's own Y-sort (the
    /// <c>YSortSprite</c> rule, ADR 0032) taken at its emission point when it spawns, plus a few rungs; or
    /// <b>carried</b>: its carrier's order, plus a few rungs. The rungs are the table below, and this file is
    /// the only place they are written down.
    ///
    /// <para><b>Why now, while hulls still sort at 1.</b> Step 3 moves hulls into the band. An effect parked
    /// at 3 to 60 would then draw under every hull, a buoy or a splash south of her included. Re-anchoring
    /// first means nothing vanishes in between. The visible change today: these effects interleave with
    /// rocks, grass and figures by position, instead of drawing under all of them.</para>
    ///
    /// <para><b>Reading a rung.</b> One rung is one sorting order, a quarter metre of Y
    /// (<see cref="SortingBands.OrdersPerMetre"/>). A negative rung draws behind whatever stands on the
    /// anchor point, a positive one in front of it. Effects sort once per spawn, from pooled state: one
    /// rounding and a clamp, no allocation.</para>
    /// </summary>
    public static class EffectSorting
    {
        // --- THE TABLE (rungs from the anchor) ----------------------------------------------------------

        /// <summary>Spray and surf spray sort exactly at their own spawn point: nothing stands on a wave crest.</summary>
        public const int SprayRungs = 0;

        /// <summary>
        /// A splash at somebody's feet (the wade splash, the landing and cast bursts) sorts one rung
        /// behind its spawn point. The figure whose feet threw it stands on that point and would otherwise tie
        /// with it, and a tie is broken by nothing we control; this is the splash yielding, as
        /// <see cref="SortingBands.HullYieldRungs"/> has the hull yield to whoever stands on her. Drops thrown
        /// south of him still draw over his legs, which is where they are.
        /// </summary>
        public const int FootSplashRungs = -1;

        /// <summary>A trap splash sorts one rung in front of its point, so it breaks round the buoy it bursts
        /// from (the buoy sorts at <see cref="BuoyRungs"/>), as it did when the two were parked at 4 and 3.</summary>
        public const int TrapSplashRungs = 1;

        /// <summary>A buoy sorts at its root, the point it floats at; it bobs on a child, so it does not flicker.</summary>
        public const int BuoyRungs = 0;

        /// <summary>An emergent shore plant sorts exactly as a <c>YSortSprite</c> at its root would.</summary>
        public const int EmergentShorePlantRungs = 0;

        /// <summary>The NPC catch, when it has no skipper to take its order from, sorts at the boat's pivot.</summary>
        public const int NpcCatchRungs = 0;

        /// <summary>
        /// The rod fight's splash sorts one rung in front of the fisher (the architecture note, option A: "at
        /// the player's order +1"). It used to sit at 51, over nothing but the low floor.
        /// </summary>
        public const int RodOverFisherRungs = 1;

        /// <summary>
        /// The fishing line sorts two rungs <b>behind</b> the fisher (ADR 0059 step 3): under his body and under
        /// the rod even when the rod is behind him (the rod sorts one rung behind in the away row), over the deck
        /// he fishes from. Step 2 put it one rung in front and it drew across the player; before that it sat at a
        /// parked 50, under him. Now that the boat's group holds the deck, behind the fisher is still over the
        /// hull, and ashore it is over the ground and anything north of him.
        /// </summary>
        public const int RodLineRungs = -2;

        /// <summary>
        /// An NPC pot boat's gear at her rail sorts one rung in front of the boat (ADR 0059 step 3): over her
        /// hull and deck, as it was when the hull sat at 1 and the gear at 2.
        /// </summary>
        public const int NpcGearOverHullRungs = 1;

        /// <summary>
        /// Dirt thrown out of a clam hole sorts exactly as a thing standing at the hole would. The hole is not
        /// the digger's feet: he stands beside it, facing it, so the foot splash's one-rung yield is wrong here
        /// (facing S it tied the dirt with him and hid it behind his legs and the blade). At the hole's own
        /// Y-sort the dirt draws over him when the hole is south of him and under him when it is beyond him (N).
        /// </summary>
        public const int DigDirtRungs = 0;

        /// <summary>
        /// The hooked fish on the FAR side of the fisher (in the water beyond his feet) sorts one rung
        /// <b>behind</b> him: under his body and his rod, over his line (which sorts at
        /// <see cref="RodLineRungs"/>, so the line goes into her mouth rather than across her). She used to sit
        /// at a parked water order and drew over his body at the deck lip and all through the lift.
        /// </summary>
        public const int FishBeyondFisherRungs = -1;

        /// <summary>The hooked fish on the NEAR side of him (south of his feet, nearer the camera in the
        /// three-quarter view) sorts one rung in front, as anything standing there would.</summary>
        public const int FishNearFisherRungs = 1;

        /// <summary>
        /// A rod splash on the FAR side of the fisher (on the water beyond his feet: the fish breaking, a
        /// jumper going back in, the fish leaving the water at the landing) sorts three rungs <b>behind</b>
        /// him: on the water surface, under the hooked fish (<see cref="FishBeyondFisherRungs"/>) and under
        /// the line (<see cref="RodLineRungs"/>). At one rung in front it drew across a villager's chest when
        /// he lifted a fish over a wharf rail. Short of his feet the splash keeps <see cref="RodOverFisherRungs"/>.
        /// </summary>
        public const int SplashBeyondFisherRungs = -3;

        // --- THE RULE ---------------------------------------------------------------------------------

        /// <summary>The rungs <paramref name="effect"/> sorts at from its anchor (or its carrier).</summary>
        public static int RungsFor(SortedEffect effect)
        {
            switch (effect)
            {
                case SortedEffect.WindSpray:
                case SortedEffect.SurfSpray: return SprayRungs;
                case SortedEffect.WadeSplash:
                case SortedEffect.MomentBurst: return FootSplashRungs;
                case SortedEffect.TrapSplash: return TrapSplashRungs;
                case SortedEffect.Buoy: return BuoyRungs;
                case SortedEffect.EmergentShorePlant: return EmergentShorePlantRungs;
                case SortedEffect.NpcCatch: return NpcCatchRungs;
                case SortedEffect.RodLine: return RodLineRungs;
                case SortedEffect.RodSplash: return RodOverFisherRungs;
                case SortedEffect.NpcGearAtRail: return NpcGearOverHullRungs;
                case SortedEffect.DigDirt: return DigDirtRungs;
                case SortedEffect.FishOnLine: return FishBeyondFisherRungs;
                default: return 0;
            }
        }

        /// <summary>
        /// True for an effect whose order comes from its carrier (<see cref="OrderOver"/>); false for one
        /// anchored at a world Y (<see cref="OrderAt"/>).
        /// </summary>
        public static bool IsCarried(SortedEffect effect) =>
            effect == SortedEffect.RodLine || effect == SortedEffect.RodSplash || effect == SortedEffect.NpcGearAtRail
            || effect == SortedEffect.FishOnLine;

        /// <summary>
        /// The decor band's Y-sort at world <paramref name="worldY"/>: <c>round(DecorBase - y * OrdersPerMetre)</c>
        /// clamped into the band. Exactly <c>YSortSprite.OrderFor</c> with the band's own numbers, held here
        /// because Boats, Fishing, Player and App cannot see Art. A non-finite Y sorts at the band's middle.
        /// </summary>
        public static int BandOrderAt(float worldY)
        {
            float y = float.IsNaN(worldY) || float.IsInfinity(worldY) ? 0f : worldY;
            int order = Mathf.RoundToInt(SortingBands.DecorBase - y * SortingBands.OrdersPerMetre);
            return Mathf.Clamp(order, SortingBands.DecorFloor, SortingBands.DecorCeiling);
        }

        /// <summary>
        /// The order an anchored <paramref name="effect"/> spawned at world <paramref name="anchorY"/> sorts
        /// at: the band's Y-sort there plus its rungs, kept inside the band.
        /// </summary>
        public static int OrderAt(SortedEffect effect, float anchorY) =>
            Mathf.Clamp(BandOrderAt(anchorY) + RungsFor(effect), SortingBands.DecorFloor, SortingBands.DecorCeiling);

        /// <summary>
        /// The order a carried <paramref name="effect"/> sorts at over a carrier sorting at
        /// <paramref name="carrierOrder"/>: the carrier's order plus its rungs. Not clamped to the band, because
        /// a carrier's order is already where it belongs (aboard, after step 3, it is a local order inside the
        /// boat's group).
        /// </summary>
        public static int OrderOver(SortedEffect effect, int carrierOrder) => carrierOrder + RungsFor(effect);

        /// <summary>
        /// The order the hooked fish sorts at over a fisher sorting at <paramref name="fisherOrder"/>, with
        /// her water point <paramref name="fishOffsetY"/> metres north of his feet (the published fight
        /// offset, never her jump lift: a leap does not carry her round him): behind him when she is beyond
        /// his feet (<see cref="FishBeyondFisherRungs"/>), in front when she is short of them
        /// (<see cref="FishNearFisherRungs"/>). Level with his feet counts as beyond, the water side.
        /// </summary>
        public static int FishOnLineOrder(int fisherOrder, float fishOffsetY)
            => fisherOrder + (fishOffsetY >= 0f || float.IsNaN(fishOffsetY) ? FishBeyondFisherRungs : FishNearFisherRungs);

        /// <summary>
        /// The order a rod splash sorts at over a fisher sorting at <paramref name="fisherOrder"/>, landing
        /// <paramref name="splashOffsetY"/> metres north of his feet: on the water behind him when it is beyond
        /// his feet (<see cref="SplashBeyondFisherRungs"/>), in front when it is short of them
        /// (<see cref="RodOverFisherRungs"/>). Level with his feet counts as beyond, as for the fish.
        /// </summary>
        public static int RodSplashOrder(int fisherOrder, float splashOffsetY)
            => splashOffsetY >= 0f || float.IsNaN(splashOffsetY)
                ? fisherOrder + SplashBeyondFisherRungs
                : OrderOver(SortedEffect.RodSplash, fisherOrder);
    }
}
