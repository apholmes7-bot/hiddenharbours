namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How long the fish in your hands is</b> — the one quantity the hand-slot rule asks a catch for,
    /// derived from the weight it already carries.
    ///
    /// <para><b>Why derived and not a field on the species Def.</b> The catch that matters here is THIS
    /// one, not its species: a 2 kg cod goes in one hand and a 12 kg cod does not, and both come off the
    /// same <c>FishSpeciesDef</c> (<c>MinWeightKg</c> 2, <c>MaxWeightKg</c> 12). A per-species length
    /// would answer the same for both and the rule would stop being about the fish you caught. Weight is
    /// already stamped per item, already saved, already priced — so the length rides it.</para>
    ///
    /// <para><b>The relation is the art rig's own, not an invention.</b> <c>fishIsoRig.js</c> builds every
    /// species from <c>len</c> × <c>girth</c> and computes mass as <c>len·girth²·scale³·390</c>. Girth
    /// tracks length across the whole shipped roster (0.150 / 0.164 / 0.153 / 0.151 of it for cod,
    /// haddock, pollock and mackerel), so mass goes as the cube of length and length goes as the cube
    /// root of mass. <see cref="LengthMeters"/> is that inversion, and
    /// <c>CatchSizeTests.TheCubeRootLaw_ReproducesTheRigsOwnFourSpecies</c> holds it to within 4 % of the
    /// rig's four declared lengths — a tighter agreement than the threshold it feeds needs.</para>
    ///
    /// <para><b>What this is not.</b> Not sim: nothing here is saved, nothing is recomputed per frame,
    /// and no gameplay number is derived from it except "does this need both hands". The day a species
    /// declares a real length (a flatfish, an eel — shapes this law does not fit) that field is read here
    /// in preference and every call site is unchanged.</para>
    /// </summary>
    public static class CatchSize
    {
        /// <summary>
        /// Nose-to-tail length in metres for a catch of this weight, through the rig's cube law.
        ///
        /// <para><paramref name="coefficient"/> is the owner's dial
        /// (<see cref="GameConfig.FishLengthPerKgCubeRootMeters"/>) — metres of fish per cube root of a
        /// kilogram. A non-positive weight or coefficient answers 0, which reads as "small enough for one
        /// hand" everywhere: an unweighed catch must never be the thing that jams the hands.</para>
        /// </summary>
        public static float LengthMeters(float weightKg, float coefficient)
        {
            if (weightKg <= 0f || coefficient <= 0f) return 0f;
            // Math.Pow, not Math.Cbrt: Cbrt is .NET Standard 2.1 only, and the profile is a project
            // setting an art or platform change could move under us. The argument is guarded positive
            // above, so Pow's negative-base NaN cannot be reached.
            return coefficient * (float)System.Math.Pow(weightKg, 1.0 / 3.0);
        }

        /// <inheritdoc cref="LengthMeters(float,float)"/>
        /// <remarks>Reads the owner's coefficient off <see cref="GameServices"/>, so an unwired
        /// config falls back to <see cref="GameConfig.DefaultFishLengthPerKgCubeRootMeters"/> rather
        /// than to zero.</remarks>
        public static float LengthMeters(in CatchItem item)
            => LengthMeters(item.WeightKg, GameServices.FishLengthPerKgCubeRootMeters);
    }
}
