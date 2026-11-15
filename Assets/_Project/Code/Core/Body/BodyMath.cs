using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The pure maths of the body</b> (body slice 1, owner ask 2026-11-15). No Unity state, no service:
    /// a saved <see cref="PlayerBodyDto"/>, the saved hunger, the clock and the owner's
    /// <see cref="BodySettings"/> in; masses, an appetite, a walk factor out. <see cref="BodyService"/>
    /// only gathers the inputs.
    ///
    /// <para><b>The metabolism is Mifflin-St Jeor</b> (1990): resting kcal/day = 10 x kg + 6.25 x cm -
    /// 5 x age + a sex constant, times an activity level. Chosen over Kleiber's mass^0.75 because the owner
    /// asked for BOTH size and height to set the appetite, and Mifflin-St Jeor is the standard adult
    /// estimate that uses both; Kleiber has no height term.</para>
    ///
    /// <para><b>One hunger, not two.</b> The body never adds a need: it scales the existing hunger's rise
    /// by <c>daily need / the reference body's daily need</c> (<see cref="HungerRateScale"/>). A bigger body
    /// empties a meal sooner, so it must eat more often. One hunger point is then the same food energy for
    /// everyone (<see cref="KcalPerHungerUnit"/>), which is what lets a meal's surplus become kilograms.</para>
    ///
    /// <para><b>Weight moves only at the edges of hunger.</b> Between empty and full, hunger IS the energy
    /// buffer, so mass holds. A meal that overfills (eats past hunger 0) stores its surplus as mass; time
    /// spent fully hungry (hunger at 1) burns mass at a constant rate down to the frame's floor. Both are
    /// piecewise straight lines on the clock, so a slept-through night gives the answer stepping would
    /// (rule 5), and the hunger rate between two anchors is set by the ANCHOR mass: mass only falls while
    /// hunger is pinned at 1, where the rate no longer matters.</para>
    /// </summary>
    public static class BodyMath
    {
        /// <summary>The lowest body walk floor the maths honours, whatever the config says.</summary>
        public const float MinimumWalkFloor = 0.6f;

        /// <summary>Centimetres per metre (the formula is written in cm).</summary>
        private const double CmPerMetre = 100d;

        /// <summary>Hours per day, for per-day rates read per game hour.</summary>
        private const double HoursPerDay = 24d;

        /// <summary>Mifflin-St Jeor coefficients: kcal/day per kg, per cm, per year of age. The published
        /// formula's own constants, not tunables.</summary>
        private const double KcalPerKg = 10d, KcalPerCm = 6.25d, KcalPerYear = 5d;

        /// <summary>A resting need never modelled below this (a guard against a mis-set config making the
        /// division in <see cref="HungerRateScale"/> blow up).</summary>
        private const double MinimumRestingKcal = 1d;

        // ---- shape ---------------------------------------------------------------------------------

        /// <summary>The body's frame, 0..1: the saved one, or the config default when unset.</summary>
        public static float FrameOf(PlayerBodyDto body, in BodySettings s)
            => Clamp01(body != null && body.Frame >= 0f ? body.Frame : s.DefaultFrame);

        /// <summary>The body's height scale, clamped to the config's range: the saved one, or 1 when unset.</summary>
        public static float HeightScaleOf(PlayerBodyDto body, in BodySettings s)
            => ClampHeightScale(body != null && body.HeightScale > 0f ? body.HeightScale : 1f, s);

        /// <summary><paramref name="scale"/> clamped to the config's height range. 1 is always allowed.</summary>
        public static float ClampHeightScale(float scale, in BodySettings s)
        {
            float lo = s.MinHeightScale > 0f && s.MinHeightScale <= 1f ? s.MinHeightScale : 1f;
            float hi = s.MaxHeightScale >= 1f ? s.MaxHeightScale : 1f;
            if (!(scale > 0f)) return 1f;
            return scale < lo ? lo : scale > hi ? hi : scale;
        }

        /// <summary>Height in metres of a height scale.</summary>
        public static double HeightMetres(float heightScale, in BodySettings s)
            => (double)s.ReferenceHeightMetres * heightScale;

        /// <summary>The mass this frame and height are built for (kg).</summary>
        public static double LeanMassKg(float frame, float heightScale, in BodySettings s)
            => Bmi(s.SmallFrameLeanBmi, s.LargeFrameLeanBmi, frame) * Sq(HeightMetres(heightScale, s));

        /// <summary>The most this frame can carry (kg). Never below the lean mass.</summary>
        public static double MaxMassKg(float frame, float heightScale, in BodySettings s)
        {
            double max = Bmi(s.SmallFrameMaxBmi, s.LargeFrameMaxBmi, frame) * Sq(HeightMetres(heightScale, s));
            double lean = LeanMassKg(frame, heightScale, s);
            return max > lean ? max : lean;
        }

        /// <summary>The least this frame wastes to (kg). Never above the lean mass.</summary>
        public static double FloorMassKg(float frame, float heightScale, in BodySettings s)
        {
            double floor = Bmi(s.SmallFrameFloorBmi, s.LargeFrameFloorBmi, frame) * Sq(HeightMetres(heightScale, s));
            double lean = LeanMassKg(frame, heightScale, s);
            return floor < lean ? floor : lean;
        }

        /// <summary>The ANCHOR mass of <paramref name="body"/>: the saved one, or its lean mass when unset.</summary>
        public static double AnchorMassKg(PlayerBodyDto body, in BodySettings s)
        {
            float frame = FrameOf(body, s), height = HeightScaleOf(body, s);
            return body != null && body.MassKg >= 0d ? body.MassKg : LeanMassKg(frame, height, s);
        }

        // ---- metabolism ----------------------------------------------------------------------------

        /// <summary>Mifflin-St Jeor resting need, kcal per day.</summary>
        public static double RestingKcalPerDay(double massKg, double heightMetres, in BodySettings s)
        {
            double r = KcalPerKg * massKg + KcalPerCm * heightMetres * CmPerMetre
                     - KcalPerYear * s.MetabolicAgeYears + s.MetabolicSexConstantKcal;
            return r > MinimumRestingKcal ? r : MinimumRestingKcal;
        }

        /// <summary>The daily food need, kcal per game day: the resting need times the activity level.</summary>
        public static double DailyKcal(double massKg, float heightScale, in BodySettings s)
            => RestingKcalPerDay(massKg, HeightMetres(heightScale, s), s) * Activity(s);

        /// <summary>The reference body's daily need: today's player (default frame, scale 1, lean).</summary>
        public static double ReferenceDailyKcal(in BodySettings s)
            => DailyKcal(LeanMassKg(Clamp01(s.DefaultFrame), 1f, s), 1f, s);

        /// <summary>
        /// How much faster than today's player this body's hunger rises: its daily need over the
        /// reference body's. Exactly 1 for the reference body (the same expression on the same inputs),
        /// so an existing save keeps its hunger rate bit for bit.
        /// </summary>
        public static float HungerRateScale(double massKg, float frame, float heightScale, in BodySettings s)
        {
            double mine = DailyKcal(massKg, heightScale, s);
            double reference = ReferenceDailyKcal(s);
            if (mine == reference) return 1f;
            return (float)(mine / reference);
        }

        /// <summary>The hunger-rate scale of <paramref name="body"/> at its anchor mass.</summary>
        public static float HungerRateScale(PlayerBodyDto body, in BodySettings s)
            => HungerRateScale(AnchorMassKg(body, s), FrameOf(body, s), HeightScaleOf(body, s), s);

        /// <summary>The food energy of one point of hunger (a full meal), kcal, the same for every body:
        /// the reference body's daily need spread over the hours a meal lasts it.</summary>
        public static double KcalPerHungerUnit(float baseHungerRisePerGameHour, in BodySettings s)
            => baseHungerRisePerGameHour > 0f
                ? ReferenceDailyKcal(s) / (HoursPerDay * baseHungerRisePerGameHour) : 0d;

        /// <summary>A food's energy in meals (hunger points), for a later food def that states kcal.</summary>
        public static float MealsFromKcal(double kcal, float baseHungerRisePerGameHour, in BodySettings s)
        {
            double unit = KcalPerHungerUnit(baseHungerRisePerGameHour, s);
            return unit > 0d && kcal > 0d ? (float)(kcal / unit) : 0f;
        }

        /// <summary><paramref name="settings"/> with the hunger's rise scaled by <paramref name="scale"/>;
        /// untouched (the same value) at exactly 1.</summary>
        public static NeedsSettings ScaledNeeds(in NeedsSettings settings, float scale)
        {
            NeedsSettings scaled = settings;
            if (scale != 1f && scale > 0f) scaled.Hunger.RisePerGameHour = settings.Hunger.RisePerGameHour * scale;
            return scaled;
        }

        // ---- mass over time ------------------------------------------------------------------------

        /// <summary>
        /// The mass of <paramref name="body"/> at <paramref name="now"/>: its anchor mass, less the constant
        /// starving loss for every game second hunger has sat at its top since the later of the mass anchor
        /// and the instant hunger reached 1 (read off the saved hunger and the SCALED rise), never below the
        /// frame's floor. An unanchored hunger loses nothing.
        /// </summary>
        public static double MassAt(PlayerBodyDto body, in NeedStateDto hunger, float scaledHungerRise,
                                    double now, double secondsPerGameHour, in BodySettings s)
        {
            double anchor = AnchorMassKg(body, s);
            if (!hunger.IsStamped || !(secondsPerGameHour > 0d) || !(s.StarvingLossKgPerGameDay > 0f)) return anchor;

            float level = Clamp01(hunger.Level);
            double fullAt;
            if (level >= 1f) fullAt = hunger.SinceGameSeconds;
            else if (scaledHungerRise > 0f)
                fullAt = hunger.SinceGameSeconds + (1d - level) / scaledHungerRise * secondsPerGameHour;
            else return anchor;

            double from = body != null && body.MassSinceGameSeconds >= 0d && body.MassSinceGameSeconds > fullAt
                ? body.MassSinceGameSeconds : fullAt;
            if (!(now > from)) return anchor;

            double floor = FloorMassKg(FrameOf(body, s), HeightScaleOf(body, s), s);
            if (anchor <= floor) return anchor;   // a retuned floor never ADDS mass
            double lost = s.StarvingLossKgPerGameDay * (now - from) / (secondsPerGameHour * HoursPerDay);
            double mass = anchor - lost;
            return mass > floor ? mass : floor;
        }

        /// <summary>The mass one meal adds: the part of <paramref name="meals"/> that overfills hunger
        /// (eats past 0), as stored energy over <see cref="BodySettings.KcalPerKgOfMass"/>, capped per meal.</summary>
        public static double MealGainKg(float meals, float hungerBefore, double kcalPerHungerUnit, in BodySettings s)
        {
            double surplus = meals - Clamp01(hungerBefore);
            if (!(surplus > 0d) || !(kcalPerHungerUnit > 0d) || !(s.KcalPerKgOfMass > 0f)) return 0d;
            double gain = surplus * kcalPerHungerUnit * Clamp01(s.SurplusStoredFraction) / s.KcalPerKgOfMass;
            double cap = s.MaxGainKgPerMeal > 0f ? s.MaxGainKgPerMeal : 0d;
            return gain < cap ? gain : cap;
        }

        /// <summary><paramref name="massKg"/> clamped to the frame's floor..max.</summary>
        public static double ClampMass(double massKg, float frame, float heightScale, in BodySettings s)
        {
            double lo = FloorMassKg(frame, heightScale, s), hi = MaxMassKg(frame, heightScale, s);
            return massKg < lo ? lo : massKg > hi ? hi : massKg;
        }

        // ---- stomach -------------------------------------------------------------------------------

        /// <summary>The stomach's fullness (meals) at <paramref name="now"/>, emptying at a constant rate.</summary>
        public static float StomachAt(PlayerBodyDto body, double now, double secondsPerGameHour, in BodySettings s)
        {
            if (body == null) return 0f;
            float held = body.StomachMeals > 0f ? body.StomachMeals : 0f;
            if (body.StomachSinceGameSeconds < 0d || !(secondsPerGameHour > 0d)) return held;
            double elapsed = now - body.StomachSinceGameSeconds;
            if (!(elapsed > 0d)) return held;
            double left = held - s.StomachEmptiesPerGameHour * (elapsed / secondsPerGameHour);
            return left > 0d ? (float)left : 0f;
        }

        /// <summary>Does <paramref name="meals"/> more overfill a stomach holding <paramref name="held"/>?</summary>
        public static bool Overfills(float held, float meals, in BodySettings s)
            => held + meals > s.StomachCapacityMeals;

        // ---- walk ----------------------------------------------------------------------------------

        /// <summary>
        /// The body's walk factor: <c>heightScale ^ stride exponent</c> (taller, longer stride, faster) x
        /// <c>1 - slow x (mass / lean - 1)</c> above lean (heavier, slower; no bonus below lean), never
        /// below the body walk floor. Exactly 1 for the reference body at its lean mass.
        /// </summary>
        public static float WalkFactor(double massKg, float frame, float heightScale, in BodySettings s)
        {
            double height = heightScale == 1f ? 1d : Math.Pow(heightScale, s.HeightStrideExponent);
            double lean = LeanMassKg(frame, heightScale, s);
            double over = lean > 0d ? massKg / lean - 1d : 0d;
            double carry = over > 0d ? 1d - Clamp01(s.SlowPerExtraLeanMass) * over : 1d;
            double factor = height * carry;
            if (factor == 1d) return 1f;
            float floor = s.WalkFloor < MinimumWalkFloor ? MinimumWalkFloor : s.WalkFloor > 1f ? 1f : s.WalkFloor;
            return factor > floor ? (float)factor : floor;
        }

        // ---- anchors -------------------------------------------------------------------------------

        /// <summary>
        /// <b>Settle the body at an instant</b> (a meal, a shape choice): carry the mass and stomach to
        /// <paramref name="now"/> and re-anchor them there. If starving moved the mass, hunger is pinned at
        /// its top at <paramref name="now"/> (it must be: mass only falls while hunger sits at 1), so the new,
        /// lighter body's rate cannot reach back and un-starve the hours already spent. Otherwise the needs
        /// are not touched. Values never change (same lines, new start points). Null-safe.
        /// </summary>
        public static void SettleBody(PlayerBodyDto body, PlayerNeedsDto needs, double now, float scaledHungerRise,
                                      in BodySettings s, double secondsPerGameHour)
        {
            if (body == null || now < 0d) return;
            if (needs != null)
            {
                double anchor = AnchorMassKg(body, s);
                double mass = MassAt(body, needs.Hunger, scaledHungerRise, now, secondsPerGameHour, s);
                if (mass != anchor)
                {
                    body.MassKg = mass;
                    needs.Hunger = new NeedStateDto(1f, now);
                }
            }
            body.MassSinceGameSeconds = now;
            if (body.StomachSinceGameSeconds >= 0d)
            {
                body.StomachMeals = StomachAt(body, now, secondsPerGameHour, s);
                body.StomachSinceGameSeconds = now;
            }
        }

        /// <summary>
        /// <b>Settle everything at a save write</b>: the body (<see cref="SettleBody"/>), then the needs at
        /// their levels now with the body's hunger scale (<see cref="NeedsMath.Settle"/>). For the default
        /// body the scale is exactly 1 and the needs settle exactly as the needs slice shipped. Null-safe.
        /// </summary>
        public static void Settle(PlayerBodyDto body, PlayerNeedsDto needs, double now, in NeedsSettings needsSettings,
                                  in BodySettings s, double secondsPerGameHour)
        {
            NeedsSettings scaled = ScaledNeeds(needsSettings, HungerRateScale(body, s));
            SettleBody(body, needs, now, scaled.Hunger.RisePerGameHour, s, secondsPerGameHour);
            NeedsMath.Settle(needs, now, scaled, secondsPerGameHour);
        }

        /// <summary>
        /// <b>At a clock seek</b> (a restore, never time passing): keep the saved mass and stomach and
        /// anchor them at <paramref name="now"/>, beside <see cref="NeedsMath.Rebase"/>. Null-safe.
        /// </summary>
        public static void Rebase(PlayerBodyDto body, double now)
        {
            if (body == null || now < 0d) return;
            if (body.MassSinceGameSeconds >= 0d) body.MassSinceGameSeconds = now;
            if (body.StomachSinceGameSeconds >= 0d) body.StomachSinceGameSeconds = now;
        }

        // ---- helpers -------------------------------------------------------------------------------

        private static double Activity(in BodySettings s) => s.ActivityFactor > 0f ? s.ActivityFactor : 1d;

        private static double Bmi(float small, float large, float frame)
        {
            float f = Clamp01(frame);
            return small + (large - (double)small) * f;
        }

        private static double Sq(double v) => v * v;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
