using System.Globalization;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The one sentence a landed catch says (juice charter §4.4, the silent dig #803): "Lifted out a
    /// soft-shell clam — it's in your hand." A shellfish is LIFTED OUT (of the flat); everything else is
    /// LANDED. The line is built once per catch, never per frame; the notebook's register keeps it
    /// and the toast shows it — <b>the one on-screen consequence of <c>CatchLanded</c></b>.
    ///
    /// <para><b>The payoff names the size</b> (owner's ask 2026-11-13): when the species' weight range
    /// is known (<see cref="GameServices.FishSizes"/>, the Core size seam) a LANDED fish says her weight
    /// and a size word instead — "Landed a 9.4 kg atlantic cod — a heavy one." The bands are data
    /// (<see cref="FishFeelSettings"/>). With no range known, and for anything lifted out by hand, the
    /// sentence is the original one, unchanged.</para>
    /// </summary>
    public static class LiftLine
    {
        public static string For(in CatchItem item)
        {
            IFishSizeFacts sizes = GameServices.FishSizes;
            if (item.Category != FishCategory.Shellfish && sizes != null
                && sizes.TryWeightRangeKg(item.SpeciesId, out float minKg, out float maxKg))
            {
                FishFeelSettings feel = GameServices.Config != null
                    ? GameServices.Config.FishFeel : FishFeelSettings.Default;
                return For(in item, minKg, maxKg, in feel);
            }
            return Plain(in item);
        }

        /// <summary>
        /// The sized sentence for a fish whose species runs <paramref name="minKg"/>..<paramref name="maxKg"/>:
        /// "Landed a 9.4 kg atlantic cod — a heavy one." Falls back to the plain sentence when the catch
        /// has no weight or the range is degenerate (nothing honest to say about size). Pure.
        /// </summary>
        public static string For(in CatchItem item, float minKg, float maxKg, in FishFeelSettings feel)
        {
            if (!(item.WeightKg > 0f) || !(maxKg - minKg > 1e-6f)) return Plain(in item);

            string name = Name(in item);
            string verb = item.Category == FishCategory.Shellfish ? "Lifted out" : "Landed";
            string kg = item.WeightKg.ToString("0.0", CultureInfo.InvariantCulture);
            FishSizeBand band = FishFeelMath.BandFor(
                FishFeelMath.Size01(item.WeightKg, minKg, maxKg), in feel);
            return $"{verb} {NumberArticle(kg)} {kg} kg {name} — {FishFeelMath.SizeWord(band)}.";
        }

        private static string Plain(in CatchItem item)
        {
            string name = Name(in item);
            string verb = item.Category == FishCategory.Shellfish ? "Lifted out" : "Landed";
            return $"{verb} {Article(name)} {name} — it's in your hand.";
        }

        private static string Name(in CatchItem item)
            => string.IsNullOrEmpty(item.DisplayName) ? "catch" : item.DisplayName.ToLowerInvariant();

        private static string Article(string lowerName)
        {
            if (string.IsNullOrEmpty(lowerName)) return "a";
            char c = lowerName[0];
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ? "an" : "a";
        }

        /// <summary>"an" before a number that is SPOKEN with a vowel first: anything starting with 8
        /// ("an 8.2 kg"), and eleven and eighteen ("an 11.0 kg", "an 18.5 kg"). Everything else is "a".</summary>
        private static string NumberArticle(string number)
        {
            if (string.IsNullOrEmpty(number)) return "a";
            if (number[0] == '8') return "an";
            int dot = number.IndexOf('.');
            int whole = dot < 0 ? number.Length : dot;
            if (whole == 2 && number[0] == '1' && (number[1] == '1' || number[1] == '8')) return "an";
            return "a";
        }
    }
}
