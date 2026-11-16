namespace HiddenHarbours.Core
{
    /// <summary>Where an island good comes from. Append-only: an asset stores the number.</summary>
    public enum GoodSource
    {
        /// <summary>Bought in Nine Mile Creek and carried over the bar at low water.</summary>
        MainlandOverBar = 0,
        /// <summary>Comes by boat at high water through the gut (fleet diesel, bulk heating oil). Never
        /// counted against the bar runs.</summary>
        BySea = 1,
        /// <summary>The island supplies it itself (fish, firewood, garden produce). Never counted.</summary>
        Island = 2,
    }

    /// <summary>What kind of thing a <see cref="CarrierDef"/> is. Append-only.</summary>
    public enum CarrierKind
    {
        /// <summary>Carried in the hands or on the back (bag, pail, rucksack).</summary>
        Carried = 0,
        /// <summary>Pulled on wheels by a walker (hand cart, garden wagon).</summary>
        Pulled = 1,
        /// <summary>A rack on a vehicle (the quad's front and rear racks).</summary>
        VehicleRack = 2,
        /// <summary>A towed trailer.</summary>
        Trailer = 3,
        /// <summary>A vehicle's own bed (the pickup).</summary>
        VehicleBed = 4,
        /// <summary>A loader attachment (tractor forks, tractor bucket). Data only: no machine exists yet.</summary>
        LoaderAttachment = 5,
        /// <summary>A container that is itself loaded onto a carrier (jerry can, drum).</summary>
        Container = 6,
        /// <summary>A pallet: a base the forks lift, loaded with containers or goods.</summary>
        Pallet = 7,
    }

    /// <summary>How a supply role's run meets the tide. Append-only.</summary>
    public enum SupplyTripMode
    {
        /// <summary>Over, errands, and back inside one low-water window (the quad).</summary>
        SameWindowRoundTrip = 0,
        /// <summary>Over on one window, stay over in town, back on a later window (walkers).</summary>
        StayOver = 1,
        /// <summary>Never crosses the bar (the islanders' pickup parked at the NMC landing).</summary>
        MainlandOnly = 2,
    }

    /// <summary>Which limit of a carrier a load hits first.</summary>
    public enum LoadDimension
    {
        None = 0,
        Mass = 1,
        Volume = 2,
    }

    /// <summary>One span of game time, in clock seconds (<c>GameClock.TotalSeconds</c>, the same axis the
    /// tide model reads), during which water stands below a threshold.</summary>
    public readonly struct TideWindow
    {
        public readonly double StartSeconds;
        public readonly double EndSeconds;

        public TideWindow(double startSeconds, double endSeconds)
        {
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
        }

        public double DurationSeconds => EndSeconds - StartSeconds;
    }

    /// <summary>A mass and a volume together: a demand, a capacity or a load.</summary>
    public readonly struct MassVolume
    {
        public readonly double MassKg;
        public readonly double VolumeLitres;

        public MassVolume(double massKg, double volumeLitres)
        {
            MassKg = massKg;
            VolumeLitres = volumeLitres;
        }

        public MassVolume Plus(MassVolume other) => new MassVolume(MassKg + other.MassKg, VolumeLitres + other.VolumeLitres);
        public MassVolume Times(double k) => new MassVolume(MassKg * k, VolumeLitres * k);
    }

    /// <summary>The answer to "does this load go on this carrier?".</summary>
    public readonly struct LoadFit
    {
        /// <summary>True when both the mass and the volume are within the carrier's limits.</summary>
        public readonly bool Fits;
        /// <summary>Load mass over the carrier's mass limit (1 = exactly full by weight).</summary>
        public readonly double MassFraction;
        /// <summary>Load volume over the carrier's volume limit (1 = exactly full by volume).</summary>
        public readonly double VolumeFraction;
        /// <summary>The limit the load is closest to (or over): the one that binds.</summary>
        public readonly LoadDimension Binding;

        public LoadFit(bool fits, double massFraction, double volumeFraction, LoadDimension binding)
        {
            Fits = fits;
            MassFraction = massFraction;
            VolumeFraction = volumeFraction;
            Binding = binding;
        }
    }
}
