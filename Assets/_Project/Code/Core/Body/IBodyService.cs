using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's body, as a seam</b> (body slice 1, owner ask 2026-11-15: "player starts by defining
    /// character bone structure which can accommodate a max mass ... taller players walk faster ... larger
    /// ones need more frequent or more calories ... players can overeat at which point they vomit").
    ///
    /// <para><b>What the body may touch</b> (the 2026-11-14 rulings bind it): the walk speed, through
    /// <see cref="CharacterFactors"/>, and the hunger's rate, through <see cref="INeedsService"/>. Never a
    /// bar, meter or number on screen: the player reads their body from the character's look (a later
    /// slice) and from how fast they walk and how soon they are hungry again.</para>
    ///
    /// <para><b>Deterministic.</b> Mass and stomach are pure functions of the saved body, the saved hunger
    /// and the clock (<see cref="BodyMath"/>). No roll, no frame step.</para>
    /// </summary>
    public interface IBodyService
    {
        /// <summary>Bone structure now, 0 small .. 1 large (the config default until the creator chooses).</summary>
        float Frame { get; }

        /// <summary>Height scale now (1 = <see cref="BodySettings.ReferenceHeightMetres"/>).</summary>
        float HeightScale { get; }

        /// <summary>True once the creator fixed the shape.</summary>
        bool ShapeChosen { get; }

        /// <summary>Mass now, kg.</summary>
        double MassKg { get; }

        /// <summary>The mass this frame and height are built for, kg.</summary>
        double LeanMassKg { get; }

        /// <summary>The most this frame can carry, kg.</summary>
        double MaxMassKg { get; }

        /// <summary>The least this frame wastes to, kg.</summary>
        double FloorMassKg { get; }

        /// <summary>The daily food need at the mass now, kcal per game day (Mifflin-St Jeor x activity).</summary>
        double DailyKcal { get; }

        /// <summary>How much faster than today's player the hunger rises (exactly 1 for the default body).
        /// Read at the ANCHOR mass (see <see cref="BodyMath"/>). Allocation-free.</summary>
        float HungerRateScale { get; }

        /// <summary>How full the stomach is now, in meals.</summary>
        float StomachMeals { get; }

        /// <summary>The body's share of the player's factor for <paramref name="stat"/>: for
        /// <see cref="TraitStat.WalkSpeed"/> the build's walk x the carried load's x cardio's (slice 2), exactly 1
        /// for every other stat and for the default, untrained, empty-handed body.
        /// Allocation-free (the walk reads it every physics tick).</summary>
        float PlayerMultiplier(TraitStat stat);

        /// <summary>
        /// The creator's choice: fix the frame (0..1) and height scale (clamped to the config's range), and
        /// start the body at that shape's lean mass. Refused (false) with no save or once a shape was chosen:
        /// the bone structure is fixed afterwards.
        /// </summary>
        bool ChooseShape(float frame, float heightScale);

        /// <summary>
        /// <b>A meal</b> of <paramref name="meals"/> (hunger points: 1 is a full meal, the unit
        /// <see cref="INeedsService.Eat"/> takes). If it would fill the stomach past
        /// <see cref="BodySettings.StomachCapacityMeals"/>, the player is sick instead: the stomach empties,
        /// the meal's energy is lost (hunger and mass untouched), a mess is left at
        /// <paramref name="position"/> in <paramref name="regionId"/> and <see cref="Vomited"/> is published.
        /// Otherwise hunger drops as <see cref="INeedsService.Eat"/> drops it and any part that overfills
        /// hunger is stored as mass, up to the frame's max. No save or a non-positive meal does nothing.
        /// </summary>
        MealOutcome Eat(float meals, Vector2 position, string regionId);

        /// <summary>
        /// <b>A meal that states its energy</b> (food follow-up): <paramref name="meals"/> fills hunger and the
        /// stomach exactly as <see cref="Eat(float, Vector2, string)"/> does (and overeats the same way), while
        /// the mass gain comes from <paramref name="energyKcal"/>: the part of that energy past what hunger was
        /// short of is stored. A non-positive <paramref name="energyKcal"/> is "unstated" and is the plain meal.
        /// The one door eating food uses (<see cref="FoodEater"/>), so hunger is relieved once.
        /// The default (an implementation that predates the field) is the plain meal.
        /// </summary>
        MealOutcome Eat(float meals, float energyKcal, Vector2 position, string regionId)
            => Eat(meals, position, regionId);

        /// <summary>Carry mass, stomach and hunger to now and re-anchor them there (values unchanged).</summary>
        void Settle();

        /// <summary>
        /// <b>The one door for effort</b> (body slice 2): work and the gym both report here. Costs the kcal of
        /// <paramref name="units"/> of <paramref name="activity"/> for this body (<see cref="StrengthMath.ActivityKcal"/>),
        /// raises hunger by that (<see cref="INeedsService.Spend"/>; what does not fit burns mass, never below
        /// the floor), trains the fitness kinds it names, and publishes <see cref="ActivityReported"/>.
        /// With <paramref name="train"/> false, still spends effort and publishes the event, but leaves
        /// every fitness anchor unchanged (for gym units past their daily credit). Existing callers train.
        /// Returns the kcal spent. No save, no def or a non-positive amount does nothing and returns 0.
        /// </summary>
        double ReportActivity(ActivityDef activity, float units, bool train = true);
    }

    /// <summary>What a meal did.</summary>
    public enum MealOutcome
    {
        /// <summary>Nothing happened: no save, or nothing to eat.</summary>
        Nothing = 0,
        /// <summary>The meal went down.</summary>
        Eaten = 1,
        /// <summary>It overfilled the stomach and came back up.</summary>
        Vomited = 2,
    }

    /// <summary>
    /// The player was sick from overeating (body slice 1). Carries the mess it left, so the world slice can
    /// play the moment and the audio a retch, without asking the body anything.
    /// </summary>
    public readonly struct Vomited
    {
        public readonly string MessId;
        public readonly string RegionId;
        public readonly Vector2 Position;
        public readonly double GameSeconds;
        /// <summary>The meal that came back up, in meals (its energy was lost).</summary>
        public readonly float MealsLost;

        public Vomited(string messId, string regionId, Vector2 position, double gameSeconds, float mealsLost)
        {
            MessId = messId;
            RegionId = regionId;
            Position = position;
            GameSeconds = gameSeconds;
            MealsLost = mealsLost;
        }
    }
}
